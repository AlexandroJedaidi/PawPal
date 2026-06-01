using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public class AppShellController : MonoBehaviour
{
    private readonly Dictionary<AppScreenId, AppScreenViewBase> screens = new Dictionary<AppScreenId, AppScreenViewBase>();

    private RectTransform rootRect;
    private RectTransform safeAreaRoot;
    private RectTransform screenLayer;
    private RectTransform modalLayer;
    private UiSpriteLibrary spriteLibrary;
    private SafeAreaFitter safeAreaFitter;
    private AdaptiveLayoutRoot adaptiveLayout;
    private BottomNav bottomNav;
    private PawPalPhotoModeController photoModeController;
    private PawPalPhotoModeView photoModeView;
    private PawPalDogInteractionModeController dogInteractionModeController;
    private PawPalDogInteractionModeView dogInteractionModeView;
    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;
    private Vector2 lastRootSize;
    private Vector2 lastSafeAreaRootSize;
    private int pendingLayoutFrames;
    private bool photoModeActive;
    private bool dogInteractionModeActive;
    private Coroutine failedPhotoModeToastRoutine;

    public UiLayoutBucket CurrentBucket { get; private set; }
    public float BottomNavHeight { get; private set; }
    public AppScreenId CurrentScreen { get; private set; }
    public bool IsPhotoModeActive
    {
        get { return photoModeActive; }
    }

    public bool IsDogInteractionModeActive
    {
        get { return dogInteractionModeActive; }
    }

    private void Awake()
    {
        BuildShell();
    }

    private void LateUpdate()
    {
        if (safeAreaFitter != null)
        {
            safeAreaFitter.Apply();
        }

        Canvas.ForceUpdateCanvases();

        Vector2 rootSize = rootRect != null ? rootRect.rect.size : Vector2.zero;
        Vector2 safeRootSize = safeAreaRoot != null ? safeAreaRoot.rect.size : Vector2.zero;
        bool screenChanged = lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height;
        bool safeAreaChanged = lastSafeArea != Screen.safeArea;
        bool rootChanged = lastRootSize != rootSize || lastSafeAreaRootSize != safeRootSize;
        if (screenChanged || safeAreaChanged || rootChanged)
        {
            pendingLayoutFrames = Mathf.Max(pendingLayoutFrames, 2);
        }

        if (pendingLayoutFrames > 0)
        {
            ApplyCurrentLayout(CurrentBucket);
            pendingLayoutFrames--;
        }
    }

    public void ShowScreen(AppScreenId screenId)
    {
        if (photoModeActive || dogInteractionModeActive)
        {
            return;
        }

        CurrentScreen = screenId;

        foreach (KeyValuePair<AppScreenId, AppScreenViewBase> pair in screens)
        {
            pair.Value.gameObject.SetActive(pair.Key == screenId);
        }

        if (bottomNav != null)
        {
            bottomNav.SetSelected(screenId);
        }
    }

    public void EnterPhotoMode()
    {
        if (dogInteractionModeActive)
        {
            if (dogInteractionModeController != null && dogInteractionModeController.IsActive)
            {
                dogInteractionModeController.RequestShellExit("photo_mode");
            }
            else
            {
                ExitDogInteractionMode();
            }
        }

        EnsurePhotoModeController();
        if (modalLayer != null)
        {
            modalLayer.gameObject.SetActive(true);
        }

        if (failedPhotoModeToastRoutine != null)
        {
            StopCoroutine(failedPhotoModeToastRoutine);
            failedPhotoModeToastRoutine = null;
        }

        if (photoModeController == null || !photoModeController.TryEnterPhotoMode())
        {
            photoModeActive = false;
            SetPhotoModeChromeVisible(false);
            if (modalLayer != null)
            {
                modalLayer.gameObject.SetActive(true);
            }

            failedPhotoModeToastRoutine = StartCoroutine(HideFailedPhotoModeToastRoutine());
            return;
        }

        photoModeActive = true;
        SetPhotoModeChromeVisible(true);
    }

    public bool EnterDogInteractionMode(string dogId, bool keepMicListening)
    {
        if (photoModeActive && photoModeController != null)
        {
            photoModeController.ExitPhotoMode();
        }

        EnsureDogInteractionModeController();
        if (modalLayer != null)
        {
            modalLayer.gameObject.SetActive(true);
        }

        if (dogInteractionModeController == null || !dogInteractionModeController.TryEnterDogInteractionMode(dogId, keepMicListening))
        {
            dogInteractionModeActive = false;
            SetDogInteractionModeChromeVisible(false);
            return false;
        }

        dogInteractionModeActive = true;
        SetDogInteractionModeChromeVisible(true);
        return true;
    }

    public void ExitDogInteractionMode()
    {
        if (dogInteractionModeController != null && dogInteractionModeController.IsActive)
        {
            dogInteractionModeController.RequestShellExit("shell_request");
            return;
        }

        HandleDogInteractionModeExitedFromController();
    }

    public bool TryPerformDogInteractionVoiceTrick(PawPalResolvedVoiceCommand command, out string message)
    {
        EnsureDogInteractionModeController();
        if (!dogInteractionModeActive || dogInteractionModeController == null)
        {
            message = "Dog interaction mode is not active.";
            return false;
        }

        return dogInteractionModeController.TryPerformVoiceTrick(command, out message);
    }

    public void SetDogInteractionMicListening(bool listening)
    {
        if (dogInteractionModeController != null)
        {
            dogInteractionModeController.SetMicListening(listening);
        }
    }

    public void ToggleDogInteractionMic()
    {
        HomeScreenView home = GetHomeScreenView();
        if (home != null)
        {
            home.ToggleVoiceMicForInteraction();
        }
    }

    private void BuildShell()
    {
        rootRect = GetComponent<RectTransform>();
        UiFactory.Stretch(rootRect, 0f, 0f, 0f, 0f);

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        spriteLibrary = GetComponent<UiSpriteLibrary>();
        if (spriteLibrary == null)
        {
            spriteLibrary = gameObject.AddComponent<UiSpriteLibrary>();
        }

        ClearGeneratedChildren();

        safeAreaRoot = UiFactory.CreateRect("SafeAreaRoot", transform);
        UiFactory.Stretch(safeAreaRoot, 0f, 0f, 0f, 0f);
        safeAreaFitter = safeAreaRoot.gameObject.AddComponent<SafeAreaFitter>();

        adaptiveLayout = safeAreaRoot.gameObject.AddComponent<AdaptiveLayoutRoot>();
        adaptiveLayout.LayoutChanged += HandleLayoutChanged;

        screenLayer = UiFactory.CreateRect("ScreenLayer", safeAreaRoot);
        UiFactory.Stretch(screenLayer, 0f, 0f, 0f, 0f);
        screenLayer.gameObject.AddComponent<RectMask2D>();
        screenLayer.gameObject.SetActive(true);

        modalLayer = UiFactory.CreateRect("ModalLayer", safeAreaRoot);
        UiFactory.Stretch(modalLayer, 0f, 0f, 0f, 0f);
        modalLayer.gameObject.SetActive(false);
        EnsurePhotoModeController();
        EnsureDogInteractionModeController();

        RectTransform navRect = UiFactory.CreateRect("BottomNav", safeAreaRoot);
        bottomNav = navRect.gameObject.AddComponent<BottomNav>();
        bottomNav.Initialize(this, spriteLibrary);

        BuildScreens();
        HandleLayoutChanged(adaptiveLayout.CurrentBucket);
        ShowScreen(AppScreenId.Home);
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            DestroyImmediate(child.gameObject);
        }

        screens.Clear();
    }

    private void BuildScreens()
    {
        screens.Clear();

        HomeScreenView home = CreateScreen<HomeScreenView>("HomeScreen");
        screens[AppScreenId.Home] = home;
        home.Initialize(this, spriteLibrary);

        SettingsScreenView settings = CreateScreen<SettingsScreenView>("SettingsScreen");
        screens[AppScreenId.Settings] = settings;
        settings.Initialize(this, spriteLibrary);

        ShopScreenView shop = CreateScreen<ShopScreenView>("ShopScreen");
        screens[AppScreenId.Shop] = shop;
        shop.Initialize(this, spriteLibrary);

        MapScreenView map = CreateScreen<MapScreenView>("MapScreen");
        screens[AppScreenId.Map] = map;
        map.Initialize(this, spriteLibrary);

        ProfileScreenView profile = CreateScreen<ProfileScreenView>("ProfileScreen");
        screens[AppScreenId.Profile] = profile;
        profile.Initialize(this, spriteLibrary);
    }

    private T CreateScreen<T>(string name) where T : AppScreenViewBase
    {
        RectTransform screenRect = UiFactory.CreateRect(name, screenLayer);
        UiFactory.Stretch(screenRect, 0f, 0f, 0f, 0f);
        return screenRect.gameObject.AddComponent<T>();
    }

    private void HandleLayoutChanged(UiLayoutBucket bucket)
    {
        CurrentBucket = bucket;
        pendingLayoutFrames = Mathf.Max(pendingLayoutFrames, 2);
        ApplyCurrentLayout(bucket);
    }

    private void ApplyCurrentLayout(UiLayoutBucket bucket)
    {
        if (safeAreaFitter != null)
        {
            safeAreaFitter.Apply();
        }

        Canvas.ForceUpdateCanvases();

        if (bottomNav != null)
        {
            bottomNav.ApplyLayout(bucket);
            BottomNavHeight = bottomNav.CurrentHeight;
            bottomNav.gameObject.SetActive(!photoModeActive && !dogInteractionModeActive);
        }

        if (screenLayer != null)
        {
            screenLayer.gameObject.SetActive(!photoModeActive);
            UiFactory.Stretch(screenLayer, 0f, (photoModeActive || dogInteractionModeActive) ? 0f : BottomNavHeight, 0f, 0f);
        }

        if (modalLayer != null)
        {
            UiFactory.Stretch(modalLayer, 0f, 0f, 0f, 0f);
            bool modalVisible = photoModeActive
                || dogInteractionModeActive
                || (photoModeView != null && photoModeView.gameObject.activeSelf)
                || (dogInteractionModeView != null && dogInteractionModeView.IsVisible);
            modalLayer.gameObject.SetActive(modalVisible);
        }

        foreach (KeyValuePair<AppScreenId, AppScreenViewBase> pair in screens)
        {
            if (pair.Value != null)
            {
                pair.Value.ApplyLayout(bucket);
            }
        }

        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;
        lastRootSize = rootRect != null ? rootRect.rect.size : Vector2.zero;
        lastSafeAreaRootSize = safeAreaRoot != null ? safeAreaRoot.rect.size : Vector2.zero;
    }

    private void EnsurePhotoModeController()
    {
        if (photoModeController != null || modalLayer == null)
        {
            return;
        }

        RectTransform photoRoot = UiFactory.CreateRect("PhotoMode", modalLayer);
        UiFactory.Stretch(photoRoot, 0f, 0f, 0f, 0f);
        photoModeView = photoRoot.gameObject.AddComponent<PawPalPhotoModeView>();
        photoModeView.Initialize(spriteLibrary);
        photoModeController = photoRoot.gameObject.AddComponent<PawPalPhotoModeController>();
        photoModeController.Initialize(this, photoModeView);
        photoModeController.PhotoModeExited += HandlePhotoModeExited;
        photoModeView.HideAll();
    }

    private void EnsureDogInteractionModeController()
    {
        if (dogInteractionModeController != null || modalLayer == null)
        {
            return;
        }

        RectTransform interactionRoot = UiFactory.CreateRect("DogInteractionMode", modalLayer);
        UiFactory.Stretch(interactionRoot, 0f, 0f, 0f, 0f);
        dogInteractionModeView = interactionRoot.gameObject.AddComponent<PawPalDogInteractionModeView>();
        dogInteractionModeView.Initialize(spriteLibrary);
        dogInteractionModeController = interactionRoot.gameObject.AddComponent<PawPalDogInteractionModeController>();
        dogInteractionModeController.Initialize(this, dogInteractionModeView);
        dogInteractionModeView.Hide();
    }

    private void HandlePhotoModeExited()
    {
        photoModeActive = false;
        SetPhotoModeChromeVisible(false);
    }

    private void SetPhotoModeChromeVisible(bool activePhotoMode)
    {
        photoModeActive = activePhotoMode;

        if (bottomNav != null)
        {
            bottomNav.gameObject.SetActive(!photoModeActive && !dogInteractionModeActive);
        }

        if (screenLayer != null)
        {
            screenLayer.gameObject.SetActive(!photoModeActive);
        }

        if (modalLayer != null)
        {
            bool modalVisible = photoModeActive
                || dogInteractionModeActive
                || (photoModeView != null && photoModeView.gameObject.activeSelf)
                || (dogInteractionModeView != null && dogInteractionModeView.IsVisible);
            modalLayer.gameObject.SetActive(modalVisible);
        }

        pendingLayoutFrames = Mathf.Max(pendingLayoutFrames, 2);
        ApplyCurrentLayout(CurrentBucket);
    }

    public void HandleDogInteractionModeExitedFromController()
    {
        if (!dogInteractionModeActive && (dogInteractionModeView == null || !dogInteractionModeView.IsVisible))
        {
            return;
        }

        dogInteractionModeActive = false;
        SetDogInteractionModeChromeVisible(false);
    }

    private void SetDogInteractionModeChromeVisible(bool activeDogInteractionMode)
    {
        dogInteractionModeActive = activeDogInteractionMode;

        if (bottomNav != null)
        {
            bottomNav.gameObject.SetActive(!photoModeActive && !dogInteractionModeActive);
        }

        if (screenLayer != null)
        {
            screenLayer.gameObject.SetActive(!photoModeActive);
            UiFactory.Stretch(screenLayer, 0f, (photoModeActive || dogInteractionModeActive) ? 0f : BottomNavHeight, 0f, 0f);
        }

        SetHomeScreenChromeVisible(!dogInteractionModeActive);

        if (modalLayer != null)
        {
            bool modalVisible = photoModeActive
                || dogInteractionModeActive
                || (photoModeView != null && photoModeView.gameObject.activeSelf)
                || (dogInteractionModeView != null && dogInteractionModeView.IsVisible);
            modalLayer.gameObject.SetActive(modalVisible);
        }

        pendingLayoutFrames = Mathf.Max(pendingLayoutFrames, 2);
        ApplyCurrentLayout(CurrentBucket);
    }

    private void SetHomeScreenChromeVisible(bool visible)
    {
        HomeScreenView home = GetHomeScreenView();
        if (home != null)
        {
            home.SetHomeChromeVisible(visible);
        }
    }

    private HomeScreenView GetHomeScreenView()
    {
        AppScreenViewBase screen;
        if (!screens.TryGetValue(AppScreenId.Home, out screen))
        {
            return null;
        }

        return screen as HomeScreenView;
    }

    private IEnumerator HideFailedPhotoModeToastRoutine()
    {
        yield return new WaitForSecondsRealtime(2.1f);
        failedPhotoModeToastRoutine = null;

        if (photoModeActive)
        {
            yield break;
        }

        if (photoModeView != null)
        {
            photoModeView.HideAll();
        }

        if (modalLayer != null)
        {
            modalLayer.gameObject.SetActive(false);
        }
    }
}
