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
        EditorApplication.delayCall += AutoInstallForDavidTest;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    [MenuItem("PawFriends/Graphics/Install Living Room Graphics Enhancer")]
    public static void InstallInOpenScene()
    {
        InstallOrApplyInOpenScene(true, true);
    }

    [MenuItem("PawFriends/Graphics/Apply Living Room Graphics Enhancer")]
    public static void ApplyExistingEnhancer()
    {
        LivingRoomGraphicsEnhancer enhancer = FindExistingEnhancer();
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
        EditorApplication.delayCall += AutoInstallForDavidTest;
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

    private static void InstallOrApplyInOpenScene(bool selectEnhancer, bool useUndo)
    {
        LivingRoomGraphicsEnhancer enhancer = FindExistingEnhancer();
        if (enhancer == null)
        {
            GameObject enhancerObject = new GameObject(EnhancerObjectName);
            if (useUndo)
            {
                Undo.RegisterCreatedObjectUndo(enhancerObject, "Create Living Room Graphics Enhancer");
            }

            GameObject lightingRoot = FindRootObject(LightingRootName);
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

    private static LivingRoomGraphicsEnhancer FindExistingEnhancer()
    {
#if UNITY_2022_2_OR_NEWER
        LivingRoomGraphicsEnhancer[] enhancers = Object.FindObjectsByType<LivingRoomGraphicsEnhancer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        LivingRoomGraphicsEnhancer[] enhancers = Object.FindObjectsOfType<LivingRoomGraphicsEnhancer>(true);
#endif
        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < enhancers.Length; i++)
        {
            if (enhancers[i] != null && enhancers[i].gameObject.scene == activeScene)
            {
                return enhancers[i];
            }
        }

        return null;
    }

    private static GameObject FindRootObject(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            return null;
        }

        GameObject[] roots = activeScene.GetRootGameObjects();
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
