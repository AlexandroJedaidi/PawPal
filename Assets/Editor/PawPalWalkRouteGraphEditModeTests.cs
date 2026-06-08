using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
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

    [Test]
    public void WalkStaminaPreviewFillIgnoresEmptyRouteCost()
    {
        Assert.AreEqual(0.8f, WalkStaminaButtonView.CalculateFill01(80f, 100f, 0f), 0.0001f);
    }

    [Test]
    public void WalkStaminaPreviewFillSubtractsSelectedRouteCost()
    {
        Assert.AreEqual(0.55f, WalkStaminaButtonView.CalculateFill01(80f, 100f, 25f), 0.0001f);
    }

    [Test]
    public void WalkStaminaPreviewFillClampsOverBudgetRouteToEmpty()
    {
        Assert.AreEqual(0f, WalkStaminaButtonView.CalculateFill01(20f, 100f, 35f), 0.0001f);
    }

    [Test]
    public void AuthoredDogEncounterCopiesVisitorMetadata()
    {
        GameObject root = null;
        try
        {
            PawPalWalkingSceneBindings bindings = BuildAuthoredEncounterScene(
                out root,
                PawPalWalkEventType.DogEncounter,
                "point_02_pet_encounter_test",
                "dog_toyterrier",
                IntroPetSpecies.Dog,
                "Milo",
                "Friendly greetings are easier when the leash stays loose.");
            PawPalWalkRoutePlan plan = BuildPlanWithEncounter("point_02_pet_encounter_test", "home_end");
            PawPalWalkSessionSaveData session = new PawPalWalkSessionSaveData { SessionId = "visitor_metadata" };

            PawPalWalkEventGenerator.PopulateEvents(session, plan, null);

            Assert.AreEqual(1, session.GeneratedEvents.Count);
            PawPalWalkGeneratedEventState walkEvent = session.GeneratedEvents[0];
            Assert.AreEqual(PawPalWalkEventType.DogEncounter, walkEvent.EventType);
            Assert.AreEqual("dog_toyterrier", walkEvent.VisitorPetDefinitionKey);
            Assert.AreEqual(IntroPetSpecies.Dog, walkEvent.VisitorSpecies);
            Assert.AreEqual("Milo", walkEvent.VisitorDisplayName);
            Assert.AreEqual("Friendly greetings are easier when the leash stays loose.", walkEvent.BodyText);
            Assert.AreEqual(string.Empty, walkEvent.RewardItemId);
            Assert.IsNotNull(bindings);
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    [Test]
    public void DogEncounterIgnoresTemplateRewardItem()
    {
        GameObject root = null;
        try
        {
            BuildAuthoredEncounterScene(
                out root,
                PawPalWalkEventType.DogEncounter,
                "rewardless_pet_encounter",
                "cat_simple",
                IntroPetSpecies.Cat,
                "Luna",
                "Cats notice quiet streets first.",
                "toy_bone_1");
            PawPalWalkRoutePlan plan = BuildPlanWithEncounter("rewardless_pet_encounter", "home_end");
            PawPalWalkSessionSaveData session = new PawPalWalkSessionSaveData { SessionId = "rewardless_dog_encounter" };

            PawPalWalkEventGenerator.PopulateEvents(session, plan, null);

            Assert.AreEqual(1, session.GeneratedEvents.Count);
            Assert.AreEqual(PawPalWalkEventType.DogEncounter, session.GeneratedEvents[0].EventType);
            Assert.AreEqual(IntroPetSpecies.Cat, session.GeneratedEvents[0].VisitorSpecies);
            Assert.AreEqual(string.Empty, session.GeneratedEvents[0].RewardItemId);
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }
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

    private static PawPalWalkingSceneBindings BuildAuthoredEncounterScene(
        out GameObject root,
        PawPalWalkEventType eventType,
        string encounterPointId,
        string visitorPetDefinitionKey,
        IntroPetSpecies visitorSpecies,
        string visitorDisplayName,
        string bodyText,
        string rewardItemId = "")
    {
        root = new GameObject("WalkEncounterTestRoot");
        PawPalWalkingSceneBindings bindings = root.AddComponent<PawPalWalkingSceneBindings>();
        PawPalWalkGraph graph = root.AddComponent<PawPalWalkGraph>();

        GameObject startObject = new GameObject("Node_Start");
        startObject.transform.SetParent(root.transform, false);
        startObject.transform.position = Vector3.zero;
        PawPalWalkNode startNode = startObject.AddComponent<PawPalWalkNode>();
        SetSerializedField(startNode, "nodeId", "home_start");
        SetSerializedField(startNode, "nodeType", PawPalWalkNodeType.Spawn);

        GameObject endObject = new GameObject("Node_End");
        endObject.transform.SetParent(root.transform, false);
        endObject.transform.position = new Vector3(10f, 0f, 0f);
        PawPalWalkNode endNode = endObject.AddComponent<PawPalWalkNode>();
        SetSerializedField(endNode, "nodeId", "home_end");
        SetSerializedField(endNode, "nodeType", PawPalWalkNodeType.Landmark);

        GameObject edgeObject = new GameObject("Edge");
        edgeObject.transform.SetParent(root.transform, false);
        PawPalWalkEdge edge = edgeObject.AddComponent<PawPalWalkEdge>();
        SetSerializedField(edge, "edgeId", "home_to_end");
        SetSerializedField(edge, "startNode", startNode);
        SetSerializedField(edge, "endNode", endNode);

        GameObject encounterObject = new GameObject("Encounter");
        encounterObject.transform.SetParent(root.transform, false);
        encounterObject.transform.position = new Vector3(5f, 0f, 0f);
        PawPalWalkEncounterPoint encounterPoint = encounterObject.AddComponent<PawPalWalkEncounterPoint>();
        SetSerializedField(encounterPoint, "encounterPointId", encounterPointId);
        SetSerializedField(encounterPoint, "sourceNode", endNode);
        SetEncounterTemplate(
            encounterPoint,
            eventType,
            encounterPointId + "_template",
            visitorPetDefinitionKey,
            visitorSpecies,
            visitorDisplayName,
            bodyText,
            rewardItemId);

        SetSerializedField(graph, "graphId", "test_walk_graph");
        SetSerializedField(graph, "defaultStartNode", startNode);
        SetSerializedField(graph, "defaultEndNode", endNode);
        SetSerializedField(bindings, "graph", graph);
        return bindings;
    }

    private static PawPalWalkRoutePlan BuildPlanWithEncounter(string encounterPointId, string sourceNodeId)
    {
        PawPalWalkRoutePlan plan = new PawPalWalkRoutePlan
        {
            GraphId = "test_walk_graph",
            StartNodeId = "home_start",
            EndNodeId = "home_end",
            RouteDistance = 10f
        };

        plan.NodeIds.Add("home_start");
        plan.NodeIds.Add("home_end");
        plan.WorldPath.Add(new PawPalWalkWorldPointData(Vector3.zero));
        plan.WorldPath.Add(new PawPalWalkWorldPointData(new Vector3(10f, 0f, 0f)));
        plan.EncounterPoints.Add(new PawPalWalkRouteEncounterData
        {
            EncounterPointId = encounterPointId,
            SourceNodeId = sourceNodeId,
            Progress = 0.5f
        });

        return plan;
    }

    private static void SetEncounterTemplate(
        PawPalWalkEncounterPoint encounterPoint,
        PawPalWalkEventType eventType,
        string templateId,
        string visitorPetDefinitionKey,
        IntroPetSpecies visitorSpecies,
        string visitorDisplayName,
        string bodyText,
        string rewardItemId)
    {
        SerializedObject serializedObject = new SerializedObject(encounterPoint);
        SerializedProperty pool = serializedObject.FindProperty("encounterPool");
        pool.arraySize = 1;
        SerializedProperty template = pool.GetArrayElementAtIndex(0);
        template.FindPropertyRelative("EventTemplateId").stringValue = templateId;
        template.FindPropertyRelative("EventType").enumValueIndex = (int)eventType;
        template.FindPropertyRelative("DisplayName").stringValue = visitorDisplayName;
        template.FindPropertyRelative("BodyText").stringValue = bodyText;
        template.FindPropertyRelative("RewardItemId").stringValue = rewardItemId;
        template.FindPropertyRelative("EncounterDogName").stringValue = visitorDisplayName;
        template.FindPropertyRelative("VisitorSpecies").enumValueIndex = (int)visitorSpecies;
        template.FindPropertyRelative("VisitorPetDefinitionKey").stringValue = visitorPetDefinitionKey;
        template.FindPropertyRelative("VisitorDisplayName").stringValue = visitorDisplayName;
        template.FindPropertyRelative("StopType").enumValueIndex = (int)PawPalWalkStopType.Bark;
        template.FindPropertyRelative("Weight").floatValue = 1f;
        template.FindPropertyRelative("OneShotPerSession").boolValue = true;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedField(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedField(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedField(Object target, string propertyName, PawPalWalkNodeType value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).enumValueIndex = (int)value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
