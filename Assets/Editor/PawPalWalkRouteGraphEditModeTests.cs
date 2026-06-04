using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PawPalWalkRouteGraphEditModeTests
{
    [Test]
    public void VisitRouteInjectsHomeStartAndEnd()
    {
        PawPalWalkGraphSnapshot snapshot = BuildSnapshot();
        PawPalWalkRoutePlan plan;
        string failureMessage;

        bool success = PawPalWalkGraphService.TryBuildPlanThroughVisitNodeIds(
            snapshot,
            "home_start",
            "home_end",
            new[] { "park" },
            out plan,
            out failureMessage);

        Assert.IsTrue(success, failureMessage);
        CollectionAssert.AreEqual(new[] { "home_start", "park", "home_end" }, plan.NodeIds);
        CollectionAssert.AreEqual(new[] { "park" }, plan.RequestedVisitNodeIds);
    }

    [Test]
    public void VisitRouteExpandsNonAdjacentVisitsThroughShortestPath()
    {
        PawPalWalkGraphSnapshot snapshot = BuildSnapshot();
        PawPalWalkRoutePlan plan;
        string failureMessage;

        bool success = PawPalWalkGraphService.TryBuildPlanThroughVisitNodeIds(
            snapshot,
            "home_start",
            "home_end",
            new[] { "market" },
            out plan,
            out failureMessage);

        Assert.IsTrue(success, failureMessage);
        CollectionAssert.AreEqual(new[] { "home_start", "park", "market", "home_end" }, plan.NodeIds);
    }

    [Test]
    public void VisitRouteValidationBlocksInsufficientStamina()
    {
        PawPalWalkGraphSnapshot snapshot = BuildSnapshot();
        PawPalWalkRoutePlan plan;
        string failureMessage;

        Assert.IsTrue(PawPalWalkGraphService.TryBuildPlanThroughVisitNodeIds(
            snapshot,
            "home_start",
            "home_end",
            new[] { "park" },
            out plan,
            out failureMessage), failureMessage);

        string validation = PawPalWalkGraphService.ValidatePlan(plan, plan.StaminaCost - 0.1f);

        Assert.AreEqual("Too tired for this route.", validation);
    }

    private static PawPalWalkGraphSnapshot BuildSnapshot()
    {
        PawPalWalkGraphSnapshot snapshot = new PawPalWalkGraphSnapshot
        {
            GraphId = "test_walk",
            DefaultStartNodeId = "home_start",
            DefaultEndNodeId = "home_end"
        };

        AddNode(snapshot, "home_start", new Vector2(0f, 0f));
        AddNode(snapshot, "park", new Vector2(10f, 0f));
        AddNode(snapshot, "market", new Vector2(20f, 0f));
        AddNode(snapshot, "home_end", new Vector2(30f, 0f));
        AddEdge(snapshot, "home_to_park", "home_start", "park", 10f);
        AddEdge(snapshot, "park_to_market", "park", "market", 10f);
        AddEdge(snapshot, "park_to_end", "park", "home_end", 20f);
        AddEdge(snapshot, "market_to_end", "market", "home_end", 10f);
        return snapshot;
    }

    private static void AddNode(PawPalWalkGraphSnapshot snapshot, string nodeId, Vector2 point)
    {
        snapshot.Nodes.Add(new PawPalWalkGraphNodeData
        {
            NodeId = nodeId,
            NodeType = PawPalWalkNodeType.Landmark,
            MapPosition = new PawPalWalkPointData(point),
            WorldPosition = new PawPalWalkWorldPointData(new Vector3(point.x, 0f, point.y))
        });
    }

    private static void AddEdge(PawPalWalkGraphSnapshot snapshot, string edgeId, string startNodeId, string endNodeId, float distance)
    {
        PawPalWalkGraphNodeData start = FindNode(snapshot.Nodes, startNodeId);
        PawPalWalkGraphNodeData end = FindNode(snapshot.Nodes, endNodeId);
        PawPalWalkGraphEdgeData edge = new PawPalWalkGraphEdgeData
        {
            EdgeId = edgeId,
            StartNodeId = startNodeId,
            EndNodeId = endNodeId,
            Distance = distance
        };
        edge.WorldPath.Add(new PawPalWalkWorldPointData(start.WorldPosition.ToVector3()));
        edge.WorldPath.Add(new PawPalWalkWorldPointData(end.WorldPosition.ToVector3()));
        snapshot.Edges.Add(edge);
    }

    private static PawPalWalkGraphNodeData FindNode(IList<PawPalWalkGraphNodeData> nodes, string nodeId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].NodeId == nodeId)
            {
                return nodes[i];
            }
        }

        return null;
    }
}
