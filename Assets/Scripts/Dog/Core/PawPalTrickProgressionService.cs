using System;
using UnityEngine;

public static class PawPalTrickProgressionService
{
    private const float MinimumSuccessChance = 0.12f;
    private const float MaximumSuccessChance = 0.96f;
    private const float FatigueRecoveryPerHour = 0.26f;

    public static PawPalTrickAttemptResult AttemptTrick(
        PawPalDogState dog,
        PawPalTrickDefinition definition,
        PawPalTrainingConfig config,
        bool praisePrimed,
        bool treatUsed,
        bool prerequisitePoseSatisfied)
    {
        PawPalTrainingConfig resolvedConfig = config != null ? config : PawPalTrainingConfig.CreateRuntimeDefault();
        PawPalTrickAttemptResult result = BuildEmptyResult(definition);
        if (dog == null)
        {
            result.Reason = PawPalTrickFailureReason.NoActiveDog;
            result.FeedbackText = "Choose a dog first.";
            return result;
        }

        if (definition == null)
        {
            result.Reason = PawPalTrickFailureReason.InvalidGesture;
            result.FeedbackText = "Try another gesture.";
            return result;
        }

        PawPalDogPersonalityProfiles.EnsureProfile(dog);
        PawPalTrickCatalog.EnsureDogTrickData(dog);
        RecoverFatigue(dog, resolvedConfig, DateTime.UtcNow);
        PawPalGameRuntime.EnsureCanonicalStaminaData(dog);
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.RefreshDogStamina(dog);
        }

        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetOrCreateProgress(dog, definition.Id);
        result.TrickId = definition.Id;
        result.AnimationToPlay = definition.SuccessAnimationName;
        result.PreviousProgress01 = PawPalTrickCatalog.GetProgress01(progress, definition);

        PawPalTrickFailureReason requirementFailure;
        if (!MeetsRequirements(dog, definition, prerequisitePoseSatisfied, out requirementFailure))
        {
            RegisterFailedAttempt(dog, progress, definition, resolvedConfig, requirementFailure, result);
            return result;
        }

        ResetDailyPracticeIfNeeded(progress, DateTime.UtcNow);
        progress.IsDiscovered = true;

        float successChance = CalculateSuccessChance(dog, definition, progress, resolvedConfig, treatUsed);

        bool success = UnityEngine.Random.value <= successChance;
        float baseXp = success ? resolvedConfig.BaseXpPerSuccess : resolvedConfig.BaseXpPerFail;
        float difficultyMultiplier = 1f + Mathf.Max(0, definition.Difficulty - 1) * resolvedConfig.MasteryXpMultiplierByDifficulty;
        float personalityXp = Mathf.Lerp(1f, definition.GetXpMultiplier(dog.Personality), resolvedConfig.PersonalityModifierStrength);
        float dailyPracticeMultiplier = GetDailyPracticeMultiplier(progress, definition, resolvedConfig);
        float xpGained = baseXp * difficultyMultiplier * personalityXp * dailyPracticeMultiplier;

        if (praisePrimed)
        {
            xpGained += resolvedConfig.PraiseXpBonus;
        }

        if (!success)
        {
            xpGained *= 0.45f;
        }

        ApplyAttemptChanges(dog, progress, definition, resolvedConfig, success, xpGained, result);
        result.CurrentProgress01 = PawPalTrickCatalog.GetProgress01(progress, definition);
        result.MadeProgress = success && result.CurrentProgress01 > result.PreviousProgress01 + 0.0001f;
        result.Success = success;
        result.XpGained = xpGained;
        result.Reason = success ? PawPalTrickFailureReason.None : PawPalTrickFailureReason.RandomMiss;
        result.BondChange = PawPalGameRuntime.GetBondDelta01FromXp(PawPalGameRuntime.GetBondXpFromLegacyDelta01(success ? 0.012f : 0.005f));
        result.MoodChange = success ? 0.018f : -0.006f;
        result.LearnedNow = progress.IsLearned && progress.LearnedAtUtcTicks == progress.LastPracticedAtUtcTicks;
        result.MasteredNow = progress.MasteryLevel >= 2 && progress.MasteryXp - xpGained < definition.MasteryRequiredXp;
        result.FeedbackText = BuildFeedbackText(dog, definition, progress, result, dailyPracticeMultiplier);
        return result;
    }

    public static PawPalTrickAttemptResult PracticeLearnedTrick(PawPalDogState dog, PawPalTrickDefinition definition, PawPalTrainingConfig config)
    {
        return AttemptTrick(dog, definition, config, true, false, true);
    }

    public static void ApplyPraise(PawPalDogState dog, PawPalTrickId trickId)
    {
        if (dog == null)
        {
            return;
        }

        PawPalTrickCatalog.EnsureDogTrickData(dog);
        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetOrCreateProgress(dog, trickId);
        if (progress != null)
        {
            progress.IsDiscovered = true;
            progress.MasteryXp += 2f;
        }

        PawPalGameRuntime.AddDogBondXp(dog, PawPalGameRuntime.GetBondXpFromLegacyDelta01(0.012f));
        dog.Mood01 = Mathf.Clamp01(dog.Mood01 + 0.02f);
        dog.TrainingFatigue01 = Mathf.Clamp01(dog.TrainingFatigue01 - 0.04f);
    }

    public static PawPalTrickFailureReason GetAvailabilityFailure(PawPalDogState dog, PawPalTrickDefinition definition, bool prerequisitePoseSatisfied)
    {
        PawPalTrickFailureReason reason;
        return MeetsRequirements(dog, definition, prerequisitePoseSatisfied, out reason)
            ? PawPalTrickFailureReason.None
            : reason;
    }

    public static PawPalTrickId? GetFirstMissingPrerequisite(PawPalDogState dog, PawPalTrickDefinition definition, bool prerequisitePoseSatisfied)
    {
        if (dog == null || definition == null)
        {
            return null;
        }

        PawPalTrickId[] requiredTricks = definition.RequiredKnownTricks;
        for (int i = 0; i < requiredTricks.Length; i++)
        {
            PawPalDogTrickProgress requiredProgress = PawPalTrickCatalog.GetProgress(dog, requiredTricks[i]);
            if (requiredProgress == null || !requiredProgress.IsLearned)
            {
                return requiredTricks[i];
            }
        }

        return null;
    }

    public static void RecoverFatigue(PawPalDogState dog, PawPalTrainingConfig config, DateTime nowUtc)
    {
        if (dog == null)
        {
            return;
        }

        PawPalTrickCatalog.EnsureDogTrickData(dog);
        DateTime lastUpdate = dog.LastTrainingFatigueUpdateUtcTicks > 0L
            ? new DateTime(dog.LastTrainingFatigueUpdateUtcTicks, DateTimeKind.Utc)
            : nowUtc;
        if (nowUtc < lastUpdate)
        {
            lastUpdate = nowUtc;
        }

        double elapsedHours = (nowUtc - lastUpdate).TotalHours;
        if (elapsedHours > 0.001d)
        {
            dog.TrainingFatigue01 = Mathf.Clamp01(dog.TrainingFatigue01 - (float)elapsedHours * FatigueRecoveryPerHour);
        }

        dog.LastTrainingFatigueUpdateUtcTicks = nowUtc.Ticks;
    }

    private static float CalculateSuccessChance(
        PawPalDogState dog,
        PawPalTrickDefinition definition,
        PawPalDogTrickProgress progress,
        PawPalTrainingConfig config,
        bool treatUsed)
    {
        float fatiguePenalty = Mathf.Lerp(0f, 0.22f, dog.TrainingFatigue01);
        float repeatedPracticePenalty = GetRepeatedPracticePenalty(progress, definition, config);
        float needPenalty = GetNeedPenalty(dog, config);
        float moodPenalty = dog.Mood01 < definition.RequiredMood01 + 0.12f ? config.LowMoodPenalty : 0f;
        float focusBonus = Mathf.Clamp01(dog.Focus / 10f) * 0.12f;
        float bondBonus = PawPalGameRuntime.GetBondProgress01(dog) * 0.08f;
        float personalityOffset = definition.GetSuccessChanceOffset(dog.Personality) * config.PersonalityModifierStrength;

        float successChance = config.BaseSuccessChance
            + focusBonus
            + bondBonus
            + personalityOffset
            + (treatUsed ? config.TreatMotivationBonus : 0f)
            - fatiguePenalty
            - repeatedPracticePenalty
            - needPenalty
            - moodPenalty
            - Mathf.Max(0, definition.Difficulty - 1) * 0.04f;
        return Mathf.Clamp(successChance, MinimumSuccessChance, MaximumSuccessChance);
    }

    private static PawPalTrickAttemptResult BuildEmptyResult(PawPalTrickDefinition definition)
    {
        return new PawPalTrickAttemptResult
        {
            TrickId = definition != null ? definition.Id : PawPalTrickId.Sit,
            Success = false,
            LearnedNow = false,
            MasteredNow = false,
            MadeProgress = false,
            Reason = PawPalTrickFailureReason.None,
            XpGained = 0f,
            BondChange = 0f,
            MoodChange = 0f,
            EnergyChange = 0f,
            StaminaChange = 0f,
            PreviousProgress01 = 0f,
            CurrentProgress01 = 0f,
            CurrentStamina = 0f,
            MaxStamina = 0f,
            AnimationToPlay = definition != null ? definition.SuccessAnimationName : string.Empty,
            FeedbackText = string.Empty
        };
    }

    private static bool MeetsRequirements(
        PawPalDogState dog,
        PawPalTrickDefinition definition,
        bool prerequisitePoseSatisfied,
        out PawPalTrickFailureReason reason)
    {
        reason = PawPalTrickFailureReason.None;
        if (dog == null || definition == null)
        {
            reason = PawPalTrickFailureReason.NoActiveDog;
            return false;
        }

        if (PawPalGameRuntime.GetBondLevel(dog) < definition.GetResolvedRequiredBondLevel())
        {
            reason = PawPalTrickFailureReason.LowBond;
            return false;
        }

        if (!PawPalTrickFocusRequirement.MeetsFocusRequirement(dog, definition))
        {
            reason = PawPalTrickFailureReason.LowFocus;
            return false;
        }

        if (PawPalGameRuntime.GetDogStamina01(dog) + 0.001f < definition.RequiredEnergy01)
        {
            reason = PawPalTrickFailureReason.LowEnergy;
            return false;
        }

        if (dog.Food01 < 0.08f)
        {
            reason = PawPalTrickFailureReason.Hungry;
            return false;
        }

        if (dog.Water01 < 0.08f)
        {
            reason = PawPalTrickFailureReason.Thirsty;
            return false;
        }

        if (GetFirstMissingPrerequisite(dog, definition, prerequisitePoseSatisfied).HasValue)
        {
            reason = PawPalTrickFailureReason.MissingPrerequisite;
            return false;
        }

        return true;
    }

    private static void RegisterFailedAttempt(
        PawPalDogState dog,
        PawPalDogTrickProgress progress,
        PawPalTrickDefinition definition,
        PawPalTrainingConfig config,
        PawPalTrickFailureReason reason,
        PawPalTrickAttemptResult result)
    {
        if (progress != null)
        {
            progress.TotalFailedAttempts++;
            progress.LastPracticedAtUtcTicks = DateTime.UtcNow.Ticks;
        }

        if (dog != null)
        {
            dog.TrainingFatigue01 = Mathf.Clamp01(dog.TrainingFatigue01 + 0.02f);
            dog.Mood01 = Mathf.Clamp01(dog.Mood01 - 0.005f);
        }

        result.Success = false;
        result.Reason = reason;
        result.CurrentProgress01 = result.PreviousProgress01;
        result.CurrentStamina = dog != null ? PawPalGameRuntime.GetCanonicalStaminaSnapshot(dog).Current : 0f;
        result.MaxStamina = dog != null ? PawPalGameRuntime.GetCanonicalStaminaSnapshot(dog).Max : 0f;
        result.FeedbackText = GetFailureText(reason, dog, definition, false);
    }

    private static void ApplyAttemptChanges(
        PawPalDogState dog,
        PawPalDogTrickProgress progress,
        PawPalTrickDefinition definition,
        PawPalTrainingConfig config,
        bool success,
        float xpGained,
        PawPalTrickAttemptResult result)
    {
        DateTime nowUtc = DateTime.UtcNow;
        progress.MasteryXp = Mathf.Clamp(progress.MasteryXp + Mathf.Max(0f, xpGained), 0f, definition.MasteryRequiredXp);
        progress.LastPracticedAtUtcTicks = nowUtc.Ticks;
        progress.LastPracticeDayUtcTicks = nowUtc.Date.Ticks;
        progress.TimesPracticedToday++;

        if (success)
        {
            progress.TotalSuccessfulAttempts++;
        }
        else
        {
            progress.TotalFailedAttempts++;
        }

        if (!progress.IsLearned && progress.MasteryXp >= definition.LearnedRequiredXp)
        {
            progress.IsLearned = true;
            progress.MasteryLevel = Mathf.Max(progress.MasteryLevel, 1);
            progress.LearnedAtUtcTicks = nowUtc.Ticks;
            if (string.IsNullOrWhiteSpace(progress.CustomVoiceCommand))
            {
                progress.CustomVoiceCommand = PawPalTrickCatalog.GetCommandLabel(definition.Id);
            }
        }

        if (progress.MasteryXp >= definition.MasteryRequiredXp)
        {
            progress.IsLearned = true;
            progress.MasteryLevel = 2;
            if (progress.LearnedAtUtcTicks <= 0L)
            {
                progress.LearnedAtUtcTicks = nowUtc.Ticks;
            }
        }

        float boredomMultiplier = definition.GetBoredomMultiplier(dog.Personality);
        float fatigueGain = config.BoredomGainPerRepeatedAttempt * boredomMultiplier;
        if (success)
        {
            fatigueGain *= 0.75f;
        }

        dog.TrainingFatigue01 = Mathf.Clamp01(dog.TrainingFatigue01 + fatigueGain);
        dog.LastTrainingFatigueUpdateUtcTicks = nowUtc.Ticks;
        PawPalGameRuntime.AddDogBondXp(dog, PawPalGameRuntime.GetBondXpFromLegacyDelta01(success ? 0.012f : 0.005f));
        dog.Mood01 = Mathf.Clamp01(dog.Mood01 + (success ? 0.018f : -0.006f));
        float staminaRatioCost = 0.035f + definition.Difficulty * 0.007f;
        float currentStamina;
        float maxStamina;
        float spentStamina = PawPalGameRuntime.SpendDogStamina(
            dog,
            PawPalGameRuntime.GetDogStaminaCostFromRatio(dog, staminaRatioCost),
            out currentStamina,
            out maxStamina);
        dog.ModifyNeed(PawPalDogNeed.Activity, -0.035f);
        dog.ModifyNeed(PawPalDogNeed.Water, -0.025f);

        if (success && dog.Focus < 10 && UnityEngine.Random.value <= PawPalDogPersonalityProfiles.GetTrainingBonusChance(dog))
        {
            dog.ModifyStat(PawPalDogStatType.Focus, 1);
        }

        if (result != null)
        {
            result.EnergyChange = -staminaRatioCost;
            result.StaminaChange = -spentStamina;
            result.CurrentStamina = currentStamina;
            result.MaxStamina = maxStamina;
        }
    }

    private static float GetNeedPenalty(PawPalDogState dog, PawPalTrainingConfig config)
    {
        if (dog == null)
        {
            return 0f;
        }

        float lowestNeed = Mathf.Min(dog.Food01, dog.Water01, dog.Hygiene01, dog.Activity01);
        return lowestNeed < 0.2f ? config.LowNeedPenalty : 0f;
    }

    private static float GetRepeatedPracticePenalty(PawPalDogTrickProgress progress, PawPalTrickDefinition definition, PawPalTrainingConfig config)
    {
        if (progress == null)
        {
            return 0f;
        }

        int limit = definition.DailyPracticeLimit > 0 ? definition.DailyPracticeLimit : config.MaxEffectivePracticePerTrickPerDay;
        int overLimit = Mathf.Max(0, progress.TimesPracticedToday - limit);
        return Mathf.Clamp01(overLimit * 0.055f);
    }

    private static float GetDailyPracticeMultiplier(PawPalDogTrickProgress progress, PawPalTrickDefinition definition, PawPalTrainingConfig config)
    {
        if (progress == null)
        {
            return 1f;
        }

        int limit = definition.DailyPracticeLimit > 0 ? definition.DailyPracticeLimit : config.MaxEffectivePracticePerTrickPerDay;
        if (progress.TimesPracticedToday < limit)
        {
            return 1f;
        }

        return Mathf.Clamp(1f - (progress.TimesPracticedToday - limit + 1) * 0.12f, 0.35f, 1f);
    }

    private static void ResetDailyPracticeIfNeeded(PawPalDogTrickProgress progress, DateTime nowUtc)
    {
        if (progress == null)
        {
            return;
        }

        long todayTicks = nowUtc.Date.Ticks;
        if (progress.LastPracticeDayUtcTicks != todayTicks)
        {
            progress.TimesPracticedToday = 0;
            progress.LastPracticeDayUtcTicks = todayTicks;
        }
    }

    private static string BuildFeedbackText(
        PawPalDogState dog,
        PawPalTrickDefinition definition,
        PawPalDogTrickProgress progress,
        PawPalTrickAttemptResult result,
        float dailyPracticeMultiplier)
    {
        if (result.Success)
        {
            if (result.MasteredNow)
            {
                return dog.DisplayName + " mastered " + definition.DisplayName + ".";
            }

            if (result.LearnedNow)
            {
                return dog.DisplayName + " learned " + definition.DisplayName + ".";
            }

            if (dailyPracticeMultiplier < 0.99f)
            {
                return dog.DisplayName + " did it, but wants a break soon.";
            }

            return dog.DisplayName + " did " + definition.DisplayName + ".";
        }

        if (dog.TrainingFatigue01 > 0.72f)
        {
            return dog.DisplayName + " needs a training break.";
        }

        return dog.DisplayName + " almost had " + definition.DisplayName + ".";
    }

    public static string GetFailureText(PawPalTrickFailureReason reason, PawPalDogState dog, PawPalTrickDefinition definition, bool prerequisitePoseSatisfied)
    {
        switch (reason)
        {
            case PawPalTrickFailureReason.LowBond:
                return "Raise your bond level before teaching that.";
            case PawPalTrickFailureReason.LowFocus:
                return "Raise your dog's focus before teaching that.";
            case PawPalTrickFailureReason.LowMood:
                return "Your dog is not in the mood yet.";
            case PawPalTrickFailureReason.LowEnergy:
                return "Your dog needs more stamina for that.";
            case PawPalTrickFailureReason.Hungry:
                return "Feed your dog before training.";
            case PawPalTrickFailureReason.Thirsty:
                return "Give water before training.";
            case PawPalTrickFailureReason.MissingPrerequisite:
                PawPalTrickId? prerequisite = GetFirstMissingPrerequisite(dog, definition, prerequisitePoseSatisfied);
                if (prerequisite.HasValue)
                {
                    PawPalTrickDefinition prerequisiteDefinition = PawPalTrickCatalog.GetDefinition(prerequisite.Value);
                    string prerequisiteName = prerequisiteDefinition != null ? prerequisiteDefinition.DisplayName : prerequisite.Value.ToString();
                    return "Learn " + prerequisiteName + " first.";
                }

                return "Teach the earlier trick first.";
            case PawPalTrickFailureReason.Busy:
                return "Your dog is busy.";
            default:
                return "Try a gentler cue.";
        }
    }
}
