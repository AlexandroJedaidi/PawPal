using System;
using System.Collections.Generic;
using UnityEngine;

public enum PawPalObedienceTrialLevelId
{
    Beginner,
    Amateur,
    Pro,
    Master,
    Champion
}

public enum PawPalObedienceTrialRoundType
{
    SingleCommand,
    Hold,
    Sequence,
    Freestyle
}

public enum PawPalObedienceTrialMedal
{
    None,
    Bronze,
    Silver,
    Gold
}

public enum PawPalObedienceTrialInputSource
{
    Button,
    Gesture,
    Voice,
    Debug
}

[Serializable]
public sealed class PawPalObedienceTrialRoundDefinition
{
    public string Id = "round";
    public string DisplayName = "Round";
    public PawPalObedienceTrialRoundType RoundType;
    public List<PawPalTrickId> RequestedTricks = new List<PawPalTrickId>();
    public List<PawPalTrickId> AllowedTricks = new List<PawPalTrickId>();
    public int SequenceLength = 2;
    public float HoldDurationSeconds = 2f;
    public float TimeLimitSeconds = 12f;
    public float ScoringWeight = 1f;
}

[Serializable]
public sealed class PawPalObedienceTrialRewardDefinition
{
    public int EntryFee = 25;
    public int BronzeBasicCurrency = 35;
    public int SilverBasicCurrency = 55;
    public int GoldBasicCurrency = 85;
}

[Serializable]
public sealed class PawPalObedienceTrialLevelDefinition
{
    public PawPalObedienceTrialLevelId LevelId;
    public string DisplayName = "Beginner";
    public int Order;
    public bool Playable = true;
    public string LockedReason = string.Empty;
    public List<PawPalTrickId> RequiredTricks = new List<PawPalTrickId>();
    public List<PawPalTrickId> OptionalFreestyleTricks = new List<PawPalTrickId>();
    public int MinimumMasteryLevel;
    public float MinimumMood01 = 0.25f;
    public float MinimumEnergy01 = 0.25f;
    public float MinimumNeedAverage01 = 0.25f;
    public PawPalObedienceTrialRewardDefinition Rewards = new PawPalObedienceTrialRewardDefinition();
    public List<PawPalObedienceTrialRoundDefinition> Rounds = new List<PawPalObedienceTrialRoundDefinition>();
}

[Serializable]
public sealed class PawPalObedienceTrialAnimationMapping
{
    public IntroPetSpecies Species;
    public PawPalTrickId TrickId;
    public string DisplayLabel = "Trick";
    public bool TrialEligible;
    public bool OptionalFreestyleEligible;
    public string MissingReason = string.Empty;
}

[CreateAssetMenu(menuName = "PawFriends/Competition/Obedience Trial Config", fileName = "ObedienceTrialConfig")]
public sealed class PawPalObedienceTrialConfig : ScriptableObject
{
    public int GoldScoreThreshold = 85;
    public int SilverScoreThreshold = 70;
    public int BronzeScoreThreshold = 55;
    public List<PawPalObedienceTrialLevelDefinition> Levels = new List<PawPalObedienceTrialLevelDefinition>();
    public List<PawPalObedienceTrialAnimationMapping> AnimationMappings = new List<PawPalObedienceTrialAnimationMapping>();
}

[Serializable]
public sealed class PawPalObedienceTrialLevelProgress
{
    public string LevelId = PawPalObedienceTrialLevelId.Beginner.ToString();
    public int BestScore;
    public PawPalObedienceTrialMedal BestMedal;
    public int CompletedCount;
}

[Serializable]
public sealed class PawPalObedienceTrialProgress
{
    public string HighestUnlockedLevel = PawPalObedienceTrialLevelId.Beginner.ToString();
    public List<PawPalObedienceTrialLevelProgress> Levels = new List<PawPalObedienceTrialLevelProgress>();
    public int CompletedCount;
    public int GoldCount;
    public long LastCompletedUtcTicks;
    public List<string> GrantedRewardIds = new List<string>();
}

public sealed class PawPalObedienceTrialRoundResult
{
    public PawPalObedienceTrialRoundDefinition Definition;
    public int Score;
    public int Mistakes;
    public string Feedback;
}

public sealed class PawPalObedienceTrialRun
{
    public PawPalDogState Pet;
    public IntroPetSpecies Species;
    public PawPalObedienceTrialLevelDefinition Level;
    public readonly List<PawPalObedienceTrialRoundResult> RoundResults = new List<PawPalObedienceTrialRoundResult>();
    public int FinalScore;
    public PawPalObedienceTrialMedal Medal;
    public string RewardId;
}
