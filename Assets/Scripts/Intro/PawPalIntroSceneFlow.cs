using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        try
        {
            SceneManager.LoadScene(HomeSceneName, LoadSceneMode.Single);
            return true;
        }
        catch
        {
            try
            {
                SceneManager.LoadScene(HomeScenePath, LoadSceneMode.Single);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool LoadIntroScene()
    {
        SetAppShellVisible(false);
        try
        {
            SceneManager.LoadScene(IntroSceneName, LoadSceneMode.Single);
            return true;
        }
        catch
        {
            try
            {
                SceneManager.LoadScene(IntroScenePath, LoadSceneMode.Single);
                return true;
            }
            catch
            {
                return false;
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
