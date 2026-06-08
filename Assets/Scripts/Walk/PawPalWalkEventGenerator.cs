using System;
using System.Collections.Generic;
using UnityEngine;

public static class PawPalWalkEventGenerator
{
    private static readonly string[] FallbackEncounterNames =
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

        int seed = session.SessionId != null ? session.SessionId.GetHashCode() : Environment.TickCount;
        System.Random random = new System.Random(seed);
        PawPalDogState dog = PawPalDogPersonalityProfiles.FindRuntimeDog(session.SelectedDogId);

        bool generatedFromAuthoredPools = false;
        PawPalWalkingSceneBindings bindings = PawPalWalkingSceneBindings.FindInScene();
        if (bindings != null && plan.EncounterPoints != null && plan.EncounterPoints.Count > 0)
        {
            generatedFromAuthoredPools = PopulateFromEncounterPools(session, plan, runtime, bindings, random);
        }

        if (!generatedFromAuthoredPools)
        {
            PopulateLegacyFallbackEvents(session, plan, runtime, dog, random);
        }

        SortAndSpaceEvents(session.GeneratedEvents);
    }

    private static bool PopulateFromEncounterPools(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, PawPalGameRuntime runtime, PawPalWalkingSceneBindings bindings, System.Random random)
    {
        Dictionary<string, List<PawPalWalkEncounterTemplate>> templatesByEncounterId = new Dictionary<string, List<PawPalWalkEncounterTemplate>>();
        if (!PawPalWalkGraphService.TryGetEncounterTemplatesForPlan(bindings, plan, templatesByEncounterId))
        {
            return false;
        }

        bool addedAny = false;
        for (int i = 0; i < plan.EncounterPoints.Count; i++)
        {
            PawPalWalkRouteEncounterData encounter = plan.EncounterPoints[i];
            if (encounter == null
                || string.IsNullOrEmpty(encounter.EncounterPointId)
                || !templatesByEncounterId.TryGetValue(encounter.EncounterPointId, out List<PawPalWalkEncounterTemplate> templates)
                || templates == null
                || templates.Count == 0)
            {
                continue;
            }

            PawPalWalkEncounterTemplate selectedTemplate = SelectTemplate(templates, random);
            if (selectedTemplate == null)
            {
                continue;
            }

            string displayName = ResolveDisplayName(selectedTemplate, random);
            string rewardItemId = ResolveRewardItemId(selectedTemplate, runtime, random);
            session.GeneratedEvents.Add(new PawPalWalkGeneratedEventState
            {
                EventId = "encounter_" + encounter.EncounterPointId + "_" + i,
                EventType = selectedTemplate.EventType,
                LocationId = encounter.SourceNodeId,
                DisplayName = displayName,
                BodyText = selectedTemplate.BodyText,
                RewardItemId = rewardItemId,
                VisitorSpecies = selectedTemplate.VisitorSpecies,
                VisitorPetDefinitionKey = TrimOrEmpty(selectedTemplate.VisitorPetDefinitionKey),
                VisitorDisplayName = ResolveVisitorDisplayName(selectedTemplate, displayName),
                SourceEncounterPointId = encounter.EncounterPointId,
                SourceNodeId = encounter.SourceNodeId,
                EventTemplateId = selectedTemplate.EventTemplateId,
                StopType = selectedTemplate.StopType,
                Progress = ClampEventProgress(encounter.Progress)
            });

            addedAny = true;
        }

        return addedAny;
    }

    private static void PopulateLegacyFallbackEvents(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, PawPalGameRuntime runtime, PawPalDogState dog, System.Random random)
    {
        AddPresentEvents(session, plan, runtime, dog, random);
        AddDogEncounterEvent(session, plan, dog, random);
        AddPersonalityMomentEvent(session, plan, dog, random);
    }

    private static void AddPresentEvents(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, PawPalGameRuntime runtime, PawPalDogState dog, System.Random random)
    {
        float chance = Mathf.Clamp01((0.18f + plan.RouteDistance * 0.005f) * PawPalDogPersonalityProfiles.GetWalkPresentChanceMultiplier(dog));
        if (random.NextDouble() > chance)
        {
            return;
        }

        string rewardItemId = PickRewardItemId(runtime, random);
        session.GeneratedEvents.Add(new PawPalWalkGeneratedEventState
        {
            EventId = "present_legacy",
            EventType = PawPalWalkEventType.PresentFound,
            DisplayName = "Present",
            RewardItemId = rewardItemId,
            StopType = PawPalWalkStopType.Sniff,
            Progress = ClampEventProgress(Mathf.Lerp(0.24f, 0.76f, (float)random.NextDouble()))
        });
    }

    private static void AddDogEncounterEvent(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, PawPalDogState dog, System.Random random)
    {
        float chance = Mathf.Clamp01((0.24f + plan.RouteDistance * 0.0035f) * PawPalDogPersonalityProfiles.GetWalkDogEncounterChanceMultiplier(dog));
        if (random.NextDouble() > chance)
        {
            return;
        }

        session.GeneratedEvents.Add(new PawPalWalkGeneratedEventState
        {
            EventId = "dog_encounter_legacy",
            EventType = PawPalWalkEventType.DogEncounter,
            DisplayName = FallbackEncounterNames[random.Next(0, FallbackEncounterNames.Length)],
            VisitorSpecies = IntroPetSpecies.Dog,
            StopType = PawPalWalkStopType.Bark,
            Progress = ClampEventProgress(Mathf.Lerp(0.28f, 0.72f, (float)random.NextDouble()))
        });
    }

    private static void AddPersonalityMomentEvent(PawPalWalkSessionSaveData session, PawPalWalkRoutePlan plan, PawPalDogState dog, System.Random random)
    {
        if (dog == null || plan.RouteDistance < 8f || random.NextDouble() > 0.45d)
        {
            return;
        }

        session.GeneratedEvents.Add(new PawPalWalkGeneratedEventState
        {
            EventId = "personality_moment_legacy",
            EventType = PawPalWalkEventType.PersonalityMoment,
            DisplayName = PawPalDogPersonalityProfiles.GetWalkMomentTitle(dog),
            BodyText = PawPalDogPersonalityProfiles.GetWalkMomentBody(dog),
            StopType = PawPalWalkStopType.Sniff,
            Progress = ClampEventProgress(Mathf.Lerp(0.22f, 0.78f, (float)random.NextDouble()))
        });
    }

    private static PawPalWalkEncounterTemplate SelectTemplate(IList<PawPalWalkEncounterTemplate> templates, System.Random random)
    {
        float totalWeight = 0f;
        for (int i = 0; i < templates.Count; i++)
        {
            PawPalWalkEncounterTemplate template = templates[i];
            if (template != null)
            {
                totalWeight += Mathf.Max(0f, template.Weight);
            }
        }

        if (totalWeight <= 0.001f)
        {
            return null;
        }

        float roll = (float)random.NextDouble() * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < templates.Count; i++)
        {
            PawPalWalkEncounterTemplate template = templates[i];
            if (template == null)
            {
                continue;
            }

            cumulative += Mathf.Max(0f, template.Weight);
            if (roll <= cumulative)
            {
                return template;
            }
        }

        return templates[templates.Count - 1];
    }

    private static string ResolveDisplayName(PawPalWalkEncounterTemplate template, System.Random random)
    {
        if (template == null)
        {
            return string.Empty;
        }

        if (template.EventType == PawPalWalkEventType.DogEncounter)
        {
            if (!string.IsNullOrWhiteSpace(template.EncounterDogName))
            {
                return template.EncounterDogName.Trim();
            }

            return FallbackEncounterNames[random.Next(0, FallbackEncounterNames.Length)];
        }

        if (!string.IsNullOrWhiteSpace(template.DisplayName))
        {
            return template.DisplayName.Trim();
        }

        switch (template.EventType)
        {
            case PawPalWalkEventType.PresentFound:
                return "Present";
            case PawPalWalkEventType.LocationVisit:
                return "Street Corner";
            case PawPalWalkEventType.PersonalityMoment:
                return "Moment";
            default:
                return "Encounter";
        }
    }

    private static string ResolveRewardItemId(PawPalWalkEncounterTemplate template, PawPalGameRuntime runtime, System.Random random)
    {
        if (template == null || template.EventType != PawPalWalkEventType.PresentFound)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(template.RewardItemId) && runtime != null && runtime.GetCatalogItem(template.RewardItemId) != null)
        {
            return template.RewardItemId.Trim();
        }

        return PickRewardItemId(runtime, random);
    }

    private static string ResolveVisitorDisplayName(PawPalWalkEncounterTemplate template, string fallbackDisplayName)
    {
        if (template == null || template.EventType != PawPalWalkEventType.DogEncounter)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(template.VisitorDisplayName))
        {
            return template.VisitorDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(template.EncounterDogName))
        {
            return template.EncounterDogName.Trim();
        }

        return TrimOrEmpty(fallbackDisplayName);
    }

    private static string TrimOrEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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

        float previous = 0.08f;
        for (int i = 0; i < events.Count; i++)
        {
            PawPalWalkGeneratedEventState walkEvent = events[i];
            walkEvent.Progress = Mathf.Clamp(walkEvent.Progress, previous + 0.06f, 0.94f);
            previous = walkEvent.Progress;
        }
    }

    private static float ClampEventProgress(float progress)
    {
        return Mathf.Clamp(progress, 0.08f, 0.94f);
    }
}
