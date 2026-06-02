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

    private static readonly Vector2[] LoaderPositions =
    {
        new Vector2(-30f, 34f),
        new Vector2(-10f, 58f),
        new Vector2(10f, 58f),
        new Vector2(30f, 34f),
        new Vector2(0f, 0f)
    };

    private static readonly float[] LoaderSizes = { 18f, 18f, 18f, 18f, 34f };
    private static readonly float[] LoaderPhases = { 0f, 0.45f, 0.9f, 1.35f, 1.8f };

    private static PawPalSceneTransitionController instance;

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private RectTransform overlayRoot;
    private RectTransform loaderRoot;
    private RectTransform haloRoot;
    private RectTransform accentHaloRoot;
    private Image[] pawPads = new Image[0];
    private RectTransform[] pawPadRects = new RectTransform[0];
    private TextMeshProUGUI loadingLabel;
    private bool transitionActive;
    private bool explicitReadyReceived = true;
    private float visibleSinceUnscaledTime;

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
        prepared.DisplayText = string.IsNullOrWhiteSpace(prepared.DisplayText) ? "Loading..." : prepared.DisplayText.Trim();
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

        for (int i = 0; i < pawPads.Length; i++)
        {
            Image pad = pawPads[i];
            RectTransform padRect = i < pawPadRects.Length ? pawPadRects[i] : null;
            if (pad == null || padRect == null || i >= LoaderPositions.Length || i >= LoaderPhases.Length)
            {
                continue;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4.2f + LoaderPhases[i] * Mathf.PI);
            float lift = Mathf.Lerp(-3f, 5f, pulse);
            float scale = Mathf.Lerp(0.82f, 1.12f, pulse);
            Color color = i == pawPads.Length - 1
                ? new Color(0.94f, 0.56f, 0.46f, Mathf.Lerp(0.62f, 0.96f, pulse))
                : new Color(0.98f, 0.74f, 0.66f, Mathf.Lerp(0.52f, 0.88f, pulse));

            pad.color = color;
            padRect.anchoredPosition = LoaderPositions[i] + new Vector2(0f, lift);
            padRect.localScale = Vector3.one * scale;
        }
    }

    private IEnumerator RunSceneTransition(string sceneName, PawPalSceneTransitionRequest request, AsyncOperation loadOperation)
    {
        explicitReadyReceived = !request.WaitForExplicitReady;
        visibleSinceUnscaledTime = Time.unscaledTime;
        SetOverlayText(request.DisplayText);
        ShowOverlayImmediate();

        loadOperation.allowSceneActivation = false;
        yield return FadeOverlay(1f, request.FadeInDuration);
        InvokeOnObscured(request);
        yield return WaitForLoadReadiness(loadOperation);
        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        for (int i = 0; i < request.MinimumPostLoadFrames; i++)
        {
            yield return null;
        }

        if (request.WaitForExplicitReady)
        {
            float timeoutAt = Time.unscaledTime + request.ExplicitReadyTimeoutSeconds;
            while (!explicitReadyReceived && Time.unscaledTime < timeoutAt)
            {
                yield return null;
            }

            if (!explicitReadyReceived)
            {
                Debug.LogWarning("PawPalSceneTransitionController timed out waiting for an explicit ready signal after loading scene '" + sceneName + "'.");
            }
        }

        yield return WaitForMinimumVisibility(request.MinimumVisibleDuration);
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
        background.color = new Color32(255, 252, 247, 255);
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform bloomLeft = UiFactory.CreateRect("BloomLeft", transform);
        bloomLeft.anchorMin = new Vector2(0f, 1f);
        bloomLeft.anchorMax = new Vector2(0f, 1f);
        bloomLeft.pivot = new Vector2(0.5f, 0.5f);
        bloomLeft.anchoredPosition = new Vector2(74f, -112f);
        bloomLeft.sizeDelta = new Vector2(220f, 220f);
        Image bloomLeftImage = bloomLeft.gameObject.AddComponent<Image>();
        bloomLeftImage.sprite = UiTheme.CircleSprite;
        bloomLeftImage.type = Image.Type.Simple;
        bloomLeftImage.preserveAspect = false;
        bloomLeftImage.color = new Color(1f, 0.86f, 0.78f, 0.26f);

        RectTransform bloomRight = UiFactory.CreateRect("BloomRight", transform);
        bloomRight.anchorMin = new Vector2(1f, 0f);
        bloomRight.anchorMax = new Vector2(1f, 0f);
        bloomRight.pivot = new Vector2(0.5f, 0.5f);
        bloomRight.anchoredPosition = new Vector2(-60f, 118f);
        bloomRight.sizeDelta = new Vector2(180f, 180f);
        Image bloomRightImage = bloomRight.gameObject.AddComponent<Image>();
        bloomRightImage.sprite = UiTheme.CircleSprite;
        bloomRightImage.type = Image.Type.Simple;
        bloomRightImage.preserveAspect = false;
        bloomRightImage.color = new Color(1f, 0.92f, 0.86f, 0.24f);

        RectTransform contentRoot = UiFactory.CreateRect("ContentRoot", transform);
        contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.sizeDelta = new Vector2(260f, 220f);
        contentRoot.anchoredPosition = new Vector2(0f, 8f);

        haloRoot = UiFactory.CreateRect("Halo", contentRoot);
        haloRoot.anchorMin = new Vector2(0.5f, 0.5f);
        haloRoot.anchorMax = new Vector2(0.5f, 0.5f);
        haloRoot.pivot = new Vector2(0.5f, 0.5f);
        haloRoot.sizeDelta = new Vector2(132f, 132f);
        Image haloImage = haloRoot.gameObject.AddComponent<Image>();
        haloImage.sprite = UiTheme.CircleSprite;
        haloImage.type = Image.Type.Simple;
        haloImage.preserveAspect = false;
        haloImage.color = new Color(1f, 0.90f, 0.84f, 0.30f);

        accentHaloRoot = UiFactory.CreateRect("AccentHalo", contentRoot);
        accentHaloRoot.anchorMin = new Vector2(0.5f, 0.5f);
        accentHaloRoot.anchorMax = new Vector2(0.5f, 0.5f);
        accentHaloRoot.pivot = new Vector2(0.5f, 0.5f);
        accentHaloRoot.sizeDelta = new Vector2(98f, 98f);
        Image accentHaloImage = accentHaloRoot.gameObject.AddComponent<Image>();
        accentHaloImage.sprite = UiTheme.CircleSprite;
        accentHaloImage.type = Image.Type.Simple;
        accentHaloImage.preserveAspect = false;
        accentHaloImage.color = new Color(1f, 1f, 1f, 0.62f);

        loaderRoot = UiFactory.CreateRect("LoaderRoot", contentRoot);
        loaderRoot.anchorMin = new Vector2(0.5f, 0.5f);
        loaderRoot.anchorMax = new Vector2(0.5f, 0.5f);
        loaderRoot.pivot = new Vector2(0.5f, 0.5f);
        loaderRoot.sizeDelta = new Vector2(120f, 120f);
        loaderRoot.anchoredPosition = new Vector2(0f, 4f);

        pawPads = new Image[LoaderPositions.Length];
        pawPadRects = new RectTransform[LoaderPositions.Length];
        for (int i = 0; i < LoaderPositions.Length; i++)
        {
            RectTransform padRoot = UiFactory.CreateRect("Pad" + i, loaderRoot);
            padRoot.anchorMin = new Vector2(0.5f, 0.5f);
            padRoot.anchorMax = new Vector2(0.5f, 0.5f);
            padRoot.pivot = new Vector2(0.5f, 0.5f);
            padRoot.anchoredPosition = LoaderPositions[i];
            padRoot.sizeDelta = new Vector2(LoaderSizes[i], LoaderSizes[i]);

            Image padImage = padRoot.gameObject.AddComponent<Image>();
            padImage.sprite = UiTheme.CircleSprite;
            padImage.type = Image.Type.Simple;
            padImage.preserveAspect = false;

            pawPads[i] = padImage;
            pawPadRects[i] = padRoot;
        }

        loadingLabel = UiFactory.CreateLabel(
            "LoadingLabel",
            contentRoot,
            "Loading...",
            18,
            UiTheme.NavBrandDark,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        loadingLabel.font = UiTheme.NavExtraBoldFont;
        loadingLabel.enableAutoSizing = true;
        loadingLabel.fontSizeMin = 14;
        loadingLabel.fontSizeMax = 20;
        loadingLabel.textWrappingMode = TextWrappingModes.Normal;
        loadingLabel.overflowMode = TextOverflowModes.Overflow;
        loadingLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        loadingLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        loadingLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        loadingLabel.rectTransform.sizeDelta = new Vector2(220f, 42f);
        loadingLabel.rectTransform.anchoredPosition = new Vector2(0f, 0f);
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
        operation = TryStartEditorSceneLoad(scenePath, builder, "primary editor scene path");
        if (operation != null)
        {
            error = string.Empty;
            return operation;
        }

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
        HideOverlayImmediate();
    }

    private void SetOverlayText(string message)
    {
        if (loadingLabel != null)
        {
            loadingLabel.text = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
        }
    }
}
