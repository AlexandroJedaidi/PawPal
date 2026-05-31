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
public sealed class PawPalWalkLocationData
{
    public string LocationId;
    public string DisplayName;
    public PawPalMapLocationType LocationType;
    public float Progress;
}

[Serializable]
public sealed class PawPalWalkRoutePlan
{
    public List<PawPalWalkPointData> RoutePoints = new List<PawPalWalkPointData>();
    public List<PawPalWalkLocationData> PlannedStops = new List<PawPalWalkLocationData>();
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
    public List<PawPalWalkPointData> RoutePoints = new List<PawPalWalkPointData>();
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
