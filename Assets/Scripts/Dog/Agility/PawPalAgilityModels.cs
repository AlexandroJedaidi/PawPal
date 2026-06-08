using System;
using System.Collections.Generic;
using UnityEngine;

public enum PawPalAgilityLevelId
{
    Beginner,
    Amateur,
    Pro,
    Master,
    Champion
}

public enum PawPalAgilityObstacleType
{
    StartGate,
    FinishGate,
    SeeSaw,
    BarrierRunAround,
    BridgeWalkOver,
    HighFence,
    WheelJump
}

public enum PawPalAgilityMedal
{
    None,
    Bronze,
    Silver,
    Gold
}

public enum PawPalAgilityTrialMode
{
    Practice,
    Scored
}

[Serializable]
public sealed class PawPalAgilityObstacleDefinition
{
    public PawPalAgilityObstacleType Type;
    public string DisplayName = "Obstacle";
    public string SceneObjectName = string.Empty;
    public Vector3 FallbackPosition;
    public float SuccessRadius = 1.35f;
    public float InputLeadDistance = 2.2f;
    public float FaultPenalty = 10f;
    public bool RequiresJumpCue;
    public bool RequiresBalanceCue;
}

[Serializable]
public sealed class PawPalAgilityLevelDefinition
{
    public PawPalAgilityLevelId LevelId;
    public string DisplayName;
    public int EntryFeeBasicCurrency;
    public float TargetTimeSeconds;
    public float TimeLimitSeconds;
    public int FaultTolerance;
    public int BronzeRewardBasicCurrency;
    public int SilverRewardBasicCurrency;
    public int GoldRewardBasicCurrency;
    public float StaminaCostRatio = 0.08f;
    public PawPalAgilityMedal RequiredPreviousMedal = PawPalAgilityMedal.Silver;
    public List<PawPalAgilityObstacleDefinition> Obstacles = new List<PawPalAgilityObstacleDefinition>();
}

[Serializable]
public sealed class PawPalAgilityTrialSessionSaveData
{
    public string SelectedDogId;
    public PawPalAgilityLevelId LevelId;
    public PawPalAgilityTrialMode Mode;
    public string ReturnSceneName;
    public int EntryFeeBasicCurrency;
    public long StartedUtcTicks;
}

[Serializable]
public sealed class PawPalAgilityLevelProgressState
{
    public PawPalAgilityLevelId LevelId;
    public bool Unlocked;
    public int BestScore;
    public float BestTimeSeconds;
    public PawPalAgilityMedal BestMedal;
    public PawPalAgilityMedal RewardedMedal;
    public int TimesCompleted;
}

[Serializable]
public sealed class PawPalAgilityPetProgressState
{
    public string DogId;
    public bool BasicPracticeComplete;
    public List<PawPalAgilityLevelProgressState> Levels = new List<PawPalAgilityLevelProgressState>();
}

public sealed class PawPalAgilityEntryStatus
{
    public bool CanPractice;
    public bool CanStartScored;
    public string PracticeMessage = string.Empty;
    public string ScoredMessage = string.Empty;
}

public sealed class PawPalAgilityRunStats
{
    public float ElapsedSeconds;
    public int Faults;
    public int ObstaclesCompleted;
    public int TotalObstacles;
    public float Flow01 = 1f;
    public bool Completed = true;
}

public sealed class PawPalAgilityConditionSnapshot
{
    public float Stamina01;
    public float Mood01;
    public float Needs01;
    public float Bond01;
    public float Stat01;
    public float Combined01;
}

public sealed class PawPalAgilityTrialResult
{
    public PawPalAgilityLevelId LevelId;
    public PawPalAgilityTrialMode Mode;
    public float ElapsedSeconds;
    public int Faults;
    public int Score;
    public PawPalAgilityMedal Medal;
    public int BasicCurrencyReward;
    public bool UnlockedNextLevel;
    public bool PracticeCompleted;
    public string Summary = string.Empty;
}

public static class PawPalAgilitySaveUtility
{
    public static PawPalAgilityPetProgressState ClonePetProgress(PawPalAgilityPetProgressState source)
    {
        PawPalAgilityPetProgressState clone = new PawPalAgilityPetProgressState();
        if (source == null)
        {
            return clone;
        }

        clone.DogId = source.DogId;
        clone.BasicPracticeComplete = source.BasicPracticeComplete;
        if (source.Levels != null)
        {
            for (int i = 0; i < source.Levels.Count; i++)
            {
                PawPalAgilityLevelProgressState level = source.Levels[i];
                if (level == null)
                {
                    continue;
                }

                clone.Levels.Add(new PawPalAgilityLevelProgressState
                {
                    LevelId = level.LevelId,
                    Unlocked = level.Unlocked,
                    BestScore = level.BestScore,
                    BestTimeSeconds = level.BestTimeSeconds,
                    BestMedal = level.BestMedal,
                    RewardedMedal = level.RewardedMedal,
                    TimesCompleted = level.TimesCompleted
                });
            }
        }

        return clone;
    }

    public static List<PawPalAgilityPetProgressState> CloneProgressList(List<PawPalAgilityPetProgressState> source)
    {
        List<PawPalAgilityPetProgressState> clone = new List<PawPalAgilityPetProgressState>();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Count; i++)
        {
            PawPalAgilityPetProgressState progress = source[i];
            if (progress != null)
            {
                clone.Add(ClonePetProgress(progress));
            }
        }

        return clone;
    }

    public static PawPalAgilityTrialSessionSaveData CloneSession(PawPalAgilityTrialSessionSaveData source)
    {
        if (source == null)
        {
            return null;
        }

        return new PawPalAgilityTrialSessionSaveData
        {
            SelectedDogId = source.SelectedDogId,
            LevelId = source.LevelId,
            Mode = source.Mode,
            ReturnSceneName = source.ReturnSceneName,
            EntryFeeBasicCurrency = source.EntryFeeBasicCurrency,
            StartedUtcTicks = source.StartedUtcTicks
        };
    }
}
