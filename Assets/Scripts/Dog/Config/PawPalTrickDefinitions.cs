using System;
using System.Collections.Generic;
using UnityEngine;

public enum PawPalTrickId
{
    Sit,
    Lie,
    Shake,
    Jump,
    Spin,
    RollOver,
    Beg,
    Stay,
    Come,
    Dance,
    Backflip,
    PlayDead,
    HighFive,
    PawLeft,
    PawRight,
    Circle,
    Pose,
    FetchReady,
    Bow,
    Speak,
    Quiet
}

public enum PawPalGestureType
{
    None,
    Tap,
    Hold,
    SwipeUp,
    SwipeDown,
    SwipeLeft,
    SwipeRight,
    HorizontalSwipe,
    CircularSwipe,
    DragFromBodyPart,
    PetStroke
}

public enum PawPalDogBodyZone
{
    Any,
    Head,
    Chest,
    Back,
    Belly,
    PawLeft,
    PawRight,
    Tail,
    GroundNearDog,
    AirAboveDog
}

public enum PawPalTrickLearningStage
{
    Undiscovered,
    Discovered,
    Practicing,
    Learned,
    Mastered
}

public enum PawPalTrickFailureReason
{
    None,
    Busy,
    MissingPrerequisite,
    LowBond,
    LowFocus,
    LowMood,
    LowEnergy,
    Hungry,
    Thirsty,
    Dirty,
    Bored,
    Cooldown,
    InvalidGesture,
    RandomMiss,
    NoActiveDog
}

[Serializable]
public sealed class PawPalTrickPersonalityModifier
{
    public PawPalDogPersonality Personality;
    public float XpMultiplier = 1f;
    public float SuccessChanceOffset;
    public float BoredomMultiplier = 1f;
}

[CreateAssetMenu(menuName = "PawFriends/Training/Trick Definition", fileName = "PawFriendsTrickDefinition")]
public sealed class PawPalTrickDefinition : ScriptableObject
{
    [SerializeField] private PawPalTrickId id;
    [SerializeField] private string displayName = "Trick";
    [SerializeField] private string description = string.Empty;
    [SerializeField] private string category = "Basic";
    [SerializeField] private int difficulty = 1;
    [SerializeField] private int requiredFocus = -1;
    [SerializeField] private int requiredBondLevel = -1;
    [SerializeField, Range(0f, 1f)] private float requiredBond01;
    [SerializeField, Range(0f, 1f)] private float requiredMood01 = 0.15f;
    [SerializeField, Range(0f, 1f)] private float requiredEnergy01 = 0.12f;
    [SerializeField] private PawPalTrickId[] requiredKnownTricks = new PawPalTrickId[0];
    [SerializeField] private PawPalGestureType gestureType = PawPalGestureType.Tap;
    [SerializeField] private PawPalDogBodyZone startZone = PawPalDogBodyZone.Any;
    [SerializeField] private PawPalDogBodyZone endZone = PawPalDogBodyZone.Any;
    [SerializeField] private string dogAnimationStateName = string.Empty;
    [SerializeField] private string successAnimationName = string.Empty;
    [SerializeField] private string failAnimationName = string.Empty;
    [SerializeField] private string iconName = "icon_paw_brand";
    [SerializeField] private string[] trainingHints = new string[0];
    [SerializeField] private float learnedRequiredXp = 60f;
    [SerializeField] private float masteryRequiredXp = 100f;
    [SerializeField] private int dailyPracticeLimit;
    [SerializeField] private int competitionScoreValue;
    [SerializeField] private string photoModePoseUnlock = string.Empty;
    [SerializeField] private string toyRequirement = string.Empty;
    [SerializeField] private PawPalTrickPersonalityModifier[] personalityModifiers = new PawPalTrickPersonalityModifier[0];
    [SerializeField] private bool coreTrainingTrick = true;

    public PawPalTrickId Id { get { return id; } }
    public string DisplayName { get { return displayName; } }
    public string Description { get { return description; } }
    public string Category { get { return category; } }
    public int Difficulty { get { return Mathf.Max(1, difficulty); } }
    public int RequiredFocus { get { return requiredFocus; } }
    public int RequiredBondLevel { get { return requiredBondLevel; } }
    public float RequiredBond01 { get { return Mathf.Clamp01(requiredBond01); } }
    public float RequiredMood01 { get { return Mathf.Clamp01(requiredMood01); } }
    public float RequiredEnergy01 { get { return Mathf.Clamp01(requiredEnergy01); } }
    public PawPalTrickId[] RequiredKnownTricks { get { return requiredKnownTricks ?? EmptyRequiredTricks; } }
    public PawPalGestureType GestureType { get { return gestureType; } }
    public PawPalDogBodyZone StartZone { get { return startZone; } }
    public PawPalDogBodyZone EndZone { get { return endZone; } }
    public string DogAnimationStateName { get { return dogAnimationStateName; } }
    public string SuccessAnimationName { get { return successAnimationName; } }
    public string FailAnimationName { get { return failAnimationName; } }
    public string IconName { get { return iconName; } }
    public string[] TrainingHints { get { return trainingHints ?? EmptyHints; } }
    public float LearnedRequiredXp { get { return Mathf.Max(1f, learnedRequiredXp); } }
    public float MasteryRequiredXp { get { return Mathf.Max(LearnedRequiredXp, masteryRequiredXp); } }
    public int DailyPracticeLimit { get { return Mathf.Max(0, dailyPracticeLimit); } }
    public int CompetitionScoreValue { get { return Mathf.Max(0, competitionScoreValue); } }
    public string PhotoModePoseUnlock { get { return photoModePoseUnlock; } }
    public string ToyRequirement { get { return toyRequirement; } }
    public PawPalTrickPersonalityModifier[] PersonalityModifiers { get { return personalityModifiers ?? EmptyModifiers; } }
    public bool CoreTrainingTrick { get { return coreTrainingTrick; } }

    private static readonly PawPalTrickId[] EmptyRequiredTricks = new PawPalTrickId[0];
    private static readonly string[] EmptyHints = new string[0];
    private static readonly PawPalTrickPersonalityModifier[] EmptyModifiers = new PawPalTrickPersonalityModifier[0];

    public string GetPrimaryHint()
    {
        return TrainingHints.Length > 0 && !string.IsNullOrWhiteSpace(TrainingHints[0])
            ? TrainingHints[0]
            : "Try a gentle gesture.";
    }

    public int GetResolvedRequiredBondLevel()
    {
        if (requiredBondLevel >= 0)
        {
            return Mathf.Clamp(requiredBondLevel, 0, PawPalGameRuntime.MaxBondLevel);
        }

        if (requiredBond01 <= 0.001f)
        {
            return 0;
        }

        return Mathf.Clamp(Mathf.CeilToInt(requiredBond01 * PawPalGameRuntime.MaxBondLevel), 1, PawPalGameRuntime.MaxBondLevel);
    }

    public float GetXpMultiplier(PawPalDogPersonality personality)
    {
        PawPalTrickPersonalityModifier modifier = FindPersonalityModifier(personality);
        return modifier != null ? Mathf.Max(0.1f, modifier.XpMultiplier) : 1f;
    }

    public float GetSuccessChanceOffset(PawPalDogPersonality personality)
    {
        PawPalTrickPersonalityModifier modifier = FindPersonalityModifier(personality);
        return modifier != null ? modifier.SuccessChanceOffset : 0f;
    }

    public float GetBoredomMultiplier(PawPalDogPersonality personality)
    {
        PawPalTrickPersonalityModifier modifier = FindPersonalityModifier(personality);
        return modifier != null ? Mathf.Max(0.1f, modifier.BoredomMultiplier) : 1f;
    }

    public void ConfigureRuntime(
        PawPalTrickId runtimeId,
        string runtimeDisplayName,
        string runtimeDescription,
        string runtimeCategory,
        int runtimeDifficulty,
        int runtimeRequiredFocus,
        int runtimeRequiredBondLevel,
        float runtimeRequiredBond01,
        float runtimeRequiredMood01,
        float runtimeRequiredEnergy01,
        PawPalGestureType runtimeGesture,
        PawPalDogBodyZone runtimeStartZone,
        PawPalDogBodyZone runtimeEndZone,
        string runtimeAnimationState,
        string runtimeIconName,
        float runtimeLearnedXp,
        float runtimeMasteryXp,
        int runtimeDailyPracticeLimit,
        string runtimePhotoPose,
        int runtimeCompetitionScore,
        string runtimeToyRequirement,
        string[] runtimeHints,
        PawPalTrickId[] runtimeRequiredTricks,
        PawPalTrickPersonalityModifier[] runtimePersonalityModifiers,
        bool runtimeCoreTrick)
    {
        id = runtimeId;
        displayName = runtimeDisplayName;
        description = runtimeDescription;
        category = runtimeCategory;
        difficulty = Mathf.Max(1, runtimeDifficulty);
        requiredFocus = runtimeRequiredFocus;
        requiredBondLevel = runtimeRequiredBondLevel;
        requiredBond01 = Mathf.Clamp01(runtimeRequiredBond01);
        requiredMood01 = Mathf.Clamp01(runtimeRequiredMood01);
        requiredEnergy01 = Mathf.Clamp01(runtimeRequiredEnergy01);
        gestureType = runtimeGesture;
        startZone = runtimeStartZone;
        endZone = runtimeEndZone;
        dogAnimationStateName = runtimeAnimationState;
        successAnimationName = runtimeAnimationState;
        iconName = runtimeIconName;
        learnedRequiredXp = Mathf.Max(1f, runtimeLearnedXp);
        masteryRequiredXp = Mathf.Max(learnedRequiredXp, runtimeMasteryXp);
        dailyPracticeLimit = Mathf.Max(0, runtimeDailyPracticeLimit);
        photoModePoseUnlock = runtimePhotoPose;
        competitionScoreValue = Mathf.Max(0, runtimeCompetitionScore);
        toyRequirement = runtimeToyRequirement ?? string.Empty;
        trainingHints = runtimeHints ?? EmptyHints;
        requiredKnownTricks = runtimeRequiredTricks ?? EmptyRequiredTricks;
        personalityModifiers = runtimePersonalityModifiers ?? EmptyModifiers;
        coreTrainingTrick = runtimeCoreTrick;
    }

    private PawPalTrickPersonalityModifier FindPersonalityModifier(PawPalDogPersonality personality)
    {
        if (personalityModifiers == null)
        {
            return null;
        }

        for (int i = 0; i < personalityModifiers.Length; i++)
        {
            PawPalTrickPersonalityModifier modifier = personalityModifiers[i];
            if (modifier != null && modifier.Personality == personality)
            {
                return modifier;
            }
        }

        return null;
    }
}

[CreateAssetMenu(menuName = "PawFriends/Training/Training Config", fileName = "PawFriendsTrainingConfig")]
public sealed class PawPalTrainingConfig : ScriptableObject
{
    [SerializeField, Range(0f, 1f)] private float baseSuccessChance = 0.52f;
    [SerializeField] private float baseXpPerSuccess = 10f;
    [SerializeField] private float baseXpPerFail = 2f;
    [SerializeField] private float praiseXpBonus = 2f;
    [SerializeField] private float treatMotivationBonus = 0.08f;
    [SerializeField] private float boredomGainPerRepeatedAttempt = 0.08f;
    [SerializeField] private float boredomRecoveryPerMinute = 0.08f;
    [SerializeField] private int maxEffectivePracticePerTrickPerDay = 6;
    [SerializeField] private float commandRecognitionTimeout = 3f;
    [SerializeField, Range(0.1f, 2f)] private float gestureLeniency = 1f;
    [SerializeField, Range(0f, 1f)] private float minBondForAdvancedTricks = 0.35f;
    [SerializeField] private float lowMoodPenalty = 0.18f;
    [SerializeField] private float lowNeedPenalty = 0.15f;
    [SerializeField] private float masteryXpMultiplierByDifficulty = 0.12f;
    [SerializeField, Range(0f, 2f)] private float personalityModifierStrength = 1f;
    [SerializeField] private float trickCooldownSeconds = 0.75f;

    public float BaseSuccessChance { get { return Mathf.Clamp01(baseSuccessChance); } }
    public float BaseXpPerSuccess { get { return Mathf.Max(0f, baseXpPerSuccess); } }
    public float BaseXpPerFail { get { return Mathf.Max(0f, baseXpPerFail); } }
    public float PraiseXpBonus { get { return Mathf.Max(0f, praiseXpBonus); } }
    public float TreatMotivationBonus { get { return Mathf.Max(0f, treatMotivationBonus); } }
    public float BoredomGainPerRepeatedAttempt { get { return Mathf.Max(0f, boredomGainPerRepeatedAttempt); } }
    public float BoredomRecoveryPerMinute { get { return Mathf.Max(0f, boredomRecoveryPerMinute); } }
    public int MaxEffectivePracticePerTrickPerDay { get { return Mathf.Max(1, maxEffectivePracticePerTrickPerDay); } }
    public float CommandRecognitionTimeout { get { return Mathf.Max(0.5f, commandRecognitionTimeout); } }
    public float GestureLeniency { get { return Mathf.Clamp(gestureLeniency, 0.1f, 2f); } }
    public float MinBondForAdvancedTricks { get { return Mathf.Clamp01(minBondForAdvancedTricks); } }
    public float LowMoodPenalty { get { return Mathf.Max(0f, lowMoodPenalty); } }
    public float LowNeedPenalty { get { return Mathf.Max(0f, lowNeedPenalty); } }
    public float MasteryXpMultiplierByDifficulty { get { return Mathf.Max(0f, masteryXpMultiplierByDifficulty); } }
    public float PersonalityModifierStrength { get { return Mathf.Clamp(personalityModifierStrength, 0f, 2f); } }
    public float TrickCooldownSeconds { get { return Mathf.Max(0f, trickCooldownSeconds); } }

    public static PawPalTrainingConfig CreateRuntimeDefault()
    {
        PawPalTrainingConfig config = CreateInstance<PawPalTrainingConfig>();
        config.name = "Runtime PawFriends Training Config";
        return config;
    }
}

[Serializable]
public sealed class PawPalDogTrickProgress
{
    public string TrickId;
    public bool IsDiscovered;
    public bool IsLearned;
    public float MasteryXp;
    public int MasteryLevel;
    public int TimesPracticedToday;
    public int TotalSuccessfulAttempts;
    public int TotalFailedAttempts;
    public string CustomVoiceCommand;
    public float CommandConfidence;
    public long LastPracticedAtUtcTicks;
    public long LearnedAtUtcTicks;
    public long LastPracticeDayUtcTicks;

    public PawPalTrickId ParsedTrickId
    {
        get
        {
            PawPalTrickId trickId;
            return Enum.TryParse(TrickId, true, out trickId) ? trickId : PawPalTrickId.Sit;
        }
    }
}

public sealed class PawPalTrickAttemptResult
{
    public PawPalTrickId TrickId;
    public bool Success;
    public float SuccessChance01;
    public bool LearnedNow;
    public bool MasteredNow;
    public bool MadeProgress;
    public PawPalTrickFailureReason Reason;
    public float XpGained;
    public float BondChange;
    public float MoodChange;
    public float EnergyChange;
    public float StaminaChange;
    public float PreviousProgress01;
    public float CurrentProgress01;
    public float CurrentStamina;
    public float MaxStamina;
    public string AnimationToPlay;
    public string FeedbackText;
}

public static class PawPalTrickSaveDefaults
{
    public const int CurrentTrickProfileVersion = 1;
    public const float DefaultBond01 = 0.35f;
    public const float DefaultMood01 = 0.72f;
}
