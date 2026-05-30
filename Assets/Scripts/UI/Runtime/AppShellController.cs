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
    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;
    private Vector2 lastRootSize;
    private Vector2 lastSafeAreaRootSize;
    private int pendingLayoutFrames;

    public UiLayoutBucket CurrentBucket { get; private set; }
    public float BottomNavHeight { get; private set; }
    public AppScreenId CurrentScreen { get; private set; }

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

    private void BuildShell()
    {
        rootRect = GetComponent<RectTransform>();
        UiFactory.Stretch(rootRect, 0f, 0f, 0f, 0f);

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvas.pixelPerfect = false;

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
        }

        if (screenLayer != null)
        {
            UiFactory.Stretch(screenLayer, 0f, BottomNavHeight, 0f, 0f);
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
}
