using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class PawPalWalkSceneFlow
{
    public const string HomeSceneName = "David_Test";
    public const string HomeScenePath = "Assets/Scenes/David_Test.unity";
    public const string WalkSceneName = "Walking";
    public const string WalkScenePath = "Assets/Scenes/Walking.unity";

    private static bool pendingShowHome;
    private static GameObject pendingWalkDogInstance;
    internal static bool ManualWalkSceneLoadRequested;

    public static bool PendingShowHome
    {
        get { return pendingShowHome; }
    }

    public static bool LoadWalkScene()
    {
        ManualWalkSceneLoadRequested = true;
        EnsureActiveDogHasCollarEquipped();
        PrepareActiveDogForWalkScene();
        ClearEditorSelectionBeforeSceneLoad();
        bool loadStarted = PawPalSceneTransitionController.LoadSceneWithTransition(
            WalkSceneName,
            WalkScenePath,
            new PawPalSceneTransitionRequest
            {
                DisplayText = "Getting everything ready",
                MinimumPostLoadFrames = 2,
                OnObscured = delegate
                {
                    SetAppShellVisible(false);
                },
                OnLoadStartFailed = delegate
                {
                    ClearPreparedWalkDog();
                    ManualWalkSceneLoadRequested = false;
                }
            });
        if (!loadStarted)
        {
            ClearPreparedWalkDog();
            ManualWalkSceneLoadRequested = false;
        }

        return loadStarted;
    }

    private static void EnsureActiveDogHasCollarEquipped()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || runtime.ActiveDog == null)
        {
            return;
        }

        runtime.TryAutoEquipOwnedCollar(runtime.ActiveDog.Id);
    }

    public static void ReturnHome(string sceneName)
    {
        pendingShowHome = true;
        ManualWalkSceneLoadRequested = false;
        ClearPreparedWalkDog();
        ClearEditorSelectionBeforeSceneLoad();
        string targetScene = string.IsNullOrEmpty(sceneName) ? HomeSceneName : sceneName;
        bool loadStarted = PawPalSceneTransitionController.LoadSceneWithTransition(
            targetScene,
            null,
            new PawPalSceneTransitionRequest
            {
                DisplayText = "Preparing your space",
                MinimumPostLoadFrames = 2,
                FallbackSceneName = HomeSceneName,
                FallbackScenePath = HomeScenePath,
                OnObscured = delegate
                {
                    SetAppShellVisible(true);
                },
                OnLoadStartFailed = delegate
                {
                    pendingShowHome = false;
                }
            });
        if (!loadStarted)
        {
            pendingShowHome = false;
        }
    }

    private static void ClearEditorSelectionBeforeSceneLoad()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Selection.activeObject = null;
        }
#endif
    }

    public static void ConsumePendingShowHome()
    {
        pendingShowHome = false;
    }

    public static bool TryConsumePreparedWalkDog(out GameObject dogObject)
    {
        dogObject = pendingWalkDogInstance;
        if (dogObject == null)
        {
            return false;
        }

        pendingWalkDogInstance = null;
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(dogObject, activeScene);
        }

        DisablePreparedDogNavMeshAgents(dogObject);
        dogObject.SetActive(true);
        return true;
    }

    public static void ClearPreparedWalkDog()
    {
        if (pendingWalkDogInstance != null)
        {
            Object.Destroy(pendingWalkDogInstance);
            pendingWalkDogInstance = null;
        }
    }

    private static void PrepareActiveDogForWalkScene()
    {
        ClearPreparedWalkDog();

        PawPalRoomPetHandle activePet = PawPalRoomPetRuntime.ResolveActivePet();
        if (activePet == null || !activePet.IsValid || !activePet.IsDog || activePet.RootTransform == null)
        {
            return;
        }

        pendingWalkDogInstance = Object.Instantiate(activePet.RootTransform.gameObject);
        if (pendingWalkDogInstance == null)
        {
            return;
        }

        pendingWalkDogInstance.name = "PreparedWalkDog_" + activePet.RootTransform.name;
        DisablePreparedDogNavMeshAgents(pendingWalkDogInstance);
        pendingWalkDogInstance.SetActive(false);
        Object.DontDestroyOnLoad(pendingWalkDogInstance);
    }

    private static void DisablePreparedDogNavMeshAgents(GameObject dogObject)
    {
        if (dogObject == null)
        {
            return;
        }

        NavMeshAgent[] agents = dogObject.GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent != null && agent.enabled)
            {
                agent.enabled = false;
            }
        }
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

        GameObject bootstrap = new GameObject("PawFriendsWalkSceneBootstrap");
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

        if (sceneName == PawPalWalkSceneFlow.WalkSceneName)
        {
            PawPalWalkSceneFlow.ManualWalkSceneLoadRequested = false;
            EnsureWalkSceneController();
            if (runtime != null && runtime.ActiveWalkSession != null)
            {
                Debug.Log("PawPalWalkSceneBootstrap initialized the walking scene for active session '" + runtime.ActiveWalkSession.SessionId + "'.");
            }

            yield break;
        }

        if (runtime != null && runtime.ActiveWalkSession != null && !runtime.ActiveWalkSession.Completed)
        {
            if (PawPalWalkSceneFlow.ManualWalkSceneLoadRequested)
            {
                yield break;
            }

            PawPalWalkSceneFlow.SetAppShellVisible(true);
            PawPalWalkSceneFlow.ClearPreparedWalkDog();
            yield break;
        }

        PawPalWalkSceneFlow.SetAppShellVisible(true);
        PawPalWalkSceneFlow.ClearPreparedWalkDog();
        if (PawPalWalkSceneFlow.PendingShowHome)
        {
            yield return null;
            ShowHomeScreen();
            PawPalWalkSceneFlow.ConsumePendingShowHome();
        }
    }

    private static void EnsureWalkSceneController()
    {
        if (Object.FindFirstObjectByType<PawPalWalkSceneController>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("PawFriendsWalkSceneController");
        controllerObject.AddComponent<PawPalWalkSceneController>();
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
