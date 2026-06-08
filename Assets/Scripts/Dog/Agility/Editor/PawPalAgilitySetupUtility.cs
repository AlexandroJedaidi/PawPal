#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PawPalAgilitySetupUtility
{
    private const string ConfigDirectory = "Assets/Resources/PawPal/Agility";
    private const string ConfigPath = ConfigDirectory + "/PawPalAgilityTrialConfig.asset";

    [MenuItem("PawFriends/Agility/Setup Agility Trial")]
    public static void SetupAgilityTrial()
    {
        PawPalAgilityTrialConfig config = EnsureConfigAsset();
        EnsureBuildSettings();
        EnsureTrialScene(config);
        Debug.Log("PawPalAgilitySetupUtility finished Agility Trial setup.");
    }

    public static PawPalAgilityTrialConfig EnsureConfigAsset()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        PawPalAgilityTrialConfig config = AssetDatabase.LoadAssetAtPath<PawPalAgilityTrialConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PawPalAgilityTrialConfig>();
            config.ResetToDefaultLevels();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        else
        {
            config.ResetToDefaultLevels();
            EditorUtility.SetDirty(config);
        }

        AssetDatabase.SaveAssets();
        return config;
    }

    public static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            EditorBuildSettingsScene buildScene = scenes[i];
            if (buildScene != null && buildScene.path == PawPalAgilityTrialSceneFlow.AgilityScenePath)
            {
                buildScene.enabled = true;
                scenes[i] = buildScene;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(PawPalAgilityTrialSceneFlow.AgilityScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureTrialScene(PawPalAgilityTrialConfig config)
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(PawPalAgilityTrialSceneFlow.AgilityScenePath);
        EnsureGround();

        PawPalAgilityLevelDefinition beginner = config != null ? config.GetLevel(PawPalAgilityLevelId.Beginner) : null;
        PawPalAgilityLevelDefinition champion = config != null ? config.GetLevel(PawPalAgilityLevelId.Champion) : null;
        PawPalAgilityLevelDefinition source = champion != null ? champion : beginner;
        if (source != null)
        {
            for (int i = 0; i < source.Obstacles.Count; i++)
            {
                PawPalAgilityObstacleDefinition obstacle = source.Obstacles[i];
                if (obstacle != null)
                {
                    EnsureObstaclePrefab(obstacle, i);
                }
            }
        }

        EnsureSceneControllerMarker();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureGround()
    {
        if (GameObject.Find("AgilityGround") != null)
        {
            return;
        }

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "AgilityGround";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(4.2f, 1f, 2.2f);
    }

    private static void EnsureObstaclePrefab(PawPalAgilityObstacleDefinition obstacle, int index)
    {
        if (!string.IsNullOrWhiteSpace(obstacle.SceneObjectName) && GameObject.Find(obstacle.SceneObjectName) != null)
        {
            return;
        }

        if (obstacle.Type == PawPalAgilityObstacleType.StartGate || obstacle.Type == PawPalAgilityObstacleType.FinishGate)
        {
            EnsureGateMarker(obstacle, index);
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(obstacle.Type));
        if (prefab == null)
        {
            Debug.LogWarning("PawPalAgilitySetupUtility could not find prefab for " + obstacle.Type + ".");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = string.IsNullOrWhiteSpace(obstacle.SceneObjectName) ? "Agility_" + obstacle.Type : obstacle.SceneObjectName;
        instance.transform.position = obstacle.FallbackPosition;
        instance.transform.rotation = Quaternion.identity;
    }

    private static void EnsureGateMarker(PawPalAgilityObstacleDefinition obstacle, int index)
    {
        string name = obstacle.Type == PawPalAgilityObstacleType.StartGate ? "AgilityStartGate" : "AgilityFinishGate";
        if (GameObject.Find(name) != null)
        {
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        marker.transform.position = obstacle.FallbackPosition + Vector3.up * 0.05f;
        marker.transform.localScale = new Vector3(1.4f, 0.1f, 1.4f);
    }

    private static void EnsureSceneControllerMarker()
    {
        if (GameObject.Find("AgilityTrialRuntime") != null)
        {
            return;
        }

        GameObject marker = new GameObject("AgilityTrialRuntime");
        marker.AddComponent<PawPalAgilityTrialSceneController>();
    }

    private static string GetPrefabPath(PawPalAgilityObstacleType type)
    {
        switch (type)
        {
            case PawPalAgilityObstacleType.BarrierRunAround:
                return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Barrier_1.prefab";
            case PawPalAgilityObstacleType.BridgeWalkOver:
                return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Bridge.prefab";
            case PawPalAgilityObstacleType.HighFence:
                return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Fence_dog.prefab";
            case PawPalAgilityObstacleType.WheelJump:
                return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Wheel.prefab";
            case PawPalAgilityObstacleType.SeeSaw:
                return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Swing.prefab";
            default:
                return string.Empty;
        }
    }
}
#endif
