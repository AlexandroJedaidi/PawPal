using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private const float BasePanelBottomOffset = 0f;
    private const float BaseAddonsBottomOffset = 122f;
    private const float StatsAddonsBottomOffset = 256f;
    private const float InventoryNameBottomOffset = 257f;
    private const float InventoryAddonsBottomOffset = 306f;
    private const float NeedPopupBottomOffset = 130f;
    private const float VoicePanelX = 58f;
    private const float VoicePanelY = 394f;
    private const float VoicePanelWidth = 276f;
    private const float VoicePanelHeight = 178f;
    private const float BottomHudReferenceHeight = UiTheme.ReferenceContentHeight;

    private RectTransform exactFrame;
    private RectTransform bottomHudFrame;
    private RectTransform addonsRect;
    private RectTransform dogDetailsRect;
    private RectTransform inventoryNameRect;
    private RectTransform inventoryPanelRect;
    private HomeAddonsView addons;
    private DogDetailsWidgetView dogDetails;
    private InventoryNameBarView inventoryNameBar;
    private InventoryPanelView inventoryPanel;
    private DogVoiceCommandDirector voiceCommandDirector;
    private PawPalVoiceInputController voiceController;
    private HomeVoicePanelView voicePanel;
    private RectTransform voicePanelRect;
    private RectTransform needPopup;
    private TextMeshProUGUI needPopupLabel;
    private Coroutine needPopupRoutine;
    private HomeDetailMode detailMode;
    private ResponsiveFigmaFrameLayout frameLayout;
    private bool voiceDogSwitchLockActive;

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
        CloseVoicePanel();

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

        bottomHudFrame = UiFactory.CreateRect("HomeBottomHudFrame", root);
        bottomHudFrame.anchorMin = new Vector2(0.5f, 0f);
        bottomHudFrame.anchorMax = new Vector2(0.5f, 0f);
        bottomHudFrame.pivot = new Vector2(0.5f, 0f);
        bottomHudFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, BottomHudReferenceHeight);
        bottomHudFrame.anchoredPosition = Vector2.zero;

        voiceCommandDirector = gameObject.AddComponent<DogVoiceCommandDirector>();
        voiceController = gameObject.AddComponent<PawPalVoiceInputController>();
        voiceController.Initialize(voiceCommandDirector);

        addons = CreateNode<HomeAddonsView>("Addons", bottomHudFrame, 107f, BaseAddonsY, 180f, 38f);
        addons.Initialize(sprites, ToggleVoicePanel, ToggleInventoryPanel, EnterPhotoMode);
        addonsRect = addons.GetComponent<RectTransform>();

        dogDetails = CreateNode<DogDetailsWidgetView>("DogDetails", bottomHudFrame, BasePanelX, BasePanelY, 246f, BasePanelHeight);
        dogDetails.Initialize(sprites, ToggleDetailsPanel);
        dogDetails.BindInteractions(SelectPreviousDog, SelectNextDog, HandleNeedPressed);
        dogDetailsRect = dogDetails.GetComponent<RectTransform>();

        inventoryNameBar = CreateNode<InventoryNameBarView>("InventoryNameBar", bottomHudFrame, InventoryRegionX, InventoryRegionY, 255f, 44f);
        inventoryNameBar.Initialize(sprites);
        inventoryNameBar.BindDogSwitching(SelectPreviousDog, SelectNextDog);
        inventoryNameRect = inventoryNameBar.GetComponent<RectTransform>();

        inventoryPanel = CreateNode<InventoryPanelView>("InventoryPanel", bottomHudFrame, InventoryRegionX, 528f, 255f, 258f);
        inventoryPanel.Initialize(sprites);
        inventoryPanel.Configure(HandleInventoryGetMoreTapped, HandleInventoryCollarTapped, HandleInventoryToyTapped);
        inventoryPanelRect = inventoryPanel.GetComponent<RectTransform>();

        voicePanel = CreateNode<HomeVoicePanelView>("VoicePanel", bottomHudFrame, VoicePanelX, VoicePanelY, VoicePanelWidth, VoicePanelHeight);
        voicePanel.Initialize(voiceController, CloseVoicePanel);
        voicePanelRect = voicePanel.GetComponent<RectTransform>();
        voicePanel.gameObject.SetActive(false);

        BuildNeedPopup();
        ApplyDetailMode();
        RefreshRuntimeState();
    }

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            frameLayout = ResponsiveFigmaFrame.Apply(root, exactFrame);
        }

        ApplyBottomHudLayout(root);
        ApplyDetailMode();
    }

    private T CreateNode<T>(string name, RectTransform parent, float x, float y, float width, float height) where T : Component
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
        return rect.gameObject.AddComponent<T>();
    }

    private void ToggleDetailsPanel()
    {
        CloseVoicePanel();
        detailMode = detailMode == HomeDetailMode.Stats ? HomeDetailMode.Collapsed : HomeDetailMode.Stats;
        ApplyDetailMode();
    }

    private void ToggleInventoryPanel()
    {
        CloseVoicePanel();
        detailMode = detailMode == HomeDetailMode.Inventory ? HomeDetailMode.Collapsed : HomeDetailMode.Inventory;
        ApplyDetailMode();
    }

    private void ToggleVoicePanel()
    {
        if (voicePanel == null)
        {
            return;
        }

        bool shouldShow = !voicePanel.gameObject.activeSelf;
        if (!shouldShow)
        {
            CloseVoicePanel();
            return;
        }

        AcquireVoiceDogSwitchLock();
        detailMode = HomeDetailMode.Collapsed;
        ApplyDetailMode();
        if (voiceController != null)
        {
            voiceController.RefreshForActiveDog();
        }

        voicePanel.gameObject.SetActive(true);
        voicePanel.Refresh();
    }

    private void CloseVoicePanel()
    {
        if (voiceController != null)
        {
            voiceController.CancelActiveOperation();
        }

        if (voicePanel != null)
        {
            voicePanel.gameObject.SetActive(false);
        }

        ReleaseVoiceDogSwitchLock();
    }

    private void EnterPhotoMode()
    {
        CloseVoicePanel();
        detailMode = HomeDetailMode.Collapsed;
        ApplyDetailMode();

        if (shell != null)
        {
            shell.EnterPhotoMode();
        }
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
            float panelHeight = statsExpanded ? StatsPanelHeight : BasePanelHeight;
            dogDetailsRect.sizeDelta = new Vector2(246f, panelHeight);
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

        if (inventoryNameRect != null)
        {
            inventoryNameRect.anchoredPosition = new Vector2(InventoryRegionX, -InventoryRegionY);
        }

        if (inventoryPanelRect != null)
        {
            inventoryPanelRect.anchoredPosition = new Vector2(InventoryRegionX, -528f);
        }

        if (voicePanelRect != null)
        {
            voicePanelRect.anchoredPosition = new Vector2(VoicePanelX, -VoicePanelY);
        }

        if (needPopup != null)
        {
            needPopup.anchoredPosition = new Vector2(0f, -GetBottomAnchoredY(52f, NeedPopupBottomOffset));
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

        RefreshVoicePanelForDogSwitch();
    }

    private void SelectPreviousDog()
    {
        if (IsDogSwitchBlockedByOverlay())
        {
            return;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.SelectPreviousDog();
            DogCycleCamera.TryFocusRuntimeActiveDogFromSelection();
            RefreshVoicePanelForDogSwitch();
        }
    }

    private void SelectNextDog()
    {
        if (IsDogSwitchBlockedByOverlay())
        {
            return;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.SelectNextDog();
            DogCycleCamera.TryFocusRuntimeActiveDogFromSelection();
            RefreshVoicePanelForDogSwitch();
        }
    }

    private bool IsDogSwitchBlockedByOverlay()
    {
        return (voicePanel != null && voicePanel.gameObject.activeSelf)
            || (shell != null && shell.IsPhotoModeActive);
    }

    private void AcquireVoiceDogSwitchLock()
    {
        if (voiceDogSwitchLockActive)
        {
            return;
        }

        DogCycleCamera.PushDogSwitchLock();
        voiceDogSwitchLockActive = true;
    }

    private void ReleaseVoiceDogSwitchLock()
    {
        if (!voiceDogSwitchLockActive)
        {
            return;
        }

        DogCycleCamera.PopDogSwitchLock();
        voiceDogSwitchLockActive = false;
    }

    private void RefreshVoicePanelForDogSwitch()
    {
        if (voiceController != null)
        {
            voiceController.RefreshForActiveDog();
        }

        if (voicePanel != null && voicePanel.gameObject.activeSelf)
        {
            voicePanel.Refresh();
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
                if (!runtime.HasFoodItemForActiveDog())
                {
                    ShowNeedPopup("Your dog needs food items.");
                    return;
                }

                runtime.TryStartFoodNeedInteraction();
                break;
            case PawPalDogNeed.Water:
                runtime.TryStartWaterNeedInteraction();
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

        string activeDogId = runtime.ActiveDog.Id;
        if (runtime.GetEquippedCollarItemId(activeDogId) == itemId)
        {
            runtime.TryUnequipCollar(activeDogId, itemId);
            return;
        }

        runtime.TryEquipCollar(activeDogId, itemId);
    }

    private void HandleInventoryToyTapped(string itemId)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        if (runtime.IsToyActiveInScene(itemId))
        {
            runtime.TryRemoveToyFromScene(itemId);
            return;
        }

        if (runtime.TrySpawnToyForThrow(itemId))
        {
            detailMode = HomeDetailMode.Collapsed;
            ApplyDetailMode();
            RefreshRuntimeState();
        }
    }

    private void BuildNeedPopup()
    {
        needPopup = UiFactory.CreateRect("NeedPopup", bottomHudFrame);
        needPopup.anchorMin = new Vector2(0.5f, 1f);
        needPopup.anchorMax = new Vector2(0.5f, 1f);
        needPopup.pivot = new Vector2(0.5f, 0.5f);
        needPopup.sizeDelta = new Vector2(254f, 52f);
        needPopup.anchoredPosition = new Vector2(0f, -GetBottomAnchoredY(52f, NeedPopupBottomOffset));

        Image background = needPopup.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.color = UiTheme.NavBackgroundCream;
        background.raycastTarget = false;

        Outline outline = needPopup.gameObject.AddComponent<Outline>();
        outline.effectColor = UiTheme.NavBrand;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        needPopupLabel = UiFactory.CreateLabel("Message", needPopup, string.Empty, 15, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        needPopupLabel.font = UiTheme.NavExtraBoldFont;
        needPopupLabel.textWrappingMode = TextWrappingModes.Normal;
        needPopupLabel.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Stretch(needPopupLabel.rectTransform, 12f, 8f, 12f, 8f);

        needPopup.gameObject.SetActive(false);
    }

    private void ApplyBottomHudLayout(RectTransform root)
    {
        if (root == null || bottomHudFrame == null)
        {
            return;
        }

        float availableWidth = root.rect.width > 0f ? root.rect.width : UiTheme.ReferenceWidth;
        float scale = UiTheme.GetHudVisualScale(availableWidth);

        bottomHudFrame.anchorMin = new Vector2(0.5f, 0f);
        bottomHudFrame.anchorMax = new Vector2(0.5f, 0f);
        bottomHudFrame.pivot = new Vector2(0.5f, 0f);
        bottomHudFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, BottomHudReferenceHeight);
        bottomHudFrame.anchoredPosition = Vector2.zero;
        bottomHudFrame.localScale = new Vector3(scale, scale, 1f);
    }

    private float GetBottomAnchoredY(float height, float bottomOffset)
    {
        float frameHeight = frameLayout.VisibleLogicalHeight > 0f ? frameLayout.VisibleLogicalHeight : UiTheme.ReferenceContentHeight;
        return Mathf.Max(0f, frameHeight - height - bottomOffset);
    }

    private void ShowNeedPopup(string message)
    {
        if (needPopup == null || needPopupLabel == null)
        {
            return;
        }

        if (needPopupRoutine != null)
        {
            StopCoroutine(needPopupRoutine);
        }

        needPopupLabel.text = message;
        needPopup.gameObject.SetActive(true);
        needPopupRoutine = StartCoroutine(HideNeedPopupAfterDelay(2f));
    }

    private IEnumerator HideNeedPopupAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, delay));
        if (needPopup != null)
        {
            needPopup.gameObject.SetActive(false);
        }

        needPopupRoutine = null;
    }
}
