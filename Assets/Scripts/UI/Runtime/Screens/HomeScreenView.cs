using UnityEngine;

public class HomeScreenView : AppScreenViewBase
{
    private enum HomeDetailMode
    {
        Collapsed,
        Stats,
        Inventory
    }

    private const float BasePanelX = 72f;
    private const float BasePanelY = 669f;
    private const float BasePanelHeight = 117f;
    private const float StatsPanelX = 74f;
    private const float StatsPanelY = 535f;
    private const float StatsPanelHeight = 251f;
    private const float BaseAddonsY = 626f;
    private const float StatsAddonsY = 492f;
    private const float InventoryAddonsY = 442f;
    private const float InventoryRegionX = 69f;
    private const float InventoryRegionY = 485f;

    private RectTransform exactFrame;
    private RectTransform addonsRect;
    private RectTransform dogDetailsRect;
    private RectTransform inventoryNameRect;
    private RectTransform inventoryPanelRect;
    private TrainerLevelWidgetView trainerLevel;
    private HomeAddonsView addons;
    private DogDetailsWidgetView dogDetails;
    private InventoryNameBarView inventoryNameBar;
    private InventoryPanelView inventoryPanel;
    private HomeDetailMode detailMode;

    protected override bool UseScreenContainer
    {
        get { return false; }
    }

    private void OnEnable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged += HandleRuntimeStateChanged;
        }

        RefreshRuntimeState();
    }

    private void OnDisable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged -= HandleRuntimeStateChanged;
        }
    }

    protected override void BuildContent()
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        exactFrame = UiFactory.CreateRect("HomeMainFrame", root);
        exactFrame.anchorMin = new Vector2(0.5f, 1f);
        exactFrame.anchorMax = new Vector2(0.5f, 1f);
        exactFrame.pivot = new Vector2(0.5f, 1f);
        exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        exactFrame.anchoredPosition = Vector2.zero;

        trainerLevel = CreateNode<TrainerLevelWidgetView>("TrainerLevel", 109f, 0f, 175f, 76f);
        trainerLevel.Initialize(5, 97f / 138f, sprites);

        addons = CreateNode<HomeAddonsView>("Addons", 107f, BaseAddonsY, 180f, 38f);
        addons.Initialize(sprites, ToggleInventoryPanel);
        addonsRect = addons.GetComponent<RectTransform>();

        dogDetails = CreateNode<DogDetailsWidgetView>("DogDetails", BasePanelX, BasePanelY, 246f, BasePanelHeight);
        dogDetails.Initialize(sprites, ToggleDetailsPanel);
        dogDetails.BindInteractions(SelectPreviousDog, SelectNextDog, HandleNeedPressed);
        dogDetailsRect = dogDetails.GetComponent<RectTransform>();

        inventoryNameBar = CreateNode<InventoryNameBarView>("InventoryNameBar", InventoryRegionX, InventoryRegionY, 255f, 44f);
        inventoryNameBar.Initialize(sprites);
        inventoryNameBar.BindDogSwitching(SelectPreviousDog, SelectNextDog);
        inventoryNameRect = inventoryNameBar.GetComponent<RectTransform>();

        inventoryPanel = CreateNode<InventoryPanelView>("InventoryPanel", InventoryRegionX, 528f, 255f, 258f);
        inventoryPanel.Initialize(sprites);
        inventoryPanel.Configure(HandleInventoryGetMoreTapped, HandleInventoryCollarTapped, HandleInventoryToyTapped);
        inventoryPanelRect = inventoryPanel.GetComponent<RectTransform>();

        ApplyDetailMode();
        RefreshRuntimeState();
    }

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            exactFrame.anchorMin = new Vector2(0.5f, 1f);
            exactFrame.anchorMax = new Vector2(0.5f, 1f);
            exactFrame.pivot = new Vector2(0.5f, 1f);
            exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
            exactFrame.anchoredPosition = Vector2.zero;
        }

        ApplyDetailMode();
    }

    private T CreateNode<T>(string name, float x, float y, float width, float height) where T : Component
    {
        RectTransform rect = UiFactory.CreateRect(name, exactFrame);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
        return rect.gameObject.AddComponent<T>();
    }

    private void ToggleDetailsPanel()
    {
        detailMode = detailMode == HomeDetailMode.Stats ? HomeDetailMode.Collapsed : HomeDetailMode.Stats;
        ApplyDetailMode();
    }

    private void ToggleInventoryPanel()
    {
        detailMode = detailMode == HomeDetailMode.Inventory ? HomeDetailMode.Collapsed : HomeDetailMode.Inventory;
        ApplyDetailMode();
    }

    private void ApplyDetailMode()
    {
        if (dogDetails != null)
        {
            dogDetails.SetExpanded(detailMode == HomeDetailMode.Stats);
            dogDetails.gameObject.SetActive(detailMode != HomeDetailMode.Inventory);
        }

        if (dogDetailsRect != null)
        {
            bool statsExpanded = detailMode == HomeDetailMode.Stats;
            dogDetailsRect.sizeDelta = new Vector2(246f, statsExpanded ? StatsPanelHeight : BasePanelHeight);
            dogDetailsRect.anchoredPosition = new Vector2(statsExpanded ? StatsPanelX : BasePanelX, -(statsExpanded ? StatsPanelY : BasePanelY));
        }

        if (addonsRect != null)
        {
            float addonsY = BaseAddonsY;
            if (detailMode == HomeDetailMode.Stats)
            {
                addonsY = StatsAddonsY;
            }
            else if (detailMode == HomeDetailMode.Inventory)
            {
                addonsY = InventoryAddonsY;
            }

            addonsRect.anchoredPosition = new Vector2(107f, -addonsY);
        }

        if (addons != null)
        {
            addons.SetInventorySelected(detailMode == HomeDetailMode.Inventory);
        }

        if (inventoryNameBar != null)
        {
            inventoryNameBar.gameObject.SetActive(detailMode == HomeDetailMode.Inventory);
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.gameObject.SetActive(detailMode == HomeDetailMode.Inventory);
            inventoryPanel.ResetScrollPosition();
        }
    }

    private void HandleRuntimeStateChanged()
    {
        RefreshRuntimeState();
    }

    private void RefreshRuntimeState()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        if (trainerLevel != null)
        {
            trainerLevel.SetState(runtime.TrainerState.Level, runtime.GetTrainerLevelProgress01());
        }

        PawPalDogState activeDog = runtime.ActiveDog;
        if (dogDetails != null)
        {
            dogDetails.SetDogState(activeDog);
        }

        if (inventoryNameBar != null && activeDog != null)
        {
            inventoryNameBar.SetDogName(activeDog.DisplayName);
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.RefreshRuntimeState(runtime);
        }
    }

    private void SelectPreviousDog()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.SelectPreviousDog();
        }
    }

    private void SelectNextDog()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.SelectNextDog();
        }
    }

    private void HandleNeedPressed(PawPalDogNeed need)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        switch (need)
        {
            case PawPalDogNeed.Food:
                runtime.TryFeedActiveDog();
                break;
            case PawPalDogNeed.Water:
                runtime.GiveWaterToActiveDog();
                break;
            case PawPalDogNeed.Hygiene:
                runtime.CleanActiveDog();
                break;
            case PawPalDogNeed.Activity:
                runtime.PlayWithActiveDog();
                break;
        }
    }

    private void HandleInventoryGetMoreTapped()
    {
        if (shell != null)
        {
            shell.ShowScreen(AppScreenId.Shop);
        }
    }

    private void HandleInventoryCollarTapped(string itemId)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || runtime.ActiveDog == null)
        {
            return;
        }

        runtime.TryEquipCollar(runtime.ActiveDog.Id, itemId);
    }

    private void HandleInventoryToyTapped(string itemId)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        runtime.TrySpawnToy(itemId);
    }
}
