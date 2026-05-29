using System;
using System.Collections.Generic;
using UnityEngine;

public enum TrainerMilestoneRewardKind
{
    None,
    Feature,
    CurrencyBasic,
    CurrencyPremium,
    PremiumFood,
    DogUnlock,
    CatalogItemReward,
    Unsupported
}

public enum TrainerMilestoneItemQuality
{
    None,
    Basic,
    Premium
}

[Serializable]
public struct TrainerProgressionSnapshot
{
    public int Level;
    public int CurrentLevelExperience;
    public int RequiredExperience;
    public float LevelProgress01;
    public float AbsoluteProgress01;
    public int CurrentMilestoneLevel;
}

[Serializable]
public sealed class TrainerActionRewardDefinition
{
    public string Id;
    public string ActionType;
    public int Experience;
    public int BasicCurrency;
    public int ClubExperience;
}

[Serializable]
public sealed class TrainerMilestoneDefinition
{
    public int Level;
    public int RequiredExperience;
    public string RewardLabel;
    public string RewardTypeLabel;
    public TrainerMilestoneRewardKind RewardKind;
    public PawPalItemCategory CatalogCategory;
    public TrainerMilestoneItemQuality ItemQuality;
    public int CurrencyAmount;
    public int Quantity;
    public bool RandomSelection;
    public string DogId;
    public string FeatureKey;
}

[Serializable]
public sealed class TrainerProgressionConfig
{
    public string SourceWorkbook;
    public string SourceSheet;
    public List<TrainerActionRewardDefinition> ActionRewards = new List<TrainerActionRewardDefinition>();
    public List<TrainerMilestoneDefinition> Milestones = new List<TrainerMilestoneDefinition>();
}

public static class TrainerProgressionDatabase
{
    private const string ResourcePath = "GameBalance/trainer_progression";

    private static readonly TrainerActionRewardDefinition PlayWithToyExtension = new TrainerActionRewardDefinition
    {
        Id = "play_with_toy_extension",
        ActionType = nameof(PawPalPlayerActionType.PlayWithToy),
        Experience = 10,
        BasicCurrency = 0,
        ClubExperience = 2
    };

    private static TrainerProgressionConfig cachedConfig;

    public static TrainerProgressionConfig Load()
    {
        if (cachedConfig != null)
        {
            return cachedConfig;
        }

        TextAsset textAsset = Resources.Load<TextAsset>(ResourcePath);
        if (textAsset == null)
        {
            Debug.LogWarning("TrainerProgressionDatabase could not load Resources/" + ResourcePath + ". Using fallback trainer progression data.");
            cachedConfig = BuildFallbackConfig();
            return cachedConfig;
        }

        TrainerProgressionConfig config = JsonUtility.FromJson<TrainerProgressionConfig>(textAsset.text);
        cachedConfig = Normalize(config);
        return cachedConfig;
    }

    public static TrainerActionRewardDefinition GetPlayWithToyExtension()
    {
        return CloneActionRewardDefinition(PlayWithToyExtension);
    }

    public static string GetMilestoneShortLabel(TrainerMilestoneDefinition definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        switch (definition.RewardKind)
        {
            case TrainerMilestoneRewardKind.Feature:
                return GetFeatureShortLabel(definition.FeatureKey, definition.RewardLabel);
            case TrainerMilestoneRewardKind.CurrencyBasic:
                return "\u20B1" + Mathf.Max(0, definition.CurrencyAmount).ToString("N0");
            case TrainerMilestoneRewardKind.CurrencyPremium:
                return Mathf.Max(0, definition.CurrencyAmount).ToString();
            case TrainerMilestoneRewardKind.DogUnlock:
                return GetDogUnlockShortLabel(definition.RewardLabel);
            default:
                return definition.RewardLabel ?? string.Empty;
        }
    }

    public static bool ShouldUseIconMarker(TrainerMilestoneDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        switch (definition.RewardKind)
        {
            case TrainerMilestoneRewardKind.CurrencyBasic:
            case TrainerMilestoneRewardKind.CurrencyPremium:
            case TrainerMilestoneRewardKind.Feature:
            case TrainerMilestoneRewardKind.DogUnlock:
                return false;
            default:
                return true;
        }
    }

    public static string GetMilestoneIconName(TrainerMilestoneDefinition definition)
    {
        if (definition == null)
        {
            return "icon_toys_brand";
        }

        switch (definition.RewardKind)
        {
            case TrainerMilestoneRewardKind.PremiumFood:
                return "icon_foodsupply_brand";
            case TrainerMilestoneRewardKind.CatalogItemReward:
                switch (definition.CatalogCategory)
                {
                    case PawPalItemCategory.Collars:
                        return "icon_collar_brand";
                    case PawPalItemCategory.Toys:
                        return "icon_toys_brand";
                    case PawPalItemCategory.Clothing:
                        return "icon_clothing_brand";
                    case PawPalItemCategory.Furniture:
                        return "icon_dogbed_brand";
                    case PawPalItemCategory.Dogs:
                        return "icon_dog_brand";
                }

                break;
            case TrainerMilestoneRewardKind.Unsupported:
                if (definition.CatalogCategory == PawPalItemCategory.Dogs)
                {
                    return "icon_dog_brand";
                }

                if (definition.CatalogCategory == PawPalItemCategory.Furniture)
                {
                    return "icon_dogbed_brand";
                }

                return "icon_toys_brand";
        }

        return "icon_toys_brand";
    }

    private static TrainerProgressionConfig Normalize(TrainerProgressionConfig config)
    {
        TrainerProgressionConfig normalized = config ?? new TrainerProgressionConfig();
        if (normalized.ActionRewards == null)
        {
            normalized.ActionRewards = new List<TrainerActionRewardDefinition>();
        }

        if (normalized.Milestones == null)
        {
            normalized.Milestones = new List<TrainerMilestoneDefinition>();
        }

        bool hasPlayWithToy = false;
        for (int i = 0; i < normalized.ActionRewards.Count; i++)
        {
            TrainerActionRewardDefinition reward = normalized.ActionRewards[i];
            if (reward == null)
            {
                continue;
            }

            if (string.Equals(reward.ActionType, nameof(PawPalPlayerActionType.PlayWithToy), StringComparison.Ordinal))
            {
                hasPlayWithToy = true;
            }
        }

        if (!hasPlayWithToy)
        {
            normalized.ActionRewards.Add(CloneActionRewardDefinition(PlayWithToyExtension));
        }

        normalized.Milestones.Sort(delegate(TrainerMilestoneDefinition left, TrainerMilestoneDefinition right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            return left.Level.CompareTo(right.Level);
        });

        return normalized;
    }

    private static TrainerProgressionConfig BuildFallbackConfig()
    {
        TrainerProgressionConfig config = new TrainerProgressionConfig();
        config.SourceWorkbook = "fallback";
        config.SourceSheet = "embedded";

        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "feed_dog",
            ActionType = nameof(PawPalPlayerActionType.FeedDog),
            Experience = 5,
            BasicCurrency = 0,
            ClubExperience = 1
        });
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "give_water",
            ActionType = nameof(PawPalPlayerActionType.GiveWater),
            Experience = 5,
            BasicCurrency = 0,
            ClubExperience = 1
        });
        config.ActionRewards.Add(CloneActionRewardDefinition(PlayWithToyExtension));
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "clean_dog",
            ActionType = nameof(PawPalPlayerActionType.CleanDog),
            Experience = 5,
            BasicCurrency = 0,
            ClubExperience = 1
        });
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "walk_dog",
            ActionType = nameof(PawPalPlayerActionType.WalkDog),
            Experience = 15,
            BasicCurrency = 0,
            ClubExperience = 3
        });
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "train_trick",
            ActionType = nameof(PawPalPlayerActionType.TrainTrick),
            Experience = 10,
            BasicCurrency = 0,
            ClubExperience = 2
        });
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "competition_first",
            ActionType = nameof(PawPalPlayerActionType.CompetitionFirst),
            Experience = 150,
            BasicCurrency = 1000,
            ClubExperience = 30
        });
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "competition_second",
            ActionType = nameof(PawPalPlayerActionType.CompetitionSecond),
            Experience = 100,
            BasicCurrency = 700,
            ClubExperience = 20
        });
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "competition_third",
            ActionType = nameof(PawPalPlayerActionType.CompetitionThird),
            Experience = 50,
            BasicCurrency = 500,
            ClubExperience = 10
        });
        config.ActionRewards.Add(new TrainerActionRewardDefinition
        {
            Id = "daily_login",
            ActionType = nameof(PawPalPlayerActionType.DailyLogin),
            Experience = 15,
            BasicCurrency = 50,
            ClubExperience = 3
        });

        int[] requirements =
        {
            60, 120, 180, 240, 300, 360, 425, 484, 542, 602,
            662, 722, 780, 834, 884, 928, 975, 1014, 1054, 1086,
            1119, 1152, 1187, 1222, 1259, 1297, 1336, 1376, 1417, 1445,
            1474, 1504, 1534, 1565, 1596, 1620, 1644, 1669, 1694, 1719,
            1736, 1754, 1771, 1789, 1807, 1816, 1825, 1834, 1843, 1852
        };

        for (int i = 0; i < requirements.Length; i++)
        {
            config.Milestones.Add(new TrainerMilestoneDefinition
            {
                Level = i + 1,
                RequiredExperience = requirements[i],
                RewardLabel = i == 0 ? "-" : string.Empty,
                RewardTypeLabel = i == 0 ? "-" : string.Empty,
                RewardKind = i == 0 ? TrainerMilestoneRewardKind.None : TrainerMilestoneRewardKind.Unsupported
            });
        }

        return Normalize(config);
    }

    private static TrainerActionRewardDefinition CloneActionRewardDefinition(TrainerActionRewardDefinition source)
    {
        return new TrainerActionRewardDefinition
        {
            Id = source.Id,
            ActionType = source.ActionType,
            Experience = source.Experience,
            BasicCurrency = source.BasicCurrency,
            ClubExperience = source.ClubExperience
        };
    }

    private static string GetFeatureShortLabel(string featureKey, string rewardLabel)
    {
        if (string.Equals(featureKey, "dog_club", StringComparison.Ordinal))
        {
            return "Clubs";
        }

        if (string.Equals(featureKey, "competition_agility", StringComparison.Ordinal))
        {
            return "Agility";
        }

        if (string.Equals(featureKey, "competition_disc", StringComparison.Ordinal))
        {
            return "Disc";
        }

        if (string.Equals(featureKey, "competition_style", StringComparison.Ordinal))
        {
            return "Style";
        }

        return rewardLabel ?? string.Empty;
    }

    private static string GetDogUnlockShortLabel(string rewardLabel)
    {
        if (string.IsNullOrEmpty(rewardLabel))
        {
            return "Dog";
        }

        string normalized = rewardLabel.Replace("nd", string.Empty).Replace("rd", string.Empty).Replace("th", string.Empty);
        string[] segments = normalized.Split(' ');
        int ordinal;
        if (segments.Length > 0 && int.TryParse(segments[0], out ordinal))
        {
            return ordinal.ToString() + ". dog";
        }

        return rewardLabel;
    }
}
