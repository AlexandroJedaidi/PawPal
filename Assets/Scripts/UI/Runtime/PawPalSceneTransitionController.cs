using System;
using System.Collections;
using System.Collections.Generic;
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
    public string BackgroundResourcePath;
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
    private const float ProgressSmoothingSpeed = 3.75f;
    private const float ProgressReadyEpsilon = 0.995f;

    private static PawPalSceneTransitionController instance;
    private static Sprite pawIconSprite;
    private static readonly Dictionary<string, Sprite> backgroundSpritesByResourcePath = new Dictionary<string, Sprite>(StringComparer.Ordinal);

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private RectTransform overlayRoot;
    private Image backgroundImage;
    private AspectRatioFitter backgroundAspectFitter;
    private RectTransform contentRoot;
    private RectTransform progressClusterRoot;
    private Image progressTrackImage;
    private Image progressFillImage;
    private Image centerPawImage;
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

        loadOperation.allowSceneActivation = false;
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
        prepared.BackgroundResourcePath = string.IsNullOrWhiteSpace(prepared.BackgroundResourcePath) ? null : prepared.BackgroundResourcePath.Trim();
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

        displayedProgress = Mathf.MoveTowards(displayedProgress, progressTarget, Time.unscaledDeltaTime * ProgressSmoothingSpeed);
        RefreshProgressVisuals();
        RefreshLoadingText();
    }

    private IEnumerator RunSceneTransition(string sceneName, PawPalSceneTransitionRequest request, AsyncOperation loadOperation)
    {
        explicitReadyReceived = !request.WaitForExplicitReady;
        visibleSinceUnscaledTime = Time.unscaledTime;
        displayedProgress = 0f;
        progressTarget = 0f;
        SetOverlayText(request.DisplayText);
        SetOverlayBackground(request.BackgroundResourcePath);
        ShowOverlayImmediate();

        loadOperation.allowSceneActivation = false;
        yield return FadeOverlay(1f, request.FadeInDuration, loadOperation);
        InvokeOnObscured(request);
        yield return WaitForLoadReadiness(loadOperation);
        progressTarget = 1f;
        yield return WaitForDisplayedProgress(ProgressReadyEpsilon);
        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        for (int i = 0; i < request.MinimumPostLoadFrames; i++)
        {
            progressTarget = 1f;
            yield return null;
        }

        if (request.WaitForExplicitReady)
        {
            float timeoutAt = Time.unscaledTime + request.ExplicitReadyTimeoutSeconds;
            while (!explicitReadyReceived && Time.unscaledTime < timeoutAt)
            {
                progressTarget = 1f;
                yield return null;
            }

            if (!explicitReadyReceived)
            {
                Debug.LogWarning("PawPalSceneTransitionController timed out waiting for an explicit ready signal after loading scene '" + sceneName + "'.");
            }
        }

        yield return WaitForMinimumVisibility(request.MinimumVisibleDuration);
        progressTarget = 1f;
        displayedProgress = 1f;
        RefreshProgressVisuals();
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

        backgroundImage = UiFactory.CreateImage("Background", transform, UiTheme.WhiteSprite, Color.white);
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;
        backgroundImage.color = new Color32(255, 251, 244, 255);
        UiFactory.Stretch(backgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        backgroundAspectFitter = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
        backgroundAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        backgroundAspectFitter.enabled = false;

        Sprite pawSprite = ResolvePawSprite();

        contentRoot = UiFactory.CreateRect("ContentRoot", transform);
        contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.sizeDelta = new Vector2(272f, 226f);
        contentRoot.anchoredPosition = new Vector2(0f, 0f);

        progressClusterRoot = UiFactory.CreateRect("ProgressClusterRoot", contentRoot);
        progressClusterRoot.anchorMin = new Vector2(0.5f, 0.5f);
        progressClusterRoot.anchorMax = new Vector2(0.5f, 0.5f);
        progressClusterRoot.pivot = new Vector2(0.5f, 0.5f);
        progressClusterRoot.sizeDelta = new Vector2(152f, 152f);
        progressClusterRoot.anchoredPosition = new Vector2(0f, 24f);

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

        centerPawImage = UiFactory.CreateImage("CenterPaw", progressClusterRoot, pawSprite, UiTheme.NavBrand);
        centerPawImage.type = Image.Type.Simple;
        centerPawImage.preserveAspect = true;
        centerPawImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        centerPawImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        centerPawImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        centerPawImage.rectTransform.sizeDelta = new Vector2(44f, 44f);
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
    }

    private IEnumerator FadeOverlay(float targetAlpha, float duration, AsyncOperation loadOperation = null)
    {
        float startAlpha = overlayGroup != null ? overlayGroup.alpha : 0f;
        if (Mathf.Approximately(startAlpha, targetAlpha))
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            UpdateProgressTargetFromOperation(loadOperation);
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
            UpdateProgressTargetFromOperation(operation);
            yield return null;
        }
    }

    private void UpdateProgressTargetFromOperation(AsyncOperation operation)
    {
        if (operation == null)
        {
            return;
        }

        progressTarget = Mathf.Clamp01(operation.progress / 0.9f);
    }

    private IEnumerator WaitForDisplayedProgress(float minimumProgress)
    {
        float targetProgress = Mathf.Clamp01(minimumProgress);
        while (displayedProgress < targetProgress)
        {
            RefreshProgressVisuals();
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

    private static Sprite ResolveLoadingBackgroundSprite(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        Sprite sprite;
        if (backgroundSpritesByResourcePath.TryGetValue(resourcePath, out sprite))
        {
            return sprite;
        }

        sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            backgroundSpritesByResourcePath[resourcePath] = sprite;
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning("PawPalSceneTransitionController could not load loading background at Resources path '" + resourcePath + "'.");
            backgroundSpritesByResourcePath[resourcePath] = null;
            return null;
        }

        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = texture.name + "_LoadingBackgroundSprite";
        backgroundSpritesByResourcePath[resourcePath] = sprite;
        return sprite;
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

    private void SetOverlayBackground(string resourcePath)
    {
        if (backgroundImage == null)
        {
            return;
        }

        Sprite backgroundSprite = ResolveLoadingBackgroundSprite(resourcePath);
        if (backgroundSprite == null)
        {
            backgroundImage.sprite = UiTheme.WhiteSprite;
            backgroundImage.color = new Color32(255, 251, 244, 255);
            if (backgroundAspectFitter != null)
            {
                backgroundAspectFitter.enabled = false;
            }

            UiFactory.Stretch(backgroundImage.rectTransform, 0f, 0f, 0f, 0f);
            return;
        }

        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = Color.white;
        backgroundImage.preserveAspect = false;
        UiFactory.Stretch(backgroundImage.rectTransform, 0f, 0f, 0f, 0f);
        if (backgroundAspectFitter != null)
        {
            backgroundAspectFitter.aspectRatio = backgroundSprite.rect.width / Mathf.Max(1f, backgroundSprite.rect.height);
            backgroundAspectFitter.enabled = true;
        }
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
