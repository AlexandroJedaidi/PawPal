using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
    public Color ToyTint = Color.white;
    public string CollarPrefabResourcePath;
    public Color CollarTint = Color.white;
    public Vector3 CollarLocalPosition;
    public Quaternion CollarLocalRotation = Quaternion.identity;
    public Vector3 CollarLocalScale = Vector3.one;
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
    private const string SecondaryStarterDogId = "miso";
    private const string StarterCollarItemId = "collar_ocean_band";
    private const string BasicFoodItemId = "food_basic";
    private const string PremiumFoodItemId = "food_premium";

    private static readonly int[] TrainerLevelRequirements =
    {
        60, 120, 180, 240, 300, 360, 425, 484, 542, 602,
        662, 722, 780, 834, 884, 928, 975, 1014, 1054, 1086,
        1119, 1152, 1187, 1222, 1259, 1297, 1336, 1376, 1417, 1445,
        1474, 1504, 1534, 1565, 1596, 1620, 1644, 1669, 1694, 1719,
        1736, 1754, 1771, 1789, 1807, 1816, 1825, 1834, 1843, 1852
    };

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

    [SerializeField] private float secondsPerGameHour = DefaultSecondsPerGameHour;

    private PawPalTrainerState trainerState;
    private PawPalDogSceneBridge sceneBridge;
    private int activeDogIndex;
    private int inventoryRevision;
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

    public bool TryFeedActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return false;
        }

        PawPalCatalogItemDefinition foodItem = null;
        if (GetItemQuantity(BasicFoodItemId) > 0)
        {
            foodItem = GetCatalogItem(BasicFoodItemId);
        }
        else if (GetItemQuantity(PremiumFoodItemId) > 0)
        {
            foodItem = GetCatalogItem(PremiumFoodItemId);
        }

        if (foodItem == null)
        {
            return false;
        }

        ConsumeItemQuantity(foodItem.Id, 1);
        ApplyFoodToActiveDog(foodItem);
        ApplyActionRewards(PawPalPlayerActionType.FeedDog, 5, 0, 1);
        CommitState(true, true, true);
        return true;
    }

    public void FeedActiveDog()
    {
        TryFeedActiveDog();
    }

    public void GiveWaterToActiveDog()
    {
        PawPalDogState dog = ActiveDog;
        if (dog == null)
        {
            return;
        }

        dog.ModifyNeed(PawPalDogNeed.Water, 0.4f);
        dog.Energy01 = Mathf.Clamp01(dog.Energy01 + 0.04f);
        ApplyActionRewards(PawPalPlayerActionType.GiveWater, 5, 0, 1);
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
        ApplyActionRewards(PawPalPlayerActionType.CleanDog, 5, 0, 1);
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

        ApplyActionRewards(PawPalPlayerActionType.PlayWithToy, 10, 0, 2);
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
        ApplyActionRewards(PawPalPlayerActionType.WalkDog, 15, 0, 3);
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

        ApplyActionRewards(PawPalPlayerActionType.TrainTrick, 10, 0, 2);
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
            NotifyStateChanged();
        }

        return spawned;
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
        int level = Mathf.Clamp(trainerState.Level, 1, TrainerLevelRequirements.Length);
        int required = TrainerLevelRequirements[level - 1];
        if (required <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)trainerState.CurrentLevelExperience / required);
    }

    public string GetTrainerExperienceText()
    {
        int level = Mathf.Clamp(trainerState.Level, 1, TrainerLevelRequirements.Length);
        int required = TrainerLevelRequirements[level - 1];
        return trainerState.CurrentLevelExperience + " / " + required + " XP";
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
        AddDefaultRosterDogs();
        BuildCatalog();
        ValidateCatalogDefinitions();
        BuildPointPackages();
        InitializeOwnedItemsFromCatalog();
        EnsureDogEquipmentState(StarterDogId).EquippedCollarItemId = StarterCollarItemId;
        nextDailyResetUtc = DateTime.UtcNow.Date.AddDays(1d);
        GenerateDailyTasks();
        inventoryRevision = 1;
        shopRevision = 1;
        activeDogIndex = 0;
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

    private void ApplyActionRewards(PawPalPlayerActionType actionType, int experience, int basicCurrency, int clubExperience)
    {
        AddTrainerExperience(experience);
        trainerState.BasicCurrency += basicCurrency;
        trainerState.TotalBasicCurrencyEarned += basicCurrency;
        trainerState.ClubExperience += clubExperience;
        RecordActionProgress(actionType);
    }

    private void AddTrainerExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        trainerState.CurrentLevelExperience += amount;

        while (trainerState.Level <= TrainerLevelRequirements.Length)
        {
            int requirement = TrainerLevelRequirements[Mathf.Clamp(trainerState.Level - 1, 0, TrainerLevelRequirements.Length - 1)];
            if (trainerState.CurrentLevelExperience < requirement)
            {
                break;
            }

            trainerState.CurrentLevelExperience -= requirement;
            trainerState.Level++;
            ApplyLevelReward(trainerState.Level);

            if (trainerState.Level > TrainerLevelRequirements.Length)
            {
                trainerState.Level = TrainerLevelRequirements.Length;
                trainerState.CurrentLevelExperience = 0;
                break;
            }
        }
    }

    private void ApplyLevelReward(int level)
    {
        switch (level)
        {
            case 4:
                trainerState.PremiumCurrency += 50;
                break;
            case 5:
                trainerState.BasicCurrency += 200;
                trainerState.TotalBasicCurrencyEarned += 200;
                break;
            case 6:
                UnlockDog("miso");
                break;
            case 10:
                UnlockDog("suki");
                break;
            case 15:
                trainerState.PremiumCurrency += 100;
                break;
        }
    }

    private void UnlockDog(string dogId)
    {
        if (HasDog(dogId))
        {
            return;
        }

        if (dogId == "miso")
        {
            dogs.Add(BuildMisoDog());
            EnsureDogEquipmentState("miso");
        }
        else if (dogId == "suki")
        {
            dogs.Add(BuildSukiDog());
            EnsureDogEquipmentState("suki");
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

        AddTrainerExperience(15);
        trainerState.BasicCurrency += 50;
        trainerState.TotalBasicCurrencyEarned += 50;
        trainerState.ClubExperience += 3;
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

        AddDefaultRosterDogs();

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

        EnsureDailyTasksPresent(false);

        activeDogIndex = Mathf.Clamp(saveData.ActiveDogIndex, 0, Mathf.Max(0, dogs.Count - 1));
        inventoryRevision++;
        shopRevision++;
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

    private void AddDefaultRosterDogs()
    {
        if (!HasDog(SecondaryStarterDogId))
        {
            dogs.Add(BuildMisoDog());
        }

        for (int i = 0; i < dogs.Count; i++)
        {
            EnsureDogEquipmentState(dogs[i].Id);
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
