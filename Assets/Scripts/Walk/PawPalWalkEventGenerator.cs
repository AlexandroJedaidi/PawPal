using System;
using System.Collections.Generic;
using UnityEngine;

public static class PawPalWalkEventGenerator
{
    private static readonly string[] EncounterNames =
    {
        "Bella",
        "Mochi",
        "Scout",
        "Luna",
        "Biscuit"
    };

    private static readonly string[] RewardCandidates =
    {
        "toy_bone_1",
        "toy_big_ball_1",
        "food_premium",
        "food_basic"
    };

    public static void PopulateEvents(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, PawPalGameRuntime runtime)
    {
        if (session == null || plan == null)
        {
            return;
        }

        session.GeneratedEvents.Clear();
        session.VisitedLocations.Clear();

        for (int i = 0; i < plan.PlannedStops.Count; i++)
        {
            PawPalWalkLocationData stop = CloneLocation(plan.PlannedStops[i]);
            session.VisitedLocations.Add(stop);
            session.GeneratedEvents.Add(new PawPalWalkGeneratedEventState
            {
                EventId = "location_" + stop.LocationId + "_" + i,
                EventType = PawPalWalkEventType.LocationVisit,
                LocationId = stop.LocationId,
                DisplayName = stop.DisplayName,
                Progress = ClampEventProgress(stop.Progress)
            });
        }

        int seed = session.SessionId != null ? session.SessionId.GetHashCode() : Environment.TickCount;
        System.Random random = new System.Random(seed);
        AddPresentEvents(session, plan, runtime, random);
        AddDogEncounterEvent(session, plan, random);
        SortAndSpaceEvents(session.GeneratedEvents);
    }

    private static void AddPresentEvents(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, PawPalGameRuntime runtime, System.Random random)
    {
        float chance = Mathf.Clamp01(0.22f + plan.RouteDistance * 0.006f);
        int presentCount = random.NextDouble() < chance ? 1 : 0;
        if (plan.RouteDistance > 36f && random.NextDouble() < 0.32f)
        {
            presentCount++;
        }

        presentCount = Mathf.Clamp(presentCount, 0, 2);
        for (int i = 0; i < presentCount; i++)
        {
            float progress = Mathf.Lerp(0.2f, 0.82f, (float)random.NextDouble());
            string rewardItemId = PickRewardItemId(runtime, random);
            session.GeneratedEvents.Add(new PawPalWalkGeneratedEventState
            {
                EventId = "present_" + i,
                EventType = PawPalWalkEventType.PresentFound,
                DisplayName = "Present",
                RewardItemId = rewardItemId,
                Progress = ClampEventProgress(progress)
            });
        }
    }

    private static void AddDogEncounterEvent(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, System.Random random)
    {
        float chance = 0.28f + plan.RouteDistance * 0.004f;
        for (int i = 0; i < plan.PlannedStops.Count; i++)
        {
            if (plan.PlannedStops[i].LocationType == PawPalMapLocationType.DogPark)
            {
                chance += 0.24f;
                break;
            }
        }

        if (random.NextDouble() > Mathf.Clamp01(chance))
        {
            return;
        }

        string dogName = EncounterNames[random.Next(0, EncounterNames.Length)];
        session.GeneratedEvents.Add(new PawPalWalkGeneratedEventState
        {
            EventId = "dog_encounter",
            EventType = PawPalWalkEventType.DogEncounter,
            DisplayName = dogName,
            Progress = ClampEventProgress(Mathf.Lerp(0.28f, 0.74f, (float)random.NextDouble()))
        });
    }

    private static string PickRewardItemId(PawPalGameRuntime runtime, System.Random random)
    {
        if (runtime != null)
        {
            int start = random.Next(0, RewardCandidates.Length);
            for (int i = 0; i < RewardCandidates.Length; i++)
            {
                string candidate = RewardCandidates[(start + i) % RewardCandidates.Length];
                if (runtime.GetCatalogItem(candidate) != null)
                {
                    return candidate;
                }
            }
        }

        return "food_basic";
    }

    private static void SortAndSpaceEvents(List<PawPalWalkGeneratedEventState> events)
    {
        events.Sort(delegate(PawPalWalkGeneratedEventState first, PawPalWalkGeneratedEventState second)
        {
            return first.Progress.CompareTo(second.Progress);
        });

        float previous = 0.12f;
        for (int i = 0; i < events.Count; i++)
        {
            PawPalWalkGeneratedEventState walkEvent = events[i];
            walkEvent.Progress = Mathf.Clamp(walkEvent.Progress, previous + 0.08f, 0.92f);
            previous = walkEvent.Progress;
        }
    }

    private static PawPalWalkLocationData CloneLocation(PawPalWalkLocationData source)
    {
        return new PawPalWalkLocationData
        {
            LocationId = source.LocationId,
            DisplayName = source.DisplayName,
            LocationType = source.LocationType,
            Progress = source.Progress
        };
    }

    private static float ClampEventProgress(float progress)
    {
        return Mathf.Clamp(progress, 0.12f, 0.92f);
    }
}
