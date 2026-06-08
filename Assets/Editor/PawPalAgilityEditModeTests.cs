using NUnit.Framework;
using UnityEngine;

public sealed class PawPalAgilityEditModeTests
{
    [Test]
    public void DefaultConfigContainsFiveNamedLevels()
    {
        PawPalAgilityTrialConfig config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
        config.ResetToDefaultLevels();

        Assert.AreEqual(5, config.Levels.Count);
        Assert.AreEqual("Beginner", config.GetLevel(PawPalAgilityLevelId.Beginner).DisplayName);
        Assert.AreEqual("Champion", config.GetLevel(PawPalAgilityLevelId.Champion).DisplayName);
    }

    [Test]
    public void DefaultConfigUsesLockedEntryFees()
    {
        PawPalAgilityTrialConfig config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
        config.ResetToDefaultLevels();

        Assert.AreEqual(10, config.GetLevel(PawPalAgilityLevelId.Beginner).EntryFeeBasicCurrency);
        Assert.AreEqual(15, config.GetLevel(PawPalAgilityLevelId.Amateur).EntryFeeBasicCurrency);
        Assert.AreEqual(25, config.GetLevel(PawPalAgilityLevelId.Pro).EntryFeeBasicCurrency);
        Assert.AreEqual(40, config.GetLevel(PawPalAgilityLevelId.Master).EntryFeeBasicCurrency);
        Assert.AreEqual(60, config.GetLevel(PawPalAgilityLevelId.Champion).EntryFeeBasicCurrency);
    }

    [Test]
    public void DefaultCoursesStartAndFinishInOrder()
    {
        PawPalAgilityTrialConfig config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
        config.ResetToDefaultLevels();

        foreach (PawPalAgilityLevelDefinition level in config.Levels)
        {
            Assert.NotNull(level);
            Assert.GreaterOrEqual(level.Obstacles.Count, 2);
            Assert.AreEqual(PawPalAgilityObstacleType.StartGate, level.Obstacles[0].Type);
            Assert.AreEqual(PawPalAgilityObstacleType.FinishGate, level.Obstacles[level.Obstacles.Count - 1].Type);
            for (int i = 0; i < level.Obstacles.Count; i++)
            {
                Assert.NotNull(level.Obstacles[i]);
            }
        }
    }

    [Test]
    public void ScoringAwardsGoldForFastCleanRun()
    {
        PawPalAgilityTrialConfig config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
        config.ResetToDefaultLevels();
        PawPalAgilityLevelDefinition level = config.GetLevel(PawPalAgilityLevelId.Beginner);
        PawPalAgilityRunStats stats = new PawPalAgilityRunStats
        {
            ElapsedSeconds = level.TargetTimeSeconds * 0.75f,
            Faults = 0,
            ObstaclesCompleted = level.Obstacles.Count,
            TotalObstacles = level.Obstacles.Count,
            Flow01 = 1f,
            Completed = true
        };
        PawPalAgilityConditionSnapshot condition = new PawPalAgilityConditionSnapshot { Combined01 = 1f };

        PawPalAgilityTrialResult result = PawPalAgilityScoringService.CalculateResult(level, stats, condition, PawPalAgilityTrialMode.Scored);

        Assert.AreEqual(PawPalAgilityMedal.Gold, result.Medal);
        Assert.GreaterOrEqual(result.Score, 88);
    }

    [Test]
    public void ScoringBlocksPlacementWhenFaultToleranceExceeded()
    {
        PawPalAgilityTrialConfig config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
        config.ResetToDefaultLevels();
        PawPalAgilityLevelDefinition level = config.GetLevel(PawPalAgilityLevelId.Beginner);
        PawPalAgilityRunStats stats = new PawPalAgilityRunStats
        {
            ElapsedSeconds = level.TargetTimeSeconds,
            Faults = level.FaultTolerance + 1,
            ObstaclesCompleted = level.Obstacles.Count,
            TotalObstacles = level.Obstacles.Count,
            Flow01 = 1f,
            Completed = true
        };

        PawPalAgilityTrialResult result = PawPalAgilityScoringService.CalculateResult(level, stats, new PawPalAgilityConditionSnapshot { Combined01 = 1f }, PawPalAgilityTrialMode.Scored);

        Assert.AreEqual(PawPalAgilityMedal.None, result.Medal);
    }

    [Test]
    public void PracticeCompletionDoesNotAwardScoredMedal()
    {
        PawPalAgilityTrialConfig config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
        config.ResetToDefaultLevels();
        PawPalAgilityLevelDefinition level = config.GetLevel(PawPalAgilityLevelId.Beginner);
        PawPalAgilityRunStats stats = new PawPalAgilityRunStats
        {
            ElapsedSeconds = level.TargetTimeSeconds * 0.75f,
            Faults = 0,
            ObstaclesCompleted = level.Obstacles.Count,
            TotalObstacles = level.Obstacles.Count,
            Flow01 = 1f,
            Completed = true
        };

        PawPalAgilityTrialResult result = PawPalAgilityScoringService.CalculateResult(level, stats, new PawPalAgilityConditionSnapshot { Combined01 = 1f }, PawPalAgilityTrialMode.Practice);

        Assert.IsTrue(result.PracticeCompleted);
        Assert.AreEqual(PawPalAgilityMedal.None, result.Medal);
        Assert.AreEqual(0, result.BasicCurrencyReward);
    }

    [Test]
    public void RewardLookupIncreasesByMedalRank()
    {
        PawPalAgilityTrialConfig config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
        config.ResetToDefaultLevels();
        PawPalAgilityLevelDefinition level = config.GetLevel(PawPalAgilityLevelId.Amateur);

        int bronze = PawPalAgilityScoringService.GetRewardForMedal(level, PawPalAgilityMedal.Bronze);
        int silver = PawPalAgilityScoringService.GetRewardForMedal(level, PawPalAgilityMedal.Silver);
        int gold = PawPalAgilityScoringService.GetRewardForMedal(level, PawPalAgilityMedal.Gold);

        Assert.Greater(silver, bronze);
        Assert.Greater(gold, silver);
    }

    [Test]
    public void SaveClonePreservesAgilityProgressButNotReferences()
    {
        PawPalAgilityPetProgressState source = new PawPalAgilityPetProgressState
        {
            DogId = "pepper",
            BasicPracticeComplete = true
        };
        source.Levels.Add(new PawPalAgilityLevelProgressState
        {
            LevelId = PawPalAgilityLevelId.Beginner,
            Unlocked = true,
            BestScore = 91,
            BestMedal = PawPalAgilityMedal.Gold,
            RewardedMedal = PawPalAgilityMedal.Silver
        });

        PawPalAgilityPetProgressState clone = PawPalAgilitySaveUtility.ClonePetProgress(source);

        Assert.AreNotSame(source, clone);
        Assert.AreNotSame(source.Levels[0], clone.Levels[0]);
        Assert.AreEqual("pepper", clone.DogId);
        Assert.IsTrue(clone.BasicPracticeComplete);
        Assert.AreEqual(91, clone.Levels[0].BestScore);
        Assert.AreEqual(PawPalAgilityMedal.Gold, clone.Levels[0].BestMedal);
    }
}
