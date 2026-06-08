using System;
using System.Collections.Generic;
using UnityEngine;

public enum PawPalMapLocationType
{
    Home,
    DogPark,
    Kennel,
    CompetitionCenter,
    Shop,
    Other
}

public enum PawPalWalkEventType
{
    PresentFound,
    DogEncounter,
    LocationVisit,
    PersonalityMoment
}

public enum PawPalWalkNodeType
{
    Spawn,
    Corner,
    Landmark
}

public enum PawPalWalkStopType
{
    Sniff,
    Pissing,
    Bark
}

[Serializable]
public sealed class PawPalDogWalkData
{
    public float CurrentWalkStamina;
    public float MaxWalkStamina;
    public float WalkStaminaXp;
    public long LastWalkCompletedAtUtcTicks;
    public long LastStaminaRefreshAtUtcTicks;
    public int TotalWalksCompleted;
    public float TotalDistanceWalked;
}

[Serializable]
public sealed class PawPalWalkPointData
{
    public float X;
    public float Y;

    public PawPalWalkPointData()
    {
    }

    public PawPalWalkPointData(Vector2 point)
    {
        X = point.x;
        Y = point.y;
    }

    public Vector2 ToVector2()
    {
        return new Vector2(X, Y);
    }
}

[Serializable]
public sealed class PawPalWalkWorldPointData
{
    public float X;
    public float Y;
    public float Z;

    public PawPalWalkWorldPointData()
    {
    }

    public PawPalWalkWorldPointData(Vector3 point)
    {
        X = point.x;
        Y = point.y;
        Z = point.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
}

[Serializable]
public sealed class PawPalWalkLocationData
{
    public string LocationId;
    public string DisplayName;
    public PawPalMapLocationType LocationType;
    public float Progress;
}

[Serializable]
public sealed class PawPalWalkRouteEncounterData
{
    public string EncounterPointId;
    public string SourceNodeId;
    public float Progress;
}

[Serializable]
public sealed class PawPalWalkGraphNodeData
{
    public string NodeId;
    public PawPalWalkNodeType NodeType;
    public PawPalWalkWorldPointData WorldPosition;
    public PawPalWalkPointData MapPosition;
}

[Serializable]
public sealed class PawPalWalkGraphEdgeData
{
    public string EdgeId;
    public string StartNodeId;
    public string EndNodeId;
    public float Distance;
    public List<PawPalWalkWorldPointData> WorldPath = new List<PawPalWalkWorldPointData>();
}

[Serializable]
public sealed class PawPalWalkGraphSnapshot
{
    public string GraphId;
    public string DefaultStartNodeId;
    public string DefaultEndNodeId;
    public List<PawPalWalkGraphNodeData> Nodes = new List<PawPalWalkGraphNodeData>();
    public List<PawPalWalkGraphEdgeData> Edges = new List<PawPalWalkGraphEdgeData>();
}

[Serializable]
public sealed class PawPalWalkRoutePlan
{
    public List<PawPalWalkPointData> RoutePoints = new List<PawPalWalkPointData>();
    public List<PawPalWalkLocationData> PlannedStops = new List<PawPalWalkLocationData>();
    public List<PawPalWalkWorldPointData> WorldPath = new List<PawPalWalkWorldPointData>();
    public List<PawPalWalkRouteEncounterData> EncounterPoints = new List<PawPalWalkRouteEncounterData>();
    public List<string> NodeIds = new List<string>();
    public List<string> EdgeIds = new List<string>();
    public List<string> RequestedVisitNodeIds = new List<string>();
    public string GraphId;
    public string StartNodeId;
    public string EndNodeId;
    public float RouteDistance;
    public float BaseStaminaCost;
    public float StaminaCost;
    public string PersonalizedCostDogId;
    public string ReturnSceneName;
}

[Serializable]
public sealed class PawPalWalkGeneratedEventState
{
    public string EventId;
    public PawPalWalkEventType EventType;
    public string LocationId;
    public string DisplayName;
    public string BodyText;
    public string RewardItemId;
    public IntroPetSpecies VisitorSpecies;
    public string VisitorPetDefinitionKey;
    public string VisitorDisplayName;
    public string SourceEncounterPointId;
    public string SourceNodeId;
    public string EventTemplateId;
    public PawPalWalkStopType StopType;
    public float Progress;
    public bool Resolved;
    public bool RewardGranted;
}

[Serializable]
public sealed class PawPalWalkSessionSaveData
{
    public string SessionId;
    public string SelectedDogId;
    public string SelectedDogName;
    public string ReturnSceneName;
    public float RouteDistance;
    public float StaminaCost;
    public long StartedAtUtcTicks;
    public long CompletedAtUtcTicks;
    public bool Completed;
    public bool FinalRewardsApplied;
    public float LastProgress;
    public bool DeferredGraphResolution;
    public string GraphId;
    public string StartNodeId;
    public string EndNodeId;
    public List<PawPalWalkPointData> RoutePoints = new List<PawPalWalkPointData>();
    public List<PawPalWalkWorldPointData> WorldPath = new List<PawPalWalkWorldPointData>();
    public List<PawPalWalkRouteEncounterData> EncounterPoints = new List<PawPalWalkRouteEncounterData>();
    public List<string> RouteNodeIds = new List<string>();
    public List<string> RouteEdgeIds = new List<string>();
    public List<string> RequestedVisitNodeIds = new List<string>();
    public List<PawPalWalkLocationData> VisitedLocations = new List<PawPalWalkLocationData>();
    public List<PawPalWalkGeneratedEventState> GeneratedEvents = new List<PawPalWalkGeneratedEventState>();
}

public struct PawPalWalkStaminaSnapshot
{
    public float Current;
    public float Max;
    public float Xp;
    public float XpNeeded;

    public float Fill01
    {
        get { return Max <= 0f ? 0f : Mathf.Clamp01(Current / Max); }
    }

    public string CompactText
    {
        get { return Mathf.RoundToInt(Current) + "/" + Mathf.RoundToInt(Max); }
    }
}

public sealed class PawPalWalkCompletionResult
{
    public string DogName;
    public float Distance;
    public float StaminaUsed;
    public float PreviousMaxStamina;
    public float NewMaxStamina;
    public readonly List<string> LocationsVisited = new List<string>();
    public readonly List<string> ItemsReceived = new List<string>();
    public readonly List<string> DogsMet = new List<string>();

    public bool IncreasedMaxStamina
    {
        get { return NewMaxStamina > PreviousMaxStamina + 0.001f; }
    }
}
