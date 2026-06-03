using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
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
        return TryLoadScene(
            HomeSceneName,
            HomeScenePath,
            new PawPalSceneTransitionRequest
            {
                DisplayText = "Preparing your space",
                WaitForExplicitReady = true,
                MinimumPostLoadFrames = 2,
                OnObscured = delegate
                {
                    SetAppShellVisible(true);
                }
            });
    }

    public static bool LoadIntroScene()
    {
        return TryLoadScene(
            IntroSceneName,
            IntroScenePath,
            new PawPalSceneTransitionRequest
            {
                DisplayText = "Setting things up",
                MinimumPostLoadFrames = 2,
                OnObscured = delegate
                {
                    SetAppShellVisible(false);
                }
            });
    }

    private static bool TryLoadScene(string sceneName, string scenePath, PawPalSceneTransitionRequest request)
    {
        ClearEditorSelectionBeforeSceneLoad();
        return PawPalSceneTransitionController.LoadSceneWithTransition(sceneName, scenePath, request);
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
