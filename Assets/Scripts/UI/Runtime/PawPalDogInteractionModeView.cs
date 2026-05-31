using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class PawPalDogInteractionModeView : MonoBehaviour
{
    private const string MicIconResourcePath = "UI/Figma/HomeMain/icon_mic";
    private const string NeutralIconName = "icon_feedback";
    private const string UnderstoodIconName = "icon_star_brand";
    private const string BondIconName = "icon_paw_brand";
    private const float CueDuration = 1.9f;
    private const float LongCueDuration = 2.3f;
    private const float CueScreenLift = 44f;
    private const float CueHeadOffset = 0.08f;
    private const float CueEdgeMargin = 18f;
    private const float ProgressCueDuration = 2.2f;
    private const float ProgressCueScreenLift = 88f;
    private const float ProgressFillAnimDuration = 0.42f;

    private static readonly Color32 DockFill = new Color32(255, 252, 243, 248);
    private static readonly Color32 DockBorder = new Color32(236, 223, 201, 255);
    private static readonly Color32 ActiveMicFill = new Color32(228, 132, 107, 255);
    private static readonly Color32 CueFill = new Color32(255, 252, 243, 244);
    private static readonly Color32 NeutralAccent = new Color32(228, 132, 107, 255);
    private static readonly Color32 UnderstoodAccent = new Color32(239, 188, 82, 255);
    private static readonly Color32 BondAccent = new Color32(103, 178, 151, 255);
    private static readonly Color32 ProgressAccent = new Color32(239, 188, 82, 255);
    private static readonly Color32 ProgressTrack = new Color32(235, 224, 203, 255);

    private UiSpriteLibrary spriteLibrary;
    private RectTransform root;
    private CanvasGroup rootGroup;
    private Image micBackground;
    private Image micIcon;
    private Image micBorder;
    private RectTransform cueRoot;
    private CanvasGroup cueGroup;
    private Image cueAccent;
    private Image cueIcon;
    private TextMeshProUGUI cueLabel;
    private RectTransform progressRoot;
    private CanvasGroup progressGroup;
    private TextMeshProUGUI progressTitle;
    private TextMeshProUGUI progressPercent;
    private Image progressFill;
    private DogRoomAgent trackedDog;
    private Camera trackedCamera;
    private Transform trackedHead;
    private float cueVisibleUntil;
    private float cueAnimationSeed;
    private float progressVisibleUntil;
    private float progressShownAt;
    private float progressFrom01;
    private float progressTo01;
    private bool visible;
    private bool micListening;
    private bool bondPulseActive;

    public event Action CloseRequested;
    public event Action MicRequested;

    public bool IsVisible
    {
        get { return visible; }
    }

    public void Initialize(UiSpriteLibrary sprites)
    {
        spriteLibrary = sprites;
        root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);
        rootGroup = gameObject.GetComponent<CanvasGroup>();
        if (rootGroup == null)
        {
            rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        BuildFloatingCue(root);
        BuildFloatingProgressCue(root);
        BuildMicButton(root);
        BuildCloseButton(root);
        SetRootVisible(false);
    }

    public void Show(PawPalDogState dog, bool micListening, float timeoutSeconds)
    {
        SetRootVisible(true);
        SetMicListening(micListening);
        SetTimeout(timeoutSeconds);
        SetBondPulse(false);
        HideCue();
        HideProgressCue();
    }

    public void Hide()
    {
        trackedDog = null;
        trackedCamera = null;
        trackedHead = null;
        HideCue();
        HideProgressCue();
        SetRootVisible(false);
    }

    public void SetTitle(string text)
    {
    }

    public void SetStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ShowCue(text, NeutralIconName, NeutralAccent, LongCueDuration);
    }

    public void SetHint(string text)
    {
    }

    public void SetMicListening(bool listening)
    {
        micListening = listening;
        RefreshMicVisual();
    }

    public void SetTimeout(float secondsRemaining)
    {
    }

    public void SetBondPulse(bool active)
    {
        bondPulseActive = active;
    }

    public void SetTrackedDog(DogRoomAgent dog, Camera camera)
    {
        trackedDog = dog;
        trackedCamera = camera;
        trackedHead = ResolveTrackedHead(dog);
    }

    public void ShowUnderstoodCue(string text)
    {
        ShowCue(string.IsNullOrWhiteSpace(text) ? "Understood" : text, UnderstoodIconName, UnderstoodAccent, CueDuration);
    }

    public void ShowBondCue(string text)
    {
        ShowCue(string.IsNullOrWhiteSpace(text) ? "Bond +" : text, BondIconName, BondAccent, CueDuration);
    }

    public void ShowNeutralCue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ShowCue(text, NeutralIconName, NeutralAccent, CueDuration);
    }

    public void ShowTrainingProgressCue(string trickName, float previousProgress01, float currentProgress01)
    {
        if (progressRoot == null || progressFill == null || progressTitle == null || progressPercent == null)
        {
            return;
        }

        progressTitle.text = string.IsNullOrWhiteSpace(trickName) ? "Training" : trickName;
        progressFrom01 = Mathf.Clamp01(previousProgress01);
        progressTo01 = Mathf.Clamp01(currentProgress01);
        progressShownAt = Time.unscaledTime;
        progressVisibleUntil = progressShownAt + ProgressCueDuration;
        cueAnimationSeed = UnityEngine.Random.value * 0.5f;
        progressFill.fillAmount = progressFrom01;
        progressPercent.text = Mathf.RoundToInt(progressTo01 * 100f) + "%";
        progressGroup.alpha = 1f;
        progressRoot.localScale = Vector3.one;
        progressRoot.gameObject.SetActive(true);
        PawPalUiAudio.PlaySparkle();
        UpdateProgressCuePosition();
    }

    private void LateUpdate()
    {
        if (!visible)
        {
            return;
        }

        UpdateCuePosition();
        UpdateProgressCuePosition();
        UpdateMicPulse();
    }

    private void SetRootVisible(bool isVisible)
    {
        visible = isVisible;
        if (rootGroup == null)
        {
            return;
        }

        rootGroup.alpha = isVisible ? 1f : 0f;
        rootGroup.interactable = isVisible;
        rootGroup.blocksRaycasts = isVisible;
    }

    private void BuildFloatingCue(RectTransform parent)
    {
        cueRoot = UiFactory.CreateRect("CueRoot", parent);
        cueRoot.anchorMin = new Vector2(0.5f, 0.5f);
        cueRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cueRoot.pivot = new Vector2(0.5f, 0.5f);
        cueRoot.sizeDelta = new Vector2(184f, 48f);

        cueGroup = cueRoot.gameObject.AddComponent<CanvasGroup>();
        cueGroup.alpha = 0f;
        cueGroup.blocksRaycasts = false;
        cueGroup.interactable = false;

        Image cueBubble = UiFactory.CreateImage("CueBubble", cueRoot, UiTheme.RoundedTenSprite, CueFill);
        cueBubble.type = Image.Type.Sliced;
        cueBubble.preserveAspect = false;
        cueBubble.raycastTarget = false;
        UiFactory.Stretch(cueBubble.rectTransform, 0f, 0f, 0f, 0f);

        Outline bubbleOutline = cueBubble.gameObject.AddComponent<Outline>();
        bubbleOutline.effectColor = DockBorder;
        bubbleOutline.effectDistance = new Vector2(1f, -1f);
        bubbleOutline.useGraphicAlpha = true;

        Shadow bubbleShadow = cueBubble.gameObject.AddComponent<Shadow>();
        bubbleShadow.effectColor = new Color(0f, 0f, 0f, 0.14f);
        bubbleShadow.effectDistance = new Vector2(0f, -2f);
        bubbleShadow.useGraphicAlpha = true;

        cueAccent = UiFactory.CreateImage("CueAccent", cueBubble.rectTransform, UiTheme.CircleSprite, NeutralAccent);
        cueAccent.type = Image.Type.Simple;
        cueAccent.preserveAspect = false;
        cueAccent.raycastTarget = false;
        cueAccent.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        cueAccent.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        cueAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
        cueAccent.rectTransform.sizeDelta = new Vector2(28f, 28f);
        cueAccent.rectTransform.anchoredPosition = new Vector2(10f, 0f);

        cueIcon = UiFactory.CreateImage("CueIcon", cueAccent.rectTransform, GetWhiteIcon(NeutralIconName), Color.white);
        cueIcon.type = Image.Type.Simple;
        cueIcon.preserveAspect = true;
        cueIcon.raycastTarget = false;
        cueIcon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        cueIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        cueIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        cueIcon.rectTransform.sizeDelta = new Vector2(17f, 17f);
        cueIcon.rectTransform.anchoredPosition = Vector2.zero;

        cueLabel = UiFactory.CreateLabel("CueLabel", cueBubble.rectTransform, string.Empty, 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        cueLabel.font = UiTheme.NavExtraBoldFont;
        cueLabel.enableAutoSizing = true;
        cueLabel.fontSizeMin = 10f;
        cueLabel.fontSizeMax = 12f;
        cueLabel.textWrappingMode = TextWrappingModes.NoWrap;
        cueLabel.overflowMode = TextOverflowModes.Ellipsis;
        cueLabel.raycastTarget = false;
        cueLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        cueLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        cueLabel.rectTransform.offsetMin = new Vector2(48f, 9f);
        cueLabel.rectTransform.offsetMax = new Vector2(-12f, -9f);

        cueRoot.gameObject.SetActive(false);
    }

    private void BuildFloatingProgressCue(RectTransform parent)
    {
        progressRoot = UiFactory.CreateRect("ProgressCueRoot", parent);
        progressRoot.anchorMin = new Vector2(0.5f, 0.5f);
        progressRoot.anchorMax = new Vector2(0.5f, 0.5f);
        progressRoot.pivot = new Vector2(0.5f, 0.5f);
        progressRoot.sizeDelta = new Vector2(196f, 62f);

        progressGroup = progressRoot.gameObject.AddComponent<CanvasGroup>();
        progressGroup.alpha = 0f;
        progressGroup.blocksRaycasts = false;
        progressGroup.interactable = false;

        Image bubble = UiFactory.CreateImage("ProgressBubble", progressRoot, UiTheme.RoundedTenSprite, CueFill);
        bubble.type = Image.Type.Sliced;
        bubble.preserveAspect = false;
        bubble.raycastTarget = false;
        UiFactory.Stretch(bubble.rectTransform, 0f, 0f, 0f, 0f);

        Outline outline = bubble.gameObject.AddComponent<Outline>();
        outline.effectColor = DockBorder;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        progressTitle = UiFactory.CreateLabel("Title", bubble.rectTransform, "Training", 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        progressTitle.font = UiTheme.NavExtraBoldFont;
        progressTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        progressTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        progressTitle.rectTransform.pivot = new Vector2(0f, 1f);
        progressTitle.rectTransform.offsetMin = new Vector2(12f, -24f);
        progressTitle.rectTransform.offsetMax = new Vector2(-56f, -6f);

        progressPercent = UiFactory.CreateLabel("Percent", bubble.rectTransform, "0%", 11, ProgressAccent, FontStyles.Normal, TextAlignmentOptions.Right);
        progressPercent.font = UiTheme.NavExtraBoldFont;
        progressPercent.rectTransform.anchorMin = new Vector2(1f, 1f);
        progressPercent.rectTransform.anchorMax = new Vector2(1f, 1f);
        progressPercent.rectTransform.pivot = new Vector2(1f, 1f);
        progressPercent.rectTransform.sizeDelta = new Vector2(46f, 18f);
        progressPercent.rectTransform.anchoredPosition = new Vector2(-12f, -8f);

        Image progressBack = UiFactory.CreateImage("ProgressBack", bubble.rectTransform, UiTheme.RoundedTenSprite, ProgressTrack);
        progressBack.type = Image.Type.Sliced;
        progressBack.preserveAspect = false;
        progressBack.raycastTarget = false;
        progressBack.rectTransform.anchorMin = new Vector2(0f, 0f);
        progressBack.rectTransform.anchorMax = new Vector2(1f, 0f);
        progressBack.rectTransform.pivot = new Vector2(0.5f, 0f);
        progressBack.rectTransform.offsetMin = new Vector2(12f, 10f);
        progressBack.rectTransform.offsetMax = new Vector2(-12f, 24f);

        progressFill = UiFactory.CreateImage("ProgressFill", progressBack.rectTransform, UiTheme.RoundedTenSprite, ProgressAccent);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.preserveAspect = false;
        progressFill.raycastTarget = false;
        UiFactory.Stretch(progressFill.rectTransform, 0f, 0f, 0f, 0f);

        progressRoot.gameObject.SetActive(false);
    }

    private void BuildMicButton(RectTransform parent)
    {
        micBackground = UiFactory.CreateImage("InteractionMicButton", parent, UiTheme.CircleSprite, DockFill);
        micBackground.type = Image.Type.Simple;
        micBackground.preserveAspect = false;
        micBackground.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        micBackground.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        micBackground.rectTransform.pivot = new Vector2(0.5f, 0f);
        micBackground.rectTransform.sizeDelta = new Vector2(64f, 64f);
        micBackground.rectTransform.anchoredPosition = new Vector2(0f, 22f);

        Shadow micShadow = micBackground.gameObject.AddComponent<Shadow>();
        micShadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
        micShadow.effectDistance = new Vector2(0f, -2f);
        micShadow.useGraphicAlpha = true;

        micBorder = UiFactory.CreateImage("SelectedBorder", micBackground.rectTransform, UiTheme.CircleOutlineSprite, UiTheme.NavBrandDark);
        micBorder.type = Image.Type.Simple;
        micBorder.preserveAspect = false;
        micBorder.raycastTarget = false;
        UiFactory.Stretch(micBorder.rectTransform, 0f, 0f, 0f, 0f);

        micIcon = UiFactory.CreateImage("Icon", micBackground.rectTransform, GetResourceSprite(MicIconResourcePath), Color.white);
        micIcon.type = Image.Type.Simple;
        micIcon.preserveAspect = true;
        micIcon.raycastTarget = false;
        micIcon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        micIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        micIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        micIcon.rectTransform.sizeDelta = new Vector2(28f, 28f);
        micIcon.rectTransform.anchoredPosition = Vector2.zero;

        Button micButton = UiFactory.AddButton(micBackground.gameObject, delegate { Raise(MicRequested); });
        PawPalUiAudio.AttachTo(micButton, PawPalUiClickSoundKind.Toggle);
        RefreshMicVisual();
    }

    private void BuildCloseButton(RectTransform parent)
    {
        Image closeBackground = UiFactory.CreateImage("CloseButton", parent, UiTheme.CircleSprite, DockFill);
        closeBackground.type = Image.Type.Simple;
        closeBackground.preserveAspect = false;
        closeBackground.rectTransform.anchorMin = new Vector2(1f, 1f);
        closeBackground.rectTransform.anchorMax = new Vector2(1f, 1f);
        closeBackground.rectTransform.pivot = new Vector2(1f, 1f);
        closeBackground.rectTransform.sizeDelta = new Vector2(42f, 42f);
        closeBackground.rectTransform.anchoredPosition = new Vector2(-16f, -18f);

        Outline closeOutline = closeBackground.gameObject.AddComponent<Outline>();
        closeOutline.effectColor = DockBorder;
        closeOutline.effectDistance = new Vector2(1f, -1f);
        closeOutline.useGraphicAlpha = true;

        Shadow closeShadow = closeBackground.gameObject.AddComponent<Shadow>();
        closeShadow.effectColor = new Color(0f, 0f, 0f, 0.14f);
        closeShadow.effectDistance = new Vector2(0f, -2f);
        closeShadow.useGraphicAlpha = true;

        TextMeshProUGUI closeLabel = UiFactory.CreateLabel("Label", closeBackground.rectTransform, "X", 16, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        closeLabel.font = UiTheme.NavExtraBoldFont;
        closeLabel.raycastTarget = false;
        UiFactory.Stretch(closeLabel.rectTransform, 0f, 0f, 0f, 0f);

        Button closeButton = UiFactory.AddButton(closeBackground.gameObject, delegate { Raise(CloseRequested); });
        PawPalUiAudio.AttachTo(closeButton, PawPalUiClickSoundKind.Menu);
    }

    private void RefreshMicVisual()
    {
        if (micBackground == null || micIcon == null)
        {
            return;
        }

        micBackground.color = micListening ? ActiveMicFill : DockFill;
        micIcon.sprite = micListening ? GetWhiteResourceSprite(MicIconResourcePath) : GetResourceSprite(MicIconResourcePath);
        micIcon.color = Color.white;
        if (micBorder != null)
        {
            micBorder.gameObject.SetActive(micListening);
        }
    }

    private void UpdateMicPulse()
    {
        if (micBackground == null)
        {
            return;
        }

        float micScale = 1f;
        if (micListening)
        {
            float pulse = Mathf.PingPong(Time.unscaledTime * 1.8f, 1f);
            micScale = Mathf.Lerp(1f, 1.06f, pulse);
        }

        micBackground.rectTransform.localScale = new Vector3(micScale, micScale, 1f);
    }

    private void ShowCue(string text, string iconName, Color accent, float duration)
    {
        if (cueRoot == null || cueLabel == null || cueAccent == null || cueIcon == null)
        {
            return;
        }

        cueLabel.text = text;
        cueAccent.color = accent;
        cueIcon.sprite = GetWhiteIcon(iconName);
        cueVisibleUntil = Time.unscaledTime + Mathf.Max(0.25f, duration);
        cueAnimationSeed = UnityEngine.Random.value * 0.5f;
        cueGroup.alpha = 1f;
        cueRoot.localScale = Vector3.one;
        cueRoot.gameObject.SetActive(true);
        UpdateCuePosition();
    }

    private void HideCue()
    {
        cueVisibleUntil = 0f;
        if (cueGroup != null)
        {
            cueGroup.alpha = 0f;
        }

        if (cueRoot != null)
        {
            cueRoot.gameObject.SetActive(false);
            cueRoot.localScale = Vector3.one;
        }
    }

    private void UpdateCuePosition()
    {
        if (cueRoot == null || cueGroup == null)
        {
            return;
        }

        if (Time.unscaledTime >= cueVisibleUntil || trackedDog == null)
        {
            HideCue();
            return;
        }

        Camera camera = trackedCamera != null ? trackedCamera : Camera.main;
        if (camera == null)
        {
            HideCue();
            return;
        }

        if (trackedHead == null)
        {
            trackedHead = ResolveTrackedHead(trackedDog);
        }

        Vector3 worldPoint = trackedHead != null
            ? trackedHead.position + Vector3.up * CueHeadOffset
            : trackedDog.transform.position + new Vector3(0f, 0.45f, 0f);
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0f)
        {
            cueGroup.alpha = 0f;
            return;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out localPoint);
        localPoint.y += CueScreenLift;

        float halfWidth = cueRoot.rect.width * 0.5f;
        float halfHeight = cueRoot.rect.height * 0.5f;
        Rect bounds = root.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + halfWidth + CueEdgeMargin, bounds.xMax - halfWidth - CueEdgeMargin);
        localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + halfHeight + CueEdgeMargin, bounds.yMax - halfHeight - CueEdgeMargin);
        cueRoot.anchoredPosition = localPoint;

        float normalizedTimeLeft = Mathf.Clamp01((cueVisibleUntil - Time.unscaledTime) / Mathf.Max(0.01f, CueDuration));
        float alpha = Mathf.Clamp01(Mathf.Min(1f, normalizedTimeLeft * 1.8f));
        cueGroup.alpha = alpha;

        float pulse = Mathf.PingPong((Time.unscaledTime + cueAnimationSeed) * 1.6f, 1f);
        float scale = bondPulseActive ? Mathf.Lerp(1f, 1.08f, pulse) : Mathf.Lerp(1f, 1.03f, pulse * 0.4f);
        cueRoot.localScale = new Vector3(scale, scale, 1f);
    }

    private void UpdateProgressCuePosition()
    {
        if (progressRoot == null || progressGroup == null)
        {
            return;
        }

        if (Time.unscaledTime >= progressVisibleUntil || trackedDog == null)
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
            ? trackedHead.position + Vector3.up * CueHeadOffset
            : trackedDog.transform.position + new Vector3(0f, 0.45f, 0f);
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0f)
        {
            progressGroup.alpha = 0f;
            return;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out localPoint);
        localPoint.y += ProgressCueScreenLift;

        float halfWidth = progressRoot.rect.width * 0.5f;
        float halfHeight = progressRoot.rect.height * 0.5f;
        Rect bounds = root.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + halfWidth + CueEdgeMargin, bounds.xMax - halfWidth - CueEdgeMargin);
        localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + halfHeight + CueEdgeMargin, bounds.yMax - halfHeight - CueEdgeMargin);
        progressRoot.anchoredPosition = localPoint;

        float animT = Mathf.Clamp01((Time.unscaledTime - progressShownAt) / Mathf.Max(0.05f, ProgressFillAnimDuration));
        progressFill.fillAmount = Mathf.Lerp(progressFrom01, progressTo01, animT);

        float normalizedTimeLeft = Mathf.Clamp01((progressVisibleUntil - Time.unscaledTime) / ProgressCueDuration);
        progressGroup.alpha = Mathf.Clamp01(Mathf.Min(1f, normalizedTimeLeft * 1.8f));
        float pulse = Mathf.PingPong((Time.unscaledTime + cueAnimationSeed) * 1.4f, 1f);
        progressRoot.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.03f, 1.03f, 1f), pulse * 0.35f);
    }

    private void HideProgressCue()
    {
        progressVisibleUntil = 0f;
        if (progressGroup != null)
        {
            progressGroup.alpha = 0f;
        }

        if (progressRoot != null)
        {
            progressRoot.gameObject.SetActive(false);
            progressRoot.localScale = Vector3.one;
        }
    }

    private Transform ResolveTrackedHead(DogRoomAgent dog)
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

        return FindHeadTransform(dog.transform);
    }

    private static Transform FindHeadTransform(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
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

        return root;
    }

    private Sprite GetResourceSprite(string resourcePath)
    {
        return spriteLibrary != null ? spriteLibrary.GetResourceSprite(resourcePath) : UiTheme.WhiteSprite;
    }

    private Sprite GetWhiteResourceSprite(string resourcePath)
    {
        return spriteLibrary != null ? spriteLibrary.GetWhiteResourceSprite(resourcePath) : UiTheme.WhiteSprite;
    }

    private Sprite GetWhiteIcon(string iconName)
    {
        return spriteLibrary != null ? spriteLibrary.GetWhiteIcon(iconName) : UiTheme.WhiteSprite;
    }

    private static void Raise(Action handler)
    {
        if (handler != null)
        {
            handler();
        }
    }
}
