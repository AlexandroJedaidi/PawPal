using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PawPalWalkSceneFlow
{
    public const string HomeSceneName = "David_Test";
    public const string HomeScenePath = "Assets/Scenes/David_Test.unity";
    public const string WalkSceneName = "David_Walk_Test";
    public const string WalkScenePath = "Assets/Scenes/David_Walk_Test.unity";

    private static bool pendingShowHome;
    internal static bool ManualWalkSceneLoadRequested;

    public static bool PendingShowHome
    {
        get { return pendingShowHome; }
    }

    public static bool LoadWalkScene()
    {
        ManualWalkSceneLoadRequested = true;
        try
        {
            SceneManager.LoadScene(WalkSceneName, LoadSceneMode.Single);
            return true;
        }
        catch
        {
            try
            {
                SceneManager.LoadScene(WalkScenePath, LoadSceneMode.Single);
                return true;
            }
            catch
            {
                ManualWalkSceneLoadRequested = false;
                return false;
            }
        }
    }

    public static void ReturnHome(string sceneName)
    {
        pendingShowHome = true;
        ManualWalkSceneLoadRequested = false;
        SetAppShellVisible(true);
        string targetScene = string.IsNullOrEmpty(sceneName) ? HomeSceneName : sceneName;
        try
        {
            SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
        }
        catch
        {
            try
            {
                SceneManager.LoadScene(HomeSceneName, LoadSceneMode.Single);
            }
            catch
            {
                SceneManager.LoadScene(HomeScenePath, LoadSceneMode.Single);
            }
        }
    }

    public static void ConsumePendingShowHome()
    {
        pendingShowHome = false;
    }

    public static void SetAppShellVisible(bool visible)
    {
        AppShellController[] shells = Object.FindObjectsByType<AppShellController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shells.Length; i++)
        {
            AppShellController shell = shells[i];
            if (shell == null)
            {
                continue;
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
    }
}

public sealed class PawPalWalkSceneBootstrap : MonoBehaviour
{
    private static PawPalWalkSceneBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("PawPalWalkSceneBootstrap");
        Object.DontDestroyOnLoad(bootstrap);
        instance = bootstrap.AddComponent<PawPalWalkSceneBootstrap>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(HandleSceneLoadedRoutine(scene.name));
    }

    private IEnumerator HandleSceneLoadedRoutine(string sceneName)
    {
        yield return null;

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            yield return null;
            runtime = PawPalGameRuntime.Instance;
        }

        if (runtime != null && runtime.ActiveWalkSession != null && !runtime.ActiveWalkSession.Completed)
        {
            if (sceneName == PawPalWalkSceneFlow.WalkSceneName)
            {
                if (!PawPalWalkSceneFlow.ManualWalkSceneLoadRequested)
                {
                    runtime.CancelActiveWalkSession();
                    PawPalWalkSceneFlow.ReturnHome(PawPalWalkSceneFlow.HomeSceneName);
                    yield break;
                }

                PawPalWalkSceneFlow.ManualWalkSceneLoadRequested = false;
                PawPalWalkSceneFlow.SetAppShellVisible(false);
                EnsureWalkSceneController(runtime.ActiveWalkSession);
            }
            else
            {
                PawPalWalkSceneFlow.ManualWalkSceneLoadRequested = false;
                runtime.CancelActiveWalkSession();
                PawPalWalkSceneFlow.SetAppShellVisible(true);
            }

            yield break;
        }

        PawPalWalkSceneFlow.SetAppShellVisible(true);
        if (PawPalWalkSceneFlow.PendingShowHome)
        {
            yield return null;
            ShowHomeScreen();
            PawPalWalkSceneFlow.ConsumePendingShowHome();
        }
    }

    private static void EnsureWalkSceneController(PawPalWalkSessionSaveData session)
    {
        if (Object.FindFirstObjectByType<PawPalWalkSceneController>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("PawPalWalkSceneController");
        PawPalWalkSceneController controller = controllerObject.AddComponent<PawPalWalkSceneController>();
        controller.Initialize(session);
    }

    private static void ShowHomeScreen()
    {
        AppShellController shell = Object.FindFirstObjectByType<AppShellController>(FindObjectsInactive.Include);
        if (shell != null)
        {
            shell.ShowScreen(AppScreenId.Home);
        }
    }
}
