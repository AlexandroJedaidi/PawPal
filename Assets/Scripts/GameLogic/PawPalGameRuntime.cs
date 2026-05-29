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
    public float Food01;
    public float Water01;
    public float Hygiene01;
    public float Activity01;
    public float Energy01;
    public int Endurance;
    public int Mobility;
    public int Speed;
    public int Focus;

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
    private static readonly List<PawPalToyRuntimeMetadata> NavigationBlockers = new List<PawPalToyRuntimeMetadata>();
    private static BoxCollider largeToySupportFloor;
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
        float radius = TryGetToyBlockingBounds(gameObject, out bounds)
            ? Mathf.Max(0.18f, Mathf.Max(bounds.extents.x, bounds.extents.z))
            : 0.35f;
        float paddedRadius = radius + 0.28f;
        dogNavigationBlockCenterLocal = transform.InverseTransformPoint(bounds.center);
        SetBlocksDogNavigation(true, paddedRadius);
        EnsureLargeToyCollider(bounds, radius);
        EnsureLargeToySupportFloor(bounds);

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.mass = Mathf.Max(0.65f, body.mass);
        body.linearDamping = Mathf.Max(0.08f, body.linearDamping);
        body.angularDamping = Mathf.Max(0.16f, body.angularDamping);
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = paddedRadius;
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

    private static void EnsureLargeToySupportFloor(Bounds toyBounds)
    {
        if (largeToySupportFloor == null)
        {
            GameObject floorObject = new GameObject("PawPalLargeToySupportFloor");
            floorObject.hideFlags = HideFlags.HideAndDontSave;
            floorObject.layer = 2;
            largeToySupportFloor = floorObject.AddComponent<BoxCollider>();
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
        float floorTopY = toyBounds.min.y - 0.01f;
        float floorThickness = 0.16f;

        largeToySupportFloor.transform.position = new Vector3(
            supportBounds.center.x,
            floorTopY - floorThickness * 0.5f,
            supportBounds.center.z);
        largeToySupportFloor.size = new Vector3(width, floorThickness, depth);
        largeToySupportFloor.center = Vector3.zero;
        largeToySupportFloor.enabled = true;
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
    private const float EnergyDrainPerHour = 0.025f;
    private const string SaveFileName = "pawpal_profile_v1.json";
    private const string StarterDogId = "pepper";
    private const string StarterCollarItemId = "collar_ocean_band";
    private const string BasicFoodItemId = "food_basic";
    private const string PremiumFoodItemId = "food_premium";

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
    private readonly Dictionary<PawPalPlayerActionType, int> actionProgress = new Dictionary<PawPalPlayerActionType, int>();
    private readonly Dictionary<string, PawPalCatalogItemDefinition> catalogById = new Dictionary<string, PawPalCatalogItemDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, PawPalOwnedItemState> ownedItemById = new Dictionary<string, PawPalOwnedItemState>(StringComparer.Ordinal);
    private readonly Dictionary<string, PawPalDogEquipmentState> dogEquipmentByDogId = new Dictionary<string, PawPalDogEquipmentState>(StringComparer.Ordinal);
    private readonly Dictionary<PawPalPlayerActionType, TrainerActionRewardDefinition> trainerActionRewardsByType = new Dictionary<PawPalPlayerActionType, TrainerActionRewardDefinition>();
    private readonly Dictionary<int, TrainerMilestoneDefinition> trainerMilestonesByLevel = new Dictionary<int, TrainerMilestoneDefinition>();
    private readonly List<int> pendingUnsupportedTrainerMilestoneLevels = new List<int>();

    [SerializeField] private float secondsPerGameHour = DefaultSecondsPerGameHour;

    private PawPalTrainerState trainerState;
    private PawPalDogSceneBridge sceneBridge;
    private TrainerProgressionConfig trainerProgressionConfig;
    private int activeDogIndex;
    private int inventoryRevision;
    private int lastProcessedTrainerMilestoneLevel;
    private int shopRevision;
    private DateTime nextDailyResetUtc;

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

    public int InventoryRevision
    {
        get { return inventoryRevision; }
    }

    public int ShopRevision
    {
        get { return shopRevision; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("PawPalGameRuntime");
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
        PawPalCatalogItemDefinition item;
        catalogById.TryGetValue(itemId, out item);
        return item;
    }

    public PawPalOwnedItemState GetOwnedItemState(string itemId)
    {
        PawPalOwnedItemState item;
        ownedItemById.TryGetValue(itemId, out item);
        return item;
    }

    public PawPalDogEquipmentState GetDogEquipmentState(string dogId)
    {
        PawPalDogEquipmentState state;
        dogEquipmentByDogId.TryGetValue(dogId, out state);
        return state;
    }

    public string GetEquippedCollarItemId(string dogId)
    {
        PawPalDogEquipmentState state = GetDogEquipmentState(dogId);
        return state != null ? state.EquippedCollarItemId : string.Empty;
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
        if (dogs.Count <= 1)
        {
            return;
        }

        activeDogIndex = (activeDogIndex + 1) % dogs.Count;
        CommitState(true, false, false);
    }

    public void SelectPreviousDog()
    {
        if (dogs.Count <= 1)
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

        dog.ModifyNeed(PawPalDogNeed.Activity, 0.3f);
        dog.ModifyNeed(PawPalDogNeed.Water, -0.1f);
        dog.ModifyNeed(PawPalDogNeed.Food, -0.08f);
        dog.ModifyNeed(PawPalDogNeed.Hygiene, -0.04f);
        dog.Energy01 = Mathf.Clamp01(dog.Energy01 - 0.08f);
        if (dog.Endurance < 10)
        {
            dog.ModifyStat(PawPalDogStatType.Endurance, 1);
        }

        trainerState.WalksCompleted++;
        ApplyActionRewards(PawPalPlayerActionType.WalkDog);
        CommitState(true, false, true);
    }

    public void TrainActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        dog.ModifyNeed(PawPalDogNeed.Activity, -0.08f);
        dog.ModifyNeed(PawPalDogNeed.Water, -0.05f);
        if (dog.Focus < 10)
        {
            dog.ModifyStat(PawPalDogStatType.Focus, 1);
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
        actionProgress.Clear();
        catalogById.Clear();
        ownedItemById.Clear();
        dogEquipmentByDogId.Clear();

        dogs.Add(BuildStarterDog());
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
            Food01 = 0.82f,
            Water01 = 0.76f,
            Hygiene01 = 0.7f,
            Activity01 = 0.67f,
            Energy01 = 0.74f,
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
            Food01 = 0.9f,
            Water01 = 0.85f,
            Hygiene01 = 0.8f,
            Activity01 = 0.72f,
            Energy01 = 0.79f,
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
            Food01 = 1f,
            Water01 = 1f,
            Hygiene01 = 1f,
            Activity01 = 0.9f,
            Energy01 = 1f,
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
        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "dog_husky",
            DisplayName = "Husky",
            Category = PawPalItemCategory.Dogs,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 800,
            AppearsInInventory = false,
            DisabledInShop = true,
            DisabledReason = "Dog breeds are not purchasable in this milestone.",
            Description = "A playful premium dog breed with high energy and strong mobility.",
            ShopSpritePath = "UI/Figma/Shop/husky"
        });

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "dog_rottweiler",
            DisplayName = "Rottweiler",
            Category = PawPalItemCategory.Dogs,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 600,
            AppearsInInventory = false,
            DisabledInShop = true,
            DisabledReason = "Dog breeds are not purchasable in this milestone.",
            Description = "A sturdy breed card that stays visible while kennel logic is deferred.",
            ShopSpritePath = "UI/Figma/Shop/rottweiler"
        });

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
            InventorySpritePath = "UI/Figma/Shop/premium_food",
            InventoryCardTheme = InventoryCardTheme.Roll
        });

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "toy_bubble_bone",
            DisplayName = "Bubble Bone",
            Category = PawPalItemCategory.Toys,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 150,
            StarterOwned = true,
            StarterQuantity = 1,
            Description = "An owned starter toy. Tapping it in inventory spawns a room toy for the dogs to discover.",
            ShopSpritePath = "UI/Figma/Shop/bubble_bone",
            InventorySpritePath = "UI/Figma/HomeInventory/item_bone",
            InventoryCardTheme = InventoryCardTheme.Bone,
            RoomPrefabResourcePath = "PawPal/RoomPrefabs/Bone_1",
            ToyTint = new Color(0.88f, 0.34f, 0.58f, 1f)
        });

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "toy_bouncy_ball",
            DisplayName = "Bouncy Ball",
            Category = PawPalItemCategory.Toys,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 150,
            Description = "A room ball toy that dogs can pick up and move around on their own.",
            ShopSpritePath = "UI/Figma/Shop/bouncy_ball",
            InventorySpritePath = "UI/Figma/HomeInventory/item_ball",
            InventoryCardTheme = InventoryCardTheme.Ball,
            RoomPrefabResourcePath = "PawPal/RoomPrefabs/Big_ball_1",
            ToyInteractionMode = PawPalToyInteractionMode.PawHitRoll,
            ToyTint = new Color(0.82f, 0.84f, 0.24f, 1f)
        });

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "toy_turbo_roll",
            DisplayName = "Turbo Roll",
            Category = PawPalItemCategory.Toys,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 150,
            Description = "A rolling toy that drops into the room with physics and gives the dogs another object to chase.",
            ShopSpritePath = "UI/Figma/Shop/turbo_roll",
            InventorySpritePath = "UI/Figma/HomeInventory/item_roll",
            InventoryCardTheme = InventoryCardTheme.Roll,
            RoomPrefabResourcePath = "PawPal/RoomPrefabs/Wheel",
            ToyTint = new Color(0.40f, 0.60f, 0.86f, 1f)
        });

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "toy_star_ball",
            DisplayName = "Star Ball",
            Category = PawPalItemCategory.Toys,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 150,
            Description = "A second ball-style toy tier that uses the same room ball family in this milestone.",
            ShopSpritePath = "UI/Figma/Shop/star_toy",
            InventorySpritePath = "UI/Figma/HomeInventory/item_ball",
            InventoryCardTheme = InventoryCardTheme.Ball,
            RoomPrefabResourcePath = "PawPal/RoomPrefabs/Big_ball_1",
            ToyInteractionMode = PawPalToyInteractionMode.PawHitRoll,
            ToyTint = new Color(0.93f, 0.83f, 0.36f, 1f)
        });

        AddCatalogItem(BuildCollarDefinition("collar_ocean_band", "Ocean Band", 50, "UI/Figma/HomeInventory/item_band", "PawPal/Accessories/CollarSimple_C2", InventoryCardTheme.Band, true, Color.white));
        AddCatalogItem(BuildCollarDefinition("collar_maple_loop", "Maple Loop", 50, "UI/Figma/Shop/maple_loop", "PawPal/Accessories/CollarSimple_C1", InventoryCardTheme.Loop, false, new Color(0.78f, 0.64f, 0.50f, 1f)));
        AddCatalogItem(BuildCollarDefinition("collar_cherry_charm", "Cherry Charm", 150, "UI/Figma/Shop/cherry_charm", "PawPal/Accessories/CollarSimple_C1", InventoryCardTheme.Charm, false, Color.white));

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "clothing_basic",
            DisplayName = "Coming soon",
            Category = PawPalItemCategory.Clothing,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 100,
            AppearsInInventory = false,
            DisabledInShop = true,
            DisabledReason = "Clothing stays visible but disabled in this milestone.",
            Description = "Clothing is still presentation-only for now."
        });

        AddCatalogItem(new PawPalCatalogItemDefinition
        {
            Id = "furniture_basic",
            DisplayName = "Coming soon",
            Category = PawPalItemCategory.Furniture,
            CurrencyType = PawPalCurrencyType.Premium,
            Price = 350,
            AppearsInInventory = false,
            DisabledInShop = true,
            DisabledReason = "Furniture stays visible but disabled in this milestone.",
            Description = "Furniture purchasing is intentionally deferred."
        });
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
        bool changed = false;

        for (int i = 0; i < dogs.Count; i++)
        {
            PawPalDogState dog = dogs[i];
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Food, FoodDrainPerHour * deltaHours);
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Water, WaterDrainPerHour * deltaHours);
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Hygiene, HygieneDrainPerHour * deltaHours);
            changed |= ApplyNeedDrain(dog, PawPalDogNeed.Activity, ActivityDrainPerHour * deltaHours);

            float nextEnergy = Mathf.Clamp01(dog.Energy01 - (EnergyDrainPerHour * deltaHours));
            if (!Mathf.Approximately(nextEnergy, dog.Energy01))
            {
                dog.Energy01 = nextEnergy;
                changed = true;
            }
        }

        if (changed)
        {
            NotifyStateChanged();
        }
    }

    private bool ApplyNeedDrain(PawPalDogState dog, PawPalDogNeed need, float drain)
    {
        float current = dog.GetNeed(need);
        float next = Mathf.Clamp01(current - drain);
        if (Mathf.Approximately(current, next))
        {
            return false;
        }

        dog.SetNeed(need, next);
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
            dog.Energy01 = Mathf.Clamp01(dog.Energy01 + 0.12f);
        }
        else
        {
            dog.ModifyNeed(PawPalDogNeed.Food, 0.35f);
            dog.Energy01 = Mathf.Clamp01(dog.Energy01 + 0.08f);
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
        dog.Energy01 = Mathf.Clamp01(dog.Energy01 + 0.04f);
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
            dogs.Add(dogState);
            EnsureDogEquipmentState(dogState.Id);
        }
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
        saveData.ActiveDogIndex = activeDogIndex;
        saveData.NextDailyResetUtcTicks = nextDailyResetUtc.Ticks;
        saveData.LastProcessedTrainerMilestoneLevel = lastProcessedTrainerMilestoneLevel;

        for (int i = 0; i < dogs.Count; i++)
        {
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

        for (int i = 0; i < pendingUnsupportedTrainerMilestoneLevels.Count; i++)
        {
            saveData.PendingUnsupportedTrainerMilestoneLevels.Add(pendingUnsupportedTrainerMilestoneLevels[i]);
        }

        return saveData;
    }

    private void RestoreFromSaveData(PawPalSaveData saveData)
    {
        trainerState = CloneTrainerState(saveData.TrainerState);

        dogs.Clear();
        for (int i = 0; i < saveData.Dogs.Count; i++)
        {
            dogs.Add(CloneDogState(saveData.Dogs[i]));
        }

        if (dogs.Count == 0)
        {
            dogs.Add(BuildStarterDog());
        }

        bool backfilledDogNeeds = BackfillIdenticalDogNeedsIfNeeded();

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

        activeDogIndex = Mathf.Clamp(saveData.ActiveDogIndex, 0, Mathf.Max(0, dogs.Count - 1));
        inventoryRevision++;
        shopRevision++;

        if (backfilledDogNeeds)
        {
            SaveProfile();
        }
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
            Food01 = source.Food01,
            Water01 = source.Water01,
            Hygiene01 = source.Hygiene01,
            Activity01 = source.Activity01,
            Energy01 = source.Energy01,
            Endurance = source.Endurance,
            Mobility = source.Mobility,
            Speed = source.Speed,
            Focus = source.Focus
        };
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
            && firstDog.Energy01 == secondDog.Energy01;
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
        dog.Food01 = Mathf.Clamp01(food);
        dog.Water01 = Mathf.Clamp01(water);
        dog.Hygiene01 = Mathf.Clamp01(hygiene);
        dog.Activity01 = Mathf.Clamp01(activity);
        dog.Energy01 = Mathf.Clamp01(energy);
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
