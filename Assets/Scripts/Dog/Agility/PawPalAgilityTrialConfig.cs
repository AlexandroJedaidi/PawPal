using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PawFriends/Agility/Agility Trial Config", fileName = "PawPalAgilityTrialConfig")]
public sealed class PawPalAgilityTrialConfig : ScriptableObject
{
    private const string ResourcePath = "PawPal/Agility/PawPalAgilityTrialConfig";

    [SerializeField] private List<PawPalAgilityLevelDefinition> levels = new List<PawPalAgilityLevelDefinition>();

    public IReadOnlyList<PawPalAgilityLevelDefinition> Levels
    {
        get { return levels; }
    }

    public static PawPalAgilityTrialConfig LoadOrCreateDefault()
    {
        PawPalAgilityTrialConfig config = Resources.Load<PawPalAgilityTrialConfig>(ResourcePath);
        if (config != null)
        {
            config.EnsureDefaults();
            return config;
        }

        config = CreateInstance<PawPalAgilityTrialConfig>();
        config.name = "PawPalAgilityTrialConfig_RuntimeDefault";
        config.ResetToDefaultLevels();
        return config;
    }

    public PawPalAgilityLevelDefinition GetLevel(PawPalAgilityLevelId levelId)
    {
        EnsureDefaults();
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].LevelId == levelId)
            {
                return levels[i];
            }
        }

        return levels.Count > 0 ? levels[0] : null;
    }

    public PawPalAgilityLevelId GetNextLevel(PawPalAgilityLevelId levelId)
    {
        switch (levelId)
        {
            case PawPalAgilityLevelId.Beginner:
                return PawPalAgilityLevelId.Amateur;
            case PawPalAgilityLevelId.Amateur:
                return PawPalAgilityLevelId.Pro;
            case PawPalAgilityLevelId.Pro:
                return PawPalAgilityLevelId.Master;
            case PawPalAgilityLevelId.Master:
                return PawPalAgilityLevelId.Champion;
            default:
                return PawPalAgilityLevelId.Champion;
        }
    }

    public bool HasNextLevel(PawPalAgilityLevelId levelId)
    {
        return levelId != PawPalAgilityLevelId.Champion;
    }

    public void ResetToDefaultLevels()
    {
        levels.Clear();
        levels.Add(BuildLevel(PawPalAgilityLevelId.Beginner, "Beginner", 10, 70f, 110f, 5, 40, 70, 120, 0.06f, new[]
        {
            PawPalAgilityObstacleType.StartGate,
            PawPalAgilityObstacleType.BarrierRunAround,
            PawPalAgilityObstacleType.BridgeWalkOver,
            PawPalAgilityObstacleType.FinishGate
        }));
        levels.Add(BuildLevel(PawPalAgilityLevelId.Amateur, "Amateur", 15, 85f, 125f, 4, 65, 110, 175, 0.075f, new[]
        {
            PawPalAgilityObstacleType.StartGate,
            PawPalAgilityObstacleType.BarrierRunAround,
            PawPalAgilityObstacleType.HighFence,
            PawPalAgilityObstacleType.BridgeWalkOver,
            PawPalAgilityObstacleType.WheelJump,
            PawPalAgilityObstacleType.FinishGate
        }));
        levels.Add(BuildLevel(PawPalAgilityLevelId.Pro, "Pro", 25, 95f, 135f, 3, 100, 165, 260, 0.09f, new[]
        {
            PawPalAgilityObstacleType.StartGate,
            PawPalAgilityObstacleType.BarrierRunAround,
            PawPalAgilityObstacleType.HighFence,
            PawPalAgilityObstacleType.SeeSaw,
            PawPalAgilityObstacleType.BridgeWalkOver,
            PawPalAgilityObstacleType.WheelJump,
            PawPalAgilityObstacleType.FinishGate
        }));
        levels.Add(BuildLevel(PawPalAgilityLevelId.Master, "Master", 40, 105f, 145f, 2, 160, 245, 360, 0.11f, new[]
        {
            PawPalAgilityObstacleType.StartGate,
            PawPalAgilityObstacleType.WheelJump,
            PawPalAgilityObstacleType.BarrierRunAround,
            PawPalAgilityObstacleType.SeeSaw,
            PawPalAgilityObstacleType.HighFence,
            PawPalAgilityObstacleType.BridgeWalkOver,
            PawPalAgilityObstacleType.WheelJump,
            PawPalAgilityObstacleType.FinishGate
        }));
        levels.Add(BuildLevel(PawPalAgilityLevelId.Champion, "Champion", 60, 115f, 155f, 1, 240, 360, 520, 0.13f, new[]
        {
            PawPalAgilityObstacleType.StartGate,
            PawPalAgilityObstacleType.BarrierRunAround,
            PawPalAgilityObstacleType.WheelJump,
            PawPalAgilityObstacleType.SeeSaw,
            PawPalAgilityObstacleType.HighFence,
            PawPalAgilityObstacleType.BridgeWalkOver,
            PawPalAgilityObstacleType.WheelJump,
            PawPalAgilityObstacleType.HighFence,
            PawPalAgilityObstacleType.FinishGate
        }));
    }

    private void EnsureDefaults()
    {
        if (levels == null)
        {
            levels = new List<PawPalAgilityLevelDefinition>();
        }

        if (levels.Count == 0)
        {
            ResetToDefaultLevels();
        }
    }

    private static PawPalAgilityLevelDefinition BuildLevel(
        PawPalAgilityLevelId levelId,
        string displayName,
        int entryFee,
        float targetTime,
        float timeLimit,
        int faultTolerance,
        int bronzeReward,
        int silverReward,
        int goldReward,
        float staminaCostRatio,
        PawPalAgilityObstacleType[] obstacleTypes)
    {
        PawPalAgilityLevelDefinition level = new PawPalAgilityLevelDefinition
        {
            LevelId = levelId,
            DisplayName = displayName,
            EntryFeeBasicCurrency = entryFee,
            TargetTimeSeconds = targetTime,
            TimeLimitSeconds = timeLimit,
            FaultTolerance = faultTolerance,
            BronzeRewardBasicCurrency = bronzeReward,
            SilverRewardBasicCurrency = silverReward,
            GoldRewardBasicCurrency = goldReward,
            StaminaCostRatio = staminaCostRatio,
            RequiredPreviousMedal = PawPalAgilityMedal.Silver
        };

        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            level.Obstacles.Add(BuildObstacle(obstacleTypes[i], i));
        }

        return level;
    }

    private static PawPalAgilityObstacleDefinition BuildObstacle(PawPalAgilityObstacleType type, int index)
    {
        Vector3 position = new Vector3(-16f + index * 4f, 0f, (index % 2 == 0) ? -1.8f : 2.1f);
        PawPalAgilityObstacleDefinition obstacle = new PawPalAgilityObstacleDefinition
        {
            Type = type,
            DisplayName = GetDisplayName(type),
            SceneObjectName = GetSceneObjectName(type),
            FallbackPosition = position,
            SuccessRadius = 1.45f,
            InputLeadDistance = 2.35f,
            FaultPenalty = type == PawPalAgilityObstacleType.StartGate || type == PawPalAgilityObstacleType.FinishGate ? 0f : 10f,
            RequiresJumpCue = type == PawPalAgilityObstacleType.HighFence || type == PawPalAgilityObstacleType.WheelJump,
            RequiresBalanceCue = type == PawPalAgilityObstacleType.SeeSaw || type == PawPalAgilityObstacleType.BridgeWalkOver
        };

        if (type == PawPalAgilityObstacleType.StartGate || type == PawPalAgilityObstacleType.FinishGate)
        {
            obstacle.SuccessRadius = 1.2f;
            obstacle.InputLeadDistance = 0.5f;
        }

        return obstacle;
    }

    private static string GetDisplayName(PawPalAgilityObstacleType type)
    {
        switch (type)
        {
            case PawPalAgilityObstacleType.StartGate:
                return "Start gate";
            case PawPalAgilityObstacleType.FinishGate:
                return "Finish gate";
            case PawPalAgilityObstacleType.SeeSaw:
                return "See-saw";
            case PawPalAgilityObstacleType.BarrierRunAround:
                return "Barrier turn";
            case PawPalAgilityObstacleType.BridgeWalkOver:
                return "Bridge";
            case PawPalAgilityObstacleType.HighFence:
                return "High fence";
            case PawPalAgilityObstacleType.WheelJump:
                return "Wheel jump";
            default:
                return "Obstacle";
        }
    }

    private static string GetSceneObjectName(PawPalAgilityObstacleType type)
    {
        switch (type)
        {
            case PawPalAgilityObstacleType.SeeSaw:
                return "Sea saw";
            case PawPalAgilityObstacleType.BarrierRunAround:
                return "Barrier_1";
            case PawPalAgilityObstacleType.BridgeWalkOver:
                return "Bridge";
            case PawPalAgilityObstacleType.HighFence:
                return "Fence_dog";
            case PawPalAgilityObstacleType.WheelJump:
                return "Wheel";
            default:
                return string.Empty;
        }
    }
}
