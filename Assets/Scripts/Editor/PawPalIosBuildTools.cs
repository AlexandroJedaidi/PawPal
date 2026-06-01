using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class PawPalIosBuildTools
{
    private const string IosIconPath = "Assets/Branding/pawfriends_app_icon.png";

    // Keep the shipping build list here instead of relying on ad-hoc Build Settings toggles.
    private static readonly string[] ReleaseScenePaths =
    {
        "Assets/Scenes/David_Test.unity"
    };

    [MenuItem("PawFriends/Build/iOS/Apply App Icons")]
    public static void ApplyIosAppIcons()
    {
        AssetDatabase.Refresh();

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IosIconPath);
        if (icon == null)
        {
            Debug.LogError("PawFriends iOS icon setup failed. Could not load " + IosIconPath + ".");
            return;
        }

        ApplyIconKind(icon, IconKind.Application);
        ApplyIconKind(icon, IconKind.Spotlight);
        ApplyIconKind(icon, IconKind.Settings);
        ApplyIconKind(icon, IconKind.Notification);

        AssetDatabase.SaveAssets();
        Debug.Log("PawFriends iOS app icons applied from " + IosIconPath + ".");
    }

    [MenuItem("PawFriends/Build/Sync Release Scenes")]
    public static void SyncReleaseScenes()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        List<string> missingPaths = new List<string>();

        for (int i = 0; i < ReleaseScenePaths.Length; i++)
        {
            string scenePath = ReleaseScenePaths[i];
            if (!File.Exists(scenePath))
            {
                missingPaths.Add(scenePath);
                continue;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        }

        if (missingPaths.Count > 0)
        {
            Debug.LogError("PawFriends release scene sync failed. Missing scenes:\n" + string.Join("\n", missingPaths));
            return;
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("PawFriends release scenes synced. Enabled scene count: " + scenes.Count + ".");
    }

    [MenuItem("PawFriends/Build/Validate Enabled Scenes")]
    public static void ValidateEnabledScenes()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes == null || scenes.Length == 0)
        {
            Debug.LogWarning("No scenes are currently enabled in Build Settings.");
            return;
        }

        List<string> suspiciousScenes = new List<string>();
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorBuildSettingsScene scene = scenes[i];
            if (scene == null || !scene.enabled)
            {
                continue;
            }

            string lowerPath = scene.path.ToLowerInvariant();
            if (lowerPath.Contains("test")
                || lowerPath.Contains("sample")
                || lowerPath.Contains("cleanup"))
            {
                suspiciousScenes.Add(scene.path);
            }
        }

        if (suspiciousScenes.Count == 0)
        {
            Debug.Log("Enabled build scenes look clean. No obvious test/sample scenes detected.");
            return;
        }

        Debug.LogWarning("Review these enabled scenes before a release build:\n" + string.Join("\n", suspiciousScenes));
    }

    private static void ApplyIconKind(Texture2D icon, IconKind kind)
    {
        int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, kind);
        if (sizes == null || sizes.Length == 0)
        {
            Debug.LogWarning("No iOS icon slots were returned for " + kind + ".");
            return;
        }

        Texture2D[] icons = new Texture2D[sizes.Length];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = icon;
        }

        PlayerSettings.SetIcons(NamedBuildTarget.iOS, icons, kind);
    }
}
