using System.Collections.Generic;
using UnityEngine;

public static class PawPalWalkGraphService
{
    private const float WorldDistanceToWalkDistanceScale = PawPalWalkRouteGraph.DistanceToStaminaScale;

    private sealed class ResolvedEncounterPoint
    {
        public string EncounterPointId;
        public string SourceNodeId;
        public Vector3 WorldPosition;
        public IList<PawPalWalkEncounterTemplate> Templates;
        public float TriggerRadius;
    }

    private sealed class ResolvedGraph
    {
        public readonly Dictionary<string, PawPalWalkGraphNodeData> NodesById = new Dictionary<string, PawPalWalkGraphNodeData>();
        public readonly Dictionary<string, PawPalWalkGraphEdgeData> EdgesById = new Dictionary<string, PawPalWalkGraphEdgeData>();
        public readonly Dictionary<string, List<PawPalWalkGraphEdgeData>> Adjacency = new Dictionary<string, List<PawPalWalkGraphEdgeData>>();
        public readonly Dictionary<string, List<ResolvedEncounterPoint>> EncounterPointsByNodeId = new Dictionary<string, List<ResolvedEncounterPoint>>();
        public PawPalWalkGraphSnapshot Snapshot = new PawPalWalkGraphSnapshot();
    }

    public static bool TryGetSceneGraphSnapshot(PawPalWalkingSceneBindings bindings, out PawPalWalkGraphSnapshot snapshot, out string failureMessage)
    {
        snapshot = null;
        if (!TryResolveGraph(bindings, out ResolvedGraph resolved, out failureMessage))
        {
            return false;
        }

        snapshot = resolved.Snapshot;
        return true;
    }

    public static bool TryBuildDefaultRoutePlan(PawPalWalkingSceneBindings bindings, out PawPalWalkRoutePlan plan, out string failureMessage)
    {
        plan = null;
        if (!TryResolveGraph(bindings, out ResolvedGraph resolved, out failureMessage))
        {
            return false;
        }

        string startNodeId = resolved.Snapshot.DefaultStartNodeId;
        string endNodeId = resolved.Snapshot.DefaultEndNodeId;
        if (string.IsNullOrEmpty(startNodeId) || string.IsNullOrEmpty(endNodeId))
        {
            failureMessage = "Walking graph is missing a default start or end node.";
            return false;
        }

        return TryBuildPlanFromResolvedGraph(resolved, startNodeId, endNodeId, out plan, out failureMessage);
    }

    public static bool TryBuildPlanFromNodeIds(PawPalWalkGraphSnapshot snapshot, IList<string> nodeIds, out PawPalWalkRoutePlan plan, out string failureMessage)
    {
        plan = null;
        failureMessage = string.Empty;
        if (snapshot == null)
        {
            failureMessage = "Walk graph snapshot is missing.";
            return false;
        }

        if (nodeIds == null || nodeIds.Count < 2)
        {
            failureMessage = "At least two walk nodes are required.";
            return false;
        }

        Dictionary<string, PawPalWalkGraphNodeData> nodesById = new Dictionary<string, PawPalWalkGraphNodeData>();
        Dictionary<string, PawPalWalkGraphEdgeData> edgesById = new Dictionary<string, PawPalWalkGraphEdgeData>();
        for (int i = 0; i < snapshot.Nodes.Count; i++)
        {
            PawPalWalkGraphNodeData node = snapshot.Nodes[i];
            if (node != null && !string.IsNullOrEmpty(node.NodeId))
            {
                nodesById[node.NodeId] = node;
            }
        }

        for (int i = 0; i < snapshot.Edges.Count; i++)
        {
            PawPalWalkGraphEdgeData edge = snapshot.Edges[i];
            if (edge != null && !string.IsNullOrEmpty(edge.EdgeId))
            {
                edgesById[edge.EdgeId] = edge;
            }
        }

        plan = new PawPalWalkRoutePlan
        {
            GraphId = snapshot.GraphId,
            StartNodeId = nodeIds[0],
            EndNodeId = nodeIds[nodeIds.Count - 1]
        };

        for (int i = 0; i < nodeIds.Count; i++)
        {
            string nodeId = nodeIds[i];
            if (!nodesById.TryGetValue(nodeId, out PawPalWalkGraphNodeData node))
            {
                failureMessage = "Walk graph node '" + nodeId + "' is missing.";
                return false;
            }

            plan.NodeIds.Add(nodeId);
            if (node.MapPosition != null)
            {
                plan.RoutePoints.Add(new PawPalWalkPointData(node.MapPosition.ToVector2()));
            }
        }

        for (int nodeIndex = 0; nodeIndex < nodeIds.Count - 1; nodeIndex++)
        {
            if (!TryFindConnectingEdge(snapshot, nodeIds[nodeIndex], nodeIds[nodeIndex + 1], out PawPalWalkGraphEdgeData edge, out bool reversePath))
            {
                failureMessage = "Walk graph is missing an edge between '" + nodeIds[nodeIndex] + "' and '" + nodeIds[nodeIndex + 1] + "'.";
                return false;
            }

            AppendEdgeToPlan(plan, edge, reversePath);
        }

        float gameplayDistance = Mathf.Max(0f, plan.RouteDistance * WorldDistanceToWalkDistanceScale);
        plan.RouteDistance = gameplayDistance;
        plan.BaseStaminaCost = gameplayDistance;
        plan.StaminaCost = plan.BaseStaminaCost;
        return true;
    }

    public static bool TryFindShortestPathNodeIds(PawPalWalkGraphSnapshot snapshot, string startNodeId, string endNodeId, List<string> destination, out string failureMessage)
    {
        failureMessage = string.Empty;
        if (destination == null)
        {
            failureMessage = "Destination list is missing.";
            return false;
        }

        destination.Clear();
        if (snapshot == null)
        {
            failureMessage = "Walk graph snapshot is missing.";
            return false;
        }

        Dictionary<string, float> distanceByNodeId = new Dictionary<string, float>();
        Dictionary<string, string> previousNodeIdByNodeId = new Dictionary<string, string>();
        List<string> open = new List<string>();
        for (int i = 0; i < snapshot.Nodes.Count; i++)
        {
            PawPalWalkGraphNodeData node = snapshot.Nodes[i];
            if (node == null || string.IsNullOrEmpty(node.NodeId))
            {
                continue;
            }

            distanceByNodeId[node.NodeId] = float.PositiveInfinity;
            open.Add(node.NodeId);
        }

        if (!distanceByNodeId.ContainsKey(startNodeId) || !distanceByNodeId.ContainsKey(endNodeId))
        {
            failureMessage = "Walk graph path endpoints are missing.";
            return false;
        }

        distanceByNodeId[startNodeId] = 0f;
        while (open.Count > 0)
        {
            int bestIndex = 0;
            string currentNodeId = open[0];
            float bestDistance = distanceByNodeId[currentNodeId];
            for (int i = 1; i < open.Count; i++)
            {
                string candidateNodeId = open[i];
                float candidateDistance = distanceByNodeId[candidateNodeId];
                if (candidateDistance < bestDistance)
                {
                    currentNodeId = candidateNodeId;
                    bestDistance = candidateDistance;
                    bestIndex = i;
                }
            }

            open.RemoveAt(bestIndex);
            if (currentNodeId == endNodeId)
            {
                break;
            }

            for (int edgeIndex = 0; edgeIndex < snapshot.Edges.Count; edgeIndex++)
            {
                PawPalWalkGraphEdgeData edge = snapshot.Edges[edgeIndex];
                if (edge == null)
                {
                    continue;
                }

                string neighbourNodeId = string.Empty;
                if (edge.StartNodeId == currentNodeId)
                {
                    neighbourNodeId = edge.EndNodeId;
                }
                else if (edge.EndNodeId == currentNodeId)
                {
                    neighbourNodeId = edge.StartNodeId;
                }
                else
                {
                    continue;
                }

                if (!distanceByNodeId.ContainsKey(neighbourNodeId))
                {
                    continue;
                }

                float candidateDistance = distanceByNodeId[currentNodeId] + Mathf.Max(0.001f, edge.Distance);
                if (candidateDistance < distanceByNodeId[neighbourNodeId])
                {
                    distanceByNodeId[neighbourNodeId] = candidateDistance;
                    previousNodeIdByNodeId[neighbourNodeId] = currentNodeId;
                }
            }
        }

        if (startNodeId != endNodeId && !previousNodeIdByNodeId.ContainsKey(endNodeId))
        {
            failureMessage = "Walk graph could not find a path between the requested nodes.";
            return false;
        }

        destination.Add(endNodeId);
        while (destination[0] != startNodeId)
        {
            destination.Insert(0, previousNodeIdByNodeId[destination[0]]);
        }

        return true;
    }

    public static string ValidatePlan(PawPalWalkRoutePlan plan, float availableStamina)
    {
        if (plan == null)
        {
            return "Walk route is missing.";
        }

        bool usesGraphPath = plan.WorldPath != null && plan.WorldPath.Count >= 2;
        if (!usesGraphPath)
        {
            return PawPalWalkRouteGraph.ValidatePlan(plan, availableStamina);
        }

        if (string.IsNullOrEmpty(plan.StartNodeId) || string.IsNullOrEmpty(plan.EndNodeId))
        {
            return "Walk route is missing its endpoints.";
        }

        if (plan.RouteDistance <= 0.001f)
        {
            return "Walk route is too short.";
        }

        if (plan.StaminaCost > availableStamina + 0.001f)
        {
            return "Too tired for this route.";
        }

        return string.Empty;
    }

    private static bool TryResolveGraph(PawPalWalkingSceneBindings bindings, out ResolvedGraph resolved, out string failureMessage)
    {
        resolved = null;
        failureMessage = string.Empty;
        PawPalWalkGraph authoredGraph = bindings != null ? bindings.Graph : null;
        if (authoredGraph == null)
        {
            authoredGraph = Object.FindFirstObjectByType<PawPalWalkGraph>(FindObjectsInactive.Include);
        }

        if (authoredGraph != null && TryBuildResolvedAuthoredGraph(authoredGraph, out resolved, out failureMessage))
        {
            return true;
        }

        if (bindings != null && TryBuildResolvedFallbackGraph(bindings, out resolved, out failureMessage))
        {
            return true;
        }

        if (string.IsNullOrEmpty(failureMessage))
        {
            failureMessage = "Walking scene has no authored walk graph and no legacy fallback route.";
        }

        return false;
    }

    private static bool TryBuildResolvedAuthoredGraph(PawPalWalkGraph authoredGraph, out ResolvedGraph resolved, out string failureMessage)
    {
        resolved = new ResolvedGraph();
        failureMessage = string.Empty;
        resolved.Snapshot.GraphId = authoredGraph.GraphId;

        List<PawPalWalkNode> nodes = new List<PawPalWalkNode>();
        authoredGraph.GetNodes(nodes);
        for (int i = 0; i < nodes.Count; i++)
        {
            PawPalWalkNode node = nodes[i];
            if (node == null)
            {
                continue;
            }

            string nodeId = node.NodeId;
            if (string.IsNullOrEmpty(nodeId))
            {
                failureMessage = "Walking graph contains a node with no id.";
                return false;
            }

            if (resolved.NodesById.ContainsKey(nodeId))
            {
                failureMessage = "Walking graph contains duplicate node id '" + nodeId + "'.";
                return false;
            }

            PawPalWalkGraphNodeData nodeData = new PawPalWalkGraphNodeData
            {
                NodeId = nodeId,
                NodeType = node.NodeType,
                WorldPosition = new PawPalWalkWorldPointData(node.WorldPosition),
                MapPosition = node.HasMapPosition ? new PawPalWalkPointData(node.MapPosition) : null
            };
            resolved.NodesById.Add(nodeId, nodeData);
            resolved.Snapshot.Nodes.Add(nodeData);
            resolved.Adjacency[nodeId] = new List<PawPalWalkGraphEdgeData>();
        }

        if (resolved.NodesById.Count < 2)
        {
            failureMessage = "Walking graph needs at least two nodes.";
            return false;
        }

        List<PawPalWalkEdge> edges = new List<PawPalWalkEdge>();
        authoredGraph.GetEdges(edges);
        List<Vector3> edgePath = new List<Vector3>();
        for (int i = 0; i < edges.Count; i++)
        {
            PawPalWalkEdge edge = edges[i];
            if (edge == null)
            {
                continue;
            }

            if (edge.StartNode == null || edge.EndNode == null)
            {
                failureMessage = "Walk edge '" + edge.EdgeId + "' is missing a start or end node.";
                return false;
            }

            if (!edge.TryGetWorldPath(edgePath))
            {
                failureMessage = "Walk edge '" + edge.EdgeId + "' has no usable world path.";
                return false;
            }

            string edgeId = edge.EdgeId;
            if (resolved.EdgesById.ContainsKey(edgeId))
            {
                failureMessage = "Walking graph contains duplicate edge id '" + edgeId + "'.";
                return false;
            }

            PawPalWalkGraphEdgeData edgeData = new PawPalWalkGraphEdgeData
            {
                EdgeId = edgeId,
                StartNodeId = edge.StartNode.NodeId,
                EndNodeId = edge.EndNode.NodeId,
                Distance = 0f
            };

            for (int pointIndex = 0; pointIndex < edgePath.Count; pointIndex++)
            {
                Vector3 point = edgePath[pointIndex];
                edgeData.WorldPath.Add(new PawPalWalkWorldPointData(point));
                if (pointIndex > 0)
                {
                    edgeData.Distance += Vector3.Distance(edgePath[pointIndex - 1], point);
                }
            }

            resolved.EdgesById.Add(edgeId, edgeData);
            resolved.Snapshot.Edges.Add(edgeData);
            resolved.Adjacency[edgeData.StartNodeId].Add(edgeData);
            resolved.Adjacency[edgeData.EndNodeId].Add(edgeData);
        }

        if (resolved.EdgesById.Count == 0)
        {
            failureMessage = "Walking graph needs at least one edge.";
            return false;
        }

        resolved.Snapshot.DefaultStartNodeId = authoredGraph.DefaultStartNode != null
            ? authoredGraph.DefaultStartNode.NodeId
            : FindFirstNodeOfType(resolved.Snapshot.Nodes, PawPalWalkNodeType.Spawn);
        resolved.Snapshot.DefaultEndNodeId = authoredGraph.DefaultEndNode != null
            ? authoredGraph.DefaultEndNode.NodeId
            : FindFarthestNodeId(resolved.Snapshot, resolved.Snapshot.DefaultStartNodeId);

        List<PawPalWalkEncounterPoint> encounters = new List<PawPalWalkEncounterPoint>();
        authoredGraph.GetEncounterPoints(encounters);
        for (int i = 0; i < encounters.Count; i++)
        {
            PawPalWalkEncounterPoint encounter = encounters[i];
            if (encounter == null || encounter.SourceNode == null)
            {
                continue;
            }

            string nodeId = encounter.SourceNode.NodeId;
            if (!resolved.EncounterPointsByNodeId.TryGetValue(nodeId, out List<ResolvedEncounterPoint> nodeEncounters))
            {
                nodeEncounters = new List<ResolvedEncounterPoint>();
                resolved.EncounterPointsByNodeId[nodeId] = nodeEncounters;
            }

            nodeEncounters.Add(new ResolvedEncounterPoint
            {
                EncounterPointId = encounter.EncounterPointId,
                SourceNodeId = nodeId,
                WorldPosition = encounter.WorldPosition,
                Templates = encounter.EncounterPool,
                TriggerRadius = encounter.TriggerRadius
            });
        }

        return true;
    }

    private static bool TryBuildResolvedFallbackGraph(PawPalWalkingSceneBindings bindings, out ResolvedGraph resolved, out string failureMessage)
    {
        resolved = new ResolvedGraph();
        failureMessage = string.Empty;

        List<Vector3> routePoints = new List<Vector3>();
        if (!bindings.TryBuildRoutePoints(routePoints) || routePoints.Count < 2)
        {
            failureMessage = "Legacy walking bindings could not build a fallback graph path.";
            return false;
        }

        resolved.Snapshot.GraphId = "walking_runtime_fallback";
        for (int i = 0; i < routePoints.Count; i++)
        {
            string nodeId = i == 0 ? "spawn_fallback" : "corner_fallback_" + i;
            PawPalWalkGraphNodeData node = new PawPalWalkGraphNodeData
            {
                NodeId = nodeId,
                NodeType = i == 0 ? PawPalWalkNodeType.Spawn : PawPalWalkNodeType.Corner,
                WorldPosition = new PawPalWalkWorldPointData(routePoints[i])
            };
            resolved.NodesById[nodeId] = node;
            resolved.Snapshot.Nodes.Add(node);
            resolved.Adjacency[nodeId] = new List<PawPalWalkGraphEdgeData>();
        }

        resolved.Snapshot.DefaultStartNodeId = resolved.Snapshot.Nodes[0].NodeId;
        resolved.Snapshot.DefaultEndNodeId = resolved.Snapshot.Nodes[resolved.Snapshot.Nodes.Count - 1].NodeId;

        for (int i = 1; i < routePoints.Count; i++)
        {
            string startNodeId = resolved.Snapshot.Nodes[i - 1].NodeId;
            string endNodeId = resolved.Snapshot.Nodes[i].NodeId;
            PawPalWalkGraphEdgeData edge = new PawPalWalkGraphEdgeData
            {
                EdgeId = "edge_fallback_" + i,
                StartNodeId = startNodeId,
                EndNodeId = endNodeId,
                Distance = Vector3.Distance(routePoints[i - 1], routePoints[i])
            };
            edge.WorldPath.Add(new PawPalWalkWorldPointData(routePoints[i - 1]));
            edge.WorldPath.Add(new PawPalWalkWorldPointData(routePoints[i]));
            resolved.EdgesById[edge.EdgeId] = edge;
            resolved.Snapshot.Edges.Add(edge);
            resolved.Adjacency[startNodeId].Add(edge);
            resolved.Adjacency[endNodeId].Add(edge);
        }

        return true;
    }

    private static bool TryBuildPlanFromResolvedGraph(ResolvedGraph resolved, string startNodeId, string endNodeId, out PawPalWalkRoutePlan plan, out string failureMessage)
    {
        plan = null;
        failureMessage = string.Empty;
        List<string> nodeIds = new List<string>();
        if (!TryFindShortestPathNodeIds(resolved.Snapshot, startNodeId, endNodeId, nodeIds, out failureMessage))
        {
            return false;
        }

        if (!TryBuildPlanFromNodeIds(resolved.Snapshot, nodeIds, out plan, out failureMessage))
        {
            return false;
        }

        plan.GraphId = resolved.Snapshot.GraphId;
        AddEncounterPointsToPlan(plan, resolved);
        return true;
    }

    private static void AppendEdgeToPlan(PawPalWalkRoutePlan plan, PawPalWalkGraphEdgeData edge, bool reversePath)
    {
        if (plan == null || edge == null)
        {
            return;
        }

        plan.EdgeIds.Add(edge.EdgeId);
        if (reversePath)
        {
            for (int i = edge.WorldPath.Count - 1; i >= 0; i--)
            {
                AppendWorldPointToPlan(plan, edge.WorldPath[i]);
            }
        }
        else
        {
            for (int i = 0; i < edge.WorldPath.Count; i++)
            {
                AppendWorldPointToPlan(plan, edge.WorldPath[i]);
            }
        }
    }

    private static void AppendWorldPointToPlan(PawPalWalkRoutePlan plan, PawPalWalkWorldPointData point)
    {
        if (plan == null || point == null)
        {
            return;
        }

        if (plan.WorldPath.Count == 0 || !SameWorldPoint(plan.WorldPath[plan.WorldPath.Count - 1], point))
        {
            if (plan.WorldPath.Count > 0)
            {
                plan.RouteDistance += Vector3.Distance(
                    plan.WorldPath[plan.WorldPath.Count - 1].ToVector3(),
                    point.ToVector3());
            }

            plan.WorldPath.Add(new PawPalWalkWorldPointData(point.ToVector3()));
        }
    }

    private static void AddEncounterPointsToPlan(PawPalWalkRoutePlan plan, ResolvedGraph resolved)
    {
        if (plan == null || resolved == null || plan.NodeIds == null || plan.NodeIds.Count == 0)
        {
            return;
        }

        for (int i = 0; i < plan.NodeIds.Count; i++)
        {
            string nodeId = plan.NodeIds[i];
            if (!resolved.EncounterPointsByNodeId.TryGetValue(nodeId, out List<ResolvedEncounterPoint> encounters))
            {
                continue;
            }

            for (int encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                ResolvedEncounterPoint encounter = encounters[encounterIndex];
                float progress = CalculateProgressAlongWorldPath(plan.WorldPath, encounter.WorldPosition);
                plan.EncounterPoints.Add(new PawPalWalkRouteEncounterData
                {
                    EncounterPointId = encounter.EncounterPointId,
                    SourceNodeId = encounter.SourceNodeId,
                    Progress = progress
                });
            }
        }

        plan.EncounterPoints.Sort(delegate(PawPalWalkRouteEncounterData first, PawPalWalkRouteEncounterData second)
        {
            return first.Progress.CompareTo(second.Progress);
        });
    }

    public static bool TryGetEncounterTemplatesForPlan(PawPalWalkingSceneBindings bindings, PawPalWalkRoutePlan plan, Dictionary<string, List<PawPalWalkEncounterTemplate>> destination)
    {
        if (destination == null)
        {
            return false;
        }

        destination.Clear();
        if (plan == null || plan.EncounterPoints == null || plan.EncounterPoints.Count == 0)
        {
            return true;
        }

        if (!TryResolveGraph(bindings, out ResolvedGraph resolved, out _))
        {
            return false;
        }

        for (int i = 0; i < plan.EncounterPoints.Count; i++)
        {
            PawPalWalkRouteEncounterData encounter = plan.EncounterPoints[i];
            if (encounter == null || string.IsNullOrEmpty(encounter.SourceNodeId))
            {
                continue;
            }

            if (!resolved.EncounterPointsByNodeId.TryGetValue(encounter.SourceNodeId, out List<ResolvedEncounterPoint> nodeEncounters))
            {
                continue;
            }

            for (int nodeEncounterIndex = 0; nodeEncounterIndex < nodeEncounters.Count; nodeEncounterIndex++)
            {
                ResolvedEncounterPoint candidate = nodeEncounters[nodeEncounterIndex];
                if (candidate.EncounterPointId != encounter.EncounterPointId)
                {
                    continue;
                }

                destination[encounter.EncounterPointId] = new List<PawPalWalkEncounterTemplate>(candidate.Templates);
                break;
            }
        }

        return true;
    }

    private static bool TryFindConnectingEdge(PawPalWalkGraphSnapshot snapshot, string startNodeId, string endNodeId, out PawPalWalkGraphEdgeData edge, out bool reversePath)
    {
        edge = null;
        reversePath = false;
        if (snapshot == null)
        {
            return false;
        }

        for (int i = 0; i < snapshot.Edges.Count; i++)
        {
            PawPalWalkGraphEdgeData candidate = snapshot.Edges[i];
            if (candidate == null)
            {
                continue;
            }

            bool sameDirection = candidate.StartNodeId == startNodeId && candidate.EndNodeId == endNodeId;
            bool reverseDirection = candidate.StartNodeId == endNodeId && candidate.EndNodeId == startNodeId;
            if (sameDirection || reverseDirection)
            {
                edge = candidate;
                reversePath = reverseDirection;
                return true;
            }
        }

        return false;
    }

    private static float CalculateProgressAlongWorldPath(IList<PawPalWalkWorldPointData> worldPath, Vector3 worldPosition)
    {
        if (worldPath == null || worldPath.Count < 2)
        {
            return 0f;
        }

        float totalDistance = 0f;
        for (int i = 1; i < worldPath.Count; i++)
        {
            totalDistance += Vector3.Distance(worldPath[i - 1].ToVector3(), worldPath[i].ToVector3());
        }

        if (totalDistance <= 0.001f)
        {
            return 0f;
        }

        float walkedDistance = 0f;
        float bestDistance = float.MaxValue;
        float bestWalkedDistance = 0f;
        for (int i = 1; i < worldPath.Count; i++)
        {
            Vector3 start = worldPath[i - 1].ToVector3();
            Vector3 end = worldPath[i].ToVector3();
            float segmentDistance = Vector3.Distance(start, end);
            if (segmentDistance <= 0.001f)
            {
                continue;
            }

            Vector3 delta = end - start;
            float t = Mathf.Clamp01(Vector3.Dot(worldPosition - start, delta) / Mathf.Max(0.001f, delta.sqrMagnitude));
            Vector3 pointOnSegment = Vector3.Lerp(start, end, t);
            float candidateDistance = Vector3.Distance(pointOnSegment, worldPosition);
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                bestWalkedDistance = walkedDistance + segmentDistance * t;
            }

            walkedDistance += segmentDistance;
        }

        return Mathf.Clamp01(bestWalkedDistance / totalDistance);
    }

    private static bool SameWorldPoint(PawPalWalkWorldPointData first, PawPalWalkWorldPointData second)
    {
        return Vector3.Distance(first.ToVector3(), second.ToVector3()) <= 0.01f;
    }

    private static string FindFirstNodeOfType(IList<PawPalWalkGraphNodeData> nodes, PawPalWalkNodeType nodeType)
    {
        if (nodes == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            PawPalWalkGraphNodeData node = nodes[i];
            if (node != null && node.NodeType == nodeType)
            {
                return node.NodeId;
            }
        }

        return nodes.Count > 0 && nodes[0] != null ? nodes[0].NodeId : string.Empty;
    }

    private static string FindFarthestNodeId(PawPalWalkGraphSnapshot snapshot, string fromNodeId)
    {
        if (snapshot == null || snapshot.Nodes == null || snapshot.Nodes.Count == 0)
        {
            return string.Empty;
        }

        string bestNodeId = snapshot.Nodes[snapshot.Nodes.Count - 1].NodeId;
        float bestDistance = -1f;
        PawPalWalkGraphNodeData fromNode = null;
        for (int i = 0; i < snapshot.Nodes.Count; i++)
        {
            if (snapshot.Nodes[i] != null && snapshot.Nodes[i].NodeId == fromNodeId)
            {
                fromNode = snapshot.Nodes[i];
                break;
            }
        }

        if (fromNode == null)
        {
            return bestNodeId;
        }

        for (int i = 0; i < snapshot.Nodes.Count; i++)
        {
            PawPalWalkGraphNodeData candidate = snapshot.Nodes[i];
            if (candidate == null || candidate.NodeId == fromNodeId)
            {
                continue;
            }

            float distance = Vector3.Distance(fromNode.WorldPosition.ToVector3(), candidate.WorldPosition.ToVector3());
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestNodeId = candidate.NodeId;
            }
        }

        return bestNodeId;
    }
}
