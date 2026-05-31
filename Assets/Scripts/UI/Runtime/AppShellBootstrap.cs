using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AppShellBootstrap : MonoBehaviour
{
    private static AppShellBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("AppShellBootstrap");
        DontDestroyOnLoad(bootstrap);
        instance = bootstrap.AddComponent<AppShellBootstrap>();
        PawPalUiAudio.EnsureInstalled();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        EnsureShellForActiveScene();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureShellForActiveScene();
    }

    private static void EnsureShellForActiveScene()
    {
        if (IsIntroPetSelectionScene())
        {
            EnsureEventSystem();
            SetExistingShellVisible(false);
            return;
        }

        DisableLegacyFigmaUi();

        AppShellController existingShell = Object.FindFirstObjectByType<AppShellController>(FindObjectsInactive.Include);
        if (existingShell != null)
        {
            SetShellVisible(existingShell, true);
            HideRuntimeUiFromSceneView(existingShell.gameObject);
            return;
        }

        EnsureEventSystem();
        CreateAppShellCanvas();
    }

    private static bool IsIntroPetSelectionScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.name == "IntroPetSelection" || scene.path == "Assets/Scenes/IntroPetSelection.unity";
    }

    private static void SetExistingShellVisible(bool visible)
    {
        AppShellController[] shells = Object.FindObjectsByType<AppShellController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shells.Length; i++)
        {
            SetShellVisible(shells[i], visible);
        }
    }

    private static void SetShellVisible(AppShellController shell, bool visible)
    {
        if (shell == null)
        {
            return;
        }

        if (!shell.gameObject.activeSelf)
        {
            shell.gameObject.SetActive(true);
        }

        Canvas canvas = shell.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = visible;
        }

        GraphicRaycaster raycaster = shell.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            raycaster.enabled = visible;
        }
    }

    private static void DisableLegacyFigmaUi()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.name == "AppShellCanvas")
            {
                continue;
            }

            bool isLegacyFigmaCanvas = HasLegacyFigmaMarkers(canvas.gameObject);
            bool isOverlayCanvas = canvas.renderMode == RenderMode.ScreenSpaceOverlay;
            if (!isLegacyFigmaCanvas && !isOverlayCanvas)
            {
                continue;
            }

            canvas.enabled = false;
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }

            Transform screenParent = canvas.transform.Find("ScreenParentTransform");
            if (screenParent != null)
            {
                screenParent.gameObject.SetActive(false);
            }

            if (canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(false);
            }
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null || current.name != "ScreenParentTransform")
            {
                continue;
            }

            current.gameObject.SetActive(false);
        }

        Behaviour[] behaviours = Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName == "PrototypeFlowController" || typeName == "FigmaStartScreenOverride" || typeName == "FigmaMobileScreenPolisher")
            {
                behaviour.enabled = false;
            }
        }
    }

    private static bool HasLegacyFigmaMarkers(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.transform.Find("ScreenParentTransform") != null)
        {
            return true;
        }

        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            if (component.GetType().Name == "PrototypeFlowController")
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(eventSystemObject);
    }

    private static void CreateAppShellCanvas()
    {
        GameObject canvasObject = new GameObject("AppShellCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.AddComponent<AppShellController>();
        HideRuntimeUiFromSceneView(canvasObject);
    }

    private static void HideRuntimeUiFromSceneView(GameObject target)
    {
#if UNITY_EDITOR
        if (target == null)
        {
            return;
        }

        UnityEditor.SceneVisibilityManager.instance.Hide(target, true);
#endif
    }
}
