using System;
using System.Collections.Generic;
using UnityEngine;

public static class PawPalTrickCatalog
{
    private const string ResourceFolder = "PawPal/Training";

    private static readonly PawPalTrickId[] CoreOrder =
    {
        PawPalTrickId.Sit,
        PawPalTrickId.Lie,
        PawPalTrickId.Shake,
        PawPalTrickId.Jump,
        PawPalTrickId.Spin
    };

    private static readonly List<PawPalTrickDefinition> definitions = new List<PawPalTrickDefinition>();
    private static readonly List<PawPalTrickDefinition> coreDefinitions = new List<PawPalTrickDefinition>();
    private static readonly Dictionary<PawPalTrickId, PawPalTrickDefinition> byId = new Dictionary<PawPalTrickId, PawPalTrickDefinition>();
    private static bool loaded;

    public static IReadOnlyList<PawPalTrickDefinition> Definitions
    {
        get
        {
            EnsureLoaded();
            return definitions;
        }
    }

    public static IReadOnlyList<PawPalTrickDefinition> CoreDefinitions
    {
        get
        {
            EnsureLoaded();
            return coreDefinitions;
        }
    }

    public static PawPalTrickDefinition GetDefinition(PawPalTrickId trickId)
    {
        EnsureLoaded();
        PawPalTrickDefinition definition;
        return byId.TryGetValue(trickId, out definition) ? definition : null;
    }

    public static bool HasDefinition(PawPalTrickId trickId)
    {
        EnsureLoaded();
        return byId.ContainsKey(trickId);
    }

    public static PawPalDogTrickProgress GetProgress(PawPalDogState dog, PawPalTrickId trickId)
    {
        if (dog == null || dog.Tricks == null)
        {
            return null;
        }

        string id = trickId.ToString();
        for (int i = 0; i < dog.Tricks.Count; i++)
        {
            PawPalDogTrickProgress progress = dog.Tricks[i];
            if (progress != null && string.Equals(progress.TrickId, id, StringComparison.OrdinalIgnoreCase))
            {
                return progress;
            }
        }

        return null;
    }

    public static PawPalDogTrickProgress GetOrCreateProgress(PawPalDogState dog, PawPalTrickId trickId)
    {
        if (dog == null)
        {
            return null;
        }

        EnsureDogTrickData(dog);
        PawPalDogTrickProgress progress = GetProgress(dog, trickId);
        if (progress != null)
        {
            return progress;
        }

        progress = CreateProgress(trickId);
        dog.Tricks.Add(progress);
        return progress;
    }

    public static bool EnsureDogTrickData(PawPalDogState dog)
    {
        if (dog == null)
        {
            return false;
        }

        bool changed = false;
        if (dog.Tricks == null)
        {
            dog.Tricks = new List<PawPalDogTrickProgress>();
            changed = true;
        }

        Dictionary<string, PawPalDogTrickProgress> seen = new Dictionary<string, PawPalDogTrickProgress>(StringComparer.OrdinalIgnoreCase);
        for (int i = dog.Tricks.Count - 1; i >= 0; i--)
        {
            PawPalDogTrickProgress progress = dog.Tricks[i];
            if (progress == null)
            {
                dog.Tricks.RemoveAt(i);
                changed = true;
                continue;
            }

            PawPalTrickId trickId = progress.ParsedTrickId;
            progress.TrickId = trickId.ToString();
            progress.MasteryXp = Mathf.Max(0f, progress.MasteryXp);
            progress.MasteryLevel = Mathf.Clamp(progress.MasteryLevel, 0, 2);
            progress.TimesPracticedToday = Mathf.Max(0, progress.TimesPracticedToday);
            progress.TotalSuccessfulAttempts = Mathf.Max(0, progress.TotalSuccessfulAttempts);
            progress.TotalFailedAttempts = Mathf.Max(0, progress.TotalFailedAttempts);
            progress.CommandConfidence = Mathf.Clamp01(progress.CommandConfidence);

            if (progress.IsLearned && progress.MasteryLevel <= 0)
            {
                progress.MasteryLevel = 1;
                changed = true;
            }

            string normalizedId = progress.TrickId;
            PawPalDogTrickProgress existing;
            if (seen.TryGetValue(normalizedId, out existing))
            {
                MergeProgress(existing, progress);
                dog.Tricks.RemoveAt(i);
                changed = true;
                continue;
            }

            seen[normalizedId] = progress;
        }

        for (int i = 0; i < CoreOrder.Length; i++)
        {
            if (!seen.ContainsKey(CoreOrder[i].ToString()))
            {
                dog.Tricks.Add(CreateProgress(CoreOrder[i]));
                changed = true;
            }
        }

        if (dog.Mood01 <= 0.001f)
        {
            dog.Mood01 = PawPalTrickSaveDefaults.DefaultMood01;
            changed = true;
        }

        float previousBond01 = dog.Bond01;
        int previousBondXp = dog.BondXp;
        int previousBondLevel = dog.BondLevel;
        PawPalGameRuntime.EnsureDogBondProgression(dog);
        if (!Mathf.Approximately(previousBond01, dog.Bond01)
            || previousBondXp != dog.BondXp
            || previousBondLevel != dog.BondLevel)
        {
            changed = true;
        }

        dog.Mood01 = Mathf.Clamp01(dog.Mood01);
        dog.TrainingFatigue01 = Mathf.Clamp01(dog.TrainingFatigue01);
        if (dog.LastTrainingFatigueUpdateUtcTicks <= 0L)
        {
            dog.LastTrainingFatigueUpdateUtcTicks = DateTime.UtcNow.Ticks;
            changed = true;
        }

        if (dog.TrickProfileVersion < PawPalTrickSaveDefaults.CurrentTrickProfileVersion)
        {
            dog.TrickProfileVersion = PawPalTrickSaveDefaults.CurrentTrickProfileVersion;
            changed = true;
        }

        return changed;
    }

    public static string GetCommandLabel(PawPalTrickId trickId)
    {
        PawPalTrickDefinition definition = GetDefinition(trickId);
        if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return definition.DisplayName.ToLowerInvariant();
        }

        return trickId.ToString().ToLowerInvariant();
    }

    public static PawPalTrickLearningStage GetLearningStage(PawPalDogTrickProgress progress, PawPalTrickDefinition definition)
    {
        if (progress == null)
        {
            return PawPalTrickLearningStage.Undiscovered;
        }

        if (definition != null && progress.MasteryXp >= definition.MasteryRequiredXp)
        {
            return PawPalTrickLearningStage.Mastered;
        }

        if (progress.IsLearned || progress.MasteryLevel >= 1)
        {
            return PawPalTrickLearningStage.Learned;
        }

        if (progress.MasteryXp > 0.001f)
        {
            return PawPalTrickLearningStage.Practicing;
        }

        return progress.IsDiscovered ? PawPalTrickLearningStage.Discovered : PawPalTrickLearningStage.Undiscovered;
    }

    public static float GetProgress01(PawPalDogTrickProgress progress, PawPalTrickDefinition definition)
    {
        if (progress == null || definition == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(progress.MasteryXp / Mathf.Max(1f, definition.LearnedRequiredXp));
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        definitions.Clear();
        coreDefinitions.Clear();
        byId.Clear();

        PawPalTrickDefinition[] assetDefinitions = Resources.LoadAll<PawPalTrickDefinition>(ResourceFolder);
        if (assetDefinitions != null)
        {
            for (int i = 0; i < assetDefinitions.Length; i++)
            {
                AddDefinition(assetDefinitions[i]);
            }
        }

        AddFallbackDefinitions();
        BuildCoreDefinitions();
    }

    private static void AddDefinition(PawPalTrickDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        if (byId.ContainsKey(definition.Id))
        {
            return;
        }

        definitions.Add(definition);
        byId[definition.Id] = definition;
    }

    private static void AddFallbackDefinitions()
    {
        AddDefinition(CreateRuntimeDefinition(
            PawPalTrickId.Sit,
            "Sit",
            "A calm sit for basic obedience and photo poses.",
            1,
            0,
            1,
            0f,
            0.12f,
            0.1f,
            PawPalGestureType.SwipeDown,
            PawPalDogBodyZone.Chest,
            PawPalDogBodyZone.GroundNearDog,
            "Sit",
            "icon_dog_brand",
            120f,
            264f,
            8,
            "sit",
            10,
            string.Empty,
            new[] { "Swipe down on your dog's chest.", "Practice calmly when stamina is steady." },
            null,
            BuildPersonalityModifiers(PawPalTrickId.Sit),
            true));

        AddDefinition(CreateRuntimeDefinition(
            PawPalTrickId.Lie,
            "Lie",
            "A relaxed lie-down used by photo mode and obedience readiness.",
            2,
            2,
            2,
            0.05f,
            0.18f,
            0.12f,
            PawPalGestureType.SwipeDown,
            PawPalDogBodyZone.Chest,
            PawPalDogBodyZone.GroundNearDog,
            "LieBellyStart",
            "icon_dogbed_brand",
            180f,
            414f,
            7,
            "lie",
            12,
            string.Empty,
            new[] { "Swipe down again after Sit.", "A calm dog learns Lie faster." },
            new[] { PawPalTrickId.Sit },
            BuildPersonalityModifiers(PawPalTrickId.Lie),
            true));

        AddDefinition(CreateRuntimeDefinition(
            PawPalTrickId.Shake,
            "Shake",
            "A friendly paw shake for bond-focused training.",
            2,
            4,
            3,
            0.08f,
            0.15f,
            0.12f,
            PawPalGestureType.DragFromBodyPart,
            PawPalDogBodyZone.PawLeft,
            PawPalDogBodyZone.Any,
            "Shake",
            "icon_paw_brand",
            240f,
            552f,
            8,
            string.Empty,
            12,
            string.Empty,
            new[] { "Drag gently from a front paw.", "Praise after paw gestures to build trust." },
            new[] { PawPalTrickId.Lie },
            BuildPersonalityModifiers(PawPalTrickId.Shake),
            true));

        AddDefinition(CreateRuntimeDefinition(
            PawPalTrickId.Jump,
            "Jump",
            "An energetic hop for basic agility.",
            2,
            6,
            5,
            0.03f,
            0.22f,
            0.24f,
            PawPalGestureType.Tap,
            PawPalDogBodyZone.AirAboveDog,
            PawPalDogBodyZone.AirAboveDog,
            "JumpStart_Place",
            "icon_activity_brand",
            300f,
            690f,
            7,
            string.Empty,
            14,
            string.Empty,
            new[] { "Tap above your dog.", "Try Jump while stamina is high." },
            new[] { PawPalTrickId.Shake },
            BuildPersonalityModifiers(PawPalTrickId.Jump),
            true));

        AddDefinition(CreateRuntimeDefinition(
            PawPalTrickId.Spin,
            "Spin",
            "A playful turn that starts the trick-book rhythm.",
            2,
            8,
            6,
            0.05f,
            0.18f,
            0.16f,
            PawPalGestureType.CircularSwipe,
            PawPalDogBodyZone.Any,
            PawPalDogBodyZone.Any,
            "Turn_L180_IP",
            "icon_nextarrow",
            360f,
            828f,
            7,
            string.Empty,
            13,
            string.Empty,
            new[] { "Draw a circle around your dog.", "Wide, smooth circles are easiest to read." },
            new[] { PawPalTrickId.Jump },
            BuildPersonalityModifiers(PawPalTrickId.Spin),
            true));

        AddDefinition(CreateRuntimeDefinition(PawPalTrickId.RollOver, "Roll Over", "Data-ready advanced trick.", 3, 6, -1, 0.32f, 0.25f, 0.22f, PawPalGestureType.CircularSwipe, PawPalDogBodyZone.Back, PawPalDogBodyZone.Belly, "RollOver", "icon_dogbed_brand", 90f, 180f, 6, string.Empty, 18, string.Empty, new[] { "Coming later: teach Lie first." }, new[] { PawPalTrickId.Lie }, BuildPersonalityModifiers(PawPalTrickId.RollOver), false));
        AddDefinition(CreateRuntimeDefinition(PawPalTrickId.Stay, "Stay", "Data-ready obedience trick.", 2, 4, -1, 0.18f, 0.2f, 0.1f, PawPalGestureType.Hold, PawPalDogBodyZone.Head, PawPalDogBodyZone.Head, "Stay", "icon_star_brand", 70f, 145f, 6, string.Empty, 15, string.Empty, new[] { "Coming later: hold near your dog's head." }, new[] { PawPalTrickId.Sit }, BuildPersonalityModifiers(PawPalTrickId.Stay), false));
        AddDefinition(CreateRuntimeDefinition(PawPalTrickId.Speak, "Speak", "Data-ready voice trick.", 2, 3, -1, 0.12f, 0.22f, 0.12f, PawPalGestureType.Tap, PawPalDogBodyZone.Head, PawPalDogBodyZone.Head, "Bark", "icon_voice_brand", 70f, 145f, 6, string.Empty, 15, string.Empty, new[] { "Coming later: tap near the head." }, null, BuildPersonalityModifiers(PawPalTrickId.Speak), false));
    }

    private static PawPalTrickDefinition CreateRuntimeDefinition(
        PawPalTrickId id,
        string displayName,
        string description,
        int difficulty,
        int requiredFocus,
        int requiredBondLevel,
        float requiredBond01,
        float requiredMood01,
        float requiredEnergy01,
        PawPalGestureType gestureType,
        PawPalDogBodyZone startZone,
        PawPalDogBodyZone endZone,
        string animationStateName,
        string iconName,
        float learnedXp,
        float masteryXp,
        int dailyPracticeLimit,
        string photoPose,
        int competitionScoreValue,
        string toyRequirement,
        string[] hints,
        PawPalTrickId[] requiredKnownTricks,
        PawPalTrickPersonalityModifier[] personalityModifiers,
        bool core)
    {
        PawPalTrickDefinition definition = ScriptableObject.CreateInstance<PawPalTrickDefinition>();
        definition.name = "Runtime Trick " + id;
        definition.ConfigureRuntime(
            id,
            displayName,
            description,
            "Basic",
            difficulty,
            requiredFocus,
            requiredBondLevel,
            requiredBond01,
            requiredMood01,
            requiredEnergy01,
            gestureType,
            startZone,
            endZone,
            animationStateName,
            iconName,
            learnedXp,
            masteryXp,
            dailyPracticeLimit,
            photoPose,
            competitionScoreValue,
            toyRequirement,
            hints,
            requiredKnownTricks,
            personalityModifiers,
            core);
        return definition;
    }

    private static PawPalTrickPersonalityModifier[] BuildPersonalityModifiers(PawPalTrickId trickId)
    {
        List<PawPalTrickPersonalityModifier> modifiers = new List<PawPalTrickPersonalityModifier>
        {
            new PawPalTrickPersonalityModifier
            {
                Personality = PawPalDogPersonality.Clever,
                XpMultiplier = 1.05f,
                SuccessChanceOffset = 0.06f,
                BoredomMultiplier = 1.08f
            },
            new PawPalTrickPersonalityModifier
            {
                Personality = PawPalDogPersonality.Gentle,
                XpMultiplier = 1.02f,
                SuccessChanceOffset = 0.02f,
                BoredomMultiplier = 0.9f
            },
            new PawPalTrickPersonalityModifier
            {
                Personality = PawPalDogPersonality.Mischievous,
                XpMultiplier = 1f,
                SuccessChanceOffset = -0.05f,
                BoredomMultiplier = 1.2f
            }
        };

        if (trickId == PawPalTrickId.Jump || trickId == PawPalTrickId.Spin)
        {
            modifiers.Add(new PawPalTrickPersonalityModifier
            {
                Personality = PawPalDogPersonality.Energetic,
                XpMultiplier = 1.06f,
                SuccessChanceOffset = 0.05f,
                BoredomMultiplier = 0.95f
            });
            modifiers.Add(new PawPalTrickPersonalityModifier
            {
                Personality = PawPalDogPersonality.Playful,
                XpMultiplier = 1.08f,
                SuccessChanceOffset = 0.06f,
                BoredomMultiplier = 0.88f
            });
        }

        if (trickId == PawPalTrickId.Lie || trickId == PawPalTrickId.Stay)
        {
            modifiers.Add(new PawPalTrickPersonalityModifier
            {
                Personality = PawPalDogPersonality.Relaxed,
                XpMultiplier = 1.04f,
                SuccessChanceOffset = 0.04f,
                BoredomMultiplier = 0.8f
            });
        }

        return modifiers.ToArray();
    }

    private static void BuildCoreDefinitions()
    {
        coreDefinitions.Clear();
        for (int i = 0; i < CoreOrder.Length; i++)
        {
            PawPalTrickDefinition definition;
            if (byId.TryGetValue(CoreOrder[i], out definition))
            {
                coreDefinitions.Add(definition);
            }
        }
    }

    private static PawPalDogTrickProgress CreateProgress(PawPalTrickId trickId)
    {
        return new PawPalDogTrickProgress
        {
            TrickId = trickId.ToString(),
            IsDiscovered = trickId == PawPalTrickId.Sit,
            IsLearned = false,
            MasteryXp = 0f,
            MasteryLevel = 0,
            TimesPracticedToday = 0,
            TotalSuccessfulAttempts = 0,
            TotalFailedAttempts = 0,
            CustomVoiceCommand = string.Empty,
            CommandConfidence = 0f,
            LastPracticedAtUtcTicks = 0L,
            LearnedAtUtcTicks = 0L,
            LastPracticeDayUtcTicks = 0L
        };
    }

    private static void MergeProgress(PawPalDogTrickProgress target, PawPalDogTrickProgress source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.IsDiscovered |= source.IsDiscovered;
        target.IsLearned |= source.IsLearned;
        target.MasteryXp = Mathf.Max(target.MasteryXp, source.MasteryXp);
        target.MasteryLevel = Mathf.Max(target.MasteryLevel, source.MasteryLevel);
        target.TimesPracticedToday = Mathf.Max(target.TimesPracticedToday, source.TimesPracticedToday);
        target.TotalSuccessfulAttempts += source.TotalSuccessfulAttempts;
        target.TotalFailedAttempts += source.TotalFailedAttempts;
        if (string.IsNullOrWhiteSpace(target.CustomVoiceCommand))
        {
            target.CustomVoiceCommand = source.CustomVoiceCommand;
        }

        target.CommandConfidence = Mathf.Max(target.CommandConfidence, source.CommandConfidence);
        target.LastPracticedAtUtcTicks = Math.Max(target.LastPracticedAtUtcTicks, source.LastPracticedAtUtcTicks);
        target.LearnedAtUtcTicks = Math.Max(target.LearnedAtUtcTicks, source.LearnedAtUtcTicks);
        target.LastPracticeDayUtcTicks = Math.Max(target.LastPracticeDayUtcTicks, source.LastPracticeDayUtcTicks);
    }
}
