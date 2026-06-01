using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PawPalIosBuildTools
{
    private const string IosIconPath = "Assets/Branding/pawfriends_app_icon.png";
    private const string AppleTeamId = "VS27MHUDGU";
    private const string IosBundleIdentifier = "com.unlimitive.pawfriends";
    private const string DefaultIosBuildPath = "Builds/iOS";
    private const string DefaultIosSimulatorBuildPath = "Builds/iOSSimulator";

    // Keep the shipping build list here instead of relying on ad-hoc Build Settings toggles.
    private static readonly string[] ReleaseScenePaths =
    {
        "Assets/Scenes/David_Test.unity"
    };

    [MenuItem("PawFriends/Build/iOS/Configure Player Settings")]
    public static void ConfigureIosPlayerSettings()
    {
        PlayerSettings.applicationIdentifier = IosBundleIdentifier;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, IosBundleIdentifier);
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.appleDeveloperTeamID = AppleTeamId;
        PlayerSettings.iOS.targetOSVersionString = "13.0";

        AssetDatabase.SaveAssets();
        Debug.Log("PawFriends iOS player settings configured for automatic signing.");
    }

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

    [MenuItem("PawFriends/Build/iOS/Export Xcode Project")]
    public static void ExportIosXcodeProject()
    {
        ExportIosXcodeProjectInternal(DefaultIosBuildPath, false);
    }

    [MenuItem("PawFriends/Build/iOS/Export Simulator Xcode Project")]
    public static void ExportIosSimulatorXcodeProject()
    {
        ExportIosXcodeProjectInternal(DefaultIosSimulatorBuildPath, true);
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

    private static void ExportIosXcodeProjectInternal(string relativeBuildPath, bool useSimulatorSdk)
    {
        ConfigureIosPlayerSettings();
        ApplyIosAppIcons();
        SyncReleaseScenes();
        ValidateEnabledScenes();

        iOSSdkVersion previousSdkVersion = PlayerSettings.iOS.sdkVersion;
        object previousSimulatorArchitecture = GetIosSimulatorArchitecture();

        try
        {
            PlayerSettings.iOS.sdkVersion = useSimulatorSdk ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            if (useSimulatorSdk)
            {
                SetIosSimulatorArchitectureByName("X86_64");
            }

            string absoluteBuildPath = Path.GetFullPath(relativeBuildPath);
            Directory.CreateDirectory(absoluteBuildPath);

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = ReleaseScenePaths,
                locationPathName = absoluteBuildPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("PawFriends iOS Xcode export failed: " + report.summary.result + ".");
                return;
            }

            string buildFlavor = useSimulatorSdk ? "simulator" : "device";
            Debug.Log("PawFriends iOS " + buildFlavor + " Xcode project exported to " + absoluteBuildPath + ".");
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = previousSdkVersion;
            SetIosSimulatorArchitecture(previousSimulatorArchitecture);
            AssetDatabase.SaveAssets();
        }
    }

    private static object GetIosSimulatorArchitecture()
    {
        MethodInfo getter = typeof(PlayerSettings).GetMethod("GetiOSSimulatorArchitecture", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return getter == null ? null : getter.Invoke(null, null);
    }

    private static void SetIosSimulatorArchitectureByName(string architectureName)
    {
        MethodInfo setter = typeof(PlayerSettings).GetMethod("SetiOSSimulatorArchitecture", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (setter == null)
        {
            return;
        }

        System.Type parameterType = setter.GetParameters()[0].ParameterType;
        object architectureValue = System.Enum.Parse(parameterType, architectureName);
        setter.Invoke(null, new[] { architectureValue });
    }

    private static void SetIosSimulatorArchitecture(object architectureValue)
    {
        if (architectureValue == null)
        {
            return;
        }

        MethodInfo setter = typeof(PlayerSettings).GetMethod("SetiOSSimulatorArchitecture", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (setter == null)
        {
            return;
        }

        setter.Invoke(null, new[] { architectureValue });
    }
}
