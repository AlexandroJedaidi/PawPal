using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PawPalTrainingTrickVisualState
{
    Available,
    Learned,
    Locked
}

public struct PawPalTrainingTrickPresentation
{
    public PawPalTrainingTrickPresentation(
        PawPalTrainingTrickVisualState visualState,
        PawPalTrickLearningStage learningStage,
        string stateText,
        string progressText,
        string actionText,
        float progress01,
        bool canAct)
    {
        VisualState = visualState;
        LearningStage = learningStage;
        StateText = stateText;
        ProgressText = progressText;
        ActionText = actionText;
        Progress01 = progress01;
        CanAct = canAct;
    }

    public readonly PawPalTrainingTrickVisualState VisualState;
    public readonly PawPalTrickLearningStage LearningStage;
    public readonly string StateText;
    public readonly string ProgressText;
    public readonly string ActionText;
    public readonly float Progress01;
    public readonly bool CanAct;
}

[RequireComponent(typeof(RectTransform))]
public sealed class PawPalTrainingModeView : MonoBehaviour
{
    private const string DefaultFeedback = "";
    private const float SheetWidth = 357f;
    private const float SheetHeight = 596f;
    private const float ScrollContentWidth = 321f;
    private const float SelectedPanelHeight = 208f;
    private const float TricksLabelTop = 220f;
    private const float TricksGridTop = 246f;
    private const float TrickCardWidth = 148f;
    private const float TrickCardHeight = 72f;
    private const float TrickCardColumnSpacing = 12f;
    private const float TrickCardRowSpacing = 10f;
    private const float ScrollBottomPadding = 18f;
    private const float ProgressCueDuration = 2.2f;
    private const float ProgressCueScreenLift = 92f;
    private const float ProgressCueHeadOffset = 0.08f;
    private const float ProgressCueEdgeMargin = 18f;
    private const float ProgressFillAnimDuration = 0.42f;

    private static readonly Color32 BackdropFill = new Color32(34, 21, 14, 46);
    private static readonly Color32 SheetFill = new Color32(252, 248, 232, 255);
    private static readonly Color32 SheetShadow = new Color32(51, 31, 20, 48);
    private static readonly Color32 PanelFill = new Color32(255, 252, 243, 255);
    private static readonly Color32 PanelBorder = new Color32(236, 223, 201, 255);
    private static readonly Color32 ProgressTrack = new Color32(235, 224, 203, 255);
    private static readonly Color32 PracticeFill = new Color32(228, 132, 107, 255);
    private static readonly Color32 PracticeDark = new Color32(138, 75, 60, 255);
    private static readonly Color32 PracticeShadow = new Color32(210, 177, 156, 255);
    private static readonly Color32 PrimaryButtonFill = new Color32(91, 178, 170, 255);
    private static readonly Color32 PrimaryButtonShadow = new Color32(121, 191, 184, 160);
    private static readonly Color32 LearnedFill = new Color32(103, 178, 151, 255);
    private static readonly Color32 LearnedTint = new Color32(236, 247, 241, 255);
    private static readonly Color32 LearnedText = new Color32(57, 132, 104, 255);
    private static readonly Color32 LockedFill = new Color32(224, 219, 212, 255);
    private static readonly Color32 LockedTint = new Color32(244, 240, 235, 255);
    private static readonly Color32 LockedText = new Color32(161, 145, 131, 255);
    private static readonly Color32 ButtonDisabledFill = new Color32(208, 197, 183, 255);
    private static readonly Color32 ButtonDisabledText = new Color32(245, 239, 232, 255);
    private static readonly Color32 ProgressCueFill = new Color32(255, 252, 243, 244);
    private static readonly Color32 ProgressCueAccent = new Color32(239, 188, 82, 255);

    private readonly List<TrainingTrickCardView> rows = new List<TrainingTrickCardView>();

    private UiSpriteLibrary spriteLibrary;
    private RectTransform rootRect;
    private RectTransform sheet;
    private RectTransform scrollViewport;
    private RectTransform scrollContent;
    private ScrollRect bodyScrollRect;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI dogNameLabel;
    private TextMeshProUGUI selectedTrickLabel;
    private TextMeshProUGUI selectedHintLabel;
    private TextMeshProUGUI selectedFeedbackLabel;
    private TextMeshProUGUI selectedProgressLabel;
    private Image selectedProgressFill;
    private Image selectedHeroBadge;
    private Image selectedHeroIcon;
    private RectTransform progressCueRoot;
    private CanvasGroup progressCueGroup;
    private TextMeshProUGUI progressCueTitle;
    private TextMeshProUGUI progressCuePercent;
    private Image progressCueFill;
    private DogRoomAgent trackedDog;
    private Camera trackedCamera;
    private Transform trackedHead;
    private float progressCueVisibleUntil;
    private float progressCueShownAt;
    private float progressCueFrom01;
    private float progressCueTo01;
    private float progressCueSeed;
    private PawPalTrickId selectedTrick = PawPalTrickId.Sit;

    public event Action CloseRequested;
    public event Action<PawPalTrickId> TrickSelected;

    public bool IsVisible
    {
        get { return gameObject.activeSelf; }
    }

    public void Initialize(UiSpriteLibrary sprites)
    {
        spriteLibrary = sprites;
        rootRect = GetComponent<RectTransform>();
        UiFactory.Stretch(rootRect, 0f, 0f, 0f, 0f);

        Image backdrop = UiFactory.CreateImage("Backdrop", rootRect, UiTheme.WhiteSprite, BackdropFill);
        backdrop.type = Image.Type.Simple;
        backdrop.preserveAspect = false;
        UiFactory.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

        sheet = UiFactory.CreateRect("TrainingSheet", rootRect);
        sheet.anchorMin = new Vector2(0.5f, 0.5f);
        sheet.anchorMax = new Vector2(0.5f, 0.5f);
        sheet.pivot = new Vector2(0.5f, 0.5f);
        sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight);
        sheet.anchoredPosition = new Vector2(0f, -4f);

        Image sheetBackground = UiFactory.CreateImage("Background", sheet, UiTheme.RoundedTenSprite, SheetFill);
        sheetBackground.type = Image.Type.Sliced;
        sheetBackground.preserveAspect = false;
        UiFactory.Stretch(sheetBackground.rectTransform, 0f, 0f, 0f, 0f);

        Image sheetOutline = UiFactory.CreateImage("Outline", sheet, UiTheme.RoundedTenOutlineSprite, PanelBorder);
        sheetOutline.type = Image.Type.Sliced;
        sheetOutline.preserveAspect = false;
        sheetOutline.raycastTarget = false;
        UiFactory.Stretch(sheetOutline.rectTransform, 0f, 0f, 0f, 0f);

        Shadow sheetShadow = sheetBackground.gameObject.AddComponent<Shadow>();
        sheetShadow.effectColor = new Color(SheetShadow.r / 255f, SheetShadow.g / 255f, SheetShadow.b / 255f, SheetShadow.a / 255f);
        sheetShadow.effectDistance = new Vector2(0f, -5f);
        sheetShadow.useGraphicAlpha = false;

        BuildHeader(sheet);
        BuildScrollableBody(sheet);
        BuildFloatingProgressCue(rootRect);
        gameObject.SetActive(false);
    }

    public void Show(PawPalDogState dog, PawPalTrickId initialTrick)
    {
        selectedTrick = initialTrick;
        gameObject.SetActive(true);
        if (bodyScrollRect != null)
        {
            bodyScrollRect.StopMovement();
            bodyScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Hide()
    {
        trackedDog = null;
        trackedCamera = null;
        trackedHead = null;
        HideProgressCue();
        gameObject.SetActive(false);
    }

    public void ShowTrainingProgressCue(DogRoomAgent dog, Camera camera, string trickName, float previousProgress01, float currentProgress01)
    {
        if (progressCueRoot == null || progressCueFill == null || progressCueTitle == null || progressCuePercent == null)
        {
            return;
        }

        trackedDog = dog;
        trackedCamera = camera;
        trackedHead = ResolveTrackedHead(dog);
        progressCueTitle.text = string.IsNullOrWhiteSpace(trickName) ? "Training" : trickName;
        progressCueFrom01 = Mathf.Clamp01(previousProgress01);
        progressCueTo01 = Mathf.Clamp01(currentProgress01);
        progressCueShownAt = Time.unscaledTime;
        progressCueVisibleUntil = progressCueShownAt + ProgressCueDuration;
        progressCueSeed = UnityEngine.Random.value * 0.5f;
        progressCueFill.fillAmount = progressCueFrom01;
        progressCuePercent.text = Mathf.RoundToInt(progressCueTo01 * 100f) + "%";
        progressCueGroup.alpha = 1f;
        progressCueRoot.localScale = Vector3.one;
        progressCueRoot.gameObject.SetActive(true);
        PawPalUiAudio.PlaySparkle();
        UpdateProgressCue();
    }

    public void Refresh(PawPalDogState dog, PawPalTrickId activeTrick, string feedback, string gestureDebug, bool canPraise)
    {
        selectedTrick = activeTrick;
        PawPalTrickDefinition selectedDefinition = PawPalTrickCatalog.GetDefinition(selectedTrick);
        PawPalTrainingTrickPresentation presentation = DescribeTrick(dog, selectedTrick);

        if (dogNameLabel != null)
        {
            dogNameLabel.text = dog != null && !string.IsNullOrWhiteSpace(dog.DisplayName) ? dog.DisplayName : "Dog";
        }

        if (selectedTrickLabel != null)
        {
            selectedTrickLabel.text = selectedDefinition != null ? selectedDefinition.DisplayName : selectedTrick.ToString();
        }

        if (selectedHeroBadge != null)
        {
            selectedHeroBadge.color = GetAccentColor(presentation);
        }

        if (selectedHeroIcon != null)
        {
            selectedHeroIcon.sprite = selectedDefinition != null && spriteLibrary != null
                ? spriteLibrary.GetWhiteIcon(selectedDefinition.IconName)
                : UiTheme.WhiteSprite;
            selectedHeroIcon.color = presentation.VisualState == PawPalTrainingTrickVisualState.Locked
                ? new Color(1f, 1f, 1f, 0.65f)
                : UiTheme.White;
        }

        if (selectedHintLabel != null)
        {
            selectedHintLabel.text = BuildTrainingInstructionText(dog, selectedDefinition, presentation);
        }

        if (selectedFeedbackLabel != null)
        {
            bool hasFeedback = !string.IsNullOrWhiteSpace(feedback);
            selectedFeedbackLabel.gameObject.SetActive(hasFeedback);
            if (hasFeedback)
            {
                selectedFeedbackLabel.text = feedback;
            }
        }

        if (selectedProgressFill != null)
        {
            UpdatePillProgressFill(selectedProgressFill, GetProgressFillAmount(presentation));
            selectedProgressFill.color = GetAccentColor(presentation);
        }

        if (selectedProgressLabel != null)
        {
            selectedProgressLabel.text = presentation.VisualState == PawPalTrainingTrickVisualState.Locked
                ? string.Empty
                : presentation.ProgressText;
            selectedProgressLabel.color = presentation.VisualState == PawPalTrainingTrickVisualState.Locked ? LockedText : UiTheme.NavBrandDark;
        }

        RefreshRows(dog);
    }

    public void SetBusy(bool busy)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            TrainingTrickCardView row = rows[i];
            if (row == null)
            {
                continue;
            }

            if (row.SelectButton != null)
            {
                row.SelectButton.interactable = !busy;
            }

        }
    }

    private void LateUpdate()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        UpdateProgressCue();
    }

    public static PawPalTrainingTrickPresentation DescribeTrick(PawPalDogState dog, PawPalTrickId trickId)
    {
        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
        if (definition == null)
        {
            return new PawPalTrainingTrickPresentation(
                PawPalTrainingTrickVisualState.Locked,
                PawPalTrickLearningStage.Undiscovered,
                "Locked",
                string.Empty,
                string.Empty,
                0f,
                false);
        }

        PawPalDogTrickProgress progress = dog != null ? PawPalTrickCatalog.GetOrCreateProgress(dog, trickId) : null;
        PawPalTrickLearningStage stage = PawPalTrickCatalog.GetLearningStage(progress, definition);
        float progress01 = stage == PawPalTrickLearningStage.Learned || stage == PawPalTrickLearningStage.Mastered
            ? 1f
            : PawPalTrickCatalog.GetProgress01(progress, definition);

        if (IsLockedByTrainingUiSequence(dog, trickId, progress))
        {
            string sequenceStateText = "Locked";
            PawPalTrickId sequencePrerequisite;
            if (TryGetTrainingUiPrerequisite(trickId, out sequencePrerequisite))
            {
                sequenceStateText = BuildPrerequisiteStateText(sequencePrerequisite);
            }

            return new PawPalTrainingTrickPresentation(
                PawPalTrainingTrickVisualState.Locked,
                stage,
                sequenceStateText,
                string.Empty,
                string.Empty,
                0f,
                false);
        }

        if (!PawPalTrickFocusRequirement.MeetsFocusRequirement(dog, definition))
        {
            return new PawPalTrainingTrickPresentation(
                PawPalTrainingTrickVisualState.Locked,
                stage,
                BuildFocusStateText(definition),
                string.Empty,
                string.Empty,
                0f,
                false);
        }

        PawPalTrickFailureReason failure = PawPalTrickProgressionService.GetAvailabilityFailure(dog, definition, false);
        if (failure != PawPalTrickFailureReason.None)
        {
            string stateText = GetFailureStateText(dog, definition, failure);
            return new PawPalTrainingTrickPresentation(
                PawPalTrainingTrickVisualState.Locked,
                stage,
                stateText,
                string.Empty,
                string.Empty,
                0f,
                false);
        }

        if (stage == PawPalTrickLearningStage.Learned || stage == PawPalTrickLearningStage.Mastered)
        {
            string learnedState = stage == PawPalTrickLearningStage.Mastered ? "Mastered" : "Learned (100%)";
            return new PawPalTrainingTrickPresentation(
                PawPalTrainingTrickVisualState.Learned,
                stage,
                learnedState,
                "100%",
                string.Empty,
                1f,
                false);
        }

        string learningStateText = progress01 > 0.001f
            ? "Learning (" + Mathf.RoundToInt(progress01 * 100f) + "%)"
            : "Teach";
        return new PawPalTrainingTrickPresentation(
            PawPalTrainingTrickVisualState.Available,
            stage,
            learningStateText,
            Mathf.RoundToInt(progress01 * 100f) + "%",
            string.Empty,
            progress01,
            false);
    }

    public static bool IsLockedByTrainingUiSequence(PawPalDogState dog, PawPalTrickId trickId)
    {
        PawPalDogTrickProgress progress = dog != null ? PawPalTrickCatalog.GetProgress(dog, trickId) : null;
        return IsLockedByTrainingUiSequence(dog, trickId, progress);
    }

    private static bool IsLockedByTrainingUiSequence(PawPalDogState dog, PawPalTrickId trickId, PawPalDogTrickProgress progress)
    {
        PawPalTrickId prerequisite;
        if (!TryGetTrainingUiPrerequisite(trickId, out prerequisite))
        {
            return false;
        }

        if (progress != null && (progress.IsLearned || progress.MasteryLevel > 0 || progress.MasteryXp > 0.001f || progress.IsDiscovered))
        {
            return false;
        }

        if (dog == null)
        {
            return true;
        }

        PawPalDogTrickProgress prerequisiteProgress = PawPalTrickCatalog.GetProgress(dog, prerequisite);
        return prerequisiteProgress == null || !prerequisiteProgress.IsLearned;
    }

    private static bool TryGetTrainingUiPrerequisite(PawPalTrickId trickId, out PawPalTrickId prerequisite)
    {
        switch (trickId)
        {
            case PawPalTrickId.Lie:
                prerequisite = PawPalTrickId.Sit;
                return true;
            case PawPalTrickId.Shake:
                prerequisite = PawPalTrickId.Lie;
                return true;
            case PawPalTrickId.Jump:
                prerequisite = PawPalTrickId.Shake;
                return true;
            case PawPalTrickId.Spin:
                prerequisite = PawPalTrickId.Jump;
                return true;
            default:
                prerequisite = PawPalTrickId.Sit;
                return false;
        }
    }

    private static string GetFailureStateText(PawPalDogState dog, PawPalTrickDefinition definition, PawPalTrickFailureReason failure)
    {
        switch (failure)
        {
            case PawPalTrickFailureReason.LowEnergy:
                return "Low stamina";
            case PawPalTrickFailureReason.LowMood:
                return "Low mood";
            case PawPalTrickFailureReason.LowBond:
                return BuildBondStateText(definition);
            case PawPalTrickFailureReason.Hungry:
                return "Hungry";
            case PawPalTrickFailureReason.Thirsty:
                return "Thirsty";
            case PawPalTrickFailureReason.MissingPrerequisite:
                PawPalTrickId? prerequisite = PawPalTrickProgressionService.GetFirstMissingPrerequisite(dog, definition, false);
                return prerequisite.HasValue ? BuildPrerequisiteStateText(prerequisite.Value) : "Locked";
            default:
                return "Locked";
        }
    }

    private static string BuildFocusStateText(PawPalTrickDefinition definition)
    {
        return "Focus " + PawPalTrickFocusRequirement.GetRequiredFocus(definition) + " required";
    }

    private static string BuildBondStateText(PawPalTrickDefinition definition)
    {
        return "Bond L" + definition.GetResolvedRequiredBondLevel();
    }

    private static string BuildPrerequisiteStateText(PawPalTrickId prerequisite)
    {
        return "Learn " + GetTrickDisplayName(prerequisite) + " first";
    }

    private static string GetTrickDisplayName(PawPalTrickId trickId)
    {
        PawPalTrickDefinition prerequisiteDefinition = PawPalTrickCatalog.GetDefinition(trickId);
        return prerequisiteDefinition != null ? prerequisiteDefinition.DisplayName : trickId.ToString();
    }

    private void BuildHeader(RectTransform parent)
    {
        titleLabel = UiFactory.CreateLabel("Title", parent, "Pet Training Centre", 20, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        titleLabel.font = UiTheme.NavExtraBoldFont;
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(18f, -46f);
        titleLabel.rectTransform.offsetMax = new Vector2(-72f, -16f);

        dogNameLabel = UiFactory.CreateLabel("DogName", parent, "Dog", 14, UiTheme.NavBrand, FontStyles.Normal, TextAlignmentOptions.Left);
        dogNameLabel.font = UiTheme.NavExtraBoldFont;
        dogNameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        dogNameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        dogNameLabel.rectTransform.pivot = new Vector2(0f, 1f);
        dogNameLabel.rectTransform.offsetMin = new Vector2(18f, -67f);
        dogNameLabel.rectTransform.offsetMax = new Vector2(-72f, -46f);

        Image closeFill = UiFactory.CreateImage("Close", parent, UiTheme.CircleSprite, PracticeFill);
        closeFill.type = Image.Type.Simple;
        closeFill.preserveAspect = false;
        closeFill.rectTransform.anchorMin = new Vector2(1f, 1f);
        closeFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        closeFill.rectTransform.pivot = new Vector2(1f, 1f);
        closeFill.rectTransform.sizeDelta = new Vector2(40f, 40f);
        closeFill.rectTransform.anchoredPosition = new Vector2(-16f, -16f);

        TextMeshProUGUI closeLabel = UiFactory.CreateLabel("CloseLabel", closeFill.rectTransform, "X", 18, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        closeLabel.font = UiTheme.NavExtraBoldFont;
        closeLabel.raycastTarget = false;
        UiFactory.Stretch(closeLabel.rectTransform, 0f, 0f, 0f, 0f);
        UiFactory.AddButton(closeFill.gameObject, delegate { Raise(CloseRequested); });
    }

    private void BuildFloatingProgressCue(RectTransform parent)
    {
        progressCueRoot = UiFactory.CreateRect("TrainingProgressCueRoot", parent);
        progressCueRoot.anchorMin = new Vector2(0.5f, 0.5f);
        progressCueRoot.anchorMax = new Vector2(0.5f, 0.5f);
        progressCueRoot.pivot = new Vector2(0.5f, 0.5f);
        progressCueRoot.sizeDelta = new Vector2(196f, 62f);

        progressCueGroup = progressCueRoot.gameObject.AddComponent<CanvasGroup>();
        progressCueGroup.alpha = 0f;
        progressCueGroup.blocksRaycasts = false;
        progressCueGroup.interactable = false;

        Image bubble = UiFactory.CreateImage("Bubble", progressCueRoot, UiTheme.RoundedTenSprite, ProgressCueFill);
        bubble.type = Image.Type.Sliced;
        bubble.preserveAspect = false;
        bubble.raycastTarget = false;
        UiFactory.Stretch(bubble.rectTransform, 0f, 0f, 0f, 0f);

        Outline outline = bubble.gameObject.AddComponent<Outline>();
        outline.effectColor = PanelBorder;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        progressCueTitle = UiFactory.CreateLabel("Title", bubble.rectTransform, "Training", 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        progressCueTitle.font = UiTheme.NavExtraBoldFont;
        progressCueTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        progressCueTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        progressCueTitle.rectTransform.pivot = new Vector2(0f, 1f);
        progressCueTitle.rectTransform.offsetMin = new Vector2(12f, -24f);
        progressCueTitle.rectTransform.offsetMax = new Vector2(-56f, -6f);

        progressCuePercent = UiFactory.CreateLabel("Percent", bubble.rectTransform, "0%", 11, ProgressCueAccent, FontStyles.Normal, TextAlignmentOptions.Right);
        progressCuePercent.font = UiTheme.NavExtraBoldFont;
        progressCuePercent.rectTransform.anchorMin = new Vector2(1f, 1f);
        progressCuePercent.rectTransform.anchorMax = new Vector2(1f, 1f);
        progressCuePercent.rectTransform.pivot = new Vector2(1f, 1f);
        progressCuePercent.rectTransform.sizeDelta = new Vector2(46f, 18f);
        progressCuePercent.rectTransform.anchoredPosition = new Vector2(-12f, -8f);

        Image progressBack = UiFactory.CreateImage("ProgressBack", bubble.rectTransform, UiTheme.RoundedTenSprite, ProgressTrack);
        progressBack.type = Image.Type.Sliced;
        progressBack.preserveAspect = false;
        progressBack.raycastTarget = false;
        progressBack.rectTransform.anchorMin = new Vector2(0f, 0f);
        progressBack.rectTransform.anchorMax = new Vector2(1f, 0f);
        progressBack.rectTransform.pivot = new Vector2(0.5f, 0f);
        progressBack.rectTransform.offsetMin = new Vector2(12f, 10f);
        progressBack.rectTransform.offsetMax = new Vector2(-12f, 24f);

        progressCueFill = UiFactory.CreateImage("ProgressFill", progressBack.rectTransform, UiTheme.RoundedTenSprite, ProgressCueAccent);
        progressCueFill.type = Image.Type.Filled;
        progressCueFill.fillMethod = Image.FillMethod.Horizontal;
        progressCueFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressCueFill.preserveAspect = false;
        progressCueFill.raycastTarget = false;
        UiFactory.Stretch(progressCueFill.rectTransform, 0f, 0f, 0f, 0f);

        progressCueRoot.gameObject.SetActive(false);
    }

    private void BuildScrollableBody(RectTransform parent)
    {
        scrollViewport = UiFactory.CreateRect("ScrollViewport", parent);
        UiFactory.Stretch(scrollViewport, 18f, 18f, 18f, 84f);

        Image viewportHit = scrollViewport.gameObject.AddComponent<Image>();
        viewportHit.color = new Color(1f, 1f, 1f, 0.002f);
        viewportHit.raycastTarget = true;
        scrollViewport.gameObject.AddComponent<RectMask2D>();

        scrollContent = UiFactory.CreateRect("ScrollContent", scrollViewport);
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(0f, 1f);
        scrollContent.pivot = new Vector2(0f, 1f);
        scrollContent.sizeDelta = new Vector2(ScrollContentWidth, SelectedPanelHeight);
        scrollContent.anchoredPosition = Vector2.zero;

        bodyScrollRect = scrollViewport.gameObject.AddComponent<ScrollRect>();
        bodyScrollRect.viewport = scrollViewport;
        bodyScrollRect.content = scrollContent;
        bodyScrollRect.horizontal = false;
        bodyScrollRect.vertical = true;
        bodyScrollRect.movementType = ScrollRect.MovementType.Clamped;
        bodyScrollRect.scrollSensitivity = 22f;

        BuildSelectedTrick(scrollContent);
        BuildTrickGrid(scrollContent);
        UpdateScrollContentHeight();
    }

    private void BuildSelectedTrick(RectTransform parent)
    {
        RectTransform panel = UiFactory.CreateRect("SelectedTrick", parent);
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(ScrollContentWidth, SelectedPanelHeight);
        panel.anchoredPosition = Vector2.zero;

        Image background = UiFactory.CreateImage("Background", panel, UiTheme.RoundedTenSprite, PanelFill);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        Image outline = UiFactory.CreateImage("Outline", panel, UiTheme.RoundedTenOutlineSprite, PanelBorder);
        outline.type = Image.Type.Sliced;
        outline.preserveAspect = false;
        outline.raycastTarget = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        Shadow panelShadow = background.gameObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
        panelShadow.effectDistance = new Vector2(0f, -2f);
        panelShadow.useGraphicAlpha = false;

        selectedTrickLabel = UiFactory.CreateLabel("SelectedName", panel, "Sit", 25, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        selectedTrickLabel.font = UiTheme.NavExtraBoldFont;
        selectedTrickLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        selectedTrickLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        selectedTrickLabel.rectTransform.pivot = new Vector2(0f, 1f);
        selectedTrickLabel.rectTransform.offsetMin = new Vector2(16f, -40f);
        selectedTrickLabel.rectTransform.offsetMax = new Vector2(-120f, -10f);

        RectTransform heroPlate = UiFactory.CreateRect("HeroPlate", panel);
        heroPlate.anchorMin = new Vector2(0f, 1f);
        heroPlate.anchorMax = new Vector2(0f, 1f);
        heroPlate.pivot = new Vector2(0f, 1f);
        heroPlate.sizeDelta = new Vector2(42f, 42f);
        heroPlate.anchoredPosition = new Vector2(16f, -38f);

        Image heroBackground = UiFactory.CreateImage("HeroBackground", heroPlate, UiTheme.RoundedTenSprite, new Color32(252, 236, 229, 255));
        heroBackground.type = Image.Type.Sliced;
        heroBackground.preserveAspect = false;
        UiFactory.Stretch(heroBackground.rectTransform, 0f, 0f, 0f, 0f);

        selectedHeroBadge = UiFactory.CreateImage("HeroBadge", heroPlate, UiTheme.CircleSprite, PracticeFill);
        selectedHeroBadge.type = Image.Type.Simple;
        selectedHeroBadge.preserveAspect = false;
        selectedHeroBadge.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        selectedHeroBadge.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        selectedHeroBadge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        selectedHeroBadge.rectTransform.sizeDelta = new Vector2(28f, 28f);
        selectedHeroBadge.rectTransform.anchoredPosition = Vector2.zero;

        selectedHeroIcon = UiFactory.CreateImage("HeroIcon", selectedHeroBadge.rectTransform, UiTheme.WhiteSprite, UiTheme.White);
        selectedHeroIcon.type = Image.Type.Simple;
        selectedHeroIcon.preserveAspect = true;
        selectedHeroIcon.raycastTarget = false;
        selectedHeroIcon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        selectedHeroIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        selectedHeroIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        selectedHeroIcon.rectTransform.sizeDelta = new Vector2(14f, 14f);
        selectedHeroIcon.rectTransform.anchoredPosition = Vector2.zero;

        Image progressBack = UiFactory.CreateImage("ProgressBack", panel, UiTheme.ProgressPillSprite, ProgressTrack);
        progressBack.type = Image.Type.Sliced;
        progressBack.preserveAspect = false;
        progressBack.rectTransform.anchorMin = new Vector2(0f, 1f);
        progressBack.rectTransform.anchorMax = new Vector2(1f, 1f);
        progressBack.rectTransform.pivot = new Vector2(0f, 1f);
        progressBack.rectTransform.offsetMin = new Vector2(70f, -78f);
        progressBack.rectTransform.offsetMax = new Vector2(-16f, -64f);

        Shadow progressShadow = progressBack.gameObject.AddComponent<Shadow>();
        progressShadow.effectColor = new Color(0f, 0f, 0f, 0.14f);
        progressShadow.effectDistance = new Vector2(0f, -1f);
        progressShadow.useGraphicAlpha = false;

        RectTransform progressInset = UiFactory.CreateRect("ProgressInset", progressBack.rectTransform);
        UiFactory.Stretch(progressInset, 3f, 2f, 3f, 2f);

        selectedProgressFill = UiFactory.CreateImage("ProgressFill", progressInset, UiTheme.ProgressPillSprite, PracticeFill);
        selectedProgressFill.type = Image.Type.Sliced;
        selectedProgressFill.preserveAspect = false;
        RectTransform selectedFillRect = selectedProgressFill.rectTransform;
        selectedFillRect.anchorMin = new Vector2(0f, 0f);
        selectedFillRect.anchorMax = new Vector2(0f, 1f);
        selectedFillRect.pivot = new Vector2(0f, 0.5f);
        selectedFillRect.anchoredPosition = Vector2.zero;
        selectedFillRect.sizeDelta = new Vector2(0f, 0f);

        selectedProgressLabel = UiFactory.CreateLabel("ProgressText", panel, "0%", 10, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Right);
        selectedProgressLabel.font = UiTheme.NavExtraBoldFont;
        selectedProgressLabel.raycastTarget = false;
        selectedProgressLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        selectedProgressLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        selectedProgressLabel.rectTransform.pivot = new Vector2(1f, 1f);
        selectedProgressLabel.rectTransform.offsetMin = new Vector2(70f, -60f);
        selectedProgressLabel.rectTransform.offsetMax = new Vector2(-16f, -42f);

        selectedHintLabel = UiFactory.CreateLabel("TrainingCueCopy", panel, "Voice and touch cues appear here.", 11, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        selectedHintLabel.font = UiTheme.NavRegularFont;
        selectedHintLabel.textWrappingMode = TextWrappingModes.Normal;
        selectedHintLabel.overflowMode = TextOverflowModes.Overflow;
        selectedHintLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        selectedHintLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        selectedHintLabel.rectTransform.pivot = new Vector2(0f, 1f);
        selectedHintLabel.rectTransform.offsetMin = new Vector2(16f, -194f);
        selectedHintLabel.rectTransform.offsetMax = new Vector2(-16f, -104f);

        selectedFeedbackLabel = UiFactory.CreateLabel("Feedback", panel, DefaultFeedback, 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        selectedFeedbackLabel.font = UiTheme.NavExtraBoldFont;
        selectedFeedbackLabel.textWrappingMode = TextWrappingModes.Normal;
        selectedFeedbackLabel.overflowMode = TextOverflowModes.Overflow;
        selectedFeedbackLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        selectedFeedbackLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        selectedFeedbackLabel.rectTransform.pivot = new Vector2(0f, 1f);
        selectedFeedbackLabel.rectTransform.offsetMin = new Vector2(16f, -200f);
        selectedFeedbackLabel.rectTransform.offsetMax = new Vector2(-16f, -182f);
        selectedFeedbackLabel.gameObject.SetActive(false);
    }

    private void BuildTrickGrid(RectTransform parent)
    {
        TextMeshProUGUI allTricksLabel = UiFactory.CreateLabel("AllTricksLabel", parent, "All Tricks", 16, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        allTricksLabel.font = UiTheme.NavExtraBoldFont;
        allTricksLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        allTricksLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        allTricksLabel.rectTransform.pivot = new Vector2(0f, 1f);
        allTricksLabel.rectTransform.sizeDelta = new Vector2(150f, 24f);
        allTricksLabel.rectTransform.anchoredPosition = new Vector2(0f, -TricksLabelTop);

        IReadOnlyList<PawPalTrickDefinition> definitions = PawPalTrickCatalog.CoreDefinitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            rows.Add(BuildRow(parent, definitions[i], i));
        }
    }

    private TrainingTrickCardView BuildRow(RectTransform parent, PawPalTrickDefinition definition, int index)
    {
        int column = index % 2;
        int rowIndex = index / 2;

        RectTransform root = UiFactory.CreateRect(definition.Id + "Card", parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(TrickCardWidth, TrickCardHeight);
        root.anchoredPosition = new Vector2(column * (TrickCardWidth + TrickCardColumnSpacing), -TricksGridTop - rowIndex * (TrickCardHeight + TrickCardRowSpacing));

        Image background = UiFactory.CreateImage("Background", root, UiTheme.RoundedTenSprite, UiTheme.CardWhite);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        Shadow shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.1f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = false;

        Image outline = UiFactory.CreateImage("Outline", root, UiTheme.RoundedTenOutlineSprite, PanelBorder);
        outline.type = Image.Type.Sliced;
        outline.preserveAspect = false;
        outline.raycastTarget = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        Button selectButton = UiFactory.AddButton(background.gameObject, delegate
        {
            selectedTrick = definition.Id;
            RaiseTrick(TrickSelected, definition.Id);
        });

        Image iconCircle = UiFactory.CreateImage("IconCircle", root, UiTheme.CircleSprite, PracticeFill);
        iconCircle.type = Image.Type.Simple;
        iconCircle.preserveAspect = false;
        iconCircle.raycastTarget = false;
        iconCircle.rectTransform.anchorMin = new Vector2(0f, 1f);
        iconCircle.rectTransform.anchorMax = new Vector2(0f, 1f);
        iconCircle.rectTransform.pivot = new Vector2(0f, 1f);
        iconCircle.rectTransform.sizeDelta = new Vector2(30f, 30f);
        iconCircle.rectTransform.anchoredPosition = new Vector2(12f, -12f);

        Image icon = UiFactory.CreateImage("Icon", iconCircle.rectTransform, spriteLibrary != null ? spriteLibrary.GetWhiteIcon(definition.IconName) : UiTheme.WhiteSprite, UiTheme.White);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(14f, 14f);
        icon.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI name = UiFactory.CreateLabel("Name", root, definition.DisplayName, 15, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        name.font = UiTheme.NavExtraBoldFont;
        name.enableAutoSizing = true;
        name.fontSizeMin = 10f;
        name.fontSizeMax = 15f;
        name.rectTransform.anchorMin = new Vector2(0f, 1f);
        name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.pivot = new Vector2(0f, 1f);
        name.rectTransform.offsetMin = new Vector2(48f, -28f);
        name.rectTransform.offsetMax = new Vector2(-10f, -10f);

        TextMeshProUGUI state = UiFactory.CreateLabel("State", root, "Teach", 10, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        state.font = UiTheme.NavMediumFont;
        state.enableAutoSizing = true;
        state.fontSizeMin = 8f;
        state.fontSizeMax = 10f;
        state.rectTransform.anchorMin = new Vector2(0f, 1f);
        state.rectTransform.anchorMax = new Vector2(1f, 1f);
        state.rectTransform.pivot = new Vector2(0f, 1f);
        state.rectTransform.offsetMin = new Vector2(48f, -45f);
        state.rectTransform.offsetMax = new Vector2(-10f, -28f);

        TextMeshProUGUI progressText = UiFactory.CreateLabel("ProgressText", root, "0%", 9, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        progressText.font = UiTheme.NavExtraBoldFont;
        progressText.raycastTarget = false;
        progressText.rectTransform.anchorMin = new Vector2(0f, 1f);
        progressText.rectTransform.anchorMax = new Vector2(1f, 1f);
        progressText.rectTransform.pivot = new Vector2(0.5f, 1f);
        progressText.rectTransform.offsetMin = new Vector2(12f, -54f);
        progressText.rectTransform.offsetMax = new Vector2(-12f, -43f);

        Image progressBack = UiFactory.CreateImage("ProgressBack", root, UiTheme.ProgressPillSprite, ProgressTrack);
        progressBack.type = Image.Type.Sliced;
        progressBack.preserveAspect = false;
        progressBack.rectTransform.anchorMin = new Vector2(0f, 1f);
        progressBack.rectTransform.anchorMax = new Vector2(1f, 1f);
        progressBack.rectTransform.pivot = new Vector2(0f, 1f);
        progressBack.rectTransform.offsetMin = new Vector2(14f, -60f);
        progressBack.rectTransform.offsetMax = new Vector2(-14f, -48f);

        RectTransform progressInset = UiFactory.CreateRect("ProgressInset", progressBack.rectTransform);
        UiFactory.Stretch(progressInset, 3f, 2f, 3f, 2f);

        Image progressFill = UiFactory.CreateImage("ProgressFill", progressInset, UiTheme.ProgressPillSprite, PracticeFill);
        progressFill.type = Image.Type.Sliced;
        progressFill.preserveAspect = false;
        RectTransform progressFillRect = progressFill.rectTransform;
        progressFillRect.anchorMin = new Vector2(0f, 0f);
        progressFillRect.anchorMax = new Vector2(0f, 1f);
        progressFillRect.pivot = new Vector2(0f, 0.5f);
        progressFillRect.anchoredPosition = Vector2.zero;
        progressFillRect.sizeDelta = new Vector2(0f, 0f);

        return new TrainingTrickCardView(
            definition.Id,
            background,
            outline,
            iconCircle,
            icon,
            name,
            state,
            progressBack,
            progressFill,
            progressText,
            selectButton);
    }

    private void UpdateScrollContentHeight()
    {
        if (scrollContent == null)
        {
            return;
        }

        int rowCount = (rows.Count + 1) / 2;
        float contentHeight = TricksGridTop
            + rowCount * TrickCardHeight
            + Mathf.Max(0, rowCount - 1) * TrickCardRowSpacing
            + ScrollBottomPadding;

        scrollContent.sizeDelta = new Vector2(ScrollContentWidth, Mathf.Max(SelectedPanelHeight, contentHeight));
    }

    private static string BuildTrainingInstructionText(PawPalDogState dog, PawPalTrickDefinition definition, PawPalTrainingTrickPresentation presentation)
    {
        if (definition == null)
        {
            return "No training data is available for this skill.";
        }

        string voiceCue = PawPalTrickCatalog.GetCommandLabel(definition.Id);
        string gestureCue = definition.GetPrimaryHint();
        int requiredFocus = PawPalTrickFocusRequirement.GetRequiredFocus(definition);
        List<string> lines = new List<string>();
        lines.Add("Requirements");
        string focusRequirement = "Focus: " + requiredFocus + " required";
        if (dog != null && dog.Focus < requiredFocus)
        {
            focusRequirement += " (current " + dog.Focus + ")";
        }

        if (definition.RequiredKnownTricks != null && definition.RequiredKnownTricks.Length > 0)
        {
            lines.Add("Prerequisite: " + GetTrickDisplayName(definition.RequiredKnownTricks[0]));
        }

        lines.Add(focusRequirement);

        int requiredBondLevel = definition.GetResolvedRequiredBondLevel();
        if (requiredBondLevel > 0)
        {
            lines.Add("Bond: Level " + requiredBondLevel);
        }

        lines.Add(string.Empty);
        lines.Add("Instructions");
        lines.Add("Voice: \"" + voiceCue + "\"");
        lines.Add("Touch: " + gestureCue);
        return string.Join("\n", lines.ToArray());
    }

    private static string GetTrainingLockReason(PawPalDogState dog, PawPalTrickDefinition definition, PawPalTrainingTrickPresentation presentation)
    {
        if (presentation.VisualState != PawPalTrainingTrickVisualState.Locked)
        {
            return string.Empty;
        }

        if (IsLockedByTrainingUiSequence(dog, definition.Id))
        {
            PawPalTrickId prerequisite;
            if (TryGetTrainingUiPrerequisite(definition.Id, out prerequisite))
            {
                return BuildPrerequisiteStateText(prerequisite) + ".";
            }
        }

        if (!PawPalTrickFocusRequirement.MeetsFocusRequirement(dog, definition))
        {
            return BuildFocusStateText(definition) + ".";
        }

        PawPalTrickFailureReason failure = PawPalTrickProgressionService.GetAvailabilityFailure(dog, definition, false);
        return failure != PawPalTrickFailureReason.None
            ? PawPalTrickProgressionService.GetFailureText(failure, dog, definition, false)
            : string.Empty;
    }

    private static float GetProgressFillAmount(PawPalTrainingTrickPresentation presentation)
    {
        if (presentation.VisualState == PawPalTrainingTrickVisualState.Locked || presentation.Progress01 <= 0.001f)
        {
            return 0f;
        }

        return Mathf.Lerp(0.08f, 0.96f, Mathf.Clamp01(presentation.Progress01));
    }

    private static void UpdatePillProgressFill(Image fillImage, float fillAmount)
    {
        if (fillImage == null)
        {
            return;
        }

        RectTransform fillRect = fillImage.rectTransform;
        RectTransform parentRect = fillRect.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(fillAmount);
        if (clamped <= 0.001f)
        {
            fillImage.gameObject.SetActive(false);
            return;
        }

        float width = Mathf.Max(0f, parentRect.rect.width * clamped);
        fillImage.gameObject.SetActive(true);
        fillRect.sizeDelta = new Vector2(width, 0f);
    }

    private void UpdateProgressCue()
    {
        if (progressCueRoot == null || progressCueGroup == null)
        {
            return;
        }

        if (Time.unscaledTime >= progressCueVisibleUntil || trackedDog == null || rootRect == null)
        {
            HideProgressCue();
            return;
        }

        Camera camera = trackedCamera != null ? trackedCamera : Camera.main;
        if (camera == null)
        {
            HideProgressCue();
            return;
        }

        if (trackedHead == null)
        {
            trackedHead = ResolveTrackedHead(trackedDog);
        }

        Vector3 worldPoint = trackedHead != null
            ? trackedHead.position + Vector3.up * ProgressCueHeadOffset
            : trackedDog.transform.position + new Vector3(0f, 0.45f, 0f);
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0f)
        {
            progressCueGroup.alpha = 0f;
            return;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPoint, null, out localPoint);
        localPoint.y += ProgressCueScreenLift;

        float halfWidth = progressCueRoot.rect.width * 0.5f;
        float halfHeight = progressCueRoot.rect.height * 0.5f;
        Rect bounds = rootRect.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + halfWidth + ProgressCueEdgeMargin, bounds.xMax - halfWidth - ProgressCueEdgeMargin);
        localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + halfHeight + ProgressCueEdgeMargin, bounds.yMax - halfHeight - ProgressCueEdgeMargin);
        progressCueRoot.anchoredPosition = localPoint;

        float fillT = Mathf.Clamp01((Time.unscaledTime - progressCueShownAt) / Mathf.Max(0.05f, ProgressFillAnimDuration));
        progressCueFill.fillAmount = Mathf.Lerp(progressCueFrom01, progressCueTo01, fillT);

        float normalizedTimeLeft = Mathf.Clamp01((progressCueVisibleUntil - Time.unscaledTime) / ProgressCueDuration);
        progressCueGroup.alpha = Mathf.Clamp01(Mathf.Min(1f, normalizedTimeLeft * 1.8f));
        float pulse = Mathf.PingPong((Time.unscaledTime + progressCueSeed) * 1.4f, 1f);
        progressCueRoot.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.03f, 1.03f, 1f), pulse * 0.35f);
    }

    private void HideProgressCue()
    {
        progressCueVisibleUntil = 0f;
        if (progressCueGroup != null)
        {
            progressCueGroup.alpha = 0f;
        }

        if (progressCueRoot != null)
        {
            progressCueRoot.gameObject.SetActive(false);
            progressCueRoot.localScale = Vector3.one;
        }
    }

    private static Transform ResolveTrackedHead(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return null;
        }

        DogCameraAttention attention = dog.GetComponentInChildren<DogCameraAttention>(true);
        if (attention != null)
        {
            Transform ownHead = attention.GetOwnHeadLookTarget();
            if (ownHead != null)
            {
                return ownHead;
            }
        }

        Transform[] children = dog.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate != null && string.Equals(candidate.name, "head", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.Contains("head") && !lowerName.Contains("aim") && !lowerName.Contains("target") && !lowerName.Contains("helper"))
            {
                return candidate;
            }
        }

        return dog.transform;
    }

    private void RefreshRows(PawPalDogState dog)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            TrainingTrickCardView row = rows[i];
            PawPalTrainingTrickPresentation presentation = DescribeTrick(dog, row.TrickId);
            bool selected = row.TrickId == selectedTrick;

            if (row.Background != null)
            {
                row.Background.color = GetCardBackgroundColor(presentation, selected);
            }

            if (row.Outline != null)
            {
                row.Outline.color = GetCardBorderColor(presentation, selected);
            }

            if (row.IconCircle != null)
            {
                row.IconCircle.color = GetAccentColor(presentation);
            }

            if (row.Icon != null)
            {
                row.Icon.color = presentation.VisualState == PawPalTrainingTrickVisualState.Locked
                    ? new Color(1f, 1f, 1f, 0.65f)
                    : UiTheme.White;
            }

            if (row.NameLabel != null)
            {
                row.NameLabel.color = presentation.VisualState == PawPalTrainingTrickVisualState.Locked ? LockedText : UiTheme.NavBrandDark;
            }

            if (row.StateLabel != null)
            {
                row.StateLabel.text = presentation.StateText;
                row.StateLabel.color = presentation.VisualState == PawPalTrainingTrickVisualState.Learned
                    ? LearnedText
                    : presentation.VisualState == PawPalTrainingTrickVisualState.Locked
                        ? LockedText
                        : UiTheme.NavBrandDark;
            }

            if (row.ProgressBack != null)
            {
                bool showProgress = presentation.VisualState != PawPalTrainingTrickVisualState.Locked;
                row.ProgressBack.gameObject.SetActive(showProgress);
                row.ProgressBack.color = ProgressTrack;
            }

            if (row.ProgressFill != null)
            {
                UpdatePillProgressFill(row.ProgressFill, GetProgressFillAmount(presentation));
                row.ProgressFill.color = GetAccentColor(presentation);
            }

            if (row.ProgressText != null)
            {
                row.ProgressText.text = presentation.ProgressText;
                row.ProgressText.gameObject.SetActive(false);
                row.ProgressText.color = presentation.VisualState == PawPalTrainingTrickVisualState.Locked ? LockedText : UiTheme.NavBrandDark;
            }

        }
    }

    private static Color32 GetAccentColor(PawPalTrainingTrickPresentation presentation)
    {
        switch (presentation.VisualState)
        {
            case PawPalTrainingTrickVisualState.Learned:
                return LearnedFill;
            case PawPalTrainingTrickVisualState.Locked:
                return LockedFill;
            default:
                return PracticeFill;
        }
    }

    private static Color32 GetCardBackgroundColor(PawPalTrainingTrickPresentation presentation, bool selected)
    {
        if (presentation.VisualState == PawPalTrainingTrickVisualState.Locked)
        {
            return selected ? new Color32(239, 235, 229, 255) : LockedTint;
        }

        if (presentation.VisualState == PawPalTrainingTrickVisualState.Learned)
        {
            return selected ? new Color32(229, 243, 236, 255) : LearnedTint;
        }

        return selected ? new Color32(255, 245, 239, 255) : UiTheme.CardWhite;
    }

    private static Color32 GetCardBorderColor(PawPalTrainingTrickPresentation presentation, bool selected)
    {
        if (selected)
        {
            return UiTheme.NavBrandDark;
        }

        switch (presentation.VisualState)
        {
            case PawPalTrainingTrickVisualState.Learned:
                return LearnedFill;
            case PawPalTrainingTrickVisualState.Locked:
                return LockedFill;
            default:
                return PanelBorder;
        }
    }

    private static void Raise(Action handler)
    {
        if (handler != null)
        {
            handler();
        }
    }

    private static void RaiseTrick(Action<PawPalTrickId> handler, PawPalTrickId trickId)
    {
        if (handler != null)
        {
            handler(trickId);
        }
    }

    private sealed class TrainingTrickCardView
    {
        public TrainingTrickCardView(
            PawPalTrickId trickId,
            Image background,
            Image outline,
            Image iconCircle,
            Image icon,
            TextMeshProUGUI nameLabel,
            TextMeshProUGUI stateLabel,
            Image progressBack,
            Image progressFill,
            TextMeshProUGUI progressText,
            Button selectButton)
        {
            TrickId = trickId;
            Background = background;
            Outline = outline;
            IconCircle = iconCircle;
            Icon = icon;
            NameLabel = nameLabel;
            StateLabel = stateLabel;
            ProgressBack = progressBack;
            ProgressFill = progressFill;
            ProgressText = progressText;
            SelectButton = selectButton;
        }

        public readonly PawPalTrickId TrickId;
        public readonly Image Background;
        public readonly Image Outline;
        public readonly Image IconCircle;
        public readonly Image Icon;
        public readonly TextMeshProUGUI NameLabel;
        public readonly TextMeshProUGUI StateLabel;
        public readonly Image ProgressBack;
        public readonly Image ProgressFill;
        public readonly TextMeshProUGUI ProgressText;
        public readonly Button SelectButton;
    }
}
