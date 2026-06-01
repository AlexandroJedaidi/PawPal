using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeScreenView : AppScreenViewBase
{
    private const float WhistlePulseDuration = 1f;

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
    private const float BaseAddonsBottomOffset = 98f;
    private const float StatsAddonsBottomOffset = 232f;
    private const float InventoryNameBottomOffset = 257f;
    private const float InventoryAddonsBottomOffset = 282f;
    private const float AddonsHeight = 38f;
    private const float InventoryNameHeight = 44f;
    private const float InventoryPanelHeight = 258f;
    private const float NeedPopupBottomOffset = 130f;
    private const float BottomHudReferenceHeight = UiTheme.ReferenceContentHeight;

    private RectTransform exactFrame;
    private RectTransform bottomHudFrame;
    private RectTransform topCameraButtonRect;
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
    private PawPalTrainingController trainingController;
    private PawPalTrainingModeView trainingView;
    private RectTransform needPopup;
    private TextMeshProUGUI needPopupLabel;
    private Coroutine needPopupRoutine;
    private Coroutine whistlePulseRoutine;
    private HomeDetailMode detailMode;
    private ResponsiveFigmaFrameLayout frameLayout;
    private bool menuDogSwitchLockActive;
    private bool whistlePulseActive;

    protected override bool UseScreenContainer
    {
        get { return false; }
    }

    public bool IsVoiceMicrophoneEnabled
    {
        get { return voiceController != null && voiceController.IsMicEnabled; }
    }

    private void OnEnable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged += HandleRuntimeStateChanged;
        }

        RefreshRuntimeState();
        RefreshAddonSelection();
    }

    private void OnDisable()
    {
        ReleaseMenuDogSwitchLock(false);
        CloseTrainingMode();
        DisableMicrophoneMode();
        if (whistlePulseRoutine != null)
        {
            StopCoroutine(whistlePulseRoutine);
            whistlePulseRoutine = null;
        }

        whistlePulseActive = false;

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
        voiceController.StateChanged += HandleVoiceControllerStateChanged;
        voiceController.FeedbackRequested += ShowNeedPopup;
        voiceController.CommandExecutionRequested += HandleVoiceCommandExecutionRequested;

        BuildTopCameraButton(exactFrame);

        addons = CreateNode<HomeAddonsView>("Addons", bottomHudFrame, 107f, BaseAddonsY, 180f, AddonsHeight);
        addons.Initialize(sprites, ToggleMicrophoneMode, ToggleInventoryPanel, EnterDogInteractionByWhistle);
        addonsRect = addons.GetComponent<RectTransform>();

        dogDetails = CreateNode<DogDetailsWidgetView>("DogDetails", bottomHudFrame, BasePanelX, BasePanelY, 246f, BasePanelHeight);
        dogDetails.Initialize(sprites, ToggleDetailsPanel);
        dogDetails.BindInteractions(SelectPreviousDog, SelectNextDog, HandleNeedPressed);
        dogDetails.BindTrickRequested(OpenTrainingForTrick);
        dogDetailsRect = dogDetails.GetComponent<RectTransform>();

        inventoryNameBar = CreateNode<InventoryNameBarView>("InventoryNameBar", bottomHudFrame, InventoryRegionX, InventoryRegionY, 255f, InventoryNameHeight);
        inventoryNameBar.Initialize(sprites);
        inventoryNameBar.BindDogSwitching(SelectPreviousDog, SelectNextDog);
        inventoryNameRect = inventoryNameBar.GetComponent<RectTransform>();

        inventoryPanel = CreateNode<InventoryPanelView>("InventoryPanel", bottomHudFrame, InventoryRegionX, 528f, 255f, InventoryPanelHeight);
        inventoryPanel.Initialize(sprites);
        inventoryPanel.Configure(HandleInventoryGetMoreTapped, HandleInventoryCollarTapped, HandleInventoryToyTapped);
        inventoryPanelRect = inventoryPanel.GetComponent<RectTransform>();

        RectTransform trainingRect = UiFactory.CreateRect("TrainingOverlay", root);
        UiFactory.Stretch(trainingRect, 0f, 0f, 0f, 0f);
        trainingView = trainingRect.gameObject.AddComponent<PawPalTrainingModeView>();
        trainingView.Initialize(sprites);
        trainingController = gameObject.AddComponent<PawPalTrainingController>();
        trainingController.Initialize(trainingView);

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

    public void ToggleVoiceMicForInteraction()
    {
        CloseTrainingMode();
        if (voiceController == null)
        {
            return;
        }

        voiceController.ToggleMic();
        bool interactionMicEnabled = voiceController.IsMicEnabled && shell != null && shell.IsDogInteractionModeActive;
        voiceController.SetInteractionTrainingCommandsEnabled(interactionMicEnabled);
        RefreshAddonSelection();
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

    private void BuildTopCameraButton(RectTransform parent)
    {
        Image button = UiFactory.CreateImage("TopCameraButton", parent, UiTheme.CircleSprite, UiTheme.NavBackgroundCream);
        button.type = Image.Type.Simple;
        button.preserveAspect = false;
        button.rectTransform.anchorMin = new Vector2(1f, 1f);
        button.rectTransform.anchorMax = new Vector2(1f, 1f);
        button.rectTransform.pivot = new Vector2(1f, 1f);
        button.rectTransform.sizeDelta = new Vector2(42f, 42f);
        button.rectTransform.anchoredPosition = new Vector2(-16f, -18f);
        topCameraButtonRect = button.rectTransform;

        Shadow shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = UiTheme.NavShadow;
        shadow.effectDistance = new Vector2(0f, -1f);
        shadow.useGraphicAlpha = true;

        Image icon = UiFactory.CreateImage("Icon", button.rectTransform, sprites.GetResourceSprite("UI/Figma/HomeMain/icon_cam"), Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(26f, 26f);
        icon.rectTransform.anchoredPosition = Vector2.zero;

        UiFactory.AddButton(button.gameObject, EnterPhotoMode);
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

    private void ToggleMicrophoneMode()
    {
        CloseTrainingMode();
        if (voiceController != null)
        {
            voiceController.ToggleMic();
            if (!voiceController.IsMicEnabled)
            {
                voiceController.SetInteractionTrainingCommandsEnabled(false);
            }
        }

        RefreshAddonSelection();
    }

    private void DisableMicrophoneMode()
    {
        if (voiceController != null)
        {
            voiceController.CancelActiveOperation();
            voiceController.SetInteractionTrainingCommandsEnabled(false);
        }

        RefreshAddonSelection();
    }

    private void EnterDogInteractionByWhistle()
    {
        TriggerWhistlePulse();
        CloseTrainingMode();
        DisableMicrophoneMode();
        detailMode = HomeDetailMode.Collapsed;
        ApplyDetailMode();

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        string dogId = runtime != null && runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty;
        if (shell != null && shell.EnterDogInteractionMode(dogId, false))
        {
            RefreshAddonSelection();
        }
    }

    private void EnterPhotoMode()
    {
        CloseTrainingMode();
        DisableMicrophoneMode();
        detailMode = HomeDetailMode.Collapsed;
        ApplyDetailMode();

        if (shell != null)
        {
            if (addons != null)
            {
                addons.SetWhistleSelected(false);
            }

            shell.EnterPhotoMode();
            RefreshAddonSelection();
        }
    }

    private void ApplyDetailMode()
    {
        bool statsExpanded = detailMode == HomeDetailMode.Stats;
        bool dogMenuOpen = statsExpanded || detailMode == HomeDetailMode.Inventory;
        UpdateMenuDogSwitchLock(dogMenuOpen);

        if (dogDetails != null)
        {
            dogDetails.SetExpanded(statsExpanded);
            dogDetails.gameObject.SetActive(detailMode != HomeDetailMode.Inventory);
        }

        if (dogDetailsRect != null)
        {
            float panelHeight = statsExpanded ? StatsPanelHeight : BasePanelHeight;
            dogDetailsRect.sizeDelta = new Vector2(246f, panelHeight);
            dogDetailsRect.anchoredPosition = new Vector2(
                statsExpanded ? StatsPanelX : BasePanelX,
                -(statsExpanded ? StatsPanelY : BasePanelY));
        }

        if (addonsRect != null)
        {
            float addonsBottomOffset = BaseAddonsBottomOffset;
            if (detailMode == HomeDetailMode.Stats)
            {
                addonsBottomOffset = StatsAddonsBottomOffset;
            }
            else if (detailMode == HomeDetailMode.Inventory)
            {
                addonsBottomOffset = InventoryAddonsBottomOffset;
            }

            addonsRect.anchoredPosition = new Vector2(107f, -GetBottomAnchoredY(AddonsHeight, addonsBottomOffset));
        }

        if (inventoryNameRect != null)
        {
            inventoryNameRect.anchoredPosition = new Vector2(
                InventoryRegionX,
                -GetBottomAnchoredY(InventoryNameHeight, InventoryNameBottomOffset));
        }

        if (inventoryPanelRect != null)
        {
            inventoryPanelRect.anchoredPosition = new Vector2(
                InventoryRegionX,
                -528f);
        }

        if (needPopup != null)
        {
            needPopup.anchoredPosition = new Vector2(0f, -GetBottomAnchoredY(52f, NeedPopupBottomOffset));
        }

        if (addons != null)
        {
            RefreshAddonSelection();
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
        if (trainingController != null)
        {
            trainingController.Refresh();
        }
    }

    private void HandleVoiceControllerStateChanged()
    {
        if (shell != null)
        {
            shell.SetDogInteractionMicListening(voiceController != null && voiceController.IsMicEnabled);
        }

        RefreshAddonSelection();
    }

    private PawPalVoiceCommandExecutionResult HandleVoiceCommandExecutionRequested(PawPalResolvedVoiceCommand command)
    {
        if (command == null || shell == null)
        {
            return PawPalVoiceCommandExecutionResult.Unhandled();
        }

        if (command.Type == PawPalResolvedVoiceCommandType.CallDog)
        {
            CloseTrainingMode();
            detailMode = HomeDetailMode.Collapsed;
            ApplyDetailMode();

            bool started = shell.EnterDogInteractionMode(command.DogId, true);
            if (started && voiceController != null)
            {
                voiceController.SetInteractionTrainingCommandsEnabled(true);
                shell.SetDogInteractionMicListening(true);
            }

            string dogName = string.IsNullOrWhiteSpace(command.DogName) ? "Your dog" : command.DogName;
            return PawPalVoiceCommandExecutionResult.HandledResult(
                started,
                started ? dogName + " is coming closer." : dogName + " is busy right now.");
        }

        if (command.Type == PawPalResolvedVoiceCommandType.PerformTrick && shell.IsDogInteractionModeActive)
        {
            string message;
            bool success = shell.TryPerformDogInteractionVoiceTrick(command, out message);
            return PawPalVoiceCommandExecutionResult.HandledResult(success, message);
        }

        return PawPalVoiceCommandExecutionResult.Unhandled();
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

        if (voiceController != null)
        {
            voiceController.RefreshCommandCatalog();
        }
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
            RefreshVoiceCommandsForDogSwitch();
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
            RefreshVoiceCommandsForDogSwitch();
        }
    }

    private bool IsDogSwitchBlockedByOverlay()
    {
        return (trainingController != null && trainingController.IsOpen)
            || (shell != null && (shell.IsPhotoModeActive || shell.IsDogInteractionModeActive));
    }

    private void OpenTrainingForTrick(PawPalTrickId trickId)
    {
        DisableMicrophoneMode();
        detailMode = HomeDetailMode.Stats;
        ApplyDetailMode();
        if (trainingController != null)
        {
            trainingController.Open(trickId);
        }
    }

    private void CloseTrainingMode()
    {
        if (trainingController != null && trainingController.IsOpen)
        {
            trainingController.Close();
        }
    }

    private void UpdateMenuDogSwitchLock(bool shouldLock)
    {
        if (shouldLock)
        {
            AcquireMenuDogSwitchLock();
        }
        else
        {
            ReleaseMenuDogSwitchLock(true);
        }
    }

    private void AcquireMenuDogSwitchLock()
    {
        if (menuDogSwitchLockActive)
        {
            return;
        }

        DogCycleCamera.PushDogSwitchLock();
        menuDogSwitchLockActive = true;
    }

    private void ReleaseMenuDogSwitchLock(bool focusActiveDogAfterRelease)
    {
        if (!menuDogSwitchLockActive)
        {
            return;
        }

        DogCycleCamera.PopDogSwitchLock();
        menuDogSwitchLockActive = false;

        if (focusActiveDogAfterRelease)
        {
            DogCycleCamera.TryFocusRuntimeActiveDogFromSelection();
        }
    }

    private void RefreshVoiceCommandsForDogSwitch()
    {
        if (voiceController != null)
        {
            voiceController.RefreshCommandCatalog();
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

    private void RefreshAddonSelection()
    {
        if (addons == null)
        {
            return;
        }

        addons.SetMicSelected(voiceController != null && voiceController.IsMicEnabled);
        addons.SetInventorySelected(detailMode == HomeDetailMode.Inventory);
        addons.SetWhistleSelected((shell != null && shell.IsDogInteractionModeActive) || whistlePulseActive);
    }

    public void SetHomeChromeVisible(bool visible)
    {
        if (bottomHudFrame != null)
        {
            bottomHudFrame.gameObject.SetActive(visible);
        }

        if (topCameraButtonRect != null)
        {
            topCameraButtonRect.gameObject.SetActive(visible);
        }

        if (!visible && needPopup != null)
        {
            needPopup.gameObject.SetActive(false);
        }

        if (visible && voiceController != null)
        {
            voiceController.SetInteractionTrainingCommandsEnabled(false);
        }

        RefreshAddonSelection();
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

    private void TriggerWhistlePulse()
    {
        whistlePulseActive = true;
        RefreshAddonSelection();

        if (whistlePulseRoutine != null)
        {
            StopCoroutine(whistlePulseRoutine);
        }

        whistlePulseRoutine = StartCoroutine(ClearWhistlePulseAfterDelay(WhistlePulseDuration));
    }

    private IEnumerator ClearWhistlePulseAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, delay));
        whistlePulseActive = false;
        whistlePulseRoutine = null;
        RefreshAddonSelection();
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
