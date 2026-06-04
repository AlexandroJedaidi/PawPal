using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PawPalWalkMapRouteNodeDefinition
{
    public string NodeId;
    public string DisplayName;
    public PawPalMapLocationType LocationType = PawPalMapLocationType.Other;
    public Vector2 MapPosition;
    public Vector2 BoxSize = new Vector2(78f, 44f);
    public bool Selectable = true;
}

[Serializable]
public sealed class PawPalWalkMapRouteEdgeDefinition
{
    public string EdgeId;
    public string StartNodeId;
    public string EndNodeId;
    public float Distance;
}

public sealed class PawPalWalkMapRouteDefinition
{
    public string GraphId = "walking_city";
    public string HomeStartNodeId = "start_01";
    public string HomeEndNodeId = "end_01";
    public readonly List<PawPalWalkMapRouteNodeDefinition> Nodes = new List<PawPalWalkMapRouteNodeDefinition>();
    public readonly List<PawPalWalkMapRouteEdgeDefinition> Edges = new List<PawPalWalkMapRouteEdgeDefinition>();

    public PawPalWalkMapRouteNodeDefinition FindNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        for (int i = 0; i < Nodes.Count; i++)
        {
            PawPalWalkMapRouteNodeDefinition node = Nodes[i];
            if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }

    public bool TryBuildSnapshot(out PawPalWalkGraphSnapshot snapshot, out string failureMessage)
    {
        snapshot = new PawPalWalkGraphSnapshot
        {
            GraphId = GraphId,
            DefaultStartNodeId = HomeStartNodeId,
            DefaultEndNodeId = HomeEndNodeId
        };
        failureMessage = string.Empty;

        HashSet<string> nodeIds = new HashSet<string>();
        for (int i = 0; i < Nodes.Count; i++)
        {
            PawPalWalkMapRouteNodeDefinition node = Nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            string nodeId = node.NodeId.Trim();
            if (!nodeIds.Add(nodeId))
            {
                failureMessage = "Map walk route has duplicate node id '" + nodeId + "'.";
                return false;
            }

            snapshot.Nodes.Add(new PawPalWalkGraphNodeData
            {
                NodeId = nodeId,
                NodeType = node.Selectable ? PawPalWalkNodeType.Landmark : PawPalWalkNodeType.Spawn,
                MapPosition = new PawPalWalkPointData(node.MapPosition),
                WorldPosition = new PawPalWalkWorldPointData(new Vector3(node.MapPosition.x, 0f, node.MapPosition.y))
            });
        }

        HashSet<string> edgeIds = new HashSet<string>();
        for (int i = 0; i < Edges.Count; i++)
        {
            PawPalWalkMapRouteEdgeDefinition edge = Edges[i];
            if (edge == null || string.IsNullOrWhiteSpace(edge.StartNodeId) || string.IsNullOrWhiteSpace(edge.EndNodeId))
            {
                continue;
            }

            string startNodeId = edge.StartNodeId.Trim();
            string endNodeId = edge.EndNodeId.Trim();
            if (!nodeIds.Contains(startNodeId) || !nodeIds.Contains(endNodeId))
            {
                failureMessage = "Map walk route edge '" + edge.EdgeId + "' references a missing node.";
                return false;
            }

            string edgeId = string.IsNullOrWhiteSpace(edge.EdgeId)
                ? startNodeId + "_to_" + endNodeId
                : edge.EdgeId.Trim();
            if (!edgeIds.Add(edgeId))
            {
                failureMessage = "Map walk route has duplicate edge id '" + edgeId + "'.";
                return false;
            }

            PawPalWalkMapRouteNodeDefinition startNode = FindNode(startNodeId);
            PawPalWalkMapRouteNodeDefinition endNode = FindNode(endNodeId);
            float distance = edge.Distance > 0.001f
                ? edge.Distance
                : Vector2.Distance(startNode.MapPosition, endNode.MapPosition);

            PawPalWalkGraphEdgeData edgeData = new PawPalWalkGraphEdgeData
            {
                EdgeId = edgeId,
                StartNodeId = startNodeId,
                EndNodeId = endNodeId,
                Distance = distance
            };
            edgeData.WorldPath.Add(new PawPalWalkWorldPointData(new Vector3(startNode.MapPosition.x, 0f, startNode.MapPosition.y)));
            edgeData.WorldPath.Add(new PawPalWalkWorldPointData(new Vector3(endNode.MapPosition.x, 0f, endNode.MapPosition.y)));
            snapshot.Edges.Add(edgeData);
        }

        if (snapshot.Nodes.Count < 2 || snapshot.Edges.Count == 0)
        {
            failureMessage = "Map walk route needs at least two nodes and one edge.";
            return false;
        }

        return true;
    }
}

public static class PawPalWalkMapRouteDefinitions
{
    public static PawPalWalkMapRouteDefinition CreateDefault()
    {
        PawPalWalkMapRouteDefinition definition = new PawPalWalkMapRouteDefinition();
        definition.Nodes.Add(CreateNode("start_01", "Home", PawPalMapLocationType.Home, new Vector2(76.5f, 625f), false));
        definition.Nodes.Add(CreateNode("dog_park", "Dog Park", PawPalMapLocationType.DogPark, new Vector2(162f, 324f), true));
        definition.Nodes.Add(CreateNode("kennel", "Kennel", PawPalMapLocationType.Kennel, new Vector2(76f, 272f), true));
        definition.Nodes.Add(CreateNode("competition_center", "Competition", PawPalMapLocationType.CompetitionCenter, new Vector2(163f, 173f), true));
        definition.Nodes.Add(CreateNode("market_corner", "Market", PawPalMapLocationType.Shop, new Vector2(260f, 468f), true));
        definition.Nodes.Add(CreateNode("river_corner", "River", PawPalMapLocationType.Other, new Vector2(335f, 370f), true));
        definition.Nodes.Add(CreateNode("end_01", "Home", PawPalMapLocationType.Home, new Vector2(76.5f, 625f), false));

        AddEdge(definition, "start_to_park", "start_01", "dog_park");
        AddEdge(definition, "start_to_kennel", "start_01", "kennel");
        AddEdge(definition, "start_to_market", "start_01", "market_corner");
        AddEdge(definition, "park_to_kennel", "dog_park", "kennel");
        AddEdge(definition, "park_to_competition", "dog_park", "competition_center");
        AddEdge(definition, "kennel_to_competition", "kennel", "competition_center");
        AddEdge(definition, "park_to_market", "dog_park", "market_corner");
        AddEdge(definition, "market_to_river", "market_corner", "river_corner");
        AddEdge(definition, "river_to_competition", "river_corner", "competition_center");
        AddEdge(definition, "park_to_end", "dog_park", "end_01");
        AddEdge(definition, "kennel_to_end", "kennel", "end_01");
        AddEdge(definition, "market_to_end", "market_corner", "end_01");
        AddEdge(definition, "river_to_end", "river_corner", "end_01");
        AddEdge(definition, "competition_to_end", "competition_center", "end_01");
        return definition;
    }

    private static PawPalWalkMapRouteNodeDefinition CreateNode(string nodeId, string label, PawPalMapLocationType type, Vector2 position, bool selectable)
    {
        return new PawPalWalkMapRouteNodeDefinition
        {
            NodeId = nodeId,
            DisplayName = label,
            LocationType = type,
            MapPosition = position,
            Selectable = selectable
        };
    }

    private static void AddEdge(PawPalWalkMapRouteDefinition definition, string edgeId, string startNodeId, string endNodeId)
    {
        PawPalWalkMapRouteNodeDefinition start = definition.FindNode(startNodeId);
        PawPalWalkMapRouteNodeDefinition end = definition.FindNode(endNodeId);
        definition.Edges.Add(new PawPalWalkMapRouteEdgeDefinition
        {
            EdgeId = edgeId,
            StartNodeId = startNodeId,
            EndNodeId = endNodeId,
            Distance = start != null && end != null ? Vector2.Distance(start.MapPosition, end.MapPosition) : 0f
        });
    }
}
