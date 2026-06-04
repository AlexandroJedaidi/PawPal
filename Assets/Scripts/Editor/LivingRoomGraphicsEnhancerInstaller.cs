using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LivingRoomGraphicsEnhancerInstaller
{
    private const string EnhancerObjectName = "Living Room Graphics Enhancer";
    private const string LightingRootName = "Lighting";
    private const string AutoInstallSceneName = "David_Test";

    static LivingRoomGraphicsEnhancerInstaller()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("PawFriends/Graphics/Install Living Room Graphics Enhancer")]
    public static void InstallInOpenScene()
    {
        InstallOrApplyInOpenScene(true, true);
    }

    [MenuItem("PawFriends/Graphics/Apply Living Room Graphics Enhancer")]
    public static void ApplyExistingEnhancer()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        LivingRoomGraphicsEnhancer enhancer = activeScene.IsValid() ? FindExistingEnhancer(activeScene) : null;
        if (enhancer == null)
        {
            Debug.LogWarning("No LivingRoomGraphicsEnhancer exists in the open scene. Use PawFriends/Graphics/Install Living Room Graphics Enhancer first.");
            return;
        }

        enhancer.ApplyGraphics();
        EditorUtility.SetDirty(enhancer);
        EditorSceneManager.MarkSceneDirty(enhancer.gameObject.scene);
        Selection.activeGameObject = enhancer.gameObject;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += RefreshOpenDavidTestScenes;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.delayCall += RefreshOpenDavidTestScenes;
    }

    private static void AutoInstallForDavidTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += AutoInstallForDavidTest;
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != AutoInstallSceneName)
        {
            return;
        }

        InstallOrApplyInOpenScene(false, false);
    }

    private static void RefreshOpenDavidTestScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            return;
        }

        int sceneCount = EditorSceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded || scene.name != AutoInstallSceneName)
            {
                continue;
            }

            InstallOrApplyInScene(scene, false, false);
        }

        SceneView.RepaintAll();
    }

    private static void InstallOrApplyInOpenScene(bool selectEnhancer, bool useUndo)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return;
        }

        InstallOrApplyInScene(activeScene, selectEnhancer, useUndo);
    }

    private static void InstallOrApplyInScene(Scene scene, bool selectEnhancer, bool useUndo)
    {
        LivingRoomGraphicsEnhancer enhancer = FindExistingEnhancer(scene);
        if (enhancer == null)
        {
            GameObject enhancerObject = new GameObject(EnhancerObjectName);
            if (useUndo)
            {
                Undo.RegisterCreatedObjectUndo(enhancerObject, "Create Living Room Graphics Enhancer");
            }

            SceneManager.MoveGameObjectToScene(enhancerObject, scene);

            GameObject lightingRoot = FindRootObject(scene, LightingRootName);
            if (lightingRoot != null)
            {
                if (useUndo)
                {
                    Undo.SetTransformParent(enhancerObject.transform, lightingRoot.transform, "Parent Living Room Graphics Enhancer");
                }
                else
                {
                    enhancerObject.transform.SetParent(lightingRoot.transform, false);
                }

                enhancerObject.transform.localPosition = Vector3.zero;
                enhancerObject.transform.localRotation = Quaternion.identity;
                enhancerObject.transform.localScale = Vector3.one;
            }

            enhancer = useUndo
                ? Undo.AddComponent<LivingRoomGraphicsEnhancer>(enhancerObject)
                : enhancerObject.AddComponent<LivingRoomGraphicsEnhancer>();
        }

        enhancer.ApplyGraphics();
        EditorUtility.SetDirty(enhancer);
        EditorSceneManager.MarkSceneDirty(enhancer.gameObject.scene);

        if (selectEnhancer)
        {
            Selection.activeGameObject = enhancer.gameObject;
        }
    }

    private static LivingRoomGraphicsEnhancer FindExistingEnhancer(Scene scene)
    {
#if UNITY_2022_2_OR_NEWER
        LivingRoomGraphicsEnhancer[] enhancers = Object.FindObjectsByType<LivingRoomGraphicsEnhancer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        LivingRoomGraphicsEnhancer[] enhancers = Object.FindObjectsOfType<LivingRoomGraphicsEnhancer>(true);
#endif
        for (int i = 0; i < enhancers.Length; i++)
        {
            if (enhancers[i] != null && enhancers[i].gameObject.scene == scene)
            {
                return enhancers[i];
            }
        }

        return null;
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }
}
