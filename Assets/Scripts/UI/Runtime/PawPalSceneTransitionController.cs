using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public sealed class PawPalSceneTransitionRequest
{
    public string DisplayText = "Loading...";
    public bool WaitForExplicitReady;
    public int MinimumPostLoadFrames = 2;
    public float FadeInDuration = 0.32f;
    public float FadeOutDuration = 0.24f;
    public float MinimumVisibleDuration = 0.65f;
    public float ExplicitReadyTimeoutSeconds = 10f;
    public string FallbackSceneName;
    public string FallbackScenePath;
    public Action OnObscured;
    public Action OnLoadStartFailed;
}

public sealed class PawPalSceneTransitionController : MonoBehaviour
{
    private const int OverlaySortingOrder = 4000;
    private const string DefaultLoadingText = "Preparing your space";
    private const string PawIconResourcePath = "UI/Icons/icon_paw_brand";
    private const int TrailVisibleCount = 3;
    private const float TrailLoopDuration = 5.2f;
    private const float TrailSpacingNormalized = 0.09f;
    private const float ProgressSmoothingSpeed = 0.9f;
    private const float ProgressShimmerDegreesPerSecond = 56f;

    private static readonly Vector2[] TrailNormalizedPositions =
    {
        new Vector2(0.50f, 0.92f),
        new Vector2(0.72f, 0.90f),
        new Vector2(0.88f, 0.78f),
        new Vector2(0.92f, 0.58f),
        new Vector2(0.90f, 0.34f),
        new Vector2(0.78f, 0.14f),
        new Vector2(0.50f, 0.08f),
        new Vector2(0.22f, 0.14f),
        new Vector2(0.10f, 0.34f),
        new Vector2(0.08f, 0.58f),
        new Vector2(0.12f, 0.78f),
        new Vector2(0.28f, 0.90f)
    };

    private static PawPalSceneTransitionController instance;
    private static Sprite pawIconSprite;

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private RectTransform overlayRoot;
    private RectTransform contentRoot;
    private RectTransform progressClusterRoot;
    private RectTransform progressShimmerRoot;
    private RectTransform haloRoot;
    private RectTransform accentHaloRoot;
    private RectTransform trailRoot;
    private Image progressTrackImage;
    private Image progressFillImage;
    private Image progressShimmerImage;
    private Image centerPawImage;
    private Image[] trailPaws = new Image[0];
    private RectTransform[] trailPawRects = new RectTransform[0];
    private TextMeshProUGUI loadingLabel;
    private bool transitionActive;
    private bool explicitReadyReceived = true;
    private float visibleSinceUnscaledTime;
    private float progressTarget;
    private float displayedProgress;
    private string baseLoadingText = DefaultLoadingText;

    public static bool IsTransitionActive
    {
        get { return instance != null && instance.transitionActive; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        EnsureInstalled();
    }

    public static bool LoadSceneWithTransition(string sceneName, string scenePath, PawPalSceneTransitionRequest request)
    {
        EnsureInstalled();
        if (instance == null)
        {
            Debug.LogError("PawPalSceneTransitionController could not be created.");
            return false;
        }

        if (instance.transitionActive)
        {
            Debug.LogWarning("PawPalSceneTransitionController ignored a scene load request because another transition is already active.");
            return false;
        }

        if (!instance.gameObject.activeSelf)
        {
            instance.gameObject.SetActive(true);
        }

        PawPalSceneTransitionRequest preparedRequest = PrepareRequest(request, scenePath);
        string loadError;
        AsyncOperation loadOperation = instance.TryStartSceneLoad(sceneName, scenePath, preparedRequest, out loadError);
        if (loadOperation == null)
        {
            InvokeOnLoadStartFailed(preparedRequest);
            Debug.LogError(loadError);
            return false;
        }

        instance.transitionActive = true;
        instance.StartCoroutine(instance.RunSceneTransition(sceneName, preparedRequest, loadOperation));
        return true;
    }

    public static void MarkActiveTransitionReady(string reason = null)
    {
        if (instance == null || !instance.transitionActive)
        {
            return;
        }

        instance.explicitReadyReceived = true;
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.Log("PawPalSceneTransitionController marked the active transition ready: " + reason);
        }
    }

    public static void ForceCompleteActiveTransition(string reason = null)
    {
        if (instance == null || !instance.transitionActive)
        {
            return;
        }

        instance.StopAllCoroutines();
        instance.FinishTransition();
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning("PawPalSceneTransitionController force-completed the active transition. " + reason);
        }
    }

    private static void EnsureInstalled()
    {
        if (instance != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("PawPalSceneTransitionController");
        DontDestroyOnLoad(controllerObject);
        instance = controllerObject.AddComponent<PawPalSceneTransitionController>();
    }

    private static PawPalSceneTransitionRequest PrepareRequest(PawPalSceneTransitionRequest request, string primaryScenePath)
    {
        PawPalSceneTransitionRequest prepared = request ?? new PawPalSceneTransitionRequest();
        prepared.DisplayText = string.IsNullOrWhiteSpace(prepared.DisplayText) ? DefaultLoadingText : prepared.DisplayText.Trim();
        prepared.MinimumPostLoadFrames = Mathf.Max(1, prepared.MinimumPostLoadFrames);
        prepared.FadeInDuration = Mathf.Max(0.01f, prepared.FadeInDuration);
        prepared.FadeOutDuration = Mathf.Max(0.01f, prepared.FadeOutDuration);
        prepared.MinimumVisibleDuration = Mathf.Max(0.1f, prepared.MinimumVisibleDuration);
        prepared.ExplicitReadyTimeoutSeconds = Mathf.Max(1f, prepared.ExplicitReadyTimeoutSeconds);
        if (string.IsNullOrEmpty(prepared.FallbackScenePath))
        {
            prepared.FallbackScenePath = primaryScenePath;
        }

        return prepared;
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
        BuildOverlay();
        HideOverlayImmediate();
    }

    private void Update()
    {
        if (!transitionActive)
        {
            return;
        }

        float time = Time.unscaledTime;
        float haloPulse = 0.5f + 0.5f * Mathf.Sin(time * 1.8f);
        if (haloRoot != null)
        {
            haloRoot.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.08f, haloPulse);
        }

        if (accentHaloRoot != null)
        {
            accentHaloRoot.localScale = Vector3.one * Mathf.Lerp(0.84f, 1.14f, 1f - haloPulse);
        }

        if (progressShimmerRoot != null)
        {
            progressShimmerRoot.localRotation = Quaternion.Euler(0f, 0f, -time * ProgressShimmerDegreesPerSecond);
        }

        displayedProgress = Mathf.MoveTowards(displayedProgress, progressTarget, Time.unscaledDeltaTime * ProgressSmoothingSpeed);
        RefreshProgressVisuals();
        RefreshLoadingText();
        RefreshTrailVisuals(time);
    }

    private IEnumerator RunSceneTransition(string sceneName, PawPalSceneTransitionRequest request, AsyncOperation loadOperation)
    {
        explicitReadyReceived = !request.WaitForExplicitReady;
        visibleSinceUnscaledTime = Time.unscaledTime;
        displayedProgress = 0f;
        progressTarget = 0.04f;
        SetOverlayText(request.DisplayText);
        ShowOverlayImmediate();

        loadOperation.allowSceneActivation = false;
        yield return FadeOverlay(1f, request.FadeInDuration);
        InvokeOnObscured(request);
        yield return WaitForLoadReadiness(loadOperation);
        progressTarget = Mathf.Max(progressTarget, 0.9f);
        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        for (int i = 0; i < request.MinimumPostLoadFrames; i++)
        {
            progressTarget = Mathf.Max(progressTarget, 0.94f);
            yield return null;
        }

        if (request.WaitForExplicitReady)
        {
            float timeoutAt = Time.unscaledTime + request.ExplicitReadyTimeoutSeconds;
            while (!explicitReadyReceived && Time.unscaledTime < timeoutAt)
            {
                progressTarget = Mathf.Max(progressTarget, 0.97f);
                yield return null;
            }

            if (!explicitReadyReceived)
            {
                Debug.LogWarning("PawPalSceneTransitionController timed out waiting for an explicit ready signal after loading scene '" + sceneName + "'.");
            }
        }

        yield return WaitForMinimumVisibility(request.MinimumVisibleDuration);
        progressTarget = 1f;
        yield return FadeOverlay(0f, request.FadeOutDuration);
        FinishTransition();
    }

    private void BuildOverlay()
    {
        overlayRoot = gameObject.GetComponent<RectTransform>();
        if (overlayRoot == null)
        {
            overlayRoot = gameObject.AddComponent<RectTransform>();
        }

        UiFactory.Stretch(overlayRoot, 0f, 0f, 0f, 0f);

        overlayCanvas = gameObject.GetComponent<Canvas>();
        if (overlayCanvas == null)
        {
            overlayCanvas = gameObject.AddComponent<Canvas>();
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = OverlaySortingOrder;
        overlayCanvas.pixelPerfect = true;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        overlayGroup = gameObject.GetComponent<CanvasGroup>();
        if (overlayGroup == null)
        {
            overlayGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Image background = UiFactory.CreateImage("Background", transform, UiTheme.WhiteSprite, Color.white);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = new Color32(255, 251, 244, 255);
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform bloomLeft = UiFactory.CreateRect("BloomLeft", transform);
        bloomLeft.anchorMin = new Vector2(0f, 1f);
        bloomLeft.anchorMax = new Vector2(0f, 1f);
        bloomLeft.pivot = new Vector2(0.5f, 0.5f);
        bloomLeft.anchoredPosition = new Vector2(60f, -118f);
        bloomLeft.sizeDelta = new Vector2(250f, 250f);
        Image bloomLeftImage = bloomLeft.gameObject.AddComponent<Image>();
        bloomLeftImage.sprite = UiTheme.CircleSprite;
        bloomLeftImage.type = Image.Type.Simple;
        bloomLeftImage.preserveAspect = false;
        bloomLeftImage.color = new Color(1f, 0.86f, 0.78f, 0.22f);

        RectTransform bloomRight = UiFactory.CreateRect("BloomRight", transform);
        bloomRight.anchorMin = new Vector2(1f, 0f);
        bloomRight.anchorMax = new Vector2(1f, 0f);
        bloomRight.pivot = new Vector2(0.5f, 0.5f);
        bloomRight.anchoredPosition = new Vector2(-48f, 126f);
        bloomRight.sizeDelta = new Vector2(210f, 210f);
        Image bloomRightImage = bloomRight.gameObject.AddComponent<Image>();
        bloomRightImage.sprite = UiTheme.CircleSprite;
        bloomRightImage.type = Image.Type.Simple;
        bloomRightImage.preserveAspect = false;
        bloomRightImage.color = new Color(1f, 0.92f, 0.86f, 0.18f);

        trailRoot = UiFactory.CreateRect("TrailRoot", transform);
        UiFactory.Stretch(trailRoot, 0f, 0f, 0f, 0f);

        trailPaws = new Image[TrailVisibleCount];
        trailPawRects = new RectTransform[TrailVisibleCount];
        Sprite pawSprite = ResolvePawSprite();
        for (int i = 0; i < TrailVisibleCount; i++)
        {
            RectTransform trailPaw = UiFactory.CreateRect("TrailPaw_" + i, trailRoot);
            trailPaw.anchorMin = new Vector2(0.5f, 0.5f);
            trailPaw.anchorMax = new Vector2(0.5f, 0.5f);
            trailPaw.pivot = new Vector2(0.5f, 0.5f);
            trailPaw.sizeDelta = new Vector2(30f, 30f);

            Image pawImage = trailPaw.gameObject.AddComponent<Image>();
            pawImage.sprite = pawSprite;
            pawImage.type = Image.Type.Simple;
            pawImage.preserveAspect = true;
            pawImage.color = new Color(0.92f, 0.60f, 0.49f, 0f);
            trailPaws[i] = pawImage;
            trailPawRects[i] = trailPaw;
        }

        contentRoot = UiFactory.CreateRect("ContentRoot", transform);
        contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.sizeDelta = new Vector2(272f, 276f);
        contentRoot.anchoredPosition = new Vector2(0f, 0f);

        haloRoot = UiFactory.CreateRect("Halo", contentRoot);
        haloRoot.anchorMin = new Vector2(0.5f, 0.5f);
        haloRoot.anchorMax = new Vector2(0.5f, 0.5f);
        haloRoot.pivot = new Vector2(0.5f, 0.5f);
        haloRoot.sizeDelta = new Vector2(170f, 170f);
        Image haloImage = haloRoot.gameObject.AddComponent<Image>();
        haloImage.sprite = UiTheme.CircleSprite;
        haloImage.type = Image.Type.Simple;
        haloImage.preserveAspect = false;
        haloImage.color = new Color(1f, 0.91f, 0.85f, 0.28f);

        accentHaloRoot = UiFactory.CreateRect("AccentHalo", contentRoot);
        accentHaloRoot.anchorMin = new Vector2(0.5f, 0.5f);
        accentHaloRoot.anchorMax = new Vector2(0.5f, 0.5f);
        accentHaloRoot.pivot = new Vector2(0.5f, 0.5f);
        accentHaloRoot.sizeDelta = new Vector2(132f, 132f);
        Image accentHaloImage = accentHaloRoot.gameObject.AddComponent<Image>();
        accentHaloImage.sprite = UiTheme.CircleSprite;
        accentHaloImage.type = Image.Type.Simple;
        accentHaloImage.preserveAspect = false;
        accentHaloImage.color = new Color(1f, 1f, 1f, 0.54f);

        progressClusterRoot = UiFactory.CreateRect("ProgressClusterRoot", contentRoot);
        progressClusterRoot.anchorMin = new Vector2(0.5f, 0.5f);
        progressClusterRoot.anchorMax = new Vector2(0.5f, 0.5f);
        progressClusterRoot.pivot = new Vector2(0.5f, 0.5f);
        progressClusterRoot.sizeDelta = new Vector2(152f, 152f);
        progressClusterRoot.anchoredPosition = new Vector2(0f, 20f);

        RectTransform trackRoot = UiFactory.CreateRect("ProgressTrack", progressClusterRoot);
        trackRoot.anchorMin = new Vector2(0.5f, 0.5f);
        trackRoot.anchorMax = new Vector2(0.5f, 0.5f);
        trackRoot.pivot = new Vector2(0.5f, 0.5f);
        trackRoot.sizeDelta = new Vector2(152f, 152f);
        progressTrackImage = trackRoot.gameObject.AddComponent<Image>();
        progressTrackImage.sprite = UiTheme.CircleSprite;
        progressTrackImage.type = Image.Type.Simple;
        progressTrackImage.preserveAspect = true;
        progressTrackImage.color = new Color(0.93f, 0.80f, 0.74f, 0.50f);

        RectTransform fillRoot = UiFactory.CreateRect("ProgressFill", progressClusterRoot);
        fillRoot.anchorMin = new Vector2(0.5f, 0.5f);
        fillRoot.anchorMax = new Vector2(0.5f, 0.5f);
        fillRoot.pivot = new Vector2(0.5f, 0.5f);
        fillRoot.sizeDelta = new Vector2(152f, 152f);
        progressFillImage = fillRoot.gameObject.AddComponent<Image>();
        progressFillImage.sprite = UiTheme.CircleSprite;
        progressFillImage.type = Image.Type.Filled;
        progressFillImage.fillMethod = Image.FillMethod.Radial360;
        progressFillImage.fillOrigin = (int)Image.Origin360.Top;
        progressFillImage.fillClockwise = true;
        progressFillImage.fillAmount = 0f;
        progressFillImage.preserveAspect = true;
        progressFillImage.color = new Color(0.89f, 0.47f, 0.38f, 1f);

        progressShimmerRoot = UiFactory.CreateRect("ProgressShimmer", progressClusterRoot);
        progressShimmerRoot.anchorMin = new Vector2(0.5f, 0.5f);
        progressShimmerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        progressShimmerRoot.pivot = new Vector2(0.5f, 0.5f);
        progressShimmerRoot.sizeDelta = new Vector2(152f, 152f);
        progressShimmerImage = progressShimmerRoot.gameObject.AddComponent<Image>();
        progressShimmerImage.sprite = UiTheme.CircleSprite;
        progressShimmerImage.type = Image.Type.Filled;
        progressShimmerImage.fillMethod = Image.FillMethod.Radial360;
        progressShimmerImage.fillOrigin = (int)Image.Origin360.Top;
        progressShimmerImage.fillClockwise = true;
        progressShimmerImage.fillAmount = 0.14f;
        progressShimmerImage.preserveAspect = true;
        progressShimmerImage.color = new Color(1f, 1f, 1f, 0.24f);

        RectTransform innerCutout = UiFactory.CreateRect("InnerCutout", progressClusterRoot);
        innerCutout.anchorMin = new Vector2(0.5f, 0.5f);
        innerCutout.anchorMax = new Vector2(0.5f, 0.5f);
        innerCutout.pivot = new Vector2(0.5f, 0.5f);
        innerCutout.sizeDelta = new Vector2(112f, 112f);
        Image innerCutoutImage = innerCutout.gameObject.AddComponent<Image>();
        innerCutoutImage.sprite = UiTheme.CircleSprite;
        innerCutoutImage.type = Image.Type.Simple;
        innerCutoutImage.preserveAspect = true;
        innerCutoutImage.color = new Color32(255, 251, 244, 255);

        RectTransform iconPlate = UiFactory.CreateRect("IconPlate", progressClusterRoot);
        iconPlate.anchorMin = new Vector2(0.5f, 0.5f);
        iconPlate.anchorMax = new Vector2(0.5f, 0.5f);
        iconPlate.pivot = new Vector2(0.5f, 0.5f);
        iconPlate.sizeDelta = new Vector2(86f, 86f);
        Image iconPlateImage = iconPlate.gameObject.AddComponent<Image>();
        iconPlateImage.sprite = UiTheme.CircleSprite;
        iconPlateImage.type = Image.Type.Simple;
        iconPlateImage.preserveAspect = true;
        iconPlateImage.color = new Color(1f, 1f, 1f, 0.96f);

        centerPawImage = UiFactory.CreateImage("CenterPaw", iconPlate, pawSprite, UiTheme.NavBrand);
        centerPawImage.type = Image.Type.Simple;
        centerPawImage.preserveAspect = true;
        centerPawImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        centerPawImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        centerPawImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        centerPawImage.rectTransform.sizeDelta = new Vector2(46f, 46f);
        centerPawImage.rectTransform.anchoredPosition = Vector2.zero;

        loadingLabel = UiFactory.CreateLabel(
            "LoadingLabel",
            contentRoot,
            DefaultLoadingText,
            19,
            UiTheme.NavBrandDark,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        loadingLabel.font = UiTheme.NavExtraBoldFont;
        loadingLabel.enableAutoSizing = true;
        loadingLabel.fontSizeMin = 15;
        loadingLabel.fontSizeMax = 22;
        loadingLabel.textWrappingMode = TextWrappingModes.Normal;
        loadingLabel.overflowMode = TextOverflowModes.Overflow;
        loadingLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        loadingLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        loadingLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        loadingLabel.rectTransform.sizeDelta = new Vector2(268f, 54f);
        loadingLabel.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        RefreshProgressVisuals();
        RefreshLoadingText();
        RefreshTrailVisuals(0f);
    }

    private IEnumerator FadeOverlay(float targetAlpha, float duration)
    {
        float startAlpha = overlayGroup != null ? overlayGroup.alpha : 0f;
        if (Mathf.Approximately(startAlpha, targetAlpha))
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            if (overlayGroup != null)
            {
                overlayGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            }

            yield return null;
        }

        if (overlayGroup != null)
        {
            overlayGroup.alpha = targetAlpha;
        }
    }

    private IEnumerator WaitForLoadReadiness(AsyncOperation operation)
    {
        while (operation != null && operation.progress < 0.89f)
        {
            progressTarget = Mathf.Max(progressTarget, Mathf.Lerp(0.08f, 0.86f, Mathf.Clamp01(operation.progress / 0.89f)));
            yield return null;
        }
    }

    private IEnumerator WaitForMinimumVisibility(float minimumVisibleDuration)
    {
        float targetTime = visibleSinceUnscaledTime + minimumVisibleDuration;
        while (Time.unscaledTime < targetTime)
        {
            yield return null;
        }
    }

    private AsyncOperation TryStartSceneLoad(string sceneName, string scenePath, PawPalSceneTransitionRequest request, out string error)
    {
        StringBuilder builder = new StringBuilder();
        AsyncOperation operation = TryStartSceneLoadByIdentifier(sceneName, builder, "primary scene name");
        if (operation != null)
        {
            error = string.Empty;
            return operation;
        }

#if UNITY_EDITOR
        operation = TryStartEditorSceneLoad(scenePath, builder, "primary editor scene path");
        if (operation != null)
        {
            error = string.Empty;
            return operation;
        }
#endif

        operation = TryStartSceneLoadByIdentifier(scenePath, builder, "primary scene path");
        if (operation != null)
        {
            error = string.Empty;
            return operation;
        }

        operation = TryStartSceneLoadByIdentifier(request.FallbackSceneName, builder, "fallback scene name");
        if (operation != null)
        {
            error = string.Empty;
            return operation;
        }

        operation = TryStartSceneLoadByIdentifier(request.FallbackScenePath, builder, "fallback scene path");
        if (operation != null)
        {
            error = string.Empty;
            return operation;
        }

#if UNITY_EDITOR
        operation = TryStartEditorSceneLoad(request.FallbackScenePath, builder, "fallback editor scene path");
        if (operation != null)
        {
            error = string.Empty;
            return operation;
        }
#endif

        error = builder.Length > 0
            ? builder.ToString()
            : "PawPalSceneTransitionController failed to begin an async scene load for '" + sceneName + "'.";
        return null;
    }

    private AsyncOperation TryStartSceneLoadByIdentifier(string identifier, StringBuilder builder, string label)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        try
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(identifier, LoadSceneMode.Single);
            if (operation != null)
            {
                return operation;
            }

            AppendLoadError(builder, label, "returned a null AsyncOperation for '" + identifier + "'.");
        }
        catch (Exception ex)
        {
            AppendLoadError(builder, label, ex.Message);
        }

        return null;
    }

#if UNITY_EDITOR
    private AsyncOperation TryStartEditorSceneLoad(string scenePath, StringBuilder builder, string label)
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(scenePath))
        {
            return null;
        }

        try
        {
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            if (operation != null)
            {
                return operation;
            }

            AppendLoadError(builder, label, "returned a null AsyncOperation for '" + scenePath + "'.");
        }
        catch (Exception ex)
        {
            AppendLoadError(builder, label, ex.Message);
        }

        return null;
    }
#endif

    private static void AppendLoadError(StringBuilder builder, string label, string message)
    {
        if (builder == null)
        {
            return;
        }

        if (builder.Length == 0)
        {
            builder.Append("PawPalSceneTransitionController could not start the requested scene load.");
        }

        builder.Append(' ');
        builder.Append(label);
        builder.Append(": ");
        builder.Append(message);
    }

    private static void InvokeOnObscured(PawPalSceneTransitionRequest request)
    {
        if (request == null || request.OnObscured == null)
        {
            return;
        }

        try
        {
            request.OnObscured();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static void InvokeOnLoadStartFailed(PawPalSceneTransitionRequest request)
    {
        if (request == null || request.OnLoadStartFailed == null)
        {
            return;
        }

        try
        {
            request.OnLoadStartFailed();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static Sprite ResolvePawSprite()
    {
        if (pawIconSprite == null)
        {
            pawIconSprite = Resources.Load<Sprite>(PawIconResourcePath);
        }

        return pawIconSprite != null ? pawIconSprite : UiTheme.CircleSprite;
    }

    private static string SanitizeLoadingText(string message)
    {
        string resolved = string.IsNullOrWhiteSpace(message) ? DefaultLoadingText : message.Trim();
        resolved = resolved.TrimEnd('.', ' ', '\u2026');
        return string.IsNullOrWhiteSpace(resolved) ? DefaultLoadingText : resolved;
    }

    private void RefreshProgressVisuals()
    {
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = Mathf.Clamp01(displayedProgress);
        }

        if (progressShimmerImage != null)
        {
            float shimmerAlpha = Mathf.Lerp(0.18f, 0.34f, 1f - Mathf.Clamp01(displayedProgress));
            progressShimmerImage.color = new Color(1f, 1f, 1f, shimmerAlpha);
        }
    }

    private void RefreshLoadingText()
    {
        if (loadingLabel == null)
        {
            return;
        }

        int dotCount = ((int)(Time.unscaledTime * 2.2f) % 3) + 1;
        loadingLabel.text = baseLoadingText + new string('.', dotCount);
    }

    private void RefreshTrailVisuals(float time)
    {
        if (trailPaws == null || trailPawRects == null || trailPaws.Length == 0)
        {
            return;
        }

        float headTravel = Mathf.Repeat(time / Mathf.Max(0.1f, TrailLoopDuration), 1f);
        for (int i = 0; i < trailPaws.Length; i++)
        {
            Image paw = trailPaws[i];
            RectTransform pawRect = i < trailPawRects.Length ? trailPawRects[i] : null;
            if (paw == null || pawRect == null)
            {
                continue;
            }

            float travel = Mathf.Repeat(headTravel - (i * TrailSpacingNormalized), 1f);
            Vector2 normalizedPosition = EvaluateTrailPosition(travel);
            pawRect.anchorMin = normalizedPosition;
            pawRect.anchorMax = normalizedPosition;
            pawRect.anchoredPosition = Vector2.zero;

            float intensity = 1f - (i / Mathf.Max(1f, TrailVisibleCount - 1f));
            float alpha = Mathf.Lerp(0.18f, 0.92f, intensity);
            float scale = Mathf.Lerp(0.82f, 1.1f, intensity);
            paw.color = new Color(0.92f, 0.60f, 0.49f, alpha);
            pawRect.localScale = Vector3.one * scale;
        }
    }

    private static Vector2 EvaluateTrailPosition(float normalizedTravel)
    {
        if (TrailNormalizedPositions == null || TrailNormalizedPositions.Length == 0)
        {
            return new Vector2(0.5f, 0.5f);
        }

        float wrapped = Mathf.Repeat(normalizedTravel, 1f);
        float segmentFloat = wrapped * TrailNormalizedPositions.Length;
        int segmentIndex = Mathf.FloorToInt(segmentFloat) % TrailNormalizedPositions.Length;
        int nextIndex = (segmentIndex + 1) % TrailNormalizedPositions.Length;
        float segmentT = segmentFloat - Mathf.Floor(segmentFloat);
        return Vector2.Lerp(TrailNormalizedPositions[segmentIndex], TrailNormalizedPositions[nextIndex], segmentT);
    }

    private void ShowOverlayImmediate()
    {
        if (overlayGroup == null)
        {
            return;
        }

        gameObject.SetActive(true);
        overlayCanvas.enabled = true;
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = true;
        overlayGroup.blocksRaycasts = true;
    }

    private void HideOverlayImmediate()
    {
        if (overlayGroup == null)
        {
            return;
        }

        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = false;
        }
    }

    private void FinishTransition()
    {
        transitionActive = false;
        explicitReadyReceived = true;
        progressTarget = 0f;
        displayedProgress = 0f;
        HideOverlayImmediate();
    }

    private void SetOverlayText(string message)
    {
        baseLoadingText = SanitizeLoadingText(message);
        RefreshLoadingText();
    }
}
