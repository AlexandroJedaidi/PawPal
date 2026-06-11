using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

public enum PawPalDogNeed
{
    Food,
    Water,
    Hygiene,
    Activity
}

public enum PawPalDogStatType
{
    Endurance,
    Mobility,
    Speed,
    Focus
}

public enum PawPalDogGender
{
    Male,
    Female
}

public enum PawPalDogPersonality
{
    Relaxed,
    Gentle,
    Energetic,
    Loyal,
    Curious,
    Clever,
    Social,
    Playful,
    Mischievous
}

public enum PawPalCurrencyType
{
    Basic,
    Premium
}

public enum PawPalPlayerActionType
{
    FeedDog,
    GiveWater,
    CleanDog,
    PlayWithToy,
    WalkDog,
    TrainTrick,
    CompetitionFirst,
    CompetitionSecond,
    CompetitionThird,
    DailyLogin,
    ShopPurchase
}

public enum PawPalItemCategory
{
    Dogs,
    Food,
    Toys,
    Collars,
    Clothing,
    Furniture
}

public enum PawPalToyInteractionMode
{
    CarryInMouth,
    PawHitRoll
}

public enum PawPalShopPreviewMode
{
    SpriteOnly,
    StandaloneModel,
    WearableOnDog,
    BreedTriptychDog
}

[Serializable]
public sealed class PawPalTrainerState
{
    public string TrainerName = "Trainer name";
    public int Level = 5;
    public int CurrentLevelExperience = 210;
    public int BasicCurrency = 5500;
    public int PremiumCurrency = 2200;
    public int ClubExperience;
    public int CompetitionsParticipated;
    public int CompetitionsWonFirst;
    public int CompetitionsWonSecond;
    public int CompetitionsWonThird;
    public int WalksCompleted;
    public int DailyActivitiesCompleted;
    public int TotalBasicCurrencyEarned;
    public int HighestDogClubRanking = 137;
}

[Serializable]
public sealed class PawPalDogState
{
    public string Id;
    public string DisplayName;
    public PawPalDogGender Gender;
    public PawPalDogPersonality Personality;
    public string FurColor;
    public string Breed;
    public int ProfileVersion;
    public float Food01;
    public float Water01;
    public float Hygiene01;
    public float Activity01;
    public float Energy01;
    public PawPalDogWalkData WalkData = new PawPalDogWalkData();
    public int TrickProfileVersion;
    public float Bond01;
    public int BondXp;
    public int BondLevel;
    public float Mood01;
    public float TrainingFatigue01;
    public long LastTrainingFatigueUpdateUtcTicks;
    public List<PawPalDogTrickProgress> Tricks = new List<PawPalDogTrickProgress>();
    public int Endurance;
    public int Mobility;
    public int Speed;
    public int Focus;
    public float NewDogWhinyHoursRemaining;
    public PawPalObedienceTrialProgress ObedienceTrialProgress = new PawPalObedienceTrialProgress();

    public float GetNeed(PawPalDogNeed need)
    {
        switch (need)
        {
            case PawPalDogNeed.Food:
                return Food01;
            case PawPalDogNeed.Water:
                return Water01;
            case PawPalDogNeed.Hygiene:
                return Hygiene01;
            case PawPalDogNeed.Activity:
                return Activity01;
            default:
                return 0f;
        }
    }

    public void SetNeed(PawPalDogNeed need, float value01)
    {
        float clamped = Mathf.Clamp01(value01);
        switch (need)
        {
            case PawPalDogNeed.Food:
                Food01 = clamped;
                break;
            case PawPalDogNeed.Water:
                Water01 = clamped;
                break;
            case PawPalDogNeed.Hygiene:
                Hygiene01 = clamped;
                break;
            case PawPalDogNeed.Activity:
                Activity01 = clamped;
                break;
        }
    }

    public void ModifyNeed(PawPalDogNeed need, float delta01)
    {
        SetNeed(need, GetNeed(need) + delta01);
    }

    public int GetStat(PawPalDogStatType statType)
    {
        switch (statType)
        {
            case PawPalDogStatType.Endurance:
                return Endurance;
            case PawPalDogStatType.Mobility:
                return Mobility;
            case PawPalDogStatType.Speed:
                return Speed;
            case PawPalDogStatType.Focus:
                return Focus;
            default:
                return 0;
        }
    }

    public void ModifyStat(PawPalDogStatType statType, int delta)
    {
        int nextValue = Mathf.Clamp(GetStat(statType) + delta, 0, 10);
        switch (statType)
        {
            case PawPalDogStatType.Endurance:
                Endurance = nextValue;
                break;
            case PawPalDogStatType.Mobility:
                Mobility = nextValue;
                break;
            case PawPalDogStatType.Speed:
                Speed = nextValue;
                break;
            case PawPalDogStatType.Focus:
                Focus = nextValue;
                break;
        }
    }
}

internal static class PawPalDogPersonalityProfiles
{
    public const int CurrentProfileVersion = 1;
    private const int PersonalityCount = 9;
    private static readonly string[] FallbackFurColors = { "Black", "Brown", "Cream", "White", "Golden", "Gray" };
    private static readonly string[] FallbackBreeds = { "Mixed breed", "Labrador", "Corgi", "Husky", "Beagle", "Shepherd" };

    public static bool EnsureProfile(PawPalDogState dog)
    {
        if (dog == null)
        {
            return false;
        }

        DogProfileSeed profile = BuildProfileSeed(dog.Id, dog.DisplayName);
        bool needsProfile = dog.ProfileVersion < CurrentProfileVersion
            || string.IsNullOrWhiteSpace(dog.FurColor)
            || string.IsNullOrWhiteSpace(dog.Breed)
            || !IsValidPersonality(dog.Personality)
            || !IsValidGender(dog.Gender);
        if (!needsProfile)
        {
            return false;
        }

        bool changed = dog.Gender != profile.Gender
            || dog.Personality != profile.Personality
            || !string.Equals(dog.FurColor, profile.FurColor, StringComparison.Ordinal)
            || !string.Equals(dog.Breed, profile.Breed, StringComparison.Ordinal)
            || dog.ProfileVersion != CurrentProfileVersion;

        dog.Gender = profile.Gender;
        dog.Personality = profile.Personality;
        dog.FurColor = profile.FurColor;
        dog.Breed = profile.Breed;
        dog.ProfileVersion = CurrentProfileVersion;
        return changed;
    }

    public static PawPalDogState FindRuntimeDog(string dogId)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || string.IsNullOrWhiteSpace(dogId))
        {
            return null;
        }

        for (int i = 0; i < runtime.Dogs.Count; i++)
        {
            PawPalDogState dog = runtime.Dogs[i];
            if (dog != null && string.Equals(dog.Id, dogId, StringComparison.OrdinalIgnoreCase))
            {
                EnsureProfile(dog);
                return dog;
            }
        }

        return null;
    }

    public static PawPalDogPersonality GetPersonalityForDogId(string dogId, string fallbackKey)
    {
        PawPalDogState dog = FindRuntimeDog(dogId);
        return dog != null ? dog.Personality : BuildProfileSeed(dogId, fallbackKey).Personality;
    }

    public static string FormatGender(PawPalDogGender gender)
    {
        return gender == PawPalDogGender.Female ? "Female" : "Male";
    }

    public static string FormatPersonality(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Gentle:
                return "Gentle";
            case PawPalDogPersonality.Energetic:
                return "Energetic";
            case PawPalDogPersonality.Loyal:
                return "Loyal";
            case PawPalDogPersonality.Curious:
                return "Curious";
            case PawPalDogPersonality.Clever:
                return "Clever";
            case PawPalDogPersonality.Social:
                return "Social";
            case PawPalDogPersonality.Playful:
                return "Playful";
            case PawPalDogPersonality.Mischievous:
                return "Mischievous";
            default:
                return "Relaxed";
        }
    }

    public static string FormatProfileText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    public static float GetNeedDrainMultiplier(PawPalDogState dog, PawPalDogNeed need)
    {
        switch (GetPersonality(dog))
        {
            case PawPalDogPersonality.Relaxed:
                return need == PawPalDogNeed.Activity ? 0.8f : need == PawPalDogNeed.Hygiene ? 0.95f : 0.85f;
            case PawPalDogPersonality.Gentle:
                return need == PawPalDogNeed.Activity ? 0.9f : 0.92f;
            case PawPalDogPersonality.Energetic:
                return need == PawPalDogNeed.Activity ? 1.25f : 1.15f;
            case PawPalDogPersonality.Loyal:
                return need == PawPalDogNeed.Activity ? 1.1f : 1f;
            case PawPalDogPersonality.Curious:
                return need == PawPalDogNeed.Activity ? 1.12f : need == PawPalDogNeed.Hygiene ? 1.05f : 1f;
            case PawPalDogPersonality.Clever:
                return need == PawPalDogNeed.Activity ? 1.1f : 1f;
            case PawPalDogPersonality.Social:
                return need == PawPalDogNeed.Activity ? 1.12f : 1f;
            case PawPalDogPersonality.Playful:
                return need == PawPalDogNeed.Activity ? 1.2f : need == PawPalDogNeed.Hygiene ? 1.15f : 1.05f;
            case PawPalDogPersonality.Mischievous:
                return need == PawPalDogNeed.Hygiene ? 1.25f : need == PawPalDogNeed.Activity ? 1.12f : 1f;
            default:
                return 1f;
        }
    }

    public static float GetEnergyDrainMultiplier(PawPalDogState dog)
    {
        switch (GetPersonality(dog))
        {
            case PawPalDogPersonality.Relaxed:
                return 0.85f;
            case PawPalDogPersonality.Gentle:
                return 0.95f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
                return 1.15f;
            case PawPalDogPersonality.Loyal:
            case PawPalDogPersonality.Curious:
            case PawPalDogPersonality.Clever:
            case PawPalDogPersonality.Social:
            case PawPalDogPersonality.Mischievous:
                return 1.08f;
            default:
                return 1f;
        }
    }

    public static float GetWalkStaminaCostMultiplier(PawPalDogState dog)
    {
        if (dog == null)
        {
            return 1f;
        }

        switch (dog.Personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 1.1f;
            case PawPalDogPersonality.Energetic:
                return 0.8f;
            case PawPalDogPersonality.Loyal:
            case PawPalDogPersonality.Clever:
            case PawPalDogPersonality.Mischievous:
                return 0.95f;
            case PawPalDogPersonality.Social:
            case PawPalDogPersonality.Playful:
                return 0.9f;
            default:
                return 1f;
        }
    }

    public static float GetWalkPresentChanceMultiplier(PawPalDogState dog)
    {
        switch (GetPersonality(dog))
        {
            case PawPalDogPersonality.Relaxed:
                return 0.95f;
            case PawPalDogPersonality.Curious:
                return 1.25f;
            case PawPalDogPersonality.Clever:
            case PawPalDogPersonality.Social:
            case PawPalDogPersonality.Playful:
                return 1.1f;
            case PawPalDogPersonality.Mischievous:
                return 1.2f;
            default:
                return 1f;
        }
    }

    public static float GetWalkDogEncounterChanceMultiplier(PawPalDogState dog)
    {
        switch (GetPersonality(dog))
        {
            case PawPalDogPersonality.Gentle:
                return 0.95f;
            case PawPalDogPersonality.Energetic:
                return 1.1f;
            case PawPalDogPersonality.Loyal:
                return 0.85f;
            case PawPalDogPersonality.Social:
                return 1.3f;
            case PawPalDogPersonality.Playful:
                return 1.15f;
            case PawPalDogPersonality.Mischievous:
                return 1.05f;
            default:
                return 1f;
        }
    }

    public static float GetTrainingBonusChance(PawPalDogState dog)
    {
        switch (GetPersonality(dog))
        {
            case PawPalDogPersonality.Clever:
                return 0.35f;
            case PawPalDogPersonality.Loyal:
                return 0.25f;
            case PawPalDogPersonality.Playful:
                return 0.2f;
            case PawPalDogPersonality.Energetic:
                return 0.15f;
            default:
                return 0f;
        }
    }

    public static float GetInteractionAnnoyedChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Mischievous:
            case PawPalDogPersonality.Clever:
                return 1.25f;
            case PawPalDogPersonality.Curious:
            case PawPalDogPersonality.Social:
            case PawPalDogPersonality.Loyal:
                return 1f;
            case PawPalDogPersonality.Gentle:
            case PawPalDogPersonality.Relaxed:
                return 0.65f;
            default:
                return 1f;
        }
    }

    public static float GetInteractionAnnoyedCooldownMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Mischievous:
            case PawPalDogPersonality.Clever:
                return 0.8f;
            case PawPalDogPersonality.Curious:
            case PawPalDogPersonality.Social:
            case PawPalDogPersonality.Loyal:
                return 1f;
            case PawPalDogPersonality.Gentle:
            case PawPalDogPersonality.Relaxed:
                return 1.35f;
            default:
                return 1f;
        }
    }

    public static float GetNewDogWhinyInitialHours(PawPalDogPersonality personality)
    {
        return personality == PawPalDogPersonality.Gentle ? 3f : 0f;
    }

    public static float GetRoomRoamRadiusMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 0.78f;
            case PawPalDogPersonality.Gentle:
            case PawPalDogPersonality.Loyal:
                return 0.88f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Curious:
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Mischievous:
                return 1.2f;
            case PawPalDogPersonality.Social:
                return 1.08f;
            default:
                return 1f;
        }
    }

    public static float GetRoomWaitMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 1.28f;
            case PawPalDogPersonality.Gentle:
                return 1.12f;
            case PawPalDogPersonality.Energetic:
                return 0.7f;
            case PawPalDogPersonality.Curious:
            case PawPalDogPersonality.Social:
                return 0.85f;
            case PawPalDogPersonality.Playful:
                return 0.75f;
            case PawPalDogPersonality.Mischievous:
                return 0.8f;
            default:
                return 1f;
        }
    }

    public static float GetRoomSpeedMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 0.82f;
            case PawPalDogPersonality.Gentle:
                return 0.92f;
            case PawPalDogPersonality.Energetic:
                return 1.18f;
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Mischievous:
                return 1.08f;
            default:
                return 1f;
        }
    }

    public static float GetRoomAmbientIdleChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
            case PawPalDogPersonality.Gentle:
                return 1.18f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Mischievous:
                return 0.88f;
            default:
                return 1f;
        }
    }

    public static float GetRoomBarkChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Gentle:
            case PawPalDogPersonality.Relaxed:
                return 0.55f;
            case PawPalDogPersonality.Social:
            case PawPalDogPersonality.Playful:
                return 1.35f;
            case PawPalDogPersonality.Mischievous:
                return 1.45f;
            default:
                return 1f;
        }
    }

    public static float GetRoomChillChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 1.65f;
            case PawPalDogPersonality.Gentle:
                return 1.25f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
                return 0.75f;
            default:
                return 1f;
        }
    }

    public static float GetRoomSleepChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 1.7f;
            case PawPalDogPersonality.Gentle:
                return 1.15f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Mischievous:
                return 0.75f;
            default:
                return 1f;
        }
    }

    public static float GetToyPickupChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 0.65f;
            case PawPalDogPersonality.Gentle:
                return 0.8f;
            case PawPalDogPersonality.Curious:
                return 1.2f;
            case PawPalDogPersonality.Playful:
                return 1.45f;
            case PawPalDogPersonality.Mischievous:
                return 1.3f;
            default:
                return 1f;
        }
    }

    public static float GetBigBallInterestChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
                return 0.65f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
                return 1.3f;
            case PawPalDogPersonality.Mischievous:
                return 1.2f;
            default:
                return 1f;
        }
    }

    public static float GetToyPlayBurstChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
            case PawPalDogPersonality.Gentle:
                return 0.72f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
                return 1.35f;
            case PawPalDogPersonality.Mischievous:
                return 1.25f;
            default:
                return 1f;
        }
    }

    public static float GetToyChasePartnerChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Loyal:
                return 0.75f;
            case PawPalDogPersonality.Social:
                return 1.35f;
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Energetic:
                return 1.2f;
            default:
                return 1f;
        }
    }

    public static float GetBigBallChaseChanceMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Relaxed:
            case PawPalDogPersonality.Gentle:
                return 0.75f;
            case PawPalDogPersonality.Energetic:
            case PawPalDogPersonality.Playful:
                return 1.25f;
            default:
                return 1f;
        }
    }

    public static float GetSocialPairWeightMultiplier(PawPalDogPersonality personality)
    {
        switch (personality)
        {
            case PawPalDogPersonality.Loyal:
                return 0.78f;
            case PawPalDogPersonality.Social:
                return 1.35f;
            case PawPalDogPersonality.Playful:
            case PawPalDogPersonality.Energetic:
                return 1.15f;
            case PawPalDogPersonality.Relaxed:
            case PawPalDogPersonality.Gentle:
                return 0.95f;
            default:
                return 1f;
        }
    }

    public static string GetWalkMomentTitle(PawPalDogState dog)
    {
        return FormatPersonality(GetPersonality(dog)) + " moment";
    }

    public static string GetWalkMomentBody(PawPalDogState dog)
    {
        string dogName = dog != null && !string.IsNullOrWhiteSpace(dog.DisplayName) ? dog.DisplayName : "Your dog";
        switch (GetPersonality(dog))
        {
            case PawPalDogPersonality.Gentle:
                return dogName + " took the route calmly and stayed close.";
            case PawPalDogPersonality.Energetic:
                return dogName + " bounced ahead, ready for the next stretch.";
            case PawPalDogPersonality.Loyal:
                return dogName + " checked back with you before trotting on.";
            case PawPalDogPersonality.Curious:
                return dogName + " paused to sniff something interesting nearby.";
            case PawPalDogPersonality.Clever:
                return dogName + " watched the route carefully and kept a steady pace.";
            case PawPalDogPersonality.Social:
                return dogName + " perked up at every friendly sound along the path.";
            case PawPalDogPersonality.Playful:
                return dogName + " turned the walk into a tiny game.";
            case PawPalDogPersonality.Mischievous:
                return dogName + " made a cheeky little detour before coming back.";
            default:
                return dogName + " took a cozy breather before continuing.";
        }
    }

    private static PawPalDogPersonality GetPersonality(PawPalDogState dog)
    {
        return dog != null ? dog.Personality : PawPalDogPersonality.Relaxed;
    }

    private static bool IsValidGender(PawPalDogGender gender)
    {
        return gender == PawPalDogGender.Male || gender == PawPalDogGender.Female;
    }

    private static bool IsValidPersonality(PawPalDogPersonality personality)
    {
        int value = (int)personality;
        return value >= 0 && value < PersonalityCount;
    }

    private static DogProfileSeed BuildProfileSeed(string dogId, string fallbackKey)
    {
        if (string.Equals(dogId, "pepper", StringComparison.OrdinalIgnoreCase))
        {
            return new DogProfileSeed(PawPalDogGender.Male, PawPalDogPersonality.Loyal, "Beige", "Labrador");
        }

        if (string.Equals(dogId, "miso", StringComparison.OrdinalIgnoreCase))
        {
            return new DogProfileSeed(PawPalDogGender.Female, PawPalDogPersonality.Relaxed, "Brown", "Corgi");
        }

        if (string.Equals(dogId, "suki", StringComparison.OrdinalIgnoreCase))
        {
            return new DogProfileSeed(PawPalDogGender.Female, PawPalDogPersonality.Energetic, "White", "Husky");
        }

        int seed = BuildStableSeed(!string.IsNullOrWhiteSpace(dogId) ? dogId : fallbackKey);
        return new DogProfileSeed(
            seed % 2 == 0 ? PawPalDogGender.Male : PawPalDogGender.Female,
            (PawPalDogPersonality)(seed % PersonalityCount),
            FallbackFurColors[seed % FallbackFurColors.Length],
            FallbackBreeds[(seed / 3) % FallbackBreeds.Length]);
    }

    private static int BuildStableSeed(string value)
    {
        unchecked
        {
            int seed = 17;
            if (!string.IsNullOrWhiteSpace(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    seed = seed * 31 + char.ToLowerInvariant(value[i]);
                }
            }

            return seed & 0x7fffffff;
        }
    }

    private struct DogProfileSeed
    {
        public DogProfileSeed(PawPalDogGender gender, PawPalDogPersonality personality, string furColor, string breed)
        {
            Gender = gender;
            Personality = personality;
            FurColor = furColor;
            Breed = breed;
        }

        public readonly PawPalDogGender Gender;
        public readonly PawPalDogPersonality Personality;
        public readonly string FurColor;
        public readonly string Breed;
    }
}

[Serializable]
public sealed class PawPalPointPackageDefinition
{
    public string Id;
    public int Points;
    public string PriceLabel;
    public string SavingsLabel;
}

[Serializable]
public sealed class PawPalCatalogItemDefinition
{
    public string Id;
    public string DisplayName;
    public PawPalItemCategory Category;
    public PawPalCurrencyType CurrencyType;
    public int Price;
    public bool AppearsInShop = true;
    public bool AppearsInInventory = true;
    public bool DisabledInShop;
    public bool CanPurchaseMultiple;
    public bool StarterOwned;
    public int StarterQuantity;
    public string DisabledReason;
    public string Description;
    public string ShopSpritePath;
    public string GeneratedShopSpritePath;
    public string PreviewSpritePath;
    public string PreviewPrefabResourcePath;
    public PawPalShopPreviewMode PreviewMode = PawPalShopPreviewMode.SpriteOnly;
    public string InventorySpritePath;
    public InventoryCardTheme InventoryCardTheme;
    public string RoomPrefabResourcePath;
    public PawPalToyInteractionMode ToyInteractionMode = PawPalToyInteractionMode.CarryInMouth;
    public Color ToyTint = Color.white;
    public string CollarPrefabResourcePath;
    public Color CollarTint = Color.white;
    public Vector3 CollarLocalPosition;
    public Quaternion CollarLocalRotation = Quaternion.identity;
    public Vector3 CollarLocalScale = Vector3.one;
}

[DisallowMultipleComponent]
public sealed class PawPalToyRuntimeMetadata : MonoBehaviour
{
    private const float LargeToyMinimumNavigationRadius = 0.12f;
    private const float LargeToyNavigationPadding = 0.04f;
    private static readonly List<PawPalToyRuntimeMetadata> NavigationBlockers = new List<PawPalToyRuntimeMetadata>();
    private static BoxCollider largeToySupportFloor;
    private static bool hasLargeToySupportFloorTop;
    private static float largeToySupportFloorTopY;
    private static float nextSceneLargeToyScanTime;

    [SerializeField] private string itemId;
    [SerializeField] private PawPalToyInteractionMode interactionMode = PawPalToyInteractionMode.CarryInMouth;
    [SerializeField] private bool blocksDogNavigation;
    [SerializeField] private float dogNavigationBlockRadius = 0.45f;
    [SerializeField] private Vector3 dogNavigationBlockCenterLocal = Vector3.zero;

    public string ItemId => itemId;
    public PawPalToyInteractionMode InteractionMode => interactionMode;
    public bool BlocksDogNavigation => blocksDogNavigation;
    public float DogNavigationBlockRadius => Mathf.Max(0.05f, dogNavigationBlockRadius);
    public Vector3 DogNavigationBlockWorldCenter => transform.TransformPoint(dogNavigationBlockCenterLocal);

    public static float GetRecommendedLargeToyNavigationBlockRadius(float visualRadius)
    {
        return Mathf.Max(LargeToyMinimumNavigationRadius, Mathf.Max(0.01f, visualRadius) + LargeToyNavigationPadding);
    }

    public static bool TryGetDogNavigationBlockRadius(Transform toy, out float radius)
    {
        radius = 0f;
        if (toy == null)
        {
            return false;
        }

        PawPalToyRuntimeMetadata metadata = toy.GetComponentInParent<PawPalToyRuntimeMetadata>();
        if (metadata == null || !metadata.BlocksDogNavigation)
        {
            return false;
        }

        radius = metadata.DogNavigationBlockRadius;
        return true;
    }

    public static bool TryGetDogNavigationBlockCenter(Transform toy, out Vector3 center)
    {
        center = toy != null ? toy.position : Vector3.zero;
        if (toy == null)
        {
            return false;
        }

        PawPalToyRuntimeMetadata metadata = toy.GetComponentInParent<PawPalToyRuntimeMetadata>();
        if (metadata == null || !metadata.BlocksDogNavigation)
        {
            return false;
        }

        center = metadata.DogNavigationBlockWorldCenter;
        return true;
    }

    public static void AutoRegisterSceneLargeToys(bool force = false)
    {
        if (Application.isPlaying && !force && Time.time < nextSceneLargeToyScanTime)
        {
            return;
        }

        if (Application.isPlaying)
        {
            nextSceneLargeToyScanTime = Time.time + 1f;
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || !IsPawHitRollToyName(candidate.name))
            {
                continue;
            }

            if (candidate.GetComponentInChildren<Renderer>(true) == null)
            {
                continue;
            }

            PawPalToyRuntimeMetadata metadata = candidate.GetComponent<PawPalToyRuntimeMetadata>();
            if (metadata == null)
            {
                metadata = candidate.gameObject.AddComponent<PawPalToyRuntimeMetadata>();
            }

            metadata.Initialize(string.Empty, PawPalToyInteractionMode.PawHitRoll);
            metadata.ConfigureLargeToyPhysicsAndNavigation();
        }
    }

    public static bool IsPawHitRollToyName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        string lowerName = objectName.ToLowerInvariant();
        return lowerName.Contains("big_ball") || lowerName.Contains("big ball");
    }

    public void Initialize(string catalogItemId, PawPalToyInteractionMode toyInteractionMode)
    {
        itemId = catalogItemId;
        interactionMode = toyInteractionMode;
        SetBlocksDogNavigation(toyInteractionMode == PawPalToyInteractionMode.PawHitRoll, dogNavigationBlockRadius);
    }

    public void SetBlocksDogNavigation(bool blocksNavigation, float radius)
    {
        blocksDogNavigation = blocksNavigation;
        dogNavigationBlockRadius = Mathf.Max(0.05f, radius);
        RefreshNavigationBlockerRegistration();
    }

    public void ConfigureLargeToyPhysicsAndNavigation()
    {
        Bounds bounds;
        float visualRadius = TryGetToyBlockingBounds(gameObject, out bounds)
            ? Mathf.Max(bounds.extents.x, bounds.extents.z)
            : 0.35f;
        float colliderRadius = Mathf.Max(0.18f, visualRadius);
        EnsureLargeToyCollider(bounds, colliderRadius);
        BoxCollider supportFloor = EnsureLargeToySupportFloor(bounds);
        SnapLargeToyOntoSupportFloor(bounds, supportFloor);

        if (TryGetToyBlockingBounds(gameObject, out bounds))
        {
            visualRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        float navigationRadius = GetRecommendedLargeToyNavigationBlockRadius(visualRadius);
        dogNavigationBlockCenterLocal = transform.InverseTransformPoint(bounds.center);
        SetBlocksDogNavigation(true, navigationRadius);
        PawPalBallBounceAudio.EnsureOn(gameObject);

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.mass = Mathf.Max(0.9f, body.mass);
        body.linearDamping = Mathf.Max(0.14f, body.linearDamping);
        body.angularDamping = Mathf.Max(0.26f, body.angularDamping);
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = navigationRadius;
        obstacle.height = Mathf.Max(0.2f, bounds.size.y);
        obstacle.center = dogNavigationBlockCenterLocal;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
        obstacle.carvingMoveThreshold = 0.08f;
        obstacle.carvingTimeToStationary = 0.15f;
    }

    private void EnsureLargeToyCollider(Bounds bounds, float radius)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider.enabled && !collider.isTrigger && IsUsableLargeToyCollider(collider, bounds, radius))
            {
                return;
            }
        }

        SphereCollider fallbackCollider = GetComponent<SphereCollider>();
        if (fallbackCollider == null)
        {
            fallbackCollider = gameObject.AddComponent<SphereCollider>();
        }

        fallbackCollider.isTrigger = false;
        fallbackCollider.enabled = true;
        fallbackCollider.center = transform.InverseTransformPoint(bounds.center);
        fallbackCollider.radius = Mathf.Max(0.18f, radius);
    }

    private static bool IsUsableLargeToyCollider(Collider collider, Bounds visualBounds, float visualRadius)
    {
        if (collider == null)
        {
            return false;
        }

        Bounds colliderBounds = collider.bounds;
        Vector3 centerDelta = colliderBounds.center - visualBounds.center;
        centerDelta.y = 0f;
        float allowedCenterOffset = Mathf.Max(0.12f, visualRadius * 0.75f);
        if (centerDelta.sqrMagnitude > allowedCenterOffset * allowedCenterOffset)
        {
            return false;
        }

        float colliderPlanarRadius = Mathf.Max(colliderBounds.extents.x, colliderBounds.extents.z);
        return colliderPlanarRadius >= Mathf.Max(0.08f, visualRadius * 0.55f);
    }

    private static BoxCollider EnsureLargeToySupportFloor(Bounds toyBounds)
    {
        if (largeToySupportFloor == null)
        {
            GameObject floorObject = new GameObject("PawFriendsLargeToySupportFloor");
            floorObject.hideFlags = HideFlags.HideAndDontSave;
            floorObject.layer = 2;
            largeToySupportFloor = floorObject.AddComponent<BoxCollider>();
            hasLargeToySupportFloorTop = false;
        }

        Bounds supportBounds = toyBounds;
        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.None);
        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (agent != null)
            {
                supportBounds.Encapsulate(agent.transform.position);
            }
        }

        float width = Mathf.Max(8f, supportBounds.size.x + 6f);
        float depth = Mathf.Max(8f, supportBounds.size.z + 6f);
        float requestedFloorTopY = Mathf.Max(0f, toyBounds.min.y - 0.01f);
        if (!hasLargeToySupportFloorTop)
        {
            largeToySupportFloorTopY = requestedFloorTopY;
            hasLargeToySupportFloorTop = true;
        }

        float floorTopY = largeToySupportFloorTopY;
        float floorThickness = 0.16f;

        largeToySupportFloor.transform.position = new Vector3(
            supportBounds.center.x,
            floorTopY - floorThickness * 0.5f,
            supportBounds.center.z);
        largeToySupportFloor.size = new Vector3(width, floorThickness, depth);
        largeToySupportFloor.center = Vector3.zero;
        largeToySupportFloor.enabled = true;
        return largeToySupportFloor;
    }

    private void SnapLargeToyOntoSupportFloor(Bounds toyBounds, BoxCollider supportFloor)
    {
        if (supportFloor == null || !supportFloor.enabled)
        {
            return;
        }

        float supportTopY = supportFloor.bounds.max.y;
        float minBottomY = supportTopY + 0.015f;
        if (toyBounds.min.y >= supportTopY - 0.001f)
        {
            return;
        }

        Vector3 position = transform.position;
        position.y += minBottomY - toyBounds.min.y;
        transform.position = position;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    public static bool IsDogNavigationPathBlocked(Vector3[] pathCorners, Transform ignoredRoot, float extraPadding)
    {
        if (pathCorners == null || pathCorners.Length < 2 || NavigationBlockers.Count == 0)
        {
            return false;
        }

        for (int cornerIndex = 1; cornerIndex < pathCorners.Length; cornerIndex++)
        {
            Vector3 start = pathCorners[cornerIndex - 1];
            Vector3 end = pathCorners[cornerIndex];
            for (int blockerIndex = NavigationBlockers.Count - 1; blockerIndex >= 0; blockerIndex--)
            {
                PawPalToyRuntimeMetadata blocker = NavigationBlockers[blockerIndex];
                if (blocker == null)
                {
                    NavigationBlockers.RemoveAt(blockerIndex);
                    continue;
                }

                if (!blocker.IsBlockingDogNavigation(ignoredRoot))
                {
                    continue;
                }

                float blockRadius = blocker.DogNavigationBlockRadius + Mathf.Max(0f, extraPadding);
                if (DistanceToPlanarSegment(blocker.DogNavigationBlockWorldCenter, start, end) < blockRadius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsDogNavigationPointBlocked(Vector3 point, Transform ignoredRoot, float extraPadding)
    {
        for (int blockerIndex = NavigationBlockers.Count - 1; blockerIndex >= 0; blockerIndex--)
        {
            PawPalToyRuntimeMetadata blocker = NavigationBlockers[blockerIndex];
            if (blocker == null)
            {
                NavigationBlockers.RemoveAt(blockerIndex);
                continue;
            }

            if (!blocker.IsBlockingDogNavigation(ignoredRoot))
            {
                continue;
            }

            Vector3 delta = blocker.DogNavigationBlockWorldCenter - point;
            delta.y = 0f;
            float blockRadius = blocker.DogNavigationBlockRadius + Mathf.Max(0f, extraPadding);
            if (delta.sqrMagnitude < blockRadius * blockRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        RefreshNavigationBlockerRegistration();
    }

    private void OnDisable()
    {
        NavigationBlockers.Remove(this);
    }

    private void RefreshNavigationBlockerRegistration()
    {
        bool shouldRegister = isActiveAndEnabled && blocksDogNavigation;
        bool isRegistered = NavigationBlockers.Contains(this);
        if (shouldRegister && !isRegistered)
        {
            NavigationBlockers.Add(this);
        }
        else if (!shouldRegister && isRegistered)
        {
            NavigationBlockers.Remove(this);
        }
    }

    private bool IsBlockingDogNavigation(Transform ignoredRoot)
    {
        if (!blocksDogNavigation || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            return false;
        }

        return ignoredRoot == null
            || (transform != ignoredRoot && !transform.IsChildOf(ignoredRoot) && !ignoredRoot.IsChildOf(transform));
    }

    private static bool TryGetToyBlockingBounds(GameObject toy, out Bounds bounds)
    {
        bounds = toy != null ? new Bounds(toy.transform.position, Vector3.zero) : new Bounds();
        if (toy == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = toy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = toy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private static float DistanceToPlanarSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        point.y = 0f;
        start.y = 0f;
        end.y = 0f;

        Vector3 segment = end - start;
        if (segment.sqrMagnitude < 0.001f)
        {
            return Vector3.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude);
        return Vector3.Distance(point, start + segment * t);
    }
}

[Serializable]
public sealed class PawPalOwnedItemState
{
    public string ItemId;
    public int Quantity;
    public bool Owned;
}

[Serializable]
public sealed class PawPalDogEquipmentState
{
    public string DogId;
    public string EquippedCollarItemId;
}

[Serializable]
public sealed class PawPalDailyTaskState
{
    public string Id;
    public string Title;
    public PawPalPlayerActionType ActionType;
    public int RequiredCount;
    public int Progress;
    public int ExperienceReward;
    public int BasicCurrencyReward;
    public bool RewardGranted;
}

[Serializable]
public sealed class PawPalSaveData
{
    public PawPalTrainerState TrainerState;
    public List<PawPalDogState> Dogs = new List<PawPalDogState>();
    public List<PawPalOwnedItemState> OwnedItems = new List<PawPalOwnedItemState>();
    public List<PawPalDogEquipmentState> DogEquipment = new List<PawPalDogEquipmentState>();
    public List<PawPalDailyTaskState> DailyTasks = new List<PawPalDailyTaskState>();
    public List<PawPalAgilityPetProgressState> AgilityProgress = new List<PawPalAgilityPetProgressState>();
    public PawPalWalkSessionSaveData ActiveWalkSession;
    public PawPalAgilityTrialSessionSaveData ActiveAgilityTrialSession;
    public int ActiveDogIndex;
    public long NextDailyResetUtcTicks;
    public int LastProcessedTrainerMilestoneLevel;
    public List<int> PendingUnsupportedTrainerMilestoneLevels = new List<int>();
}

[DisallowMultipleComponent]
public sealed class PawPalGameRuntime : MonoBehaviour
{
    private const float DefaultSecondsPerGameHour = 180f;
    private const float FoodDrainPerHour = 0.03f;
    private const float WaterDrainPerHour = 0.03f;
    private const float HygieneDrainPerHour = 0.02f;
    private const float ActivityDrainPerHour = 0.035f;
    private const float PetMorningStartHour = 7f;
    private const float PetDaySteadyStartHour = 10f;
    private const float PetEveningStartHour = 18.5f;
    private const float PetNightStartHour = 22f;
    private const float PetWakeTransitionHours = 0.75f;
    private const float PetEveningTransitionHours = 1.25f;
    private const float OvernightNeedDrainMultiplier = 0.05f;
    private const float SleepyNeedDrainMultiplier = 0.35f;
    private const float DefaultMaxWalkStamina = 100f;
    private const float MaxWalkStaminaCap = 180f;
    private const float WalkStaminaRefillPerHour = 12.5f;
    private const float WalkStaminaXpPerWalk = 4f;
    private const float WalkStaminaXpPerDistance = 0.04f;
    private const float WalkStaminaXpNeededPerLevel = 30f;
    private const float WalkStaminaIncreasePerLevel = 5f;
    public const int BondXpPerLevel = 100;
    public const int MaxBondLevel = 10;
    public const int MaxBondXp = (MaxBondLevel - 1) * BondXpPerLevel;
    private const string SaveFileName = "pawpal_profile_v1.json";
    private const string StarterDogId = "pepper";
    private const string StarterCollarItemId = "collar_simple_c2";
    private const string BasicFoodItemId = "food_basic";
    private const string PremiumFoodItemId = "food_premium";
    private const string DownloadedShopSpriteRoot = "UI/Downloaded/Shop/";

    private static readonly Quaternion DefaultCollarRotation = new Quaternion(-0.000001496502f, 0.97860277f, 0.2057589f, 7.7398e-10f);
    private static readonly Vector3 DefaultCollarPosition = new Vector3(0f, 0.0171f, 0f);
    private static readonly Vector3 DefaultCollarScale = new Vector3(0.95f, 0.95f, 1f);

    private static PawPalGameRuntime instance;

    private readonly List<PawPalDogState> dogs = new List<PawPalDogState>();
    private readonly List<PawPalCatalogItemDefinition> catalogItems = new List<PawPalCatalogItemDefinition>();
    private readonly List<PawPalOwnedItemState> ownedItems = new List<PawPalOwnedItemState>();
    private readonly List<PawPalDogEquipmentState> dogEquipment = new List<PawPalDogEquipmentState>();
    private readonly List<PawPalPointPackageDefinition> pointPackages = new List<PawPalPointPackageDefinition>();
    private readonly List<PawPalDailyTaskState> dailyTasks = new List<PawPalDailyTaskState>();
    private readonly List<PawPalAgilityPetProgressState> agilityProgress = new List<PawPalAgilityPetProgressState>();
    private readonly Dictionary<PawPalPlayerActionType, int> actionProgress = new Dictionary<PawPalPlayerActionType, int>();
    private readonly Dictionary<string, PawPalCatalogItemDefinition> catalogById = new Dictionary<string, PawPalCatalogItemDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, PawPalOwnedItemState> ownedItemById = new Dictionary<string, PawPalOwnedItemState>(StringComparer.Ordinal);
    private readonly Dictionary<string, PawPalDogEquipmentState> dogEquipmentByDogId = new Dictionary<string, PawPalDogEquipmentState>(StringComparer.Ordinal);
    private readonly Dictionary<PawPalPlayerActionType, TrainerActionRewardDefinition> trainerActionRewardsByType = new Dictionary<PawPalPlayerActionType, TrainerActionRewardDefinition>();
    private readonly Dictionary<int, TrainerMilestoneDefinition> trainerMilestonesByLevel = new Dictionary<int, TrainerMilestoneDefinition>();
    private readonly List<int> pendingUnsupportedTrainerMilestoneLevels = new List<int>();
    private readonly HashSet<string> temporaryIntroDogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IntroPetSpecies> petSpeciesById = new Dictionary<string, IntroPetSpecies>(StringComparer.OrdinalIgnoreCase);

    [SerializeField] private float secondsPerGameHour = DefaultSecondsPerGameHour;

    private PawPalTrainerState trainerState;
    private PawPalDogSceneBridge sceneBridge;
    private TrainerProgressionConfig trainerProgressionConfig;
    private int activeDogIndex;
    private int lastPersistentActiveDogIndex;
    private int inventoryRevision;
    private int lastProcessedTrainerMilestoneLevel;
    private int shopRevision;
    private DateTime nextDailyResetUtc;
    private PawPalWalkSessionSaveData activeWalkSession;
    private PawPalAgilityTrialSessionSaveData activeAgilityTrialSession;

    public static PawPalGameRuntime Instance
    {
        get { return instance; }
    }

    public event Action StateChanged;

    public PawPalTrainerState TrainerState
    {
        get { return trainerState; }
    }

    public IReadOnlyList<PawPalDogState> Dogs
    {
        get { return dogs; }
    }

    public int ActiveDogIndex
    {
        get
        {
            if (dogs.Count == 0)
            {
                return 0;
            }

            activeDogIndex = Mathf.Clamp(activeDogIndex, 0, dogs.Count - 1);
            return activeDogIndex;
        }
    }

    public IReadOnlyList<PawPalCatalogItemDefinition> CatalogItems
    {
        get { return catalogItems; }
    }

    public IReadOnlyList<PawPalOwnedItemState> OwnedItems
    {
        get { return ownedItems; }
    }

    public IReadOnlyList<PawPalDogEquipmentState> DogEquipmentStates
    {
        get { return dogEquipment; }
    }

    public IList<PawPalPointPackageDefinition> PointPackages
    {
        get { return pointPackages; }
    }

    public IList<PawPalDailyTaskState> DailyTasks
    {
        get
        {
            EnsureDailyTasksCurrent();
            EnsureDailyTasksPresent(false);
            return dailyTasks;
        }
    }

    public PawPalDogState ActiveDog
    {
        get
        {
            if (dogs.Count == 0)
            {
                return null;
            }

            activeDogIndex = Mathf.Clamp(activeDogIndex, 0, dogs.Count - 1);
            return dogs[activeDogIndex];
        }
    }

    public IntroPetSpecies ActivePetSpecies
    {
        get
        {
            PawPalDogState activePet = ActiveDog;
            return activePet != null ? GetPetSpecies(activePet.Id) : IntroPetSpecies.Dog;
        }
    }

    public int InventoryRevision
    {
        get { return inventoryRevision; }
    }

    public int ShopRevision
    {
        get { return shopRevision; }
    }

    public PawPalWalkSessionSaveData ActiveWalkSession
    {
        get { return activeWalkSession; }
    }

    public PawPalAgilityTrialSessionSaveData ActiveAgilityTrialSession
    {
        get { return activeAgilityTrialSession; }
    }

    public IReadOnlyList<PawPalAgilityPetProgressState> AgilityProgress
    {
        get { return agilityProgress; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("PawFriendsGameRuntime");
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<PawPalGameRuntime>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadTrainerProgressionData();
        InitializeDefaults();
        LoadProfile();
        EnsureBridge();
        NotifyStateChanged();
    }

    private void Update()
    {
        EnsureDailyTasksCurrent();
        TickDogNeeds(Time.unscaledDeltaTime);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveProfile();
        }
    }

    private void OnApplicationQuit()
    {
        CancelUnfinishedActiveWalkSession("quitting the application");
        SaveProfile();
    }

    public IReadOnlyList<PawPalCatalogItemDefinition> GetCatalogItems(PawPalItemCategory category)
    {
        List<PawPalCatalogItemDefinition> filtered = new List<PawPalCatalogItemDefinition>();
        for (int i = 0; i < catalogItems.Count; i++)
        {
            PawPalCatalogItemDefinition item = catalogItems[i];
            if (item.Category == category && item.AppearsInShop)
            {
                filtered.Add(item);
            }
        }

        return filtered;
    }

    public PawPalCatalogItemDefinition GetCatalogItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        PawPalCatalogItemDefinition item;
        catalogById.TryGetValue(itemId, out item);
        return item;
    }

    public PawPalOwnedItemState GetOwnedItemState(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        PawPalOwnedItemState item;
        ownedItemById.TryGetValue(itemId, out item);
        return item;
    }

    public PawPalDogEquipmentState GetDogEquipmentState(string dogId)
    {
        if (string.IsNullOrWhiteSpace(dogId))
        {
            return null;
        }

        PawPalDogEquipmentState state;
        dogEquipmentByDogId.TryGetValue(dogId, out state);
        return state;
    }

    public string GetEquippedCollarItemId(string dogId)
    {
        PawPalDogEquipmentState state = GetDogEquipmentState(dogId);
        return state != null && !string.IsNullOrWhiteSpace(state.EquippedCollarItemId)
            ? state.EquippedCollarItemId
            : string.Empty;
    }

    public int GetItemQuantity(string itemId)
    {
        PawPalOwnedItemState state = GetOwnedItemState(itemId);
        return state != null ? Mathf.Max(0, state.Quantity) : 0;
    }

    public bool IsItemOwned(string itemId)
    {
        PawPalOwnedItemState state = GetOwnedItemState(itemId);
        return state != null && state.Owned && state.Quantity > 0;
    }

    public bool IsItemPurchasable(string itemId)
    {
        PawPalCatalogItemDefinition item = GetCatalogItem(itemId);
        if (item == null || !item.AppearsInShop || item.DisabledInShop)
        {
            return false;
        }

        if (!item.CanPurchaseMultiple && IsItemOwned(itemId))
        {
            return false;
        }

        return true;
    }

    public bool CanAfford(string itemId)
    {
        PawPalCatalogItemDefinition item = GetCatalogItem(itemId);
        if (item == null)
        {
            return false;
        }

        if (item.CurrencyType == PawPalCurrencyType.Premium)
        {
            return trainerState.PremiumCurrency >= item.Price;
        }

        return trainerState.BasicCurrency >= item.Price;
    }

    public void SelectNextDog()
    {
        if (dogs.Count <= 1 || IsTemporaryIntroSelectionLocked())
        {
            return;
        }

        activeDogIndex = (activeDogIndex + 1) % dogs.Count;
        CommitState(true, false, false);
    }

    public void SelectPreviousDog()
    {
        if (dogs.Count <= 1 || IsTemporaryIntroSelectionLocked())
        {
            return;
        }

        activeDogIndex--;
        if (activeDogIndex < 0)
        {
            activeDogIndex = dogs.Count - 1;
        }

        CommitState(true, false, false);
    }

    public bool SelectDogIndex(int dogIndex, bool saveProfile)
    {
        if (dogs.Count == 0 || dogIndex < 0 || dogIndex >= dogs.Count)
        {
            return false;
        }

        if (IsTemporaryIntroSelectionLocked() && activeDogIndex != dogIndex)
        {
            return false;
        }

        if (activeDogIndex == dogIndex)
        {
            return false;
        }

        activeDogIndex = dogIndex;
        CommitState(saveProfile, false, false);
        return true;
    }

    public bool SelectDogById(string dogId, bool saveProfile)
    {
        if (string.IsNullOrEmpty(dogId))
        {
            return false;
        }

        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            if (dog == null || !string.Equals(dog.Id, dogId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return SelectDogIndex(i, saveProfile);
        }

        return false;
    }

    private bool IsTemporaryIntroSelectionLocked()
    {
        PawPalDogState activeDog = ActiveDog;
        return activeDog != null && IsTemporaryIntroDogId(activeDog.Id);
    }

    public bool IsTemporaryIntroDog(string dogId)
    {
        return IsTemporaryIntroDogId(dogId);
    }

    public bool RegisterTemporaryIntroDog(PawPalDogState dogState, bool setActive)
    {
        return RegisterTemporaryIntroPet(dogState, IntroPetSpecies.Dog, setActive);
    }

    public bool RegisterTemporaryIntroPet(PawPalDogState dogState, IntroPetSpecies species, bool setActive)
    {
        if (dogState == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(dogState.Id))
        {
            dogState.Id = "intro_pet_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        PawPalDogPersonalityProfiles.EnsureProfile(dogState);
        EnsureWalkData(dogState);
        EnsureTrickData(dogState);
        RemoveOtherTemporaryIntroPets(dogState.Id);

        int dogIndex = -1;
        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState existingDog = dogs[i];
            if (existingDog != null && string.Equals(existingDog.Id, dogState.Id, StringComparison.OrdinalIgnoreCase))
            {
                dogIndex = i;
                break;
            }
        }

        if (dogIndex >= 0)
        {
            dogs[dogIndex] = dogState;
        }
        else
        {
            dogIndex = dogs.Count;
            dogs.Add(dogState);
        }

        temporaryIntroDogIds.Add(dogState.Id);
        petSpeciesById[dogState.Id] = species;
        EnsureDogEquipmentState(dogState.Id);

        if (setActive)
        {
            lastPersistentActiveDogIndex = GetSaveActiveDogIndex();
            activeDogIndex = dogIndex;
        }

        CommitState(false, false, false);
        return true;
    }

    private void RemoveOtherTemporaryIntroPets(string keepDogId)
    {
        for (int i = dogs.Count - 1; i >= 0; i--)
        {
            PawPalDogState existingDog = dogs[i];
            if (existingDog == null || !IsTemporaryIntroDogId(existingDog.Id))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(keepDogId)
                && string.Equals(existingDog.Id, keepDogId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            dogs.RemoveAt(i);
            temporaryIntroDogIds.Remove(existingDog.Id);
            petSpeciesById.Remove(existingDog.Id);
            dogEquipmentByDogId.Remove(existingDog.Id);

            if (activeDogIndex > i)
            {
                activeDogIndex--;
            }

            if (lastPersistentActiveDogIndex > i)
            {
                lastPersistentActiveDogIndex--;
            }
        }

        activeDogIndex = Mathf.Clamp(activeDogIndex, 0, Mathf.Max(0, dogs.Count - 1));
        lastPersistentActiveDogIndex = Mathf.Clamp(lastPersistentActiveDogIndex, 0, Mathf.Max(0, dogs.Count - 1));
    }

    public IntroPetSpecies GetPetSpecies(string petId)
    {
        if (string.IsNullOrWhiteSpace(petId))
        {
            return IntroPetSpecies.Dog;
        }

        IntroPetSpecies species;
        return petSpeciesById.TryGetValue(petId, out species) ? species : IntroPetSpecies.Dog;
    }

    public bool TryFeedActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return false;
        }

        PawPalCatalogItemDefinition foodItem = GetAvailableFoodItem();
        if (foodItem == null)
        {
            return false;
        }

        ConsumeItemQuantity(foodItem.Id, 1);
        ApplyFoodToActiveDog(foodItem);
        ApplyActionRewards(PawPalPlayerActionType.FeedDog);
        CommitState(true, true, true);
        return true;
    }

    public void FeedActiveDog()
    {
        TryFeedActiveDog();
    }

    public bool HasFoodItemForActiveDog()
    {
        return ActiveDog != null && GetAvailableFoodItem() != null;
    }

    public bool TryStartFoodNeedInteraction()
    {
        PawPalCatalogItemDefinition foodItem = GetAvailableFoodItem();
        if (ActiveDog == null || foodItem == null)
        {
            return false;
        }

        DogNeedInteractionDirector director = ResolveNeedInteractionDirector();
        if (director == null)
        {
            Debug.LogWarning("PawPalGameRuntime could not find a DogNeedInteractionDirector for food interaction.");
            return false;
        }

        string dogId = ActiveDog.Id;
        string foodItemId = foodItem.Id;
        return director.TryStartInteraction(PawPalDogNeed.Food, delegate
        {
            CompleteFoodNeedInteraction(dogId, foodItemId);
        });
    }

    public bool TryStartWaterNeedInteraction()
    {
        if (ActiveDog == null)
        {
            return false;
        }

        DogNeedInteractionDirector director = ResolveNeedInteractionDirector();
        if (director == null)
        {
            Debug.LogWarning("PawPalGameRuntime could not find a DogNeedInteractionDirector for water interaction.");
            return false;
        }

        string dogId = ActiveDog.Id;
        return director.TryStartInteraction(PawPalDogNeed.Water, delegate
        {
            CompleteWaterNeedInteraction(dogId);
        });
    }

    public void GiveWaterToActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        ApplyWaterToDog(dog);
        ApplyActionRewards(PawPalPlayerActionType.GiveWater);
        CommitState(true, false, true);
    }

    public void CleanActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        dog.ModifyNeed(PawPalDogNeed.Hygiene, 0.45f);
        ApplyActionRewards(PawPalPlayerActionType.CleanDog);
        CommitState(true, false, true);
    }

    public bool TryStartHygieneNeedInteraction()
    {
        if (ActiveDog == null)
        {
            return false;
        }

        PawPalPetGroomingDirector director = ResolvePetGroomingDirector();
        if (director == null)
        {
            Debug.LogWarning("PawPalGameRuntime could not find a PawPalPetGroomingDirector for hygiene interaction.");
            return false;
        }

        string dogId = ActiveDog.Id;
        return director.TryStartInteraction(delegate
        {
            CompleteHygieneNeedInteraction(dogId);
        });
    }

    public void PlayWithActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        dog.ModifyNeed(PawPalDogNeed.Activity, 0.35f);
        dog.ModifyNeed(PawPalDogNeed.Water, -0.05f);
        dog.ModifyNeed(PawPalDogNeed.Food, -0.04f);
        if (dog.Speed < 10)
        {
            dog.ModifyStat(PawPalDogStatType.Speed, 1);
        }

        ApplyActionRewards(PawPalPlayerActionType.PlayWithToy);
        CommitState(true, false, true);
    }

    public void WalkActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        ApplyWalkNeedChanges(dog, 0.32f, 0.11f, 0.08f, 0.05f);
        float currentStamina;
        float maxStamina;
        SpendDogStamina(dog, GetDogStaminaCostFromRatio(dog, 0.08f), out currentStamina, out maxStamina);
        if (dog.Endurance < 10)
        {
            dog.ModifyStat(PawPalDogStatType.Endurance, 1);
        }

        trainerState.WalksCompleted++;
        ApplyActionRewards(PawPalPlayerActionType.WalkDog);
        CommitState(true, false, true);
    }

    public PawPalWalkStaminaSnapshot GetActiveDogWalkStaminaSnapshot()
    {
        return GetWalkStaminaSnapshot(ActiveDog);
    }

    public PawPalWalkStaminaSnapshot GetWalkStaminaSnapshot(PawPalDogState dog)
    {
        EnsureWalkData(dog);
        RefreshWalkStamina(dog, DateTime.UtcNow, false);
        return GetCanonicalStaminaSnapshot(dog);
    }

    public void RefreshDogStamina(PawPalDogState dog)
    {
        RefreshWalkStamina(dog, DateTime.UtcNow, false);
    }

    public static void EnsureCanonicalStaminaData(PawPalDogState dog)
    {
        if (dog == null)
        {
            return;
        }

        if (dog.WalkData == null)
        {
            dog.WalkData = new PawPalDogWalkData();
        }

        PawPalDogWalkData walkData = dog.WalkData;
        if (walkData.MaxWalkStamina <= 0.001f)
        {
            walkData.MaxWalkStamina = Mathf.Clamp(DefaultMaxWalkStamina + dog.Endurance * 2f, DefaultMaxWalkStamina, MaxWalkStaminaCap);
            float legacyEnergy01 = Mathf.Clamp01(dog.Energy01);
            walkData.CurrentWalkStamina = Mathf.Lerp(0f, walkData.MaxWalkStamina, legacyEnergy01 > 0.001f ? legacyEnergy01 : 1f);
        }

        walkData.MaxWalkStamina = Mathf.Clamp(walkData.MaxWalkStamina, 1f, MaxWalkStaminaCap);
        walkData.CurrentWalkStamina = Mathf.Clamp(walkData.CurrentWalkStamina, 0f, walkData.MaxWalkStamina);
        if (walkData.LastStaminaRefreshAtUtcTicks <= 0L)
        {
            walkData.LastStaminaRefreshAtUtcTicks = DateTime.UtcNow.Ticks;
        }

        SyncLegacyEnergyFromStamina(dog);
    }

    public static void EnsureDogBondProgression(PawPalDogState dog)
    {
        if (dog == null)
        {
            return;
        }

        bool hasBondProgression = dog.BondXp > 0 || dog.BondLevel > 0;
        float legacyBond01 = Mathf.Clamp01(dog.Bond01);
        if (!hasBondProgression)
        {
            if (legacyBond01 <= 0.001f)
            {
                legacyBond01 = PawPalTrickSaveDefaults.DefaultBond01;
            }

            dog.BondXp = Mathf.Clamp(Mathf.RoundToInt(legacyBond01 * MaxBondXp), 0, MaxBondXp);
        }

        dog.BondXp = Mathf.Clamp(dog.BondXp, 0, MaxBondXp);
        dog.BondLevel = GetBondLevelForXp(dog.BondXp);
        SyncLegacyBond01FromBondProgression(dog);
    }

    public static int GetBondLevel(PawPalDogState dog)
    {
        EnsureDogBondProgression(dog);
        return dog != null ? dog.BondLevel : 1;
    }

    public static float GetBondProgress01(PawPalDogState dog)
    {
        EnsureDogBondProgression(dog);
        return dog != null ? Mathf.Clamp01(dog.BondXp / (float)MaxBondXp) : 0f;
    }

    public static int AddDogBondXp(PawPalDogState dog, int deltaXp)
    {
        EnsureDogBondProgression(dog);
        if (dog == null)
        {
            return 0;
        }

        int previousXp = dog.BondXp;
        dog.BondXp = Mathf.Clamp(dog.BondXp + Mathf.Max(0, deltaXp), 0, MaxBondXp);
        dog.BondLevel = GetBondLevelForXp(dog.BondXp);
        SyncLegacyBond01FromBondProgression(dog);
        return dog.BondXp - previousXp;
    }

    public static float GetBondDelta01FromXp(int xpDelta)
    {
        return MaxBondXp > 0 ? Mathf.Clamp01(Mathf.Max(0, xpDelta) / (float)MaxBondXp) : 0f;
    }

    public static int GetBondXpFromLegacyDelta01(float bondDelta01)
    {
        return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, bondDelta01) * MaxBondXp));
    }

    public static int GetBondLevelForXp(int bondXp)
    {
        return Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Clamp(bondXp, 0, MaxBondXp) / (float)BondXpPerLevel), 1, MaxBondLevel);
    }

    public static PawPalWalkStaminaSnapshot GetCanonicalStaminaSnapshot(PawPalDogState dog)
    {
        EnsureCanonicalStaminaData(dog);
        PawPalDogWalkData walkData = dog != null ? dog.WalkData : null;
        return new PawPalWalkStaminaSnapshot
        {
            Current = walkData != null ? walkData.CurrentWalkStamina : 0f,
            Max = walkData != null ? walkData.MaxWalkStamina : DefaultMaxWalkStamina,
            Xp = walkData != null ? walkData.WalkStaminaXp : 0f,
            XpNeeded = WalkStaminaXpNeededPerLevel
        };
    }

    public static float GetDogStamina01(PawPalDogState dog)
    {
        PawPalWalkStaminaSnapshot snapshot = GetCanonicalStaminaSnapshot(dog);
        return snapshot.Fill01;
    }

    public static float GetDogStaminaCostFromRatio(PawPalDogState dog, float staminaRatio)
    {
        PawPalWalkStaminaSnapshot snapshot = GetCanonicalStaminaSnapshot(dog);
        return Mathf.Max(0f, snapshot.Max * Mathf.Clamp01(staminaRatio));
    }

    public static float SpendDogStamina(PawPalDogState dog, float amount, out float currentStamina, out float maxStamina)
    {
        EnsureCanonicalStaminaData(dog);
        if (dog == null || dog.WalkData == null)
        {
            currentStamina = 0f;
            maxStamina = DefaultMaxWalkStamina;
            return 0f;
        }

        PawPalDogWalkData walkData = dog.WalkData;
        float clampedAmount = Mathf.Max(0f, amount);
        float previousStamina = walkData.CurrentWalkStamina;
        walkData.CurrentWalkStamina = Mathf.Max(0f, walkData.CurrentWalkStamina - clampedAmount);
        walkData.LastStaminaRefreshAtUtcTicks = DateTime.UtcNow.Ticks;
        SyncLegacyEnergyFromStamina(dog);
        currentStamina = walkData.CurrentWalkStamina;
        maxStamina = walkData.MaxWalkStamina;
        return previousStamina - walkData.CurrentWalkStamina;
    }

    private static void SyncLegacyEnergyFromStamina(PawPalDogState dog)
    {
        if (dog == null || dog.WalkData == null || dog.WalkData.MaxWalkStamina <= 0.001f)
        {
            return;
        }

        dog.Energy01 = Mathf.Clamp01(dog.WalkData.CurrentWalkStamina / dog.WalkData.MaxWalkStamina);
    }

    private static void SyncLegacyBond01FromBondProgression(PawPalDogState dog)
    {
        if (dog == null)
        {
            return;
        }

        dog.Bond01 = MaxBondXp > 0 ? Mathf.Clamp01(dog.BondXp / (float)MaxBondXp) : 0f;
    }

    internal void ApplyWalkPersonalityToPlan(PawPalDogState dog, PawPalWalkRoutePlan routePlan)
    {
        if (routePlan == null)
        {
            return;
        }

        if (dog != null)
        {
            PawPalDogPersonalityProfiles.EnsureProfile(dog);
        }

        float baseCost = routePlan.BaseStaminaCost > 0.001f
            ? routePlan.BaseStaminaCost
            : routePlan.StaminaCost;
        routePlan.BaseStaminaCost = Mathf.Max(0f, baseCost);
        routePlan.StaminaCost = Mathf.Max(0f, routePlan.BaseStaminaCost * PawPalDogPersonalityProfiles.GetWalkStaminaCostMultiplier(dog));
        routePlan.PersonalizedCostDogId = dog != null ? dog.Id : string.Empty;
    }

    public bool TryStartWalkSession(PawPalWalkRoutePlan routePlan, out string failureMessage)
    {
        failureMessage = string.Empty;
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            failureMessage = "Choose a dog first.";
            return false;
        }

        CancelUnfinishedActiveWalkSession("starting a new walk");
        if (activeWalkSession != null && !activeWalkSession.Completed)
        {
            failureMessage = "Finish this walk first.";
            return false;
        }

        EnsureWalkData(dog);
        RefreshWalkStamina(dog, DateTime.UtcNow, true);
        ApplyWalkPersonalityToPlan(dog, routePlan);
        failureMessage = PawPalWalkGraphService.ValidatePlan(routePlan, dog.WalkData.CurrentWalkStamina);
        if (!string.IsNullOrEmpty(failureMessage))
        {
            return false;
        }

        float currentStamina;
        float maxStamina;
        SpendDogStamina(dog, routePlan.StaminaCost, out currentStamina, out maxStamina);

        if (string.IsNullOrEmpty(routePlan.ReturnSceneName))
        {
            routePlan.ReturnSceneName = PawPalWalkSceneFlow.HomeSceneName;
        }

        activeWalkSession = BuildWalkSession(dog, routePlan);
        PawPalWalkEventGenerator.PopulateEvents(activeWalkSession, routePlan, this);
        CommitState(true, false, false);
        return true;
    }

    public bool TryStartDeferredWalkSession(string returnSceneName, out string failureMessage)
    {
        return TryStartDeferredWalkSession(null, string.Empty, string.Empty, returnSceneName, out failureMessage);
    }

    public bool TryStartDeferredWalkSession(
        IList<string> requestedVisitNodeIds,
        string startNodeId,
        string endNodeId,
        string returnSceneName,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            failureMessage = "Choose a dog first.";
            return false;
        }

        CancelUnfinishedActiveWalkSession("starting a new walk");
        if (activeWalkSession != null && !activeWalkSession.Completed)
        {
            failureMessage = "Finish this walk first.";
            return false;
        }

        DateTime nowUtc = DateTime.UtcNow;
        activeWalkSession = new PawPalWalkSessionSaveData
        {
            SessionId = dog.Id + "_" + nowUtc.Ticks,
            SelectedDogId = dog.Id,
            SelectedDogName = dog.DisplayName,
            ReturnSceneName = string.IsNullOrEmpty(returnSceneName) ? PawPalWalkSceneFlow.HomeSceneName : returnSceneName,
            StartedAtUtcTicks = nowUtc.Ticks,
            DeferredGraphResolution = true,
            StartNodeId = string.Empty,
            EndNodeId = string.Empty
        };
        if (requestedVisitNodeIds != null)
        {
            for (int i = 0; i < requestedVisitNodeIds.Count; i++)
            {
                string nodeId = requestedVisitNodeIds[i];
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    activeWalkSession.RequestedVisitNodeIds.Add(nodeId.Trim());
                }
            }
        }

        CommitState(true, false, false);
        return true;
    }

    public bool TryResolveActiveWalkSessionRoute(PawPalWalkRoutePlan routePlan, out string failureMessage)
    {
        failureMessage = string.Empty;
        if (activeWalkSession == null || activeWalkSession.Completed)
        {
            failureMessage = "There is no active walk session to resolve.";
            return false;
        }

        PawPalDogState dog = FindDogState(activeWalkSession.SelectedDogId);
        if (dog == null)
        {
            dog = ActiveDog;
        }

        if (dog == null)
        {
            failureMessage = "Choose a dog first.";
            return false;
        }

        EnsureWalkData(dog);
        RefreshWalkStamina(dog, DateTime.UtcNow, true);
        ApplyWalkPersonalityToPlan(dog, routePlan);
        failureMessage = PawPalWalkGraphService.ValidatePlan(routePlan, dog.WalkData.CurrentWalkStamina);
        if (!string.IsNullOrEmpty(failureMessage))
        {
            return false;
        }

        float currentStamina;
        float maxStamina;
        SpendDogStamina(dog, routePlan.StaminaCost, out currentStamina, out maxStamina);

        if (string.IsNullOrEmpty(routePlan.ReturnSceneName))
        {
            routePlan.ReturnSceneName = string.IsNullOrEmpty(activeWalkSession.ReturnSceneName)
                ? PawPalWalkSceneFlow.HomeSceneName
                : activeWalkSession.ReturnSceneName;
        }

        CopyRoutePlanToSession(routePlan, activeWalkSession);
        activeWalkSession.DeferredGraphResolution = false;
        PawPalWalkEventGenerator.PopulateEvents(activeWalkSession, routePlan, this);
        CommitState(true, false, false);
        return true;
    }

    public bool CancelActiveWalkSession()
    {
        return CancelUnfinishedActiveWalkSession("canceling the active walk");
    }

    private bool CancelUnfinishedActiveWalkSession(string reason)
    {
        if (activeWalkSession == null || activeWalkSession.Completed)
        {
            return false;
        }

        string sessionId = activeWalkSession.SessionId;
        if (!activeWalkSession.FinalRewardsApplied)
        {
            RefundWalkSessionStamina(activeWalkSession);
        }

        activeWalkSession = null;
        CommitState(true, false, false);
        Debug.Log("PawPalGameRuntime cleared unfinished walk session '" + sessionId + "' while " + reason + ".");
        return true;
    }

    private bool CancelLoadedUnfinishedWalkSession()
    {
        if (activeWalkSession == null || activeWalkSession.Completed)
        {
            return false;
        }

        string sessionId = activeWalkSession.SessionId;
        if (!activeWalkSession.FinalRewardsApplied)
        {
            RefundWalkSessionStamina(activeWalkSession);
        }

        activeWalkSession = null;
        Debug.Log("PawPalGameRuntime cleared saved unfinished walk session '" + sessionId + "' on profile load.");
        return true;
    }

    private void RefundWalkSessionStamina(PawPalWalkSessionSaveData session)
    {
        if (session == null || session.StaminaCost <= 0.001f)
        {
            return;
        }

        PawPalDogState dog = FindDogState(session.SelectedDogId);
        if (dog == null)
        {
            return;
        }

        EnsureWalkData(dog);
        RefreshWalkStamina(dog, DateTime.UtcNow, false);
        dog.WalkData.CurrentWalkStamina = Mathf.Min(dog.WalkData.MaxWalkStamina, dog.WalkData.CurrentWalkStamina + session.StaminaCost);
        dog.WalkData.LastStaminaRefreshAtUtcTicks = DateTime.UtcNow.Ticks;
        SyncLegacyEnergyFromStamina(dog);
    }

    public void UpdateActiveWalkSessionProgress(float progress01)
    {
        if (activeWalkSession == null || activeWalkSession.Completed)
        {
            return;
        }

        activeWalkSession.LastProgress = Mathf.Clamp01(progress01);
    }

    public void MarkActiveWalkEventResolved(string eventId)
    {
        if (activeWalkSession == null || string.IsNullOrEmpty(eventId))
        {
            return;
        }

        for (int i = 0; i < activeWalkSession.GeneratedEvents.Count; i++)
        {
            PawPalWalkGeneratedEventState walkEvent = activeWalkSession.GeneratedEvents[i];
            if (walkEvent != null && walkEvent.EventId == eventId)
            {
                walkEvent.Resolved = true;
                activeWalkSession.LastProgress = Mathf.Max(activeWalkSession.LastProgress, walkEvent.Progress);
                CommitState(true, false, false);
                return;
            }
        }
    }

    public PawPalWalkCompletionResult CompleteActiveWalkSession()
    {
        PawPalWalkCompletionResult result = BuildEmptyWalkCompletionResult();
        if (activeWalkSession == null)
        {
            return result;
        }

        PawPalWalkSessionSaveData session = activeWalkSession;
        PawPalDogState dog = FindDogState(session.SelectedDogId);
        if (dog == null)
        {
            dog = ActiveDog;
        }

        result.DogName = !string.IsNullOrEmpty(session.SelectedDogName) ? session.SelectedDogName : (dog != null ? dog.DisplayName : "Your dog");
        result.Distance = session.RouteDistance;
        result.StaminaUsed = session.StaminaCost;

        bool inventoryChanged = false;
        if (dog != null)
        {
            EnsureWalkData(dog);
            result.PreviousMaxStamina = dog.WalkData.MaxWalkStamina;
        }

        for (int i = 0; i < session.VisitedLocations.Count; i++)
        {
            PawPalWalkLocationData location = session.VisitedLocations[i];
            if (location != null && !string.IsNullOrEmpty(location.DisplayName) && !result.LocationsVisited.Contains(location.DisplayName))
            {
                result.LocationsVisited.Add(location.DisplayName);
            }
        }

        for (int i = 0; i < session.GeneratedEvents.Count; i++)
        {
            PawPalWalkGeneratedEventState walkEvent = session.GeneratedEvents[i];
            if (walkEvent == null)
            {
                continue;
            }

            if (walkEvent.EventType == PawPalWalkEventType.PresentFound)
            {
                string itemId = string.IsNullOrEmpty(walkEvent.RewardItemId) ? BasicFoodItemId : walkEvent.RewardItemId;
                PawPalCatalogItemDefinition item = GetCatalogItem(itemId);
                if (item == null)
                {
                    itemId = BasicFoodItemId;
                    item = GetCatalogItem(itemId);
                }

                if (!walkEvent.RewardGranted && item != null)
                {
                    AddItemQuantity(itemId, 1);
                    walkEvent.RewardGranted = true;
                    inventoryChanged = true;
                }

                result.ItemsReceived.Add(item != null ? item.DisplayName : "Present");
            }
            else if (walkEvent.EventType == PawPalWalkEventType.DogEncounter)
            {
                result.DogsMet.Add(string.IsNullOrEmpty(walkEvent.DisplayName) ? "a friendly dog" : walkEvent.DisplayName);
            }
        }

        if (dog != null && !session.FinalRewardsApplied)
        {
            ApplyCompletedWalkToDog(dog, session);
            trainerState.WalksCompleted++;
            ApplyActionRewards(PawPalPlayerActionType.WalkDog);
        }

        if (dog != null)
        {
            result.NewMaxStamina = dog.WalkData.MaxWalkStamina;
        }

        session.Completed = true;
        session.FinalRewardsApplied = true;
        session.CompletedAtUtcTicks = DateTime.UtcNow.Ticks;
        activeWalkSession = null;
        CommitState(true, inventoryChanged, true);
        return result;
    }

    public PawPalAgilityPetProgressState GetAgilityProgress(string dogId)
    {
        return EnsureAgilityProgress(dogId);
    }

    public PawPalAgilityLevelProgressState GetAgilityLevelProgress(string dogId, PawPalAgilityLevelId levelId)
    {
        PawPalAgilityPetProgressState progress = EnsureAgilityProgress(dogId);
        return progress != null ? EnsureAgilityLevelProgress(progress, levelId) : null;
    }

    public PawPalAgilityEntryStatus GetAgilityEntryStatus(string dogId, PawPalAgilityLevelId levelId)
    {
        PawPalAgilityEntryStatus status = new PawPalAgilityEntryStatus();
        PawPalDogState dog = FindDogState(dogId);
        if (dog == null)
        {
            status.PracticeMessage = "Choose a pet first.";
            status.ScoredMessage = "Choose a pet first.";
            return status;
        }

        PawPalAgilityTrialConfig config = PawPalAgilityTrialConfig.LoadOrCreateDefault();
        PawPalAgilityLevelDefinition level = config.GetLevel(levelId);
        if (level == null)
        {
            status.PracticeMessage = "Agility Trial is not configured.";
            status.ScoredMessage = "Agility Trial is not configured.";
            return status;
        }

        PawPalAgilityPetProgressState petProgress = EnsureAgilityProgress(dogId);
        status.CanPractice = true;
        status.PracticeMessage = "Practice is free.";

        if (!IsAgilityLevelUnlocked(petProgress, levelId, config))
        {
            status.ScoredMessage = levelId == PawPalAgilityLevelId.Beginner
                ? "Complete basic practice first."
                : "Earn Silver or Gold in the previous level.";
            return status;
        }

        if (trainerState.BasicCurrency < level.EntryFeeBasicCurrency)
        {
            status.ScoredMessage = "Not enough Basic currency.";
            return status;
        }

        status.CanStartScored = true;
        status.ScoredMessage = "Entry fee: " + level.EntryFeeBasicCurrency;
        return status;
    }

    public bool TryStartAgilityTrial(string dogId, PawPalAgilityLevelId levelId, PawPalAgilityTrialMode mode, string returnSceneName, out string failureMessage)
    {
        failureMessage = string.Empty;
        PawPalDogState dog = FindDogState(dogId);
        if (dog == null)
        {
            failureMessage = "Choose a pet first.";
            return false;
        }

        PawPalAgilityTrialConfig config = PawPalAgilityTrialConfig.LoadOrCreateDefault();
        PawPalAgilityLevelDefinition level = config.GetLevel(levelId);
        if (level == null)
        {
            failureMessage = "Agility Trial is not configured.";
            return false;
        }

        PawPalAgilityEntryStatus status = GetAgilityEntryStatus(dog.Id, levelId);
        if (mode == PawPalAgilityTrialMode.Scored)
        {
            if (!status.CanStartScored)
            {
                failureMessage = status.ScoredMessage;
                return false;
            }

            trainerState.BasicCurrency = Mathf.Max(0, trainerState.BasicCurrency - Mathf.Max(0, level.EntryFeeBasicCurrency));
        }
        else if (!status.CanPractice)
        {
            failureMessage = status.PracticeMessage;
            return false;
        }

        activeAgilityTrialSession = new PawPalAgilityTrialSessionSaveData
        {
            SelectedDogId = dog.Id,
            LevelId = levelId,
            Mode = mode,
            ReturnSceneName = string.IsNullOrEmpty(returnSceneName) ? PawPalWalkSceneFlow.HomeSceneName : returnSceneName,
            EntryFeeBasicCurrency = mode == PawPalAgilityTrialMode.Scored ? Mathf.Max(0, level.EntryFeeBasicCurrency) : 0,
            StartedUtcTicks = DateTime.UtcNow.Ticks
        };

        CommitState(true, false, false);
        return true;
    }

    public void CancelActiveAgilityTrial()
    {
        if (activeAgilityTrialSession == null)
        {
            return;
        }

        activeAgilityTrialSession = null;
        CommitState(true, false, false);
    }

    public PawPalAgilityTrialResult CompleteActiveAgilityTrial(PawPalAgilityRunStats runStats)
    {
        PawPalAgilityTrialResult result = new PawPalAgilityTrialResult();
        if (activeAgilityTrialSession == null)
        {
            result.Summary = "No active Agility Trial.";
            return result;
        }

        PawPalAgilityTrialSessionSaveData session = activeAgilityTrialSession;
        PawPalDogState dog = FindDogState(session.SelectedDogId);
        if (dog == null)
        {
            dog = ActiveDog;
        }

        PawPalAgilityTrialConfig config = PawPalAgilityTrialConfig.LoadOrCreateDefault();
        PawPalAgilityLevelDefinition level = config.GetLevel(session.LevelId);
        PawPalAgilityConditionSnapshot condition = PawPalAgilityScoringService.BuildConditionSnapshot(dog);
        result = PawPalAgilityScoringService.CalculateResult(level, runStats, condition, session.Mode);

        if (dog != null)
        {
            ApplyAgilityConditionCost(dog, level, session.Mode);
        }

        if (dog != null && session.Mode == PawPalAgilityTrialMode.Practice && result.PracticeCompleted)
        {
            PawPalAgilityPetProgressState progress = EnsureAgilityProgress(dog.Id);
            progress.BasicPracticeComplete = true;
            EnsureAgilityLevelProgress(progress, PawPalAgilityLevelId.Beginner).Unlocked = true;
        }
        else if (dog != null && session.Mode == PawPalAgilityTrialMode.Scored)
        {
            ApplyAgilityScoredResult(dog, level, config, result);
        }

        activeAgilityTrialSession = null;
        CommitState(true, false, true);
        return result;
    }

    public void TrainActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        PawPalTrickDefinition sit = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        PawPalTrickAttemptResult result = PawPalTrickProgressionService.AttemptTrick(dog, sit, null, true, false, true);
        RecordTrickTrainingResult(result);
    }

    public PawPalDogTrickProgress GetActiveDogTrickProgress(PawPalTrickId trickId)
    {
        return GetDogTrickProgress(ActiveDog, trickId);
    }

    public PawPalDogTrickProgress GetDogTrickProgress(PawPalDogState dog, PawPalTrickId trickId)
    {
        if (dog == null)
        {
            return null;
        }

        EnsureTrickData(dog);
        return PawPalTrickCatalog.GetOrCreateProgress(dog, trickId);
    }

    public bool IsActiveDogTrickLearned(PawPalTrickId trickId)
    {
        return IsDogTrickLearned(ActiveDog, trickId);
    }

    public bool IsDogTrickLearned(PawPalDogState dog, PawPalTrickId trickId)
    {
        PawPalDogTrickProgress progress = GetDogTrickProgress(dog, trickId);
        return progress != null && progress.IsLearned;
    }

    public bool IsDogTrickLearned(string dogId, PawPalTrickId trickId)
    {
        return IsDogTrickLearned(FindDogState(dogId), trickId);
    }

    public bool CanActiveDogUsePhotoPose(PawPalTrickId trickId)
    {
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        return definition != null
            && !string.IsNullOrEmpty(definition.PhotoModePoseUnlock)
            && IsActiveDogTrickLearned(trickId);
    }

    public void RecordTrickTrainingResult(PawPalTrickAttemptResult result)
    {
        if (result == null || ActiveDog == null)
        {
            return;
        }

        EnsureTrickData(ActiveDog);
        RefreshDogStamina(ActiveDog);
        ApplyActionRewards(PawPalPlayerActionType.TrainTrick);
        CommitState(true, false, true);
    }

    public void PraiseActiveDogForTrick(PawPalTrickId trickId)
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        PawPalTrickProgressionService.ApplyPraise(dog, trickId);
        CommitState(true, false, true);
    }

    public void ApplyActiveDogInteractionBond(float bondDelta01, float moodDelta01, float activityDelta01)
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        ApplyDogInteractionBond(dog, bondDelta01, moodDelta01, activityDelta01);
        CommitState(true, false, true);
    }

    public static void ApplyDogInteractionBond(PawPalDogState dog, float bondDelta01, float moodDelta01, float activityDelta01)
    {
        if (dog == null)
        {
            return;
        }

        PawPalTrickCatalog.EnsureDogTrickData(dog);
        AddDogBondXp(dog, GetBondXpFromLegacyDelta01(bondDelta01));
        dog.Mood01 = Mathf.Clamp01(dog.Mood01 + moodDelta01);
        dog.ModifyNeed(PawPalDogNeed.Activity, activityDelta01);
    }

    public void MarkActiveDogTrickCommandLearned(PawPalTrickId trickId, string commandLabel, float confidence)
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        EnsureTrickData(dog);
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetOrCreateProgress(dog, trickId);
        if (progress == null)
        {
            return;
        }

        progress.IsDiscovered = true;
        progress.IsLearned = true;
        progress.MasteryLevel = Mathf.Max(progress.MasteryLevel, 1);
        progress.CommandConfidence = Mathf.Max(progress.CommandConfidence, Mathf.Clamp01(confidence));
        progress.CustomVoiceCommand = string.IsNullOrWhiteSpace(commandLabel)
            ? PawPalTrickCatalog.GetCommandLabel(trickId)
            : commandLabel;
        if (definition != null)
        {
            progress.MasteryXp = Mathf.Max(progress.MasteryXp, definition.LearnedRequiredXp);
        }

        if (progress.LearnedAtUtcTicks <= 0L)
        {
            progress.LearnedAtUtcTicks = DateTime.UtcNow.Ticks;
        }

        ApplyActionRewards(PawPalPlayerActionType.TrainTrick);
        CommitState(true, false, true);
    }

    public bool PurchasePointPackage(string packageId)
    {
        PawPalPointPackageDefinition package = FindPointPackage(packageId);
        if (package == null)
        {
            return false;
        }

        trainerState.PremiumCurrency += package.Points;
        RecordActionProgress(PawPalPlayerActionType.ShopPurchase);
        CommitState(true, false, true);
        return true;
    }

    public bool TryPurchaseItem(string itemId)
    {
        PawPalCatalogItemDefinition item = GetCatalogItem(itemId);
        if (item == null || item.DisabledInShop || !item.AppearsInShop)
        {
            return false;
        }

        if (!item.CanPurchaseMultiple && IsItemOwned(itemId))
        {
            return false;
        }

        if (!CanAfford(itemId))
        {
            return false;
        }

        if (item.CurrencyType == PawPalCurrencyType.Premium)
        {
            trainerState.PremiumCurrency -= item.Price;
        }
        else
        {
            trainerState.BasicCurrency -= item.Price;
        }

        if (item.CanPurchaseMultiple)
        {
            AddItemQuantity(item.Id, 1);
        }
        else
        {
            SetItemOwned(item.Id, true, 1);
        }

        RecordActionProgress(PawPalPlayerActionType.ShopPurchase);
        CommitState(true, true, true);
        return true;
    }

    public bool TryEquipCollar(string dogId, string itemId)
    {
        PawPalCatalogItemDefinition item = GetCatalogItem(itemId);
        if (item == null || item.Category != PawPalItemCategory.Collars || !IsItemOwned(itemId))
        {
            return false;
        }

        PawPalDogEquipmentState equipmentState = EnsureDogEquipmentState(dogId);
        if (equipmentState.EquippedCollarItemId == itemId)
        {
            return false;
        }

        equipmentState.EquippedCollarItemId = itemId;
        CommitState(true, true, false);
        return true;
    }

    public bool TryAutoEquipOwnedCollar(string dogId)
    {
        if (string.IsNullOrWhiteSpace(dogId))
        {
            return false;
        }

        string equippedItemId = GetEquippedCollarItemId(dogId);
        PawPalCatalogItemDefinition equippedItem = GetCatalogItem(equippedItemId);
        if (equippedItem != null
            && equippedItem.Category == PawPalItemCategory.Collars
            && IsItemOwned(equippedItem.Id))
        {
            return true;
        }

        PawPalCatalogItemDefinition preferredItem = null;
        PawPalCatalogItemDefinition fallbackItem = null;
        for (int i = 0; i < catalogItems.Count; i++)
        {
            PawPalCatalogItemDefinition item = catalogItems[i];
            if (item == null
                || item.Category != PawPalItemCategory.Collars
                || !IsItemOwned(item.Id))
            {
                continue;
            }

            if (string.Equals(item.Id, StarterCollarItemId, StringComparison.Ordinal))
            {
                preferredItem = item;
                break;
            }

            if (fallbackItem == null)
            {
                fallbackItem = item;
            }
        }

        PawPalCatalogItemDefinition selectedItem = preferredItem ?? fallbackItem;
        if (selectedItem == null)
        {
            return false;
        }

        return TryEquipCollar(dogId, selectedItem.Id) || string.Equals(GetEquippedCollarItemId(dogId), selectedItem.Id, StringComparison.Ordinal);
    }

    public bool TryUnequipCollar(string dogId, string itemId)
    {
        if (string.IsNullOrEmpty(dogId) || string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        PawPalDogEquipmentState equipmentState = GetDogEquipmentState(dogId);
        if (equipmentState == null || equipmentState.EquippedCollarItemId != itemId)
        {
            return false;
        }

        equipmentState.EquippedCollarItemId = string.Empty;
        CommitState(true, true, false);
        return true;
    }

    public bool TrySpawnToy(string itemId)
    {
        PawPalCatalogItemDefinition item = GetCatalogItem(itemId);
        if (item == null || item.Category != PawPalItemCategory.Toys || !IsItemOwned(itemId))
        {
            return false;
        }

        EnsureBridge();
        if (sceneBridge == null)
        {
            return false;
        }

        bool spawned = sceneBridge.TrySpawnToy(item);
        if (spawned)
        {
            inventoryRevision++;
            NotifyStateChanged();
        }

        return spawned;
    }

    public bool TrySpawnToyForThrow(string itemId)
    {
        PawPalCatalogItemDefinition item = GetCatalogItem(itemId);
        if (item == null || item.Category != PawPalItemCategory.Toys || !IsItemOwned(itemId))
        {
            return false;
        }

        EnsureBridge();
        if (sceneBridge == null)
        {
            return false;
        }

        bool spawned = sceneBridge.TrySpawnToyForThrow(item);
        if (spawned)
        {
            inventoryRevision++;
            NotifyStateChanged();
        }

        return spawned;
    }

    public bool IsToyActiveInScene(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        EnsureBridge();
        return sceneBridge != null && sceneBridge.IsToyActiveInScene(itemId);
    }

    public bool TryRemoveToyFromScene(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        EnsureBridge();
        if (sceneBridge == null || !sceneBridge.TryRemoveToyFromScene(itemId))
        {
            return false;
        }

        inventoryRevision++;
        NotifyStateChanged();
        return true;
    }

    public bool TryEnsureDogForSceneBinding(string dogId)
    {
        if (string.IsNullOrWhiteSpace(dogId))
        {
            return false;
        }

        if (HasDog(dogId))
        {
            return true;
        }

        PawPalDogState dogState = BuildKnownDogState(dogId);
        if (dogState == null)
        {
            return false;
        }

        dogs.Add(dogState);
        EnsureDogEquipmentState(dogState.Id);
        BackfillIdenticalDogNeedsIfNeeded();
        activeDogIndex = Mathf.Clamp(activeDogIndex, 0, Mathf.Max(0, dogs.Count - 1));
        CommitState(false, false, true);
        return true;
    }

    public void SaveProfile()
    {
        try
        {
            RefreshAllWalkStamina(DateTime.UtcNow);
            string savePath = GetSavePath();
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PawPalSaveData saveData = BuildSaveData();
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(savePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalGameRuntime could not save profile: " + exception.Message);
        }
    }

    public bool TrySpendBasicCurrency(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (trainerState.BasicCurrency < amount)
        {
            return false;
        }

        trainerState.BasicCurrency -= amount;
        CommitState(true, false, true);
        return true;
    }

    public void GrantBasicCurrency(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        trainerState.BasicCurrency += amount;
        trainerState.TotalBasicCurrencyEarned += amount;
        CommitState(true, false, true);
    }

    public void RecordObedienceTrialResult(PawPalObedienceTrialMedal medal)
    {
        trainerState.CompetitionsParticipated++;
        switch (medal)
        {
            case PawPalObedienceTrialMedal.Gold:
                trainerState.CompetitionsWonFirst++;
                break;
            case PawPalObedienceTrialMedal.Silver:
                trainerState.CompetitionsWonSecond++;
                break;
            case PawPalObedienceTrialMedal.Bronze:
                trainerState.CompetitionsWonThird++;
                break;
        }
    }

    public void CommitObedienceTrialProgress()
    {
        CommitState(true, false, true);
    }

    public void LoadProfile()
    {
        string savePath = GetSavePath();
        if (!File.Exists(savePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            PawPalSaveData saveData = JsonUtility.FromJson<PawPalSaveData>(json);
            if (saveData == null || saveData.TrainerState == null)
            {
                return;
            }

            RestoreFromSaveData(saveData);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalGameRuntime could not load profile: " + exception.Message);
        }
    }

    public string GetBasicCurrencyText()
    {
        return "\u20B1" + trainerState.BasicCurrency.ToString("N0");
    }

    public string GetPremiumCurrencyText()
    {
        return trainerState.PremiumCurrency.ToString();
    }

    public float GetTrainerLevelProgress01()
    {
        return GetTrainerProgressionSnapshot().LevelProgress01;
    }

    public string GetTrainerExperienceText()
    {
        TrainerProgressionSnapshot snapshot = GetTrainerProgressionSnapshot();
        return snapshot.CurrentLevelExperience + " / " + snapshot.RequiredExperience + " XP";
    }

    public TrainerProgressionSnapshot GetTrainerProgressionSnapshot()
    {
        TrainerProgressionSnapshot snapshot = new TrainerProgressionSnapshot();
        int maxLevel = GetMaxTrainerLevel();
        if (maxLevel <= 0)
        {
            snapshot.Level = 1;
            snapshot.RequiredExperience = 1;
            return snapshot;
        }

        int level = Mathf.Clamp(trainerState.Level, 1, maxLevel);
        TrainerMilestoneDefinition milestone = GetTrainerMilestoneDefinition(level);
        int requiredExperience = milestone != null ? Mathf.Max(1, milestone.RequiredExperience) : 1;

        snapshot.Level = level;
        snapshot.RequiredExperience = requiredExperience;
        snapshot.CurrentMilestoneLevel = level;

        if (level >= maxLevel)
        {
            snapshot.CurrentLevelExperience = requiredExperience;
            snapshot.LevelProgress01 = 1f;
            snapshot.AbsoluteProgress01 = 1f;
            return snapshot;
        }

        snapshot.CurrentLevelExperience = Mathf.Clamp(trainerState.CurrentLevelExperience, 0, requiredExperience);
        snapshot.LevelProgress01 = requiredExperience <= 0
            ? 0f
            : Mathf.Clamp01((float)snapshot.CurrentLevelExperience / requiredExperience);
        snapshot.AbsoluteProgress01 = Mathf.Clamp01(((level - 1f) + snapshot.LevelProgress01) / Mathf.Max(1f, maxLevel - 1f));
        return snapshot;
    }

    public IReadOnlyList<TrainerMilestoneDefinition> GetTrainerMilestones()
    {
        return trainerProgressionConfig != null
            ? trainerProgressionConfig.Milestones
            : TrainerProgressionDatabase.Load().Milestones;
    }

    public string GetDailyResetCountdownText()
    {
        EnsureDailyTasksCurrent();
        TimeSpan remaining = nextDailyResetUtc - DateTime.UtcNow;
        if (remaining.TotalSeconds < 0d)
        {
            remaining = TimeSpan.Zero;
        }

        return remaining.Hours.ToString("00") + "h " + remaining.Minutes.ToString("00") + "m";
    }

    private void InitializeDefaults()
    {
        LoadTrainerProgressionData();
        trainerState = new PawPalTrainerState();
        dogs.Clear();
        catalogItems.Clear();
        ownedItems.Clear();
        dogEquipment.Clear();
        pointPackages.Clear();
        dailyTasks.Clear();
        agilityProgress.Clear();
        actionProgress.Clear();
        catalogById.Clear();
        ownedItemById.Clear();
        dogEquipmentByDogId.Clear();
        temporaryIntroDogIds.Clear();
        activeWalkSession = null;
        activeAgilityTrialSession = null;

        dogs.Add(BuildStarterDog());
        EnsureAllAgilityProgress();
        BuildCatalog();
        ValidateCatalogDefinitions();
        BuildPointPackages();
        InitializeOwnedItemsFromCatalog();
        EnsureDogEquipmentState(StarterDogId).EquippedCollarItemId = StarterCollarItemId;
        nextDailyResetUtc = DateTime.UtcNow.Date.AddDays(1d);
        GenerateDailyTasks();
        InitializeTrainerMilestoneStateForSeededProfile();
        inventoryRevision = 1;
        shopRevision = 1;
        activeDogIndex = 0;
        lastPersistentActiveDogIndex = 0;
    }

    private void LoadTrainerProgressionData()
    {
        trainerProgressionConfig = TrainerProgressionDatabase.Load();
        trainerActionRewardsByType.Clear();
        trainerMilestonesByLevel.Clear();

        IList<TrainerActionRewardDefinition> actionRewards = trainerProgressionConfig != null
            ? trainerProgressionConfig.ActionRewards
            : null;
        if (actionRewards != null)
        {
            for (int i = 0; i < actionRewards.Count; i++)
            {
                TrainerActionRewardDefinition reward = actionRewards[i];
                if (reward == null || string.IsNullOrEmpty(reward.ActionType))
                {
                    continue;
                }

                PawPalPlayerActionType actionType;
                if (!Enum.TryParse(reward.ActionType, true, out actionType))
                {
                    continue;
                }

                trainerActionRewardsByType[actionType] = reward;
            }
        }

        IList<TrainerMilestoneDefinition> milestones = trainerProgressionConfig != null
            ? trainerProgressionConfig.Milestones
            : null;
        if (milestones != null)
        {
            for (int i = 0; i < milestones.Count; i++)
            {
                TrainerMilestoneDefinition milestone = milestones[i];
                if (milestone == null || milestone.Level <= 0)
                {
                    continue;
                }

                trainerMilestonesByLevel[milestone.Level] = milestone;
            }
        }
    }

    private void InitializeTrainerMilestoneStateForSeededProfile()
    {
        pendingUnsupportedTrainerMilestoneLevels.Clear();
        lastProcessedTrainerMilestoneLevel = Mathf.Clamp(trainerState.Level, 1, GetMaxTrainerLevel());

        for (int level = 2; level <= lastProcessedTrainerMilestoneLevel; level++)
        {
            TrainerMilestoneDefinition milestone = GetTrainerMilestoneDefinition(level);
            if (ShouldTrackPendingUnsupportedMilestone(milestone) && !CanApplyMilestoneReward(milestone))
            {
                pendingUnsupportedTrainerMilestoneLevels.Add(level);
            }
        }
    }

    private PawPalDogState BuildStarterDog()
    {
        return new PawPalDogState
        {
            Id = StarterDogId,
            DisplayName = "Pepper",
            Gender = PawPalDogGender.Male,
            Personality = PawPalDogPersonality.Loyal,
            FurColor = "Beige",
            Breed = "Labrador",
            ProfileVersion = PawPalDogPersonalityProfiles.CurrentProfileVersion,
            Food01 = 0.82f,
            Water01 = 0.76f,
            Hygiene01 = 0.7f,
            Activity01 = 0.67f,
            Energy01 = 0.74f,
            TrickProfileVersion = PawPalTrickSaveDefaults.CurrentTrickProfileVersion,
            Bond01 = 0.42f,
            BondXp = Mathf.RoundToInt(0.42f * MaxBondXp),
            BondLevel = GetBondLevelForXp(Mathf.RoundToInt(0.42f * MaxBondXp)),
            Mood01 = PawPalTrickSaveDefaults.DefaultMood01,
            TrainingFatigue01 = 0f,
            LastTrainingFatigueUpdateUtcTicks = DateTime.UtcNow.Ticks,
            Endurance = 2,
            Mobility = 4,
            Speed = 3,
            Focus = 5
        };
    }

    private PawPalDogState BuildMisoDog()
    {
        return new PawPalDogState
        {
            Id = "miso",
            DisplayName = "Miso",
            Gender = PawPalDogGender.Female,
            Personality = PawPalDogPersonality.Relaxed,
            FurColor = "Brown",
            Breed = "Corgi",
            ProfileVersion = PawPalDogPersonalityProfiles.CurrentProfileVersion,
            Food01 = 0.9f,
            Water01 = 0.85f,
            Hygiene01 = 0.8f,
            Activity01 = 0.72f,
            Energy01 = 0.79f,
            TrickProfileVersion = PawPalTrickSaveDefaults.CurrentTrickProfileVersion,
            Bond01 = PawPalTrickSaveDefaults.DefaultBond01,
            BondXp = Mathf.RoundToInt(PawPalTrickSaveDefaults.DefaultBond01 * MaxBondXp),
            BondLevel = GetBondLevelForXp(Mathf.RoundToInt(PawPalTrickSaveDefaults.DefaultBond01 * MaxBondXp)),
            Mood01 = 0.76f,
            TrainingFatigue01 = 0f,
            LastTrainingFatigueUpdateUtcTicks = DateTime.UtcNow.Ticks,
            Endurance = 4,
            Mobility = 3,
            Speed = 4,
            Focus = 3
        };
    }

    private PawPalDogState BuildSukiDog()
    {
        return new PawPalDogState
        {
            Id = "suki",
            DisplayName = "Suki",
            Gender = PawPalDogGender.Female,
            Personality = PawPalDogPersonality.Energetic,
            FurColor = "White",
            Breed = "Husky",
            ProfileVersion = PawPalDogPersonalityProfiles.CurrentProfileVersion,
            Food01 = 1f,
            Water01 = 1f,
            Hygiene01 = 1f,
            Activity01 = 0.9f,
            Energy01 = 1f,
            TrickProfileVersion = PawPalTrickSaveDefaults.CurrentTrickProfileVersion,
            Bond01 = 0.38f,
            BondXp = Mathf.RoundToInt(0.38f * MaxBondXp),
            BondLevel = GetBondLevelForXp(Mathf.RoundToInt(0.38f * MaxBondXp)),
            Mood01 = 0.82f,
            TrainingFatigue01 = 0f,
            LastTrainingFatigueUpdateUtcTicks = DateTime.UtcNow.Ticks,
            Endurance = 3,
            Mobility = 5,
            Speed = 4,
            Focus = 4
        };
    }

    private PawPalDogState BuildKnownDogState(string dogId)
    {
        if (string.Equals(dogId, StarterDogId, StringComparison.OrdinalIgnoreCase))
        {
            return BuildStarterDog();
        }

        if (string.Equals(dogId, "miso", StringComparison.OrdinalIgnoreCase))
        {
            return BuildMisoDog();
        }

        if (string.Equals(dogId, "suki", StringComparison.OrdinalIgnoreCase))
        {
            return BuildSukiDog();
        }

        return null;
    }

    private void BuildCatalog()
    {
        AddCatalogItem(BuildDogBreedDefinition("dog_beagle", "Beagle", "beagle", 650));
        AddCatalogItem(BuildDogBreedDefinition("dog_border_collie", "Border Collie", "border_collie", 850));
        AddCatalogItem(BuildDogBreedDefinition("dog_boxer", "Boxer", "boxer", 700));
        AddCatalogItem(BuildDogBreedDefinition("dog_bullterrier", "Bull Terrier", "bullterrier", 650));
        AddCatalogItem(BuildDogBreedDefinition("dog_corgi", "Corgi", "corgi", 700));
        AddCatalogItem(BuildDogBreedDefinition("dog_dalmatian", "Dalmatian", "dalmatian", 750));
        AddCatalogItem(BuildDogBreedDefinition("dog_doberman", "Doberman", "doberman", 800));
        AddCatalogItem(BuildDogBreedDefinition("dog_frenchbulldog", "French Bulldog", "frenchbulldog", 700));
        AddCatalogItem(BuildDogBreedDefinition("dog_goldenretriever", "Golden Retriever", "goldenretriever", 850));
        AddCatalogItem(BuildDogBreedDefinition("dog_husky", "Husky", "husky", 800));
        AddCatalogItem(BuildDogBreedDefinition("dog_jackrussellterrier", "Jack Russell Terrier", "jackrussellterrier", 600));
        AddCatalogItem(BuildDogBreedDefinition("dog_labrador", "Labrador", "labrador", 800));
        AddCatalogItem(BuildDogBreedDefinition("dog_pitbull", "Pitbull", "pitbull", 750));
        AddCatalogItem(BuildDogBreedDefinition("dog_pug", "Pug", "pug", 650));
        AddCatalogItem(BuildDogBreedDefinition("dog_rottweiler", "Rottweiler", "rottweiler", 800));
        AddCatalogItem(BuildDogBreedDefinition("dog_shepherd", "Shepherd", "shepherd", 800));
        AddCatalogItem(BuildDogBreedDefinition("dog_shibainu", "Shiba Inu", "shibainu", 700));
        AddCatalogItem(BuildDogBreedDefinition("dog_spitz", "Spitz", "spitz", 650));
        AddCatalogItem(BuildDogBreedDefinition("dog_toyterrier", "Toy Terrier", "toyterrier", 600));

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = BasicFoodItemId,
            DisplayName = "Basic food",
            Category = PawPalItemCategory.Food,
            CurrencyType = PawPalCurrencyType.Basic,
            Price = 50,
            CanPurchaseMultiple = true,
            StarterOwned = true,
            StarterQuantity = 5,
            Description = "A simple daily meal. Feeding from Home consumes one unit automatically.",
            ShopSpritePath = "UI/Figma/Shop/basic_food",
            PreviewSpritePath = "UI/Figma/Shop/basic_food",
            InventorySpritePath = "UI/Figma/Shop/basic_food",
            InventoryCardTheme = InventoryCardTheme.Bone
        });

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = PremiumFoodItemId,
            DisplayName = "Premium food",
            Category = PawPalItemCategory.Food,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 10,
            CanPurchaseMultiple = true,
            Description = "A richer meal that restores a little more food and energy when basic food is gone.",
            ShopSpritePath = "UI/Figma/Shop/premium_food",
            PreviewSpritePath = "UI/Figma/Shop/premium_food",
            InventorySpritePath = "UI/Figma/Shop/premium_food",
            InventoryCardTheme = InventoryCardTheme.Roll
        });

        AddCatalogItem(BuildDownloadedToyDefinition("toy_bone_1", "Golden Bone", "bone_1", "PawPal/RoomPrefabs/Bone_1", InventoryCardTheme.Bone, PawPalToyInteractionMode.CarryInMouth, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_bone_2", "Rose Bone", "bone_2", string.Empty, InventoryCardTheme.Bone, PawPalToyInteractionMode.CarryInMouth, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_ball_1", "Lime Ball", "ball_1", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_ball_2", "Rally Ball", "ball_2", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_ball_3", "Sunny Ball", "ball_3", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_big_ball_1", "Stripe Ball", "big_ball_1", "PawPal/RoomPrefabs/Big_ball_1", InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_big_ball_2", "Sunset Ball", "big_ball_2", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_big_ball_3", "Forest Ball", "big_ball_3", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("toy_big_ball_4", "Blush Ball", "big_ball_4", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cat_ball_4", "Ball 4", "cat_ball_4", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cat_ball_3", "Ball 3", "cat_ball_3", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cat_ball_2", "Ball 2", "cat_ball_2", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cat_ball_1", "Ball 1", "cat_ball_1", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cattoy", "Cat Toy", "cattoy", string.Empty, InventoryCardTheme.Bone, PawPalToyInteractionMode.CarryInMouth, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("mouse_c1", "Mouse C1", "mouse_c1", string.Empty, InventoryCardTheme.Bone, PawPalToyInteractionMode.CarryInMouth, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("mouse_c2", "Mouse C2", "mouse_c2", string.Empty, InventoryCardTheme.Bone, PawPalToyInteractionMode.CarryInMouth, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cat_ball_7", "Ball 7", "cat_ball_7", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cat_ball_6", "Ball 6", "cat_ball_6", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));
        AddCatalogItem(BuildDownloadedToyDefinition("cat_ball_5", "Ball 5", "cat_ball_5", string.Empty, InventoryCardTheme.Ball, PawPalToyInteractionMode.PawHitRoll, Color.white));

        AddCatalogItem(BuildCollarDefinition("collar_simple_c1", "Ruby Band", DownloadedShopSpriteRoot + "collarsimple_c1", "PawPal/Accessories/CollarSimple_C1", InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collar_simple_c2", "Ocean Band", 50, DownloadedShopSpriteRoot + "collarsimple_c2", "PawPal/Accessories/CollarSimple_C2", InventoryCardTheme.Band, true, Color.white));
        AddCatalogItem(BuildCollarDefinition("collar_simple_c3", "Forest Band", DownloadedShopSpriteRoot + "collarsimple_c3", "PawPal/Accessories/CollarSimple_C3", InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collar_standard", "Shadow Loop", DownloadedShopSpriteRoot + "collar", "PawPal/Accessories/CollarSimple_C1", InventoryCardTheme.Loop, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collar_c1", "Collar C1", DownloadedShopSpriteRoot + "collar_c1", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collar_c2", "Collar C2", DownloadedShopSpriteRoot + "collar_c2", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collar_c3", "Collar C3", DownloadedShopSpriteRoot + "collar_c3", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collarbow_c1", "Collar Bow C1", DownloadedShopSpriteRoot + "collarbow_c1", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collarbow_c2", "Collar Bow C2", DownloadedShopSpriteRoot + "collarbow_c2", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("collarbow_c3", "Collar Bow C3", DownloadedShopSpriteRoot + "collarbow_c3", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("ears_1", "Ears 1", DownloadedShopSpriteRoot + "ears_1", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("ears_2", "Ears 2", DownloadedShopSpriteRoot + "ears_2", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("ears_3", "Ears 3", DownloadedShopSpriteRoot + "ears_3", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("ears_4", "Ears 4", DownloadedShopSpriteRoot + "ears_4", string.Empty, InventoryCardTheme.Band, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("straycollar_1", "Stray Collar 1", DownloadedShopSpriteRoot + "straycollar_1", string.Empty, InventoryCardTheme.Loop, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("straycollar_2", "Stray Collar 2", DownloadedShopSpriteRoot + "straycollar_2", string.Empty, InventoryCardTheme.Loop, false, Color.white));
        AddCatalogItem(BuildCollarDefinition("straycollar_3", "Stray Collar 3", DownloadedShopSpriteRoot + "straycollar_3", string.Empty, InventoryCardTheme.Loop, false, Color.white));

        AddCatalogItem(BuildDownloadedFurnitureDefinition("cardboardbox", "Cardboard Box", "cardboardbox"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("cattray", "Cat Tray", "cattray"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("carrier_1", "Carrier 1", "carrier_1"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("carrier_2", "Carrier 2", "carrier_2"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("cathouse_1", "Cat House 1", "cathouse_1"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("cathouse_2", "Cat House 2", "cathouse_2"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("scratchingpost_1", "Scratching Post 1", "scratchingpost_1"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("scratchingpost_2", "Scratching Post 2", "scratchingpost_2"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("catsofa", "Cat Sofa", "catsofa"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("doblebowl", "Doble Bowl", "doblebowl"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("bowl_2", "Bowl 2", "bowl_2"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("bowl_1", "Bowl 1", "bowl_1"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("bowlauto", "Bowl Auto", "bowlauto"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("bed_1_color_1", "Slate Bed", "bed_1_color_1"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("bed_1_color_2", "Denim Bed", "bed_1_color_2"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("bed_2_color_1", "Pawprint Bed", "bed_2_color_1"));
        AddCatalogItem(BuildDownloadedFurnitureDefinition("bed_2_color_2", "Sage Bed", "bed_2_color_2"));
    }

    private PawPalCatalogItemDefinition BuildDogBreedDefinition(string id, string displayName, string spriteName, int price)
    {
        string spritePath = DownloadedShopSpriteRoot + spriteName;
        return new PawPalCatalogItemDefinition
        {
            Id = id,
            DisplayName = displayName,
            Category = PawPalItemCategory.Dogs,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = Mathf.Max(1, price),
            AppearsInInventory = false,
            Description = "Unlock the " + displayName + " breed for your pawfriends collection.",
            ShopSpritePath = spritePath,
            PreviewSpritePath = spritePath,
            PreviewMode = PawPalShopPreviewMode.BreedTriptychDog
        };
    }

    private PawPalCatalogItemDefinition BuildDownloadedToyDefinition(
        string id,
        string displayName,
        string spriteName,
        string roomPrefabResourcePath,
        InventoryCardTheme inventoryCardTheme,
        PawPalToyInteractionMode interactionMode,
        Color toyTint)
    {
        string spritePath = DownloadedShopSpriteRoot + spriteName;
        bool hasRoomPrefab = !string.IsNullOrEmpty(roomPrefabResourcePath);
        return new PawPalCatalogItemDefinition
        {
            Id = id,
            DisplayName = displayName,
            Category = PawPalItemCategory.Toys,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 150,
            AppearsInInventory = true,
            Description = hasRoomPrefab
                ? "A room toy that can be spawned from the Home inventory after purchase."
                : "A toy from the game asset set. Room spawning is deferred until its prefab is added to Resources.",
            ShopSpritePath = spritePath,
            PreviewSpritePath = spritePath,
            PreviewPrefabResourcePath = roomPrefabResourcePath,
            PreviewMode = hasRoomPrefab ? PawPalShopPreviewMode.StandaloneModel : PawPalShopPreviewMode.SpriteOnly,
            InventorySpritePath = spritePath,
            InventoryCardTheme = inventoryCardTheme,
            RoomPrefabResourcePath = roomPrefabResourcePath,
            ToyInteractionMode = interactionMode,
            ToyTint = toyTint
        };
    }

    private PawPalCatalogItemDefinition BuildDownloadedFurnitureDefinition(string id, string displayName, string spriteName)
    {
        string spritePath = DownloadedShopSpriteRoot + spriteName;
        return new PawPalCatalogItemDefinition
        {
            Id = id,
            DisplayName = displayName,
            Category = PawPalItemCategory.Furniture,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 350,
            AppearsInInventory = false,
            Description = "A furniture item from the game asset set for the shop Furniture category.",
            ShopSpritePath = spritePath,
            PreviewSpritePath = spritePath,
            PreviewMode = PawPalShopPreviewMode.SpriteOnly
        };
    }

    private PawPalCatalogItemDefinition BuildCollarDefinition(string id, string displayName, string shopSpritePath, string collarPrefabResourcePath, InventoryCardTheme inventoryCardTheme, bool starterOwned, Color collarTint)
    {
        return BuildCollarDefinition(id, displayName, 75, shopSpritePath, collarPrefabResourcePath, inventoryCardTheme, starterOwned, collarTint);
    }

    private PawPalCatalogItemDefinition BuildCollarDefinition(string id, string displayName, int price, string shopSpritePath, string collarPrefabResourcePath, InventoryCardTheme inventoryCardTheme, bool starterOwned, Color collarTint)
    {
        return new PawPalCatalogItemDefinition
        {
            Id = id,
            DisplayName = displayName,
            Category = PawPalItemCategory.Collars,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = price,
            StarterOwned = starterOwned,
            StarterQuantity = starterOwned ? 1 : 0,
            Description = "A visible collar that can be equipped from the Home inventory for the active dog.",
            ShopSpritePath = shopSpritePath,
            GeneratedShopSpritePath = "UI/Generated/Shop/" + id,
            PreviewSpritePath = shopSpritePath,
            PreviewPrefabResourcePath = collarPrefabResourcePath,
            PreviewMode = PawPalShopPreviewMode.WearableOnDog,
            InventorySpritePath = shopSpritePath,
            InventoryCardTheme = inventoryCardTheme,
            CollarPrefabResourcePath = collarPrefabResourcePath,
            CollarTint = collarTint,
            CollarLocalPosition = DefaultCollarPosition,
            CollarLocalRotation = DefaultCollarRotation,
            CollarLocalScale = DefaultCollarScale
        };
    }

    private void AddCatalogItem(PawPalCatalogItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.Id))
        {
            Debug.LogWarning("PawPalGameRuntime skipped a catalog item because it had no valid id.");
            return;
        }

        PawPalShopPreviewCatalog.ApplyPreviewDefaults(item);

        if (catalogById.ContainsKey(item.Id))
        {
            Debug.LogWarning("PawPalGameRuntime found a duplicate catalog id '" + item.Id + "'. The later definition will overwrite the earlier lookup.");
        }

        catalogItems.Add(item);
        catalogById[item.Id] = item;
    }

    private void BuildPointPackages()
    {
        pointPackages.Add(new PawPalPointPackageDefinition { Id = "points_200", Points = 200, PriceLabel = "1.99EUR" });
        pointPackages.Add(new PawPalPointPackageDefinition { Id = "points_500", Points = 500, PriceLabel = "4.49EUR", SavingsLabel = "10% saved" });
        pointPackages.Add(new PawPalPointPackageDefinition { Id = "points_1000", Points = 1000, PriceLabel = "8.99EUR", SavingsLabel = "10% saved" });
        pointPackages.Add(new PawPalPointPackageDefinition { Id = "points_2500", Points = 2500, PriceLabel = "19.99EUR", SavingsLabel = "20% saved" });
        pointPackages.Add(new PawPalPointPackageDefinition { Id = "points_3500", Points = 3500, PriceLabel = "25.99EUR", SavingsLabel = "25% saved" });
        pointPackages.Add(new PawPalPointPackageDefinition { Id = "points_5000", Points = 5000, PriceLabel = "34.99EUR", SavingsLabel = "30% saved" });
    }

    private void InitializeOwnedItemsFromCatalog()
    {
        for (int i = 0; i < catalogItems.Count; i++)
        {
            PawPalCatalogItemDefinition item = catalogItems[i];
            PawPalOwnedItemState ownedItem = new PawPalOwnedItemState
            {
                ItemId = item.Id,
                Quantity = item.StarterQuantity,
                Owned = item.StarterOwned || item.StarterQuantity > 0
            };

            ownedItems.Add(ownedItem);
            ownedItemById[item.Id] = ownedItem;
        }
    }

    private void ValidateCatalogDefinitions()
    {
        if (GetCatalogItem(StarterCollarItemId) == null)
        {
            Debug.LogWarning("PawPalGameRuntime could not find the starter collar item '" + StarterCollarItemId + "'.");
        }

        if (GetCatalogItem(BasicFoodItemId) == null)
        {
            Debug.LogWarning("PawPalGameRuntime could not find the basic food item '" + BasicFoodItemId + "'.");
        }

        for (int i = 0; i < catalogItems.Count; i++)
        {
            PawPalCatalogItemDefinition item = catalogItems[i];
            if (item == null)
            {
                continue;
            }

            if (item.AppearsInShop && !item.DisabledInShop && string.IsNullOrEmpty(item.ShopSpritePath))
            {
                Debug.LogWarning("PawPalGameRuntime item '" + item.Id + "' appears in the shop but has no shop sprite path.");
            }

            if (item.AppearsInInventory && item.Category != PawPalItemCategory.Dogs && string.IsNullOrEmpty(item.InventorySpritePath))
            {
                Debug.LogWarning("PawPalGameRuntime item '" + item.Id + "' appears in inventory but has no inventory sprite path.");
            }

            ValidateCatalogResourcePath(item.Id, "collar prefab", item.CollarPrefabResourcePath);
            ValidateCatalogResourcePath(item.Id, "room prefab", item.RoomPrefabResourcePath);
            ValidateCatalogResourcePath(item.Id, "shop preview prefab", item.PreviewPrefabResourcePath);
        }
    }

    private void ValidateCatalogResourcePath(string itemId, string label, string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return;
        }

        if (Resources.Load<GameObject>(resourcePath) == null)
        {
            Debug.LogWarning("PawPalGameRuntime item '" + itemId + "' is missing its " + label + " at Resources path '" + resourcePath + "'.");
        }
    }

    private void TickDogNeeds(float deltaTime)
    {
        if (dogs.Count == 0)
        {
            return;
        }

        float hourScale = Mathf.Max(1f, secondsPerGameHour);
        float deltaHours = deltaTime / hourScale;
        float passiveNeedDrainMultiplier = GetPassiveNeedDrainMultiplier();
        bool changed = false;

        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            PawPalDogPersonalityProfiles.EnsureProfile(dog);
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Food, FoodDrainPerHour * deltaHours * passiveNeedDrainMultiplier);
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Water, WaterDrainPerHour * deltaHours * passiveNeedDrainMultiplier);
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Hygiene, HygieneDrainPerHour * deltaHours * passiveNeedDrainMultiplier);
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Activity, ActivityDrainPerHour * deltaHours * passiveNeedDrainMultiplier);
            changed |= RefreshWalkStamina(dog, DateTime.UtcNow, false);

            changed |= TickNewDogWhinyHours(dog, deltaHours);
        }

        if (changed)
        {
            NotifyStateChanged();
        }
    }

    private bool ApplyNeedDrain(PawPalDogState dog, PawPalDogNeed need, float drain)
    {
        drain *= PawPalDogPersonalityProfiles.GetNeedDrainMultiplier(dog, need);
        float current = dog.GetNeed(need);
        float next = Mathf.Clamp01(current - drain);
        if (Mathf.Approximately(current, next))
        {
            return false;
        }

        dog.SetNeed(need, next);
        return true;
    }

    private static float GetPassiveNeedDrainMultiplier()
    {
        PawPalPetCircadianState circadian = PawPalTimeOfDayEvaluator.EvaluatePetCircadian(
            (float)DateTime.Now.TimeOfDay.TotalHours,
            PetMorningStartHour,
            PetDaySteadyStartHour,
            PetEveningStartHour,
            PetNightStartHour,
            PetWakeTransitionHours,
            PetEveningTransitionHours);

        if (circadian.ForceSleep)
        {
            return OvernightNeedDrainMultiplier;
        }

        return Mathf.Lerp(1f, SleepyNeedDrainMultiplier, circadian.Sleepiness01);
    }

    private static bool TickNewDogWhinyHours(PawPalDogState dog, float deltaHours)
    {
        if (dog == null || dog.NewDogWhinyHoursRemaining <= 0f || deltaHours <= 0f)
        {
            return false;
        }

        float nextHours = Mathf.Max(0f, dog.NewDogWhinyHoursRemaining - deltaHours);
        if (Mathf.Approximately(nextHours, dog.NewDogWhinyHoursRemaining))
        {
            return false;
        }

        dog.NewDogWhinyHoursRemaining = nextHours;
        return true;
    }

    private void ApplyFoodToActiveDog(PawPalCatalogItemDefinition foodItem)
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        ApplyFoodToDog(dog, foodItem);
    }

    private void ApplyFoodToDog(PawPalDogState dog, PawPalCatalogItemDefinition foodItem)
    {
        if (dog == null)
        {
            return;
        }

        if (foodItem != null && foodItem.Id == PremiumFoodItemId)
        {
            dog.ModifyNeed(PawPalDogNeed.Food, 0.45f);
        }
        else
        {
            dog.ModifyNeed(PawPalDogNeed.Food, 0.35f);
        }
    }

    private PawPalCatalogItemDefinition GetAvailableFoodItem()
    {
        if (GetItemQuantity(BasicFoodItemId) > 0)
        {
            return GetCatalogItem(BasicFoodItemId);
        }

        if (GetItemQuantity(PremiumFoodItemId) > 0)
        {
            return GetCatalogItem(PremiumFoodItemId);
        }

        return null;
    }

    private void CompleteFoodNeedInteraction(string dogId, string foodItemId)
    {
        PawPalDogState dog = FindDogState(dogId);
        if (dog == null || string.IsNullOrEmpty(foodItemId) || GetItemQuantity(foodItemId) <= 0)
        {
            return;
        }

        PawPalCatalogItemDefinition foodItem = GetCatalogItem(foodItemId);
        if (foodItem == null)
        {
            return;
        }

        ConsumeItemQuantity(foodItem.Id, 1);
        ApplyFoodToDog(dog, foodItem);
        ApplyActionRewards(PawPalPlayerActionType.FeedDog);
        CommitState(true, true, true);
    }

    private void CompleteWaterNeedInteraction(string dogId)
    {
        PawPalDogState dog = FindDogState(dogId);
        if (dog == null)
        {
            return;
        }

        ApplyWaterToDog(dog);
        ApplyActionRewards(PawPalPlayerActionType.GiveWater);
        CommitState(true, false, true);
    }

    private void ApplyWaterToDog(PawPalDogState dog)
    {
        if (dog == null)
        {
            return;
        }

        dog.ModifyNeed(PawPalDogNeed.Water, 0.4f);
    }

    private void EnsureWalkData(PawPalDogState dog)
    {
        EnsureCanonicalStaminaData(dog);
    }

    private bool EnsureTrickData(PawPalDogState dog)
    {
        PawPalVoiceProfileStore unusedLegacyStore = null;
        return EnsureTrickData(dog, ref unusedLegacyStore);
    }

    private bool EnsureTrickData(PawPalDogState dog, ref PawPalVoiceProfileStore legacyVoiceStore)
    {
        if (dog == null)
        {
            return false;
        }

        int previousVersion = dog.TrickProfileVersion;
        bool changed = PawPalTrickCatalog.EnsureDogTrickData(dog);
        if (previousVersion < PawPalTrickSaveDefaults.CurrentTrickProfileVersion)
        {
            changed |= TryMigrateLegacySitVoiceProfile(dog, ref legacyVoiceStore);
        }

        return changed;
    }

    private bool TryMigrateLegacySitVoiceProfile(PawPalDogState dog, ref PawPalVoiceProfileStore legacyVoiceStore)
    {
        if (dog == null || string.IsNullOrEmpty(dog.Id))
        {
            return false;
        }

        if (legacyVoiceStore == null)
        {
            legacyVoiceStore = new PawPalVoiceProfileStore();
        }

        if (!legacyVoiceStore.IsTrickLearned(dog.Id, PawPalVoiceTrick.Sit, 3))
        {
            return false;
        }

        PawPalTrickDefinition sit = PawPalTrickCatalog.GetDefinition(PawPalTrickId.Sit);
        PawPalDogTrickProgress progress = PawPalTrickCatalog.GetOrCreateProgress(dog, PawPalTrickId.Sit);
        if (progress == null || progress.IsLearned)
        {
            return false;
        }

        progress.IsDiscovered = true;
        progress.IsLearned = true;
        progress.MasteryLevel = Mathf.Max(progress.MasteryLevel, 1);
        progress.MasteryXp = Mathf.Max(progress.MasteryXp, sit != null ? sit.LearnedRequiredXp : 45f);
        progress.CustomVoiceCommand = PawPalTrickCatalog.GetCommandLabel(PawPalTrickId.Sit);
        progress.CommandConfidence = Mathf.Max(progress.CommandConfidence, 0.7f);
        progress.LearnedAtUtcTicks = DateTime.UtcNow.Ticks;
        return true;
    }

    private bool RefreshWalkStamina(PawPalDogState dog, DateTime nowUtc, bool persistRefreshTime)
    {
        EnsureWalkData(dog);
        if (dog == null || dog.WalkData == null)
        {
            return false;
        }

        PawPalDogWalkData walkData = dog.WalkData;
        DateTime lastRefreshUtc = walkData.LastStaminaRefreshAtUtcTicks > 0L
            ? new DateTime(walkData.LastStaminaRefreshAtUtcTicks, DateTimeKind.Utc)
            : nowUtc;
        if (nowUtc < lastRefreshUtc)
        {
            lastRefreshUtc = nowUtc;
        }

        double elapsedHours = (nowUtc - lastRefreshUtc).TotalHours;
        if (elapsedHours <= 0.001d)
        {
            return false;
        }

        float previous = walkData.CurrentWalkStamina;
        if (walkData.CurrentWalkStamina < walkData.MaxWalkStamina)
        {
            walkData.CurrentWalkStamina = Mathf.Min(walkData.MaxWalkStamina, walkData.CurrentWalkStamina + (float)elapsedHours * WalkStaminaRefillPerHour);
        }

        if (persistRefreshTime || !Mathf.Approximately(previous, walkData.CurrentWalkStamina))
        {
            walkData.LastStaminaRefreshAtUtcTicks = nowUtc.Ticks;
        }

        SyncLegacyEnergyFromStamina(dog);
        return !Mathf.Approximately(previous, walkData.CurrentWalkStamina);
    }

    private void RefreshAllWalkStamina(DateTime nowUtc)
    {
        for (int i = 0; i < dogs.Count; i++)
        {
            RefreshWalkStamina(dogs[i], nowUtc, false);
        }
    }

    private PawPalWalkSessionSaveData BuildWalkSession(PawPalDogState dog, PawPalWalkRoutePlan routePlan)
    {
        DateTime nowUtc = DateTime.UtcNow;
        PawPalWalkSessionSaveData session = new PawPalWalkSessionSaveData
        {
            SessionId = (dog != null ? dog.Id : "dog") + "_" + nowUtc.Ticks,
            SelectedDogId = dog != null ? dog.Id : string.Empty,
            SelectedDogName = dog != null ? dog.DisplayName : "Your dog",
            ReturnSceneName = routePlan.ReturnSceneName,
            RouteDistance = routePlan.RouteDistance,
            StaminaCost = routePlan.StaminaCost,
            StartedAtUtcTicks = nowUtc.Ticks,
            LastProgress = 0f
        };

        CopyRoutePlanToSession(routePlan, session);

        return session;
    }

    private static void CopyRoutePlanToSession(PawPalWalkRoutePlan routePlan, PawPalWalkSessionSaveData session)
    {
        if (routePlan == null || session == null)
        {
            return;
        }

        session.GraphId = routePlan.GraphId;
        session.StartNodeId = routePlan.StartNodeId;
        session.EndNodeId = routePlan.EndNodeId;
        if (!string.IsNullOrEmpty(routePlan.ReturnSceneName))
        {
            session.ReturnSceneName = routePlan.ReturnSceneName;
        }
        session.RouteDistance = routePlan.RouteDistance;
        session.StaminaCost = routePlan.StaminaCost;

        session.RoutePoints.Clear();
        if (routePlan.RoutePoints != null)
        {
            for (int i = 0; i < routePlan.RoutePoints.Count; i++)
            {
                session.RoutePoints.Add(CloneWalkPoint(routePlan.RoutePoints[i]));
            }
        }

        session.WorldPath.Clear();
        if (routePlan.WorldPath != null)
        {
            for (int i = 0; i < routePlan.WorldPath.Count; i++)
            {
                session.WorldPath.Add(CloneWalkWorldPoint(routePlan.WorldPath[i]));
            }
        }

        session.RouteNodeIds.Clear();
        if (routePlan.NodeIds != null)
        {
            for (int i = 0; i < routePlan.NodeIds.Count; i++)
            {
                session.RouteNodeIds.Add(routePlan.NodeIds[i]);
            }
        }

        session.RouteEdgeIds.Clear();
        if (routePlan.EdgeIds != null)
        {
            for (int i = 0; i < routePlan.EdgeIds.Count; i++)
            {
                session.RouteEdgeIds.Add(routePlan.EdgeIds[i]);
            }
        }

        session.RequestedVisitNodeIds.Clear();
        if (routePlan.RequestedVisitNodeIds != null)
        {
            for (int i = 0; i < routePlan.RequestedVisitNodeIds.Count; i++)
            {
                session.RequestedVisitNodeIds.Add(routePlan.RequestedVisitNodeIds[i]);
            }
        }

        session.EncounterPoints.Clear();
        if (routePlan.EncounterPoints != null)
        {
            for (int i = 0; i < routePlan.EncounterPoints.Count; i++)
            {
                session.EncounterPoints.Add(CloneRouteEncounter(routePlan.EncounterPoints[i]));
            }
        }

        session.VisitedLocations.Clear();
        if (routePlan.PlannedStops != null)
        {
            for (int i = 0; i < routePlan.PlannedStops.Count; i++)
            {
                session.VisitedLocations.Add(CloneWalkLocation(routePlan.PlannedStops[i]));
            }
        }
    }

    private void ApplyCompletedWalkToDog(PawPalDogState dog, PawPalWalkSessionSaveData session)
    {
        if (dog == null || session == null)
        {
            return;
        }

        EnsureWalkData(dog);
        RefreshWalkStamina(dog, DateTime.UtcNow, false);

        float distance01 = Mathf.Clamp01(Mathf.InverseLerp(8f, 36f, Mathf.Max(0f, session.RouteDistance)));
        ApplyWalkNeedChanges(
            dog,
            Mathf.Lerp(0.32f, 0.42f, distance01),
            Mathf.Lerp(0.10f, 0.16f, distance01),
            Mathf.Lerp(0.08f, 0.12f, distance01),
            Mathf.Lerp(0.05f, 0.09f, distance01));
        float walkCurrentStamina;
        float walkMaxStamina;
        SpendDogStamina(dog, GetDogStaminaCostFromRatio(dog, 0.04f), out walkCurrentStamina, out walkMaxStamina);

        PawPalDogWalkData walkData = dog.WalkData;
        walkData.TotalWalksCompleted++;
        walkData.TotalDistanceWalked += Mathf.Max(0f, session.RouteDistance);
        walkData.LastWalkCompletedAtUtcTicks = DateTime.UtcNow.Ticks;

        walkData.WalkStaminaXp += WalkStaminaXpPerWalk + Mathf.Max(0f, session.RouteDistance) * WalkStaminaXpPerDistance;
        while (walkData.WalkStaminaXp >= WalkStaminaXpNeededPerLevel && walkData.MaxWalkStamina < MaxWalkStaminaCap)
        {
            walkData.WalkStaminaXp -= WalkStaminaXpNeededPerLevel;
            walkData.MaxWalkStamina = Mathf.Min(MaxWalkStaminaCap, walkData.MaxWalkStamina + WalkStaminaIncreasePerLevel);
        }
        walkData.CurrentWalkStamina = Mathf.Clamp(walkData.CurrentWalkStamina, 0f, walkData.MaxWalkStamina);
        SyncLegacyEnergyFromStamina(dog);

        if (walkData.TotalWalksCompleted % 3 == 0 && dog.Endurance < 10)
        {
            dog.ModifyStat(PawPalDogStatType.Endurance, 1);
        }
    }

    private static void ApplyWalkNeedChanges(
        PawPalDogState dog,
        float activityGain01,
        float waterLoss01,
        float foodLoss01,
        float hygieneLoss01)
    {
        if (dog == null)
        {
            return;
        }

        dog.ModifyNeed(PawPalDogNeed.Activity, Mathf.Max(0f, activityGain01));
        dog.ModifyNeed(PawPalDogNeed.Water, -Mathf.Max(0f, waterLoss01));
        dog.ModifyNeed(PawPalDogNeed.Food, -Mathf.Max(0f, foodLoss01));
        dog.ModifyNeed(PawPalDogNeed.Hygiene, -Mathf.Max(0f, hygieneLoss01));
    }

    private PawPalWalkCompletionResult BuildEmptyWalkCompletionResult()
    {
        return new PawPalWalkCompletionResult
        {
            DogName = ActiveDog != null ? ActiveDog.DisplayName : "Your dog",
            PreviousMaxStamina = ActiveDog != null && ActiveDog.WalkData != null ? ActiveDog.WalkData.MaxWalkStamina : DefaultMaxWalkStamina,
            NewMaxStamina = ActiveDog != null && ActiveDog.WalkData != null ? ActiveDog.WalkData.MaxWalkStamina : DefaultMaxWalkStamina
        };
    }

    private PawPalAgilityPetProgressState EnsureAgilityProgress(string dogId)
    {
        if (string.IsNullOrWhiteSpace(dogId))
        {
            return null;
        }

        for (int i = 0; i < agilityProgress.Count; i++)
        {
            PawPalAgilityPetProgressState progress = agilityProgress[i];
            if (progress != null && string.Equals(progress.DogId, dogId, StringComparison.OrdinalIgnoreCase))
            {
                EnsureAllAgilityLevelProgress(progress);
                return progress;
            }
        }

        PawPalAgilityPetProgressState created = new PawPalAgilityPetProgressState { DogId = dogId };
        EnsureAllAgilityLevelProgress(created);
        agilityProgress.Add(created);
        return created;
    }

    private void EnsureAllAgilityProgress()
    {
        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            if (dog != null && !IsTemporaryIntroDogId(dog.Id))
            {
                EnsureAgilityProgress(dog.Id);
            }
        }
    }

    private static void EnsureAllAgilityLevelProgress(PawPalAgilityPetProgressState progress)
    {
        if (progress == null)
        {
            return;
        }

        EnsureAgilityLevelProgress(progress, PawPalAgilityLevelId.Beginner);
        EnsureAgilityLevelProgress(progress, PawPalAgilityLevelId.Amateur);
        EnsureAgilityLevelProgress(progress, PawPalAgilityLevelId.Pro);
        EnsureAgilityLevelProgress(progress, PawPalAgilityLevelId.Master);
        EnsureAgilityLevelProgress(progress, PawPalAgilityLevelId.Champion);
    }

    private static PawPalAgilityLevelProgressState EnsureAgilityLevelProgress(PawPalAgilityPetProgressState progress, PawPalAgilityLevelId levelId)
    {
        if (progress == null)
        {
            return null;
        }

        if (progress.Levels == null)
        {
            progress.Levels = new List<PawPalAgilityLevelProgressState>();
        }

        for (int i = 0; i < progress.Levels.Count; i++)
        {
            PawPalAgilityLevelProgressState level = progress.Levels[i];
            if (level != null && level.LevelId == levelId)
            {
                return level;
            }
        }

        PawPalAgilityLevelProgressState created = new PawPalAgilityLevelProgressState { LevelId = levelId };
        progress.Levels.Add(created);
        return created;
    }

    private static bool IsAgilityLevelUnlocked(PawPalAgilityPetProgressState progress, PawPalAgilityLevelId levelId, PawPalAgilityTrialConfig config)
    {
        if (progress == null)
        {
            return false;
        }

        if (levelId == PawPalAgilityLevelId.Beginner)
        {
            return progress.BasicPracticeComplete || EnsureAgilityLevelProgress(progress, levelId).Unlocked;
        }

        PawPalAgilityLevelProgressState levelProgress = EnsureAgilityLevelProgress(progress, levelId);
        if (levelProgress.Unlocked)
        {
            return true;
        }

        PawPalAgilityLevelId previousLevel = GetPreviousAgilityLevel(levelId);
        PawPalAgilityLevelProgressState previousProgress = EnsureAgilityLevelProgress(progress, previousLevel);
        PawPalAgilityLevelDefinition level = config != null ? config.GetLevel(levelId) : null;
        PawPalAgilityMedal requiredMedal = level != null ? level.RequiredPreviousMedal : PawPalAgilityMedal.Silver;
        return GetAgilityMedalRank(previousProgress.BestMedal) >= GetAgilityMedalRank(requiredMedal);
    }

    private static PawPalAgilityLevelId GetPreviousAgilityLevel(PawPalAgilityLevelId levelId)
    {
        switch (levelId)
        {
            case PawPalAgilityLevelId.Amateur:
                return PawPalAgilityLevelId.Beginner;
            case PawPalAgilityLevelId.Pro:
                return PawPalAgilityLevelId.Amateur;
            case PawPalAgilityLevelId.Master:
                return PawPalAgilityLevelId.Pro;
            case PawPalAgilityLevelId.Champion:
                return PawPalAgilityLevelId.Master;
            default:
                return PawPalAgilityLevelId.Beginner;
        }
    }

    private static int GetAgilityMedalRank(PawPalAgilityMedal medal)
    {
        switch (medal)
        {
            case PawPalAgilityMedal.Gold:
                return 3;
            case PawPalAgilityMedal.Silver:
                return 2;
            case PawPalAgilityMedal.Bronze:
                return 1;
            default:
                return 0;
        }
    }

    private void ApplyAgilityScoredResult(
        PawPalDogState dog,
        PawPalAgilityLevelDefinition level,
        PawPalAgilityTrialConfig config,
        PawPalAgilityTrialResult result)
    {
        if (dog == null || level == null || result == null)
        {
            return;
        }

        PawPalAgilityPetProgressState progress = EnsureAgilityProgress(dog.Id);
        PawPalAgilityLevelProgressState levelProgress = EnsureAgilityLevelProgress(progress, level.LevelId);
        trainerState.CompetitionsParticipated++;

        if (result.Medal == PawPalAgilityMedal.None)
        {
            return;
        }

        levelProgress.TimesCompleted++;
        if (result.Score > levelProgress.BestScore)
        {
            levelProgress.BestScore = result.Score;
        }

        if (levelProgress.BestTimeSeconds <= 0.001f || result.ElapsedSeconds < levelProgress.BestTimeSeconds)
        {
            levelProgress.BestTimeSeconds = result.ElapsedSeconds;
        }

        if (GetAgilityMedalRank(result.Medal) > GetAgilityMedalRank(levelProgress.BestMedal))
        {
            levelProgress.BestMedal = result.Medal;
        }

        int previousReward = PawPalAgilityScoringService.GetRewardForMedal(level, levelProgress.RewardedMedal);
        int newReward = PawPalAgilityScoringService.GetRewardForMedal(level, result.Medal);
        int rewardDelta = Mathf.Max(0, newReward - previousReward);
        if (rewardDelta > 0)
        {
            trainerState.BasicCurrency += rewardDelta;
            trainerState.TotalBasicCurrencyEarned += rewardDelta;
            result.BasicCurrencyReward = rewardDelta;
            levelProgress.RewardedMedal = result.Medal;
        }

        if (result.Medal == PawPalAgilityMedal.Gold)
        {
            trainerState.CompetitionsWonFirst++;
        }
        else if (result.Medal == PawPalAgilityMedal.Silver)
        {
            trainerState.CompetitionsWonSecond++;
        }
        else if (result.Medal == PawPalAgilityMedal.Bronze)
        {
            trainerState.CompetitionsWonThird++;
        }

        if (GetAgilityMedalRank(result.Medal) >= GetAgilityMedalRank(PawPalAgilityMedal.Silver)
            && config != null
            && config.HasNextLevel(level.LevelId))
        {
            PawPalAgilityLevelId nextLevel = config.GetNextLevel(level.LevelId);
            PawPalAgilityLevelProgressState nextProgress = EnsureAgilityLevelProgress(progress, nextLevel);
            if (!nextProgress.Unlocked)
            {
                nextProgress.Unlocked = true;
                result.UnlockedNextLevel = true;
            }
        }
    }

    private static void ApplyAgilityConditionCost(PawPalDogState dog, PawPalAgilityLevelDefinition level, PawPalAgilityTrialMode mode)
    {
        if (dog == null)
        {
            return;
        }

        float staminaRatio = level != null ? level.StaminaCostRatio : 0.08f;
        if (mode == PawPalAgilityTrialMode.Practice)
        {
            staminaRatio *= 0.5f;
        }

        float currentStamina;
        float maxStamina;
        SpendDogStamina(dog, GetDogStaminaCostFromRatio(dog, staminaRatio), out currentStamina, out maxStamina);
        dog.ModifyNeed(PawPalDogNeed.Activity, mode == PawPalAgilityTrialMode.Practice ? 0.08f : 0.14f);
        dog.ModifyNeed(PawPalDogNeed.Water, mode == PawPalAgilityTrialMode.Practice ? -0.03f : -0.06f);
        dog.ModifyNeed(PawPalDogNeed.Food, mode == PawPalAgilityTrialMode.Practice ? -0.02f : -0.04f);
        dog.ModifyNeed(PawPalDogNeed.Hygiene, mode == PawPalAgilityTrialMode.Practice ? -0.03f : -0.05f);
        dog.Mood01 = Mathf.Clamp01(dog.Mood01 + (mode == PawPalAgilityTrialMode.Practice ? 0.03f : 0.06f));
        AddDogBondXp(dog, mode == PawPalAgilityTrialMode.Practice ? 2 : 5);
        if (mode == PawPalAgilityTrialMode.Scored && dog.Speed < 10)
        {
            dog.ModifyStat(PawPalDogStatType.Speed, 1);
        }
    }

    private PawPalDogState FindDogState(string dogId)
    {
        if (string.IsNullOrEmpty(dogId))
        {
            return null;
        }

        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            if (dog != null && string.Equals(dog.Id, dogId, StringComparison.OrdinalIgnoreCase))
            {
                return dog;
            }
        }

        return null;
    }

    private DogNeedInteractionDirector ResolveNeedInteractionDirector()
    {
        if (DogNeedInteractionDirector.Instance != null)
        {
            return DogNeedInteractionDirector.Instance;
        }

        DogNeedInteractionDirector director = FindFirstObjectByType<DogNeedInteractionDirector>();
        if (director != null)
        {
            return director;
        }

        GameObject directorObject = new GameObject("DogNeedInteractionDirector");
        return directorObject.AddComponent<DogNeedInteractionDirector>();
    }

    private PawPalPetGroomingDirector ResolvePetGroomingDirector()
    {
        if (PawPalPetGroomingDirector.Instance != null)
        {
            return PawPalPetGroomingDirector.Instance;
        }

        PawPalPetGroomingDirector director = FindFirstObjectByType<PawPalPetGroomingDirector>();
        if (director != null)
        {
            return director;
        }

        GameObject directorObject = new GameObject("PawPalPetGroomingDirector");
        return directorObject.AddComponent<PawPalPetGroomingDirector>();
    }

    private void CompleteHygieneNeedInteraction(string dogId)
    {
        PawPalDogState dog = FindDogState(dogId);
        if (dog == null)
        {
            return;
        }

        dog.SetNeed(PawPalDogNeed.Hygiene, 1f);
        ApplyActionRewards(PawPalPlayerActionType.CleanDog);
        CommitState(true, false, true);
    }

    private void ApplyActionRewards(PawPalPlayerActionType actionType)
    {
        TrainerActionRewardDefinition reward = GetActionRewardDefinition(actionType);
        if (reward != null)
        {
            AddTrainerExperience(reward.Experience);
            if (reward.BasicCurrency > 0)
            {
                trainerState.BasicCurrency += reward.BasicCurrency;
                trainerState.TotalBasicCurrencyEarned += reward.BasicCurrency;
            }

            if (reward.ClubExperience > 0)
            {
                trainerState.ClubExperience += reward.ClubExperience;
            }
        }

        RecordActionProgress(actionType);
    }

    private TrainerActionRewardDefinition GetActionRewardDefinition(PawPalPlayerActionType actionType)
    {
        TrainerActionRewardDefinition reward;
        trainerActionRewardsByType.TryGetValue(actionType, out reward);
        return reward;
    }

    private void AddTrainerExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int maxLevel = GetMaxTrainerLevel();
        if (maxLevel <= 0)
        {
            return;
        }

        if (trainerState.Level >= maxLevel)
        {
            trainerState.Level = maxLevel;
            trainerState.CurrentLevelExperience = 0;
            return;
        }

        trainerState.CurrentLevelExperience += amount;

        while (trainerState.Level < maxLevel)
        {
            TrainerMilestoneDefinition currentLevelMilestone = GetTrainerMilestoneDefinition(trainerState.Level);
            int requirement = currentLevelMilestone != null
                ? Mathf.Max(1, currentLevelMilestone.RequiredExperience)
                : 1;
            if (trainerState.CurrentLevelExperience < requirement)
            {
                break;
            }

            trainerState.CurrentLevelExperience -= requirement;
            trainerState.Level++;
            ProcessTrainerMilestone(trainerState.Level, true);
        }

        if (trainerState.Level >= maxLevel)
        {
            trainerState.Level = maxLevel;
            trainerState.CurrentLevelExperience = 0;
        }
    }

    private void UnlockDog(string dogId)
    {
        if (HasDog(dogId))
        {
            return;
        }

        PawPalDogState dogState = BuildKnownDogState(dogId);
        if (dogState != null)
        {
            InitializeNewDogWhinyState(dogState);
            dogs.Add(dogState);
            EnsureDogEquipmentState(dogState.Id);
        }
    }

    private static void InitializeNewDogWhinyState(PawPalDogState dog)
    {
        if (dog == null)
        {
            return;
        }

        PawPalDogPersonalityProfiles.EnsureProfile(dog);
        dog.NewDogWhinyHoursRemaining = PawPalDogPersonalityProfiles.GetNewDogWhinyInitialHours(dog.Personality);
    }

    private bool HasDog(string dogId)
    {
        for (int i = 0; i < dogs.Count; i++)
        {
            if (dogs[i].Id == dogId)
            {
                return true;
            }
        }

        return false;
    }

    private void ProcessTrainerMilestone(int level, bool grantReward)
    {
        TrainerMilestoneDefinition milestone = GetTrainerMilestoneDefinition(level);
        if (milestone == null)
        {
            lastProcessedTrainerMilestoneLevel = Mathf.Max(lastProcessedTrainerMilestoneLevel, level);
            return;
        }

        bool rewardApplied = milestone.RewardKind == TrainerMilestoneRewardKind.None;
        if (grantReward && !rewardApplied)
        {
            rewardApplied = TryApplyMilestoneReward(milestone);
        }

        if (ShouldTrackPendingUnsupportedMilestone(milestone) && !rewardApplied)
        {
            AddPendingUnsupportedTrainerMilestone(level);
        }
        else
        {
            RemovePendingUnsupportedTrainerMilestone(level);
        }

        lastProcessedTrainerMilestoneLevel = Mathf.Max(lastProcessedTrainerMilestoneLevel, level);
    }

    private bool TryApplyMilestoneReward(TrainerMilestoneDefinition milestone)
    {
        if (milestone == null)
        {
            return false;
        }

        switch (milestone.RewardKind)
        {
            case TrainerMilestoneRewardKind.None:
                return true;
            case TrainerMilestoneRewardKind.CurrencyBasic:
                trainerState.BasicCurrency += milestone.CurrencyAmount;
                trainerState.TotalBasicCurrencyEarned += milestone.CurrencyAmount;
                return true;
            case TrainerMilestoneRewardKind.CurrencyPremium:
                trainerState.PremiumCurrency += milestone.CurrencyAmount;
                return true;
            case TrainerMilestoneRewardKind.PremiumFood:
                if (GetCatalogItem(PremiumFoodItemId) == null)
                {
                    return false;
                }

                AddItemQuantity(PremiumFoodItemId, Mathf.Max(1, milestone.Quantity));
                return true;
            case TrainerMilestoneRewardKind.DogUnlock:
                if (string.IsNullOrEmpty(milestone.DogId))
                {
                    return false;
                }

                UnlockDog(milestone.DogId);
                return HasDog(milestone.DogId);
            case TrainerMilestoneRewardKind.CatalogItemReward:
            {
                string rewardItemId = FindMilestoneRewardItemId(milestone);
                if (string.IsNullOrEmpty(rewardItemId))
                {
                    return false;
                }

                AddItemQuantity(rewardItemId, Mathf.Max(1, milestone.Quantity));
                return true;
            }
            default:
                return false;
        }
    }

    private bool CanApplyMilestoneReward(TrainerMilestoneDefinition milestone)
    {
        if (milestone == null)
        {
            return false;
        }

        switch (milestone.RewardKind)
        {
            case TrainerMilestoneRewardKind.None:
            case TrainerMilestoneRewardKind.CurrencyBasic:
            case TrainerMilestoneRewardKind.CurrencyPremium:
                return true;
            case TrainerMilestoneRewardKind.PremiumFood:
                return GetCatalogItem(PremiumFoodItemId) != null;
            case TrainerMilestoneRewardKind.DogUnlock:
                return !string.IsNullOrEmpty(milestone.DogId);
            case TrainerMilestoneRewardKind.CatalogItemReward:
                return !string.IsNullOrEmpty(FindMilestoneRewardItemId(milestone));
            default:
                return false;
        }
    }

    private string FindMilestoneRewardItemId(TrainerMilestoneDefinition milestone)
    {
        PawPalCatalogItemDefinition preferredCandidate = null;
        PawPalCatalogItemDefinition fallbackCandidate = null;

        for (int i = 0; i < catalogItems.Count; i++)
        {
            PawPalCatalogItemDefinition item = catalogItems[i];
            if (item == null || item.Category != milestone.CatalogCategory)
            {
                continue;
            }

            if (!ItemMatchesMilestoneQuality(item, milestone))
            {
                continue;
            }

            if (item.Category != PawPalItemCategory.Dogs && !item.AppearsInInventory)
            {
                continue;
            }

            if (!item.CanPurchaseMultiple && IsItemOwned(item.Id))
            {
                fallbackCandidate = fallbackCandidate ?? item;
                continue;
            }

            if (!IsItemOwned(item.Id))
            {
                return item.Id;
            }

            preferredCandidate = preferredCandidate ?? item;
        }

        PawPalCatalogItemDefinition selected = preferredCandidate ?? fallbackCandidate;
        if (selected == null)
        {
            return string.Empty;
        }

        if (!selected.CanPurchaseMultiple && IsItemOwned(selected.Id))
        {
            return string.Empty;
        }

        return selected.Id;
    }

    private bool ItemMatchesMilestoneQuality(PawPalCatalogItemDefinition item, TrainerMilestoneDefinition milestone)
    {
        if (milestone == null || milestone.ItemQuality == TrainerMilestoneItemQuality.None)
        {
            return true;
        }

        int threshold = GetMilestoneQualityThreshold(milestone.CatalogCategory);
        if (threshold <= 0)
        {
            return false;
        }

        if (milestone.ItemQuality == TrainerMilestoneItemQuality.Basic)
        {
            return item.Price <= threshold;
        }

        return item.Price > threshold;
    }

    private int GetMilestoneQualityThreshold(PawPalItemCategory category)
    {
        switch (category)
        {
            case PawPalItemCategory.Collars:
                return 50;
            case PawPalItemCategory.Toys:
                return 150;
            case PawPalItemCategory.Clothing:
                return 100;
            case PawPalItemCategory.Furniture:
                return 350;
            default:
                return 0;
        }
    }

    private bool ShouldTrackPendingUnsupportedMilestone(TrainerMilestoneDefinition milestone)
    {
        if (milestone == null || milestone.RewardKind == TrainerMilestoneRewardKind.None)
        {
            return false;
        }

        switch (milestone.RewardKind)
        {
            case TrainerMilestoneRewardKind.Feature:
            case TrainerMilestoneRewardKind.Unsupported:
                return true;
            case TrainerMilestoneRewardKind.DogUnlock:
                return string.IsNullOrEmpty(milestone.DogId);
            case TrainerMilestoneRewardKind.PremiumFood:
            case TrainerMilestoneRewardKind.CatalogItemReward:
                return !CanApplyMilestoneReward(milestone);
            default:
                return false;
        }
    }

    private void AddPendingUnsupportedTrainerMilestone(int level)
    {
        if (!pendingUnsupportedTrainerMilestoneLevels.Contains(level))
        {
            pendingUnsupportedTrainerMilestoneLevels.Add(level);
        }
    }

    private void RemovePendingUnsupportedTrainerMilestone(int level)
    {
        pendingUnsupportedTrainerMilestoneLevels.Remove(level);
    }

    private TrainerMilestoneDefinition GetTrainerMilestoneDefinition(int level)
    {
        TrainerMilestoneDefinition milestone;
        trainerMilestonesByLevel.TryGetValue(level, out milestone);
        return milestone;
    }

    private int GetMaxTrainerLevel()
    {
        return trainerMilestonesByLevel.Count;
    }

    private void RecordActionProgress(PawPalPlayerActionType actionType)
    {
        int current;
        actionProgress.TryGetValue(actionType, out current);
        actionProgress[actionType] = current + 1;

        for (int i = 0; i < dailyTasks.Count; i++)
        {
            PawPalDailyTaskState task = dailyTasks[i];
            if (task.ActionType != actionType)
            {
                continue;
            }

            task.Progress = Mathf.Min(task.RequiredCount, task.Progress + 1);
            if (!task.RewardGranted && task.Progress >= task.RequiredCount)
            {
                task.RewardGranted = true;
                trainerState.DailyActivitiesCompleted++;
                AddTrainerExperience(task.ExperienceReward);
                trainerState.BasicCurrency += task.BasicCurrencyReward;
                trainerState.TotalBasicCurrencyEarned += task.BasicCurrencyReward;
            }
        }
    }

    private void EnsureDailyTasksCurrent()
    {
        if (DateTime.UtcNow < nextDailyResetUtc)
        {
            EnsureDailyTasksPresent(false);
            return;
        }

        GenerateDailyTasks();
        CommitState(true, false, true);
    }

    private void GenerateDailyTasks()
    {
        nextDailyResetUtc = DateTime.UtcNow.Date.AddDays(1d);
        dailyTasks.Clear();
        actionProgress.Clear();
        PopulateDailyTasks();

        TrainerActionRewardDefinition dailyLoginReward = GetActionRewardDefinition(PawPalPlayerActionType.DailyLogin);
        if (dailyLoginReward != null)
        {
            AddTrainerExperience(dailyLoginReward.Experience);
            if (dailyLoginReward.BasicCurrency > 0)
            {
                trainerState.BasicCurrency += dailyLoginReward.BasicCurrency;
                trainerState.TotalBasicCurrencyEarned += dailyLoginReward.BasicCurrency;
            }

            if (dailyLoginReward.ClubExperience > 0)
            {
                trainerState.ClubExperience += dailyLoginReward.ClubExperience;
            }
        }
    }

    private void EnsureDailyTasksPresent(bool persistState)
    {
        if (dailyTasks.Count > 0)
        {
            return;
        }

        PopulateDailyTasks();
        if (persistState)
        {
            CommitState(true, false, true);
        }
    }

    private void PopulateDailyTasks()
    {
        dailyTasks.Clear();
        actionProgress.Clear();

        dailyTasks.Add(new PawPalDailyTaskState
        {
            Id = "feed_dog",
            Title = "Feed your dog",
            ActionType = PawPalPlayerActionType.FeedDog,
            RequiredCount = 3,
            ExperienceReward = 30,
            BasicCurrencyReward = 50
        });

        dailyTasks.Add(new PawPalDailyTaskState
        {
            Id = "walk_dog",
            Title = "Go for a walk",
            ActionType = PawPalPlayerActionType.WalkDog,
            RequiredCount = 2,
            ExperienceReward = 40,
            BasicCurrencyReward = 60
        });

        dailyTasks.Add(new PawPalDailyTaskState
        {
            Id = "clean_dog",
            Title = "Clean your dog",
            ActionType = PawPalPlayerActionType.CleanDog,
            RequiredCount = 3,
            ExperienceReward = 30,
            BasicCurrencyReward = 50
        });
    }

    private PawPalDogEquipmentState EnsureDogEquipmentState(string dogId)
    {
        PawPalDogEquipmentState state = GetDogEquipmentState(dogId);
        if (state != null)
        {
            return state;
        }

        state = new PawPalDogEquipmentState { DogId = dogId };
        dogEquipment.Add(state);
        dogEquipmentByDogId[dogId] = state;
        return state;
    }

    private void AddItemQuantity(string itemId, int amount)
    {
        PawPalOwnedItemState state = EnsureOwnedItemState(itemId);
        state.Quantity = Mathf.Max(0, state.Quantity + amount);
        state.Owned = state.Quantity > 0;
    }

    private void ConsumeItemQuantity(string itemId, int amount)
    {
        PawPalOwnedItemState state = EnsureOwnedItemState(itemId);
        state.Quantity = Mathf.Max(0, state.Quantity - Mathf.Max(0, amount));
        state.Owned = state.Quantity > 0;
    }

    private void SetItemOwned(string itemId, bool owned, int quantity)
    {
        PawPalOwnedItemState state = EnsureOwnedItemState(itemId);
        state.Owned = owned;
        state.Quantity = owned ? Mathf.Max(1, quantity) : 0;
    }

    private PawPalOwnedItemState EnsureOwnedItemState(string itemId)
    {
        PawPalOwnedItemState state = GetOwnedItemState(itemId);
        if (state != null)
        {
            return state;
        }

        state = new PawPalOwnedItemState { ItemId = itemId };
        ownedItems.Add(state);
        ownedItemById[itemId] = state;
        return state;
    }

    private PawPalPointPackageDefinition FindPointPackage(string packageId)
    {
        for (int i = 0; i < pointPackages.Count; i++)
        {
            PawPalPointPackageDefinition package = pointPackages[i];
            if (package.Id == packageId)
            {
                return package;
            }
        }

        return null;
    }

    private PawPalSaveData BuildSaveData()
    {
        PawPalSaveData saveData = new PawPalSaveData();
        saveData.TrainerState = CloneTrainerState(trainerState);
        saveData.ActiveDogIndex = GetSaveActiveDogIndex();
        saveData.NextDailyResetUtcTicks = nextDailyResetUtc.Ticks;
        saveData.LastProcessedTrainerMilestoneLevel = lastProcessedTrainerMilestoneLevel;
        saveData.ActiveWalkSession = activeWalkSession != null && IsTemporaryIntroDogId(activeWalkSession.SelectedDogId)
            ? null
            : CloneWalkSession(activeWalkSession);
        saveData.ActiveAgilityTrialSession = activeAgilityTrialSession != null && IsTemporaryIntroDogId(activeAgilityTrialSession.SelectedDogId)
            ? null
            : PawPalAgilitySaveUtility.CloneSession(activeAgilityTrialSession);

        for (int i = 0; i < dogs.Count; i++)
        {
            if (dogs[i] == null || IsTemporaryIntroDogId(dogs[i].Id))
            {
                continue;
            }

            PawPalDogPersonalityProfiles.EnsureProfile(dogs[i]);
            EnsureWalkData(dogs[i]);
            EnsureTrickData(dogs[i]);
            saveData.Dogs.Add(CloneDogState(dogs[i]));
        }

        for (int i = 0; i < ownedItems.Count; i++)
        {
            PawPalOwnedItemState item = ownedItems[i];
            saveData.OwnedItems.Add(new PawPalOwnedItemState
            {
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                Owned = item.Owned
            });
        }

        for (int i = 0; i < dogEquipment.Count; i++)
        {
            PawPalDogEquipmentState state = dogEquipment[i];
            if (state == null || IsTemporaryIntroDogId(state.DogId))
            {
                continue;
            }

            saveData.DogEquipment.Add(new PawPalDogEquipmentState
            {
                DogId = state.DogId,
                EquippedCollarItemId = state.EquippedCollarItemId
            });
        }

        for (int i = 0; i < dailyTasks.Count; i++)
        {
            saveData.DailyTasks.Add(CloneDailyTaskState(dailyTasks[i]));
        }

        for (int i = 0; i < agilityProgress.Count; i++)
        {
            PawPalAgilityPetProgressState progress = agilityProgress[i];
            if (progress != null && !IsTemporaryIntroDogId(progress.DogId))
            {
                saveData.AgilityProgress.Add(PawPalAgilitySaveUtility.ClonePetProgress(progress));
            }
        }

        for (int i = 0; i < pendingUnsupportedTrainerMilestoneLevels.Count; i++)
        {
            saveData.PendingUnsupportedTrainerMilestoneLevels.Add(pendingUnsupportedTrainerMilestoneLevels[i]);
        }

        return saveData;
    }

    private int GetSaveActiveDogIndex()
    {
        int persistentIndex = 0;
        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            if (dog == null || IsTemporaryIntroDogId(dog.Id))
            {
                continue;
            }

            if (i == activeDogIndex)
            {
                lastPersistentActiveDogIndex = persistentIndex;
                return persistentIndex;
            }

            persistentIndex++;
        }

        if (persistentIndex == 0)
        {
            return 0;
        }

        return Mathf.Clamp(lastPersistentActiveDogIndex, 0, persistentIndex - 1);
    }

    private bool IsTemporaryIntroDogId(string dogId)
    {
        return !string.IsNullOrWhiteSpace(dogId) && temporaryIntroDogIds.Contains(dogId);
    }

    private void RestoreFromSaveData(PawPalSaveData saveData)
    {
        trainerState = CloneTrainerState(saveData.TrainerState);

        dogs.Clear();
        temporaryIntroDogIds.Clear();
        for (int i = 0; i < saveData.Dogs.Count; i++)
        {
            dogs.Add(CloneDogState(saveData.Dogs[i]));
        }

        if (dogs.Count == 0)
        {
            dogs.Add(BuildStarterDog());
        }

        for (int i = 0; i < dogs.Count; i++)
        {
            EnsureWalkData(dogs[i]);
        }

        bool migratedDogProfiles = EnsureDogProfiles();
        bool migratedTrickData = EnsureAllTrickData();
        bool backfilledDogNeeds = BackfillIdenticalDogNeedsIfNeeded();
        agilityProgress.Clear();
        if (saveData.AgilityProgress != null)
        {
            for (int i = 0; i < saveData.AgilityProgress.Count; i++)
            {
                PawPalAgilityPetProgressState progress = PawPalAgilitySaveUtility.ClonePetProgress(saveData.AgilityProgress[i]);
                if (progress != null && !string.IsNullOrWhiteSpace(progress.DogId))
                {
                    agilityProgress.Add(progress);
                }
            }
        }

        EnsureAllAgilityProgress();

        ownedItems.Clear();
        ownedItemById.Clear();
        for (int i = 0; i < saveData.OwnedItems.Count; i++)
        {
            PawPalOwnedItemState state = new PawPalOwnedItemState
            {
                ItemId = saveData.OwnedItems[i].ItemId,
                Quantity = Mathf.Max(0, saveData.OwnedItems[i].Quantity),
                Owned = saveData.OwnedItems[i].Owned || saveData.OwnedItems[i].Quantity > 0
            };

            ownedItems.Add(state);
            ownedItemById[state.ItemId] = state;
        }

        BackfillMissingOwnedItems();

        dogEquipment.Clear();
        dogEquipmentByDogId.Clear();
        for (int i = 0; i < saveData.DogEquipment.Count; i++)
        {
            PawPalDogEquipmentState state = new PawPalDogEquipmentState
            {
                DogId = saveData.DogEquipment[i].DogId,
                EquippedCollarItemId = saveData.DogEquipment[i].EquippedCollarItemId
            };

            dogEquipment.Add(state);
            dogEquipmentByDogId[state.DogId] = state;
        }

        for (int i = 0; i < dogs.Count; i++)
        {
            EnsureDogEquipmentState(dogs[i].Id);
        }

        bool migratedEquipment = MigrateLegacyEquipmentReferences();

        dailyTasks.Clear();
        actionProgress.Clear();
        for (int i = 0; i < saveData.DailyTasks.Count; i++)
        {
            dailyTasks.Add(CloneDailyTaskState(saveData.DailyTasks[i]));
        }

        nextDailyResetUtc = saveData.NextDailyResetUtcTicks > 0
            ? new DateTime(saveData.NextDailyResetUtcTicks, DateTimeKind.Utc)
            : DateTime.UtcNow.Date.AddDays(1d);

        RestoreTrainerMilestoneState(saveData);
        EnsureDailyTasksPresent(false);
        activeWalkSession = CloneWalkSession(saveData.ActiveWalkSession);
        activeAgilityTrialSession = PawPalAgilitySaveUtility.CloneSession(saveData.ActiveAgilityTrialSession);
        bool canceledUnfinishedWalkSession = CancelLoadedUnfinishedWalkSession();

        activeDogIndex = Mathf.Clamp(saveData.ActiveDogIndex, 0, Mathf.Max(0, dogs.Count - 1));
        lastPersistentActiveDogIndex = activeDogIndex;
        inventoryRevision++;
        shopRevision++;

        if (migratedDogProfiles || migratedTrickData || backfilledDogNeeds || migratedEquipment || canceledUnfinishedWalkSession)
        {
            SaveProfile();
        }
    }

    private bool EnsureAllTrickData()
    {
        bool changed = false;
        PawPalVoiceProfileStore legacyVoiceStore = null;
        for (int i = 0; i < dogs.Count; i++)
        {
            changed |= EnsureTrickData(dogs[i], ref legacyVoiceStore);
        }

        return changed;
    }

    private bool EnsureDogProfiles()
    {
        bool changed = false;
        for (int i = 0; i < dogs.Count; i++)
        {
            changed |= PawPalDogPersonalityProfiles.EnsureProfile(dogs[i]);
        }

        return changed;
    }

    private bool MigrateLegacyEquipmentReferences()
    {
        bool changed = false;
        for (int i = 0; i < dogEquipment.Count; i++)
        {
            PawPalDogEquipmentState state = dogEquipment[i];
            if (state == null)
            {
                continue;
            }

            if (string.Equals(state.EquippedCollarItemId, "collar_ocean_band", StringComparison.Ordinal))
            {
                state.EquippedCollarItemId = StarterCollarItemId;
                changed = true;
            }
        }

        return changed;
    }

    private void RestoreTrainerMilestoneState(PawPalSaveData saveData)
    {
        pendingUnsupportedTrainerMilestoneLevels.Clear();
        if (saveData != null && saveData.PendingUnsupportedTrainerMilestoneLevels != null)
        {
            for (int i = 0; i < saveData.PendingUnsupportedTrainerMilestoneLevels.Count; i++)
            {
                AddPendingUnsupportedTrainerMilestone(saveData.PendingUnsupportedTrainerMilestoneLevels[i]);
            }
        }

        int maxLevel = GetMaxTrainerLevel();
        int currentLevel = Mathf.Clamp(trainerState.Level, 1, maxLevel);
        trainerState.Level = currentLevel;
        if (trainerState.Level >= maxLevel)
        {
            trainerState.CurrentLevelExperience = 0;
        }

        if (saveData != null && (saveData.LastProcessedTrainerMilestoneLevel > 0 || pendingUnsupportedTrainerMilestoneLevels.Count > 0))
        {
            lastProcessedTrainerMilestoneLevel = Mathf.Clamp(saveData.LastProcessedTrainerMilestoneLevel, 0, currentLevel);
            for (int level = lastProcessedTrainerMilestoneLevel + 1; level <= currentLevel; level++)
            {
                ProcessTrainerMilestone(level, true);
            }

            PrunePendingUnsupportedMilestones(currentLevel);
            return;
        }

        MigrateLegacyTrainerMilestones(currentLevel);
    }

    private void MigrateLegacyTrainerMilestones(int currentLevel)
    {
        pendingUnsupportedTrainerMilestoneLevels.Clear();
        lastProcessedTrainerMilestoneLevel = 1;

        for (int level = 2; level <= currentLevel; level++)
        {
            ProcessTrainerMilestone(level, !WasLegacyMilestoneAlreadyGranted(level));
        }
    }

    private void PrunePendingUnsupportedMilestones(int currentLevel)
    {
        for (int i = pendingUnsupportedTrainerMilestoneLevels.Count - 1; i >= 0; i--)
        {
            int level = pendingUnsupportedTrainerMilestoneLevels[i];
            if (level <= 0 || level > currentLevel)
            {
                pendingUnsupportedTrainerMilestoneLevels.RemoveAt(i);
                continue;
            }

            TrainerMilestoneDefinition milestone = GetTrainerMilestoneDefinition(level);
            if (!ShouldTrackPendingUnsupportedMilestone(milestone))
            {
                pendingUnsupportedTrainerMilestoneLevels.RemoveAt(i);
            }
        }
    }

    private bool WasLegacyMilestoneAlreadyGranted(int level)
    {
        switch (level)
        {
            case 4:
            case 5:
            case 6:
            case 10:
            case 15:
                return true;
            default:
                return false;
        }
    }

    private void BackfillMissingOwnedItems()
    {
        for (int i = 0; i < catalogItems.Count; i++)
        {
            PawPalCatalogItemDefinition item = catalogItems[i];
            if (ownedItemById.ContainsKey(item.Id))
            {
                continue;
            }

            PawPalOwnedItemState state = new PawPalOwnedItemState
            {
                ItemId = item.Id,
                Quantity = item.StarterQuantity,
                Owned = item.StarterOwned || item.StarterQuantity > 0
            };

            ownedItems.Add(state);
            ownedItemById[item.Id] = state;
        }
    }

    private static PawPalTrainerState CloneTrainerState(PawPalTrainerState source)
    {
        return new PawPalTrainerState
        {
            TrainerName = source.TrainerName,
            Level = source.Level,
            CurrentLevelExperience = source.CurrentLevelExperience,
            BasicCurrency = source.BasicCurrency,
            PremiumCurrency = source.PremiumCurrency,
            ClubExperience = source.ClubExperience,
            CompetitionsParticipated = source.CompetitionsParticipated,
            CompetitionsWonFirst = source.CompetitionsWonFirst,
            CompetitionsWonSecond = source.CompetitionsWonSecond,
            CompetitionsWonThird = source.CompetitionsWonThird,
            WalksCompleted = source.WalksCompleted,
            DailyActivitiesCompleted = source.DailyActivitiesCompleted,
            TotalBasicCurrencyEarned = source.TotalBasicCurrencyEarned,
            HighestDogClubRanking = source.HighestDogClubRanking
        };
    }

    private static PawPalDogState CloneDogState(PawPalDogState source)
    {
        return new PawPalDogState
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Gender = source.Gender,
            Personality = source.Personality,
            FurColor = source.FurColor,
            Breed = source.Breed,
            ProfileVersion = source.ProfileVersion,
            Food01 = source.Food01,
            Water01 = source.Water01,
            Hygiene01 = source.Hygiene01,
            Activity01 = source.Activity01,
            Energy01 = source.Energy01,
            WalkData = CloneWalkData(source.WalkData),
            TrickProfileVersion = source.TrickProfileVersion,
            Bond01 = source.Bond01,
            BondXp = source.BondXp,
            BondLevel = source.BondLevel,
            Mood01 = source.Mood01,
            TrainingFatigue01 = source.TrainingFatigue01,
            LastTrainingFatigueUpdateUtcTicks = source.LastTrainingFatigueUpdateUtcTicks,
            Tricks = CloneTrickProgress(source.Tricks),
            Endurance = source.Endurance,
            Mobility = source.Mobility,
            Speed = source.Speed,
            Focus = source.Focus,
            NewDogWhinyHoursRemaining = source.NewDogWhinyHoursRemaining,
            ObedienceTrialProgress = PawPalObedienceTrialDatabase.CloneProgress(source.ObedienceTrialProgress)
        };
    }

    private static List<PawPalDogTrickProgress> CloneTrickProgress(List<PawPalDogTrickProgress> source)
    {
        List<PawPalDogTrickProgress> clone = new List<PawPalDogTrickProgress>();
        if (source == null)
        {
            return clone;
        }

        for (int i = 0; i < source.Count; i++)
        {
            PawPalDogTrickProgress progress = source[i];
            if (progress == null)
            {
                continue;
            }

            clone.Add(new PawPalDogTrickProgress
            {
                TrickId = progress.TrickId,
                IsDiscovered = progress.IsDiscovered,
                IsLearned = progress.IsLearned,
                MasteryXp = progress.MasteryXp,
                MasteryLevel = progress.MasteryLevel,
                TimesPracticedToday = progress.TimesPracticedToday,
                TotalSuccessfulAttempts = progress.TotalSuccessfulAttempts,
                TotalFailedAttempts = progress.TotalFailedAttempts,
                CustomVoiceCommand = progress.CustomVoiceCommand,
                CommandConfidence = progress.CommandConfidence,
                LastPracticedAtUtcTicks = progress.LastPracticedAtUtcTicks,
                LearnedAtUtcTicks = progress.LearnedAtUtcTicks,
                LastPracticeDayUtcTicks = progress.LastPracticeDayUtcTicks
            });
        }

        return clone;
    }

    private static PawPalDogWalkData CloneWalkData(PawPalDogWalkData source)
    {
        if (source == null)
        {
            return new PawPalDogWalkData();
        }

        return new PawPalDogWalkData
        {
            CurrentWalkStamina = source.CurrentWalkStamina,
            MaxWalkStamina = source.MaxWalkStamina,
            WalkStaminaXp = source.WalkStaminaXp,
            LastWalkCompletedAtUtcTicks = source.LastWalkCompletedAtUtcTicks,
            LastStaminaRefreshAtUtcTicks = source.LastStaminaRefreshAtUtcTicks,
            TotalWalksCompleted = source.TotalWalksCompleted,
            TotalDistanceWalked = source.TotalDistanceWalked
        };
    }

    private static PawPalWalkLocationData CloneWalkLocation(PawPalWalkLocationData source)
    {
        if (source == null)
        {
            return new PawPalWalkLocationData();
        }

        return new PawPalWalkLocationData
        {
            LocationId = source.LocationId,
            DisplayName = source.DisplayName,
            LocationType = source.LocationType,
            Progress = source.Progress
        };
    }

    private static PawPalWalkPointData CloneWalkPoint(PawPalWalkPointData source)
    {
        if (source == null)
        {
            return new PawPalWalkPointData();
        }

        return new PawPalWalkPointData
        {
            X = source.X,
            Y = source.Y
        };
    }

    private static PawPalWalkWorldPointData CloneWalkWorldPoint(PawPalWalkWorldPointData source)
    {
        if (source == null)
        {
            return new PawPalWalkWorldPointData();
        }

        return new PawPalWalkWorldPointData
        {
            X = source.X,
            Y = source.Y,
            Z = source.Z
        };
    }

    private static PawPalWalkRouteEncounterData CloneRouteEncounter(PawPalWalkRouteEncounterData source)
    {
        if (source == null)
        {
            return new PawPalWalkRouteEncounterData();
        }

        return new PawPalWalkRouteEncounterData
        {
            EncounterPointId = source.EncounterPointId,
            SourceNodeId = source.SourceNodeId,
            Progress = source.Progress
        };
    }

    private static PawPalWalkGeneratedEventState CloneWalkEvent(PawPalWalkGeneratedEventState source)
    {
        if (source == null)
        {
            return new PawPalWalkGeneratedEventState();
        }

        return new PawPalWalkGeneratedEventState
        {
            EventId = source.EventId,
            EventType = source.EventType,
            LocationId = source.LocationId,
            DisplayName = source.DisplayName,
            BodyText = source.BodyText,
            RewardItemId = source.RewardItemId,
            SourceEncounterPointId = source.SourceEncounterPointId,
            SourceNodeId = source.SourceNodeId,
            EventTemplateId = source.EventTemplateId,
            StopType = source.StopType,
            Progress = source.Progress,
            Resolved = source.Resolved,
            RewardGranted = source.RewardGranted
        };
    }

    private static PawPalWalkSessionSaveData CloneWalkSession(PawPalWalkSessionSaveData source)
    {
        if (source == null)
        {
            return null;
        }

        PawPalWalkSessionSaveData clone = new PawPalWalkSessionSaveData
        {
            SessionId = source.SessionId,
            SelectedDogId = source.SelectedDogId,
            SelectedDogName = source.SelectedDogName,
            ReturnSceneName = source.ReturnSceneName,
            RouteDistance = source.RouteDistance,
            StaminaCost = source.StaminaCost,
            StartedAtUtcTicks = source.StartedAtUtcTicks,
            CompletedAtUtcTicks = source.CompletedAtUtcTicks,
            Completed = source.Completed,
            FinalRewardsApplied = source.FinalRewardsApplied,
            LastProgress = source.LastProgress,
            DeferredGraphResolution = source.DeferredGraphResolution,
            GraphId = source.GraphId,
            StartNodeId = source.StartNodeId,
            EndNodeId = source.EndNodeId
        };

        if (source.RoutePoints != null)
        {
            for (int i = 0; i < source.RoutePoints.Count; i++)
            {
                clone.RoutePoints.Add(CloneWalkPoint(source.RoutePoints[i]));
            }
        }

        if (source.WorldPath != null)
        {
            for (int i = 0; i < source.WorldPath.Count; i++)
            {
                clone.WorldPath.Add(CloneWalkWorldPoint(source.WorldPath[i]));
            }
        }

        if (source.EncounterPoints != null)
        {
            for (int i = 0; i < source.EncounterPoints.Count; i++)
            {
                clone.EncounterPoints.Add(CloneRouteEncounter(source.EncounterPoints[i]));
            }
        }

        if (source.RouteNodeIds != null)
        {
            for (int i = 0; i < source.RouteNodeIds.Count; i++)
            {
                clone.RouteNodeIds.Add(source.RouteNodeIds[i]);
            }
        }

        if (source.RouteEdgeIds != null)
        {
            for (int i = 0; i < source.RouteEdgeIds.Count; i++)
            {
                clone.RouteEdgeIds.Add(source.RouteEdgeIds[i]);
            }
        }

        if (source.RequestedVisitNodeIds != null)
        {
            for (int i = 0; i < source.RequestedVisitNodeIds.Count; i++)
            {
                clone.RequestedVisitNodeIds.Add(source.RequestedVisitNodeIds[i]);
            }
        }

        if (source.VisitedLocations != null)
        {
            for (int i = 0; i < source.VisitedLocations.Count; i++)
            {
                clone.VisitedLocations.Add(CloneWalkLocation(source.VisitedLocations[i]));
            }
        }

        if (source.GeneratedEvents != null)
        {
            for (int i = 0; i < source.GeneratedEvents.Count; i++)
            {
                clone.GeneratedEvents.Add(CloneWalkEvent(source.GeneratedEvents[i]));
            }
        }

        return clone;
    }

    private bool BackfillIdenticalDogNeedsIfNeeded()
    {
        if (dogs.Count < 2)
        {
            return false;
        }

        bool changed = false;
        for (int i = 1; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            if (dog == null)
            {
                continue;
            }

            for (int previousIndex = 0; previousIndex < i; previousIndex++)
            {
                PawPalDogState previousDog = dogs[previousIndex];
                if (previousDog == null || !HasIdenticalNeedProfile(dog, previousDog))
                {
                    continue;
                }

                ApplyDeterministicNeedBackfill(dog, i);
                changed = true;
                break;
            }
        }

        return changed;
    }

    private static bool HasIdenticalNeedProfile(PawPalDogState firstDog, PawPalDogState secondDog)
    {
        return firstDog.Food01 == secondDog.Food01
            && firstDog.Water01 == secondDog.Water01
            && firstDog.Hygiene01 == secondDog.Hygiene01
            && firstDog.Activity01 == secondDog.Activity01
            && Mathf.Approximately(GetDogStamina01(firstDog), GetDogStamina01(secondDog));
    }

    private static void ApplyDeterministicNeedBackfill(PawPalDogState dog, int dogIndex)
    {
        if (dog == null)
        {
            return;
        }

        if (string.Equals(dog.Id, "pepper", StringComparison.OrdinalIgnoreCase))
        {
            SetDogNeeds(dog, 0.82f, 0.76f, 0.7f, 0.67f, 0.74f);
            return;
        }

        if (string.Equals(dog.Id, "miso", StringComparison.OrdinalIgnoreCase))
        {
            SetDogNeeds(dog, 0.64f, 0.88f, 0.78f, 0.92f, 0.81f);
            return;
        }

        if (string.Equals(dog.Id, "suki", StringComparison.OrdinalIgnoreCase))
        {
            SetDogNeeds(dog, 0.93f, 0.58f, 0.86f, 0.74f, 0.89f);
            return;
        }

        int seed = Mathf.Max(1, dogIndex + 1) * 37;
        string key = !string.IsNullOrEmpty(dog.Id) ? dog.Id : dog.DisplayName;
        if (!string.IsNullOrEmpty(key))
        {
            for (int i = 0; i < key.Length; i++)
            {
                seed += key[i] * (i + 1);
            }
        }

        SetDogNeeds(
            dog,
            0.58f + (seed % 23) * 0.01f,
            0.62f + (seed % 19) * 0.01f,
            0.66f + (seed % 17) * 0.01f,
            0.7f + (seed % 13) * 0.01f,
            0.72f + (seed % 11) * 0.01f);
    }

    private static void SetDogNeeds(PawPalDogState dog, float food, float water, float hygiene, float activity, float energy)
    {
        if (dog == null)
        {
            return;
        }

        dog.Food01 = Mathf.Clamp01(food);
        dog.Water01 = Mathf.Clamp01(water);
        dog.Hygiene01 = Mathf.Clamp01(hygiene);
        dog.Activity01 = Mathf.Clamp01(activity);
        dog.Energy01 = Mathf.Clamp01(energy);
        EnsureCanonicalStaminaData(dog);
        if (dog.WalkData != null)
        {
            dog.WalkData.CurrentWalkStamina = Mathf.Lerp(0f, dog.WalkData.MaxWalkStamina, dog.Energy01);
            dog.WalkData.LastStaminaRefreshAtUtcTicks = DateTime.UtcNow.Ticks;
        }
    }

    private static PawPalDailyTaskState CloneDailyTaskState(PawPalDailyTaskState source)
    {
        return new PawPalDailyTaskState
        {
            Id = source.Id,
            Title = source.Title,
            ActionType = source.ActionType,
            RequiredCount = source.RequiredCount,
            Progress = source.Progress,
            ExperienceReward = source.ExperienceReward,
            BasicCurrencyReward = source.BasicCurrencyReward,
            RewardGranted = source.RewardGranted
        };
    }

    private string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    private void EnsureBridge()
    {
        if (sceneBridge == null)
        {
            sceneBridge = GetComponent<PawPalDogSceneBridge>();
        }

        if (sceneBridge == null)
        {
            sceneBridge = gameObject.AddComponent<PawPalDogSceneBridge>();
        }

        if (sceneBridge != null)
        {
            sceneBridge.Initialize(this);
        }
    }

    private void CommitState(bool saveProfile, bool inventoryChanged, bool shopChanged)
    {
        if (inventoryChanged)
        {
            inventoryRevision++;
        }

        if (shopChanged)
        {
            shopRevision++;
        }

        if (saveProfile)
        {
            SaveProfile();
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        Action handler = StateChanged;
        if (handler != null)
        {
            handler();
        }
    }
}
