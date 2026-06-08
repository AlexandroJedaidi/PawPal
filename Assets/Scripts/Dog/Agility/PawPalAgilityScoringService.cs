using UnityEngine;

public static class PawPalAgilityScoringService
{
    public static PawPalAgilityConditionSnapshot BuildConditionSnapshot(PawPalDogState dog)
    {
        PawPalAgilityConditionSnapshot snapshot = new PawPalAgilityConditionSnapshot();
        if (dog == null)
        {
            snapshot.Stamina01 = 0f;
            snapshot.Mood01 = 0f;
            snapshot.Needs01 = 0f;
            snapshot.Bond01 = 0f;
            snapshot.Stat01 = 0f;
            snapshot.Combined01 = 0f;
            return snapshot;
        }

        snapshot.Stamina01 = PawPalGameRuntime.GetDogStamina01(dog);
        snapshot.Mood01 = Mathf.Clamp01(dog.Mood01);
        snapshot.Needs01 = Mathf.Clamp01((dog.Food01 + dog.Water01 + dog.Hygiene01 + dog.Activity01) * 0.25f);
        snapshot.Bond01 = Mathf.Clamp01(dog.Bond01);
        snapshot.Stat01 = Mathf.Clamp01((dog.Speed + dog.Mobility + dog.Focus + dog.Endurance) / 40f);
        snapshot.Combined01 = Mathf.Clamp01(
            snapshot.Stamina01 * 0.35f
            + snapshot.Mood01 * 0.2f
            + snapshot.Needs01 * 0.2f
            + snapshot.Bond01 * 0.1f
            + snapshot.Stat01 * 0.15f);
        return snapshot;
    }

    public static PawPalAgilityTrialResult CalculateResult(
        PawPalAgilityLevelDefinition level,
        PawPalAgilityRunStats stats,
        PawPalAgilityConditionSnapshot condition,
        PawPalAgilityTrialMode mode)
    {
        PawPalAgilityTrialResult result = new PawPalAgilityTrialResult
        {
            LevelId = level != null ? level.LevelId : PawPalAgilityLevelId.Beginner,
            Mode = mode,
            ElapsedSeconds = stats != null ? Mathf.Max(0f, stats.ElapsedSeconds) : 0f,
            Faults = stats != null ? Mathf.Max(0, stats.Faults) : 0,
            Medal = PawPalAgilityMedal.None
        };

        if (mode == PawPalAgilityTrialMode.Practice)
        {
            result.PracticeCompleted = stats == null || stats.Completed;
            result.Score = 0;
            result.Summary = result.PracticeCompleted ? "Practice complete" : "Practice stopped";
            return result;
        }

        if (level == null || stats == null || !stats.Completed || stats.TotalObstacles <= 0)
        {
            result.Score = 0;
            result.Summary = "Course incomplete";
            return result;
        }

        float completion01 = Mathf.Clamp01(stats.ObstaclesCompleted / (float)Mathf.Max(1, stats.TotalObstacles));
        float targetTime = Mathf.Max(1f, level.TargetTimeSeconds);
        float timeLimit = Mathf.Max(targetTime, level.TimeLimitSeconds);
        float timeScore = Mathf.InverseLerp(timeLimit, targetTime * 0.8f, result.ElapsedSeconds) * 42f;
        float accuracyScore = Mathf.Max(0f, 34f - result.Faults * 7f);
        float flowScore = Mathf.Clamp01(stats.Flow01) * 14f;
        float conditionScore = condition != null ? Mathf.Clamp01(condition.Combined01) * 10f : 0f;
        float rawScore = timeScore + accuracyScore + flowScore + conditionScore;
        rawScore *= Mathf.Lerp(0.65f, 1f, completion01);
        result.Score = Mathf.Clamp(Mathf.RoundToInt(rawScore), 0, 100);

        bool withinTime = result.ElapsedSeconds <= timeLimit;
        bool withinFaultTolerance = result.Faults <= Mathf.Max(0, level.FaultTolerance);
        if (!withinTime || !withinFaultTolerance)
        {
            result.Medal = PawPalAgilityMedal.None;
            result.Summary = !withinTime ? "Too slow" : "Too many faults";
            return result;
        }

        if (result.Score >= 88 && result.ElapsedSeconds <= targetTime && result.Faults <= 1)
        {
            result.Medal = PawPalAgilityMedal.Gold;
            result.Summary = "Gold placement";
        }
        else if (result.Score >= 72 && result.Faults <= Mathf.Max(1, level.FaultTolerance - 1))
        {
            result.Medal = PawPalAgilityMedal.Silver;
            result.Summary = "Silver placement";
        }
        else if (result.Score >= 50)
        {
            result.Medal = PawPalAgilityMedal.Bronze;
            result.Summary = "Bronze placement";
        }
        else
        {
            result.Medal = PawPalAgilityMedal.None;
            result.Summary = "No placement";
        }

        return result;
    }

    public static int GetRewardForMedal(PawPalAgilityLevelDefinition level, PawPalAgilityMedal medal)
    {
        if (level == null)
        {
            return 0;
        }

        switch (medal)
        {
            case PawPalAgilityMedal.Gold:
                return Mathf.Max(0, level.GoldRewardBasicCurrency);
            case PawPalAgilityMedal.Silver:
                return Mathf.Max(0, level.SilverRewardBasicCurrency);
            case PawPalAgilityMedal.Bronze:
                return Mathf.Max(0, level.BronzeRewardBasicCurrency);
            default:
                return 0;
        }
    }
}
