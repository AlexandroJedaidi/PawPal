using System;
using System.Collections.Generic;
using UnityEngine;

public static class PawPalObedienceTrialDatabase
{
    private const string ConfigResourcePath = "PawPal/Competition/ObedienceTrialConfig";

    private static PawPalObedienceTrialConfig config;

    public static PawPalObedienceTrialConfig Config
    {
        get
        {
            if (config == null)
            {
                config = Resources.Load<PawPalObedienceTrialConfig>(ConfigResourcePath);
            }

            if (config == null || config.Levels == null || config.Levels.Count == 0)
            {
                config = CreateRuntimeDefaultConfig();
            }

            return config;
        }
    }

    public static IReadOnlyList<PawPalObedienceTrialLevelDefinition> Levels
    {
        get { return Config.Levels; }
    }

    public static PawPalObedienceTrialLevelDefinition GetLevel(PawPalObedienceTrialLevelId levelId)
    {
        IReadOnlyList<PawPalObedienceTrialLevelDefinition> levels = Levels;
        for (int i = 0; i < levels.Count; i++)
        {
            PawPalObedienceTrialLevelDefinition level = levels[i];
            if (level != null && level.LevelId == levelId)
            {
                return level;
            }
        }

        return null;
    }

    public static PawPalObedienceTrialProgress EnsureProgress(PawPalDogState pet)
    {
        if (pet == null)
        {
            return null;
        }

        if (pet.ObedienceTrialProgress == null)
        {
            pet.ObedienceTrialProgress = new PawPalObedienceTrialProgress();
        }

        if (string.IsNullOrWhiteSpace(pet.ObedienceTrialProgress.HighestUnlockedLevel))
        {
            pet.ObedienceTrialProgress.HighestUnlockedLevel = PawPalObedienceTrialLevelId.Beginner.ToString();
        }

        if (pet.ObedienceTrialProgress.Levels == null)
        {
            pet.ObedienceTrialProgress.Levels = new List<PawPalObedienceTrialLevelProgress>();
        }

        if (pet.ObedienceTrialProgress.GrantedRewardIds == null)
        {
            pet.ObedienceTrialProgress.GrantedRewardIds = new List<string>();
        }

        return pet.ObedienceTrialProgress;
    }

    public static PawPalObedienceTrialLevelProgress GetOrCreateLevelProgress(PawPalObedienceTrialProgress progress, PawPalObedienceTrialLevelId levelId)
    {
        if (progress == null)
        {
            return null;
        }

        if (progress.Levels == null)
        {
            progress.Levels = new List<PawPalObedienceTrialLevelProgress>();
        }

        string id = levelId.ToString();
        for (int i = 0; i < progress.Levels.Count; i++)
        {
            PawPalObedienceTrialLevelProgress candidate = progress.Levels[i];
            if (candidate != null && string.Equals(candidate.LevelId, id, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        PawPalObedienceTrialLevelProgress created = new PawPalObedienceTrialLevelProgress
        {
            LevelId = id
        };
        progress.Levels.Add(created);
        return created;
    }

    public static PawPalObedienceTrialProgress CloneProgress(PawPalObedienceTrialProgress source)
    {
        PawPalObedienceTrialProgress clone = new PawPalObedienceTrialProgress();
        if (source == null)
        {
            return clone;
        }

        clone.HighestUnlockedLevel = string.IsNullOrWhiteSpace(source.HighestUnlockedLevel)
            ? PawPalObedienceTrialLevelId.Beginner.ToString()
            : source.HighestUnlockedLevel;
        clone.CompletedCount = source.CompletedCount;
        clone.GoldCount = source.GoldCount;
        clone.LastCompletedUtcTicks = source.LastCompletedUtcTicks;

        if (source.Levels != null)
        {
            for (int i = 0; i < source.Levels.Count; i++)
            {
                PawPalObedienceTrialLevelProgress level = source.Levels[i];
                if (level == null)
                {
                    continue;
                }

                clone.Levels.Add(new PawPalObedienceTrialLevelProgress
                {
                    LevelId = level.LevelId,
                    BestScore = level.BestScore,
                    BestMedal = level.BestMedal,
                    CompletedCount = level.CompletedCount
                });
            }
        }

        if (source.GrantedRewardIds != null)
        {
            for (int i = 0; i < source.GrantedRewardIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(source.GrantedRewardIds[i]))
                {
                    clone.GrantedRewardIds.Add(source.GrantedRewardIds[i]);
                }
            }
        }

        return clone;
    }

    public static bool IsLevelUnlocked(PawPalDogState pet, PawPalObedienceTrialLevelDefinition level)
    {
        PawPalObedienceTrialProgress progress = EnsureProgress(pet);
        if (progress == null || level == null)
        {
            return false;
        }

        PawPalObedienceTrialLevelId unlockedId;
        if (!TryParseLevel(progress.HighestUnlockedLevel, out unlockedId))
        {
            unlockedId = PawPalObedienceTrialLevelId.Beginner;
        }

        PawPalObedienceTrialLevelDefinition unlocked = GetLevel(unlockedId);
        int unlockedOrder = unlocked != null ? unlocked.Order : 0;
        return level.Order <= unlockedOrder;
    }

    public static bool CanEnterLevel(PawPalDogState pet, IntroPetSpecies species, PawPalObedienceTrialLevelDefinition level, out string reason)
    {
        reason = string.Empty;
        if (pet == null)
        {
            reason = "Select a pet before entering.";
            return false;
        }

        if (level == null)
        {
            reason = "Trial level data is missing.";
            return false;
        }

        if (!level.Playable)
        {
            reason = string.IsNullOrWhiteSpace(level.LockedReason)
                ? "This level is waiting for real trick animation mappings."
                : level.LockedReason;
            return false;
        }

        if (!IsLevelUnlocked(pet, level))
        {
            reason = "Earn Silver or Gold in the previous playable level to unlock this trial.";
            return false;
        }

        if (!MeetsReadiness(pet, level, out reason))
        {
            return false;
        }

        for (int i = 0; i < level.RequiredTricks.Count; i++)
        {
            PawPalTrickId trickId = level.RequiredTricks[i];
            if (!IsTrialEligible(species, trickId))
            {
                reason = GetMappingIssue(species, trickId);
                return false;
            }

            PawPalDogTrickProgress progress = PawPalTrickCatalog.GetProgress(pet, trickId);
            if (progress == null || !progress.IsLearned || progress.MasteryLevel < level.MinimumMasteryLevel)
            {
                reason = "Learn and practice " + GetTrickLabel(trickId) + " before entering.";
                return false;
            }
        }

        return true;
    }

    public static bool MeetsReadiness(PawPalDogState pet, PawPalObedienceTrialLevelDefinition level, out string reason)
    {
        reason = string.Empty;
        if (pet == null || level == null)
        {
            return false;
        }

        float needAverage = (pet.Food01 + pet.Water01 + pet.Hygiene01 + pet.Activity01 + pet.Energy01) / 5f;
        if (needAverage < level.MinimumNeedAverage01 || pet.Mood01 < level.MinimumMood01 || pet.Energy01 < level.MinimumEnergy01)
        {
            reason = "Your pet needs better mood, energy, and care before this trial.";
            return false;
        }

        return true;
    }

    public static bool IsTrialEligible(IntroPetSpecies species, PawPalTrickId trickId)
    {
        PawPalObedienceTrialAnimationMapping mapping = FindMapping(species, trickId);
        return mapping != null && mapping.TrialEligible;
    }

    public static bool IsFreestyleEligible(IntroPetSpecies species, PawPalTrickId trickId)
    {
        PawPalObedienceTrialAnimationMapping mapping = FindMapping(species, trickId);
        return mapping != null && (mapping.TrialEligible || mapping.OptionalFreestyleEligible);
    }

    public static PawPalObedienceTrialAnimationMapping FindMapping(IntroPetSpecies species, PawPalTrickId trickId)
    {
        List<PawPalObedienceTrialAnimationMapping> mappings = Config.AnimationMappings;
        if (mappings == null)
        {
            return null;
        }

        for (int i = 0; i < mappings.Count; i++)
        {
            PawPalObedienceTrialAnimationMapping mapping = mappings[i];
            if (mapping != null && mapping.Species == species && mapping.TrickId == trickId)
            {
                return mapping;
            }
        }

        return null;
    }

    public static string GetMappingIssue(IntroPetSpecies species, PawPalTrickId trickId)
    {
        PawPalObedienceTrialAnimationMapping mapping = FindMapping(species, trickId);
        if (mapping != null && !string.IsNullOrWhiteSpace(mapping.MissingReason))
        {
            return mapping.MissingReason;
        }

        return GetTrickLabel(trickId) + " is not enabled for " + species + " obedience trials yet.";
    }

    public static string GetTrickLabel(PawPalTrickId trickId)
    {
        return PawPalTrickCatalog.GetCommandLabel(trickId);
    }

    public static List<PawPalTrickId> GetLearnedFreestyleTricks(PawPalDogState pet, IntroPetSpecies species, PawPalObedienceTrialLevelDefinition level)
    {
        List<PawPalTrickId> tricks = new List<PawPalTrickId>();
        if (pet == null || level == null)
        {
            return tricks;
        }

        AddLearnedEligibleTricks(tricks, pet, species, level.RequiredTricks, false);
        AddLearnedEligibleTricks(tricks, pet, species, level.OptionalFreestyleTricks, true);
        return tricks;
    }

    public static PawPalObedienceTrialMedal GetMedal(int score)
    {
        PawPalObedienceTrialConfig activeConfig = Config;
        if (score >= activeConfig.GoldScoreThreshold)
        {
            return PawPalObedienceTrialMedal.Gold;
        }

        if (score >= activeConfig.SilverScoreThreshold)
        {
            return PawPalObedienceTrialMedal.Silver;
        }

        if (score >= activeConfig.BronzeScoreThreshold)
        {
            return PawPalObedienceTrialMedal.Bronze;
        }

        return PawPalObedienceTrialMedal.None;
    }

    public static int GetRewardCurrency(PawPalObedienceTrialLevelDefinition level, PawPalObedienceTrialMedal medal)
    {
        if (level == null || level.Rewards == null)
        {
            return 0;
        }

        switch (medal)
        {
            case PawPalObedienceTrialMedal.Gold:
                return level.Rewards.GoldBasicCurrency;
            case PawPalObedienceTrialMedal.Silver:
                return level.Rewards.SilverBasicCurrency;
            case PawPalObedienceTrialMedal.Bronze:
                return level.Rewards.BronzeBasicCurrency;
            default:
                return 0;
        }
    }

    public static void ApplyCompletedRun(PawPalObedienceTrialRun run, out bool unlockedNext)
    {
        unlockedNext = false;
        if (run == null || run.Pet == null || run.Level == null)
        {
            return;
        }

        PawPalObedienceTrialProgress progress = EnsureProgress(run.Pet);
        PawPalObedienceTrialLevelProgress levelProgress = GetOrCreateLevelProgress(progress, run.Level.LevelId);
        levelProgress.CompletedCount++;
        progress.CompletedCount++;
        progress.LastCompletedUtcTicks = DateTime.UtcNow.Ticks;

        if (run.FinalScore > levelProgress.BestScore)
        {
            levelProgress.BestScore = run.FinalScore;
        }

        if (run.Medal > levelProgress.BestMedal)
        {
            levelProgress.BestMedal = run.Medal;
        }

        if (run.Medal == PawPalObedienceTrialMedal.Gold)
        {
            progress.GoldCount++;
        }

        if (run.Medal == PawPalObedienceTrialMedal.Silver || run.Medal == PawPalObedienceTrialMedal.Gold)
        {
            PawPalObedienceTrialLevelDefinition next = GetNextPlayableLevel(run.Level);
            if (next != null && !IsLevelUnlocked(run.Pet, next))
            {
                progress.HighestUnlockedLevel = next.LevelId.ToString();
                unlockedNext = true;
            }
        }
    }

    public static PawPalObedienceTrialLevelDefinition GetNextPlayableLevel(PawPalObedienceTrialLevelDefinition current)
    {
        if (current == null)
        {
            return null;
        }

        PawPalObedienceTrialLevelDefinition best = null;
        IReadOnlyList<PawPalObedienceTrialLevelDefinition> levels = Levels;
        for (int i = 0; i < levels.Count; i++)
        {
            PawPalObedienceTrialLevelDefinition level = levels[i];
            if (level == null || !level.Playable || level.Order <= current.Order)
            {
                continue;
            }

            if (best == null || level.Order < best.Order)
            {
                best = level;
            }
        }

        return best;
    }

    public static int CalculateFinalScore(List<PawPalObedienceTrialRoundResult> results)
    {
        if (results == null || results.Count == 0)
        {
            return 0;
        }

        float weightedScore = 0f;
        float weightTotal = 0f;
        for (int i = 0; i < results.Count; i++)
        {
            PawPalObedienceTrialRoundResult result = results[i];
            if (result == null || result.Definition == null)
            {
                continue;
            }

            float weight = Mathf.Max(0.1f, result.Definition.ScoringWeight);
            weightedScore += Mathf.Clamp(result.Score, 0, 100) * weight;
            weightTotal += weight;
        }

        return weightTotal > 0f ? Mathf.RoundToInt(weightedScore / weightTotal) : 0;
    }

    public static string BuildRewardId(PawPalDogState pet, PawPalObedienceTrialLevelDefinition level, PawPalObedienceTrialMedal medal)
    {
        string petId = pet != null && !string.IsNullOrWhiteSpace(pet.Id) ? pet.Id : "pet";
        string levelId = level != null ? level.LevelId.ToString() : "level";
        return "obedience_trial_v1:" + petId + ":" + levelId + ":" + medal;
    }

    private static void AddLearnedEligibleTricks(List<PawPalTrickId> output, PawPalDogState pet, IntroPetSpecies species, List<PawPalTrickId> source, bool freestyleOnlyAllowed)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            PawPalTrickId trick = source[i];
            if (output.Contains(trick))
            {
                continue;
            }

            bool eligible = freestyleOnlyAllowed ? IsFreestyleEligible(species, trick) : IsTrialEligible(species, trick);
            PawPalDogTrickProgress progress = PawPalTrickCatalog.GetProgress(pet, trick);
            if (eligible && progress != null && progress.IsLearned)
            {
                output.Add(trick);
            }
        }
    }

    private static bool TryParseLevel(string value, out PawPalObedienceTrialLevelId levelId)
    {
        try
        {
            levelId = (PawPalObedienceTrialLevelId)Enum.Parse(typeof(PawPalObedienceTrialLevelId), value, true);
            return true;
        }
        catch
        {
            levelId = PawPalObedienceTrialLevelId.Beginner;
            return false;
        }
    }

    private static PawPalObedienceTrialConfig CreateRuntimeDefaultConfig()
    {
        PawPalObedienceTrialConfig created = ScriptableObject.CreateInstance<PawPalObedienceTrialConfig>();
        created.name = "RuntimeObedienceTrialConfig";
        created.GoldScoreThreshold = 85;
        created.SilverScoreThreshold = 70;
        created.BronzeScoreThreshold = 55;

        AddMappings(created, IntroPetSpecies.Dog, true, false, string.Empty, PawPalTrickId.Sit, PawPalTrickId.Lie, PawPalTrickId.Jump);
        AddMappings(created, IntroPetSpecies.Cat, true, false, string.Empty, PawPalTrickId.Sit, PawPalTrickId.Lie, PawPalTrickId.Jump);
        AddMappings(created, IntroPetSpecies.Cat, false, true, "Cat spin is optional freestyle only and is not required by V1 levels.", PawPalTrickId.Spin);
        AddBlockedMapping(created, IntroPetSpecies.Dog, PawPalTrickId.Spin, "Dog Spin is locked until a real obedience-safe spin animation mapping exists.");
        AddBlockedMapping(created, IntroPetSpecies.Dog, PawPalTrickId.Shake, "Shake/Paw is locked until a real paw animation mapping exists.");
        AddBlockedMapping(created, IntroPetSpecies.Cat, PawPalTrickId.Shake, "Shake/Paw is locked until a real cat paw animation mapping exists.");
        AddBlockedMapping(created, IntroPetSpecies.Dog, PawPalTrickId.Come, "Come is locked until a reliable recall animation/locomotion mapping exists.");
        AddBlockedMapping(created, IntroPetSpecies.Cat, PawPalTrickId.Come, "Come is locked until a reliable recall animation/locomotion mapping exists.");
        AddBlockedMapping(created, IntroPetSpecies.Dog, PawPalTrickId.Pose, "Pose is locked until competition pose mappings exist.");
        AddBlockedMapping(created, IntroPetSpecies.Cat, PawPalTrickId.Pose, "Pose is locked until competition pose mappings exist.");

        created.Levels.Add(CreateBeginnerLevel());
        created.Levels.Add(CreateAmateurLevel());
        created.Levels.Add(CreateLockedLevel(PawPalObedienceTrialLevelId.Pro, 2, "Pro", "Pro requires real Shake/Paw and Come mappings before it can be played."));
        created.Levels.Add(CreateLockedLevel(PawPalObedienceTrialLevelId.Master, 3, "Master", "Master requires real Shake/Paw, Come, Pose, and dog Spin mappings before it can be played."));
        created.Levels.Add(CreateLockedLevel(PawPalObedienceTrialLevelId.Champion, 4, "Champion", "Champion remains locked until the full advanced obedience animation set exists."));
        return created;
    }

    private static PawPalObedienceTrialLevelDefinition CreateBeginnerLevel()
    {
        PawPalObedienceTrialLevelDefinition level = new PawPalObedienceTrialLevelDefinition
        {
            LevelId = PawPalObedienceTrialLevelId.Beginner,
            DisplayName = "Beginner",
            Order = 0,
            Playable = true,
            MinimumMasteryLevel = 1,
            MinimumMood01 = 0.25f,
            MinimumEnergy01 = 0.25f,
            MinimumNeedAverage01 = 0.25f
        };
        level.RequiredTricks.Add(PawPalTrickId.Sit);
        level.RequiredTricks.Add(PawPalTrickId.Lie);
        level.RequiredTricks.Add(PawPalTrickId.Jump);
        level.OptionalFreestyleTricks.Add(PawPalTrickId.Spin);
        level.Rewards.EntryFee = 20;
        level.Rewards.BronzeBasicCurrency = 35;
        level.Rewards.SilverBasicCurrency = 55;
        level.Rewards.GoldBasicCurrency = 80;
        level.Rounds.Add(CreateRound("beginner_single", "Single command", PawPalObedienceTrialRoundType.SingleCommand, 8f, 0f, PawPalTrickId.Sit));
        level.Rounds.Add(CreateRound("beginner_hold", "Hold", PawPalObedienceTrialRoundType.Hold, 5f, 2f, PawPalTrickId.Lie));
        level.Rounds.Add(CreateSequenceRound("beginner_sequence", "Sequence", 12f, PawPalTrickId.Sit, PawPalTrickId.Jump));
        level.Rounds.Add(CreateFreestyleRound("beginner_freestyle", "Freestyle", 10f));
        return level;
    }

    private static PawPalObedienceTrialLevelDefinition CreateAmateurLevel()
    {
        PawPalObedienceTrialLevelDefinition level = new PawPalObedienceTrialLevelDefinition
        {
            LevelId = PawPalObedienceTrialLevelId.Amateur,
            DisplayName = "Amateur",
            Order = 1,
            Playable = true,
            MinimumMasteryLevel = 2,
            MinimumMood01 = 0.45f,
            MinimumEnergy01 = 0.45f,
            MinimumNeedAverage01 = 0.45f
        };
        level.RequiredTricks.Add(PawPalTrickId.Sit);
        level.RequiredTricks.Add(PawPalTrickId.Lie);
        level.RequiredTricks.Add(PawPalTrickId.Jump);
        level.OptionalFreestyleTricks.Add(PawPalTrickId.Spin);
        level.Rewards.EntryFee = 35;
        level.Rewards.BronzeBasicCurrency = 50;
        level.Rewards.SilverBasicCurrency = 80;
        level.Rewards.GoldBasicCurrency = 120;
        level.Rounds.Add(CreateRound("amateur_single", "Fast command", PawPalObedienceTrialRoundType.SingleCommand, 7f, 0f, PawPalTrickId.Jump));
        level.Rounds.Add(CreateRound("amateur_hold", "Long hold", PawPalObedienceTrialRoundType.Hold, 7f, 3.5f, PawPalTrickId.Sit));
        level.Rounds.Add(CreateSequenceRound("amateur_sequence", "Long sequence", 15f, PawPalTrickId.Sit, PawPalTrickId.Lie, PawPalTrickId.Jump));
        level.Rounds.Add(CreateFreestyleRound("amateur_freestyle", "Freestyle", 12f));
        return level;
    }

    private static PawPalObedienceTrialLevelDefinition CreateLockedLevel(PawPalObedienceTrialLevelId levelId, int order, string displayName, string reason)
    {
        PawPalObedienceTrialLevelDefinition level = new PawPalObedienceTrialLevelDefinition
        {
            LevelId = levelId,
            DisplayName = displayName,
            Order = order,
            Playable = false,
            LockedReason = reason,
            MinimumMasteryLevel = 2
        };
        level.RequiredTricks.Add(PawPalTrickId.Shake);
        level.RequiredTricks.Add(PawPalTrickId.Come);
        level.RequiredTricks.Add(PawPalTrickId.Pose);
        level.RequiredTricks.Add(PawPalTrickId.Spin);
        return level;
    }

    private static PawPalObedienceTrialRoundDefinition CreateRound(string id, string displayName, PawPalObedienceTrialRoundType type, float timeLimit, float holdDuration, PawPalTrickId trick)
    {
        PawPalObedienceTrialRoundDefinition round = new PawPalObedienceTrialRoundDefinition
        {
            Id = id,
            DisplayName = displayName,
            RoundType = type,
            TimeLimitSeconds = timeLimit,
            HoldDurationSeconds = holdDuration,
            SequenceLength = 1,
            ScoringWeight = 1f
        };
        round.RequestedTricks.Add(trick);
        round.AllowedTricks.Add(trick);
        return round;
    }

    private static PawPalObedienceTrialRoundDefinition CreateSequenceRound(string id, string displayName, float timeLimit, params PawPalTrickId[] tricks)
    {
        PawPalObedienceTrialRoundDefinition round = new PawPalObedienceTrialRoundDefinition
        {
            Id = id,
            DisplayName = displayName,
            RoundType = PawPalObedienceTrialRoundType.Sequence,
            TimeLimitSeconds = timeLimit,
            SequenceLength = tricks != null ? tricks.Length : 0,
            ScoringWeight = 1.15f
        };

        if (tricks != null)
        {
            for (int i = 0; i < tricks.Length; i++)
            {
                round.RequestedTricks.Add(tricks[i]);
                round.AllowedTricks.Add(tricks[i]);
            }
        }

        return round;
    }

    private static PawPalObedienceTrialRoundDefinition CreateFreestyleRound(string id, string displayName, float timeLimit)
    {
        return new PawPalObedienceTrialRoundDefinition
        {
            Id = id,
            DisplayName = displayName,
            RoundType = PawPalObedienceTrialRoundType.Freestyle,
            TimeLimitSeconds = timeLimit,
            ScoringWeight = 1f
        };
    }

    private static void AddMappings(PawPalObedienceTrialConfig target, IntroPetSpecies species, bool trialEligible, bool freestyleEligible, string reason, params PawPalTrickId[] tricks)
    {
        for (int i = 0; i < tricks.Length; i++)
        {
            target.AnimationMappings.Add(new PawPalObedienceTrialAnimationMapping
            {
                Species = species,
                TrickId = tricks[i],
                DisplayLabel = GetTrickLabel(tricks[i]),
                TrialEligible = trialEligible,
                OptionalFreestyleEligible = freestyleEligible,
                MissingReason = reason
            });
        }
    }

    private static void AddBlockedMapping(PawPalObedienceTrialConfig target, IntroPetSpecies species, PawPalTrickId trick, string reason)
    {
        AddMappings(target, species, false, false, reason, trick);
    }
}
