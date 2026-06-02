using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public static class PawPalIntroSceneFlow
{
    public const string IntroSceneName = "IntroPetSelection";
    public const string IntroScenePath = "Assets/Scenes/IntroPetSelection.unity";
    public const string HomeSceneName = "David_Test";
    public const string HomeScenePath = "Assets/Scenes/David_Test.unity";

    public static bool IsIntroScene(Scene scene)
    {
        return scene.name == IntroSceneName || scene.path == IntroScenePath;
    }

    public static bool IsHomeScene(Scene scene)
    {
        return scene.name == HomeSceneName || scene.path == HomeScenePath;
    }

    public static bool LoadHomeScene()
    {
        SetAppShellVisible(true);
        return TryLoadScene(HomeSceneName, HomeScenePath);
    }

    public static bool LoadIntroScene()
    {
        SetAppShellVisible(false);
        return TryLoadScene(IntroSceneName, IntroScenePath);
    }

    private static bool TryLoadScene(string sceneName, string scenePath)
    {
        ClearEditorSelectionBeforeSceneLoad();

        try
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }
        catch
        {
            try
            {
                SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
                return true;
            }
            catch
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    try
                    {
                        EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
                        return true;
                    }
                    catch
                    {
                    }
                }
#endif
                return false;
            }
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
