using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class PawPalAgilityTrialSceneFlow
{
    public const string AgilitySceneName = "TrialAgility";
    public const string AgilityScenePath = "Assets/Scenes/TrialAgility.unity";
    private const string LoadingBackgroundResourcePath = "UI/Downloaded/loading_walk";

    public static bool LoadAgilityScene()
    {
        ClearEditorSelectionBeforeSceneLoad();
        return PawPalSceneTransitionController.LoadSceneWithTransition(
            AgilitySceneName,
            AgilityScenePath,
            new PawPalSceneTransitionRequest
            {
                DisplayText = "Preparing the course",
                BackgroundResourcePath = LoadingBackgroundResourcePath,
                MinimumPostLoadFrames = 2,
                OnObscured = delegate
                {
                    PawPalWalkSceneFlow.SetAppShellVisible(false);
                },
                OnLoadStartFailed = delegate
                {
                    PawPalWalkSceneFlow.SetAppShellVisible(true);
                }
            });
    }

    public static void ReturnHome(string sceneName)
    {
        ClearEditorSelectionBeforeSceneLoad();
        PawPalWalkSceneFlow.SetAppShellVisible(true);
        PawPalWalkSceneFlow.ReturnHome(string.IsNullOrEmpty(sceneName) ? PawPalWalkSceneFlow.HomeSceneName : sceneName);
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
}

public sealed class PawPalAgilityTrialSceneBootstrap : MonoBehaviour
{
    private static PawPalAgilityTrialSceneBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("PawFriendsAgilityTrialSceneBootstrap");
        Object.DontDestroyOnLoad(bootstrap);
        instance = bootstrap.AddComponent<PawPalAgilityTrialSceneBootstrap>();
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
        if (sceneName != PawPalAgilityTrialSceneFlow.AgilitySceneName)
        {
            yield break;
        }

        PawPalWalkSceneFlow.SetAppShellVisible(false);
        if (Object.FindFirstObjectByType<PawPalAgilityTrialSceneController>(FindObjectsInactive.Include) == null)
        {
            GameObject controller = new GameObject("PawPalAgilityTrialSceneController");
            controller.AddComponent<PawPalAgilityTrialSceneController>();
        }
    }
}
