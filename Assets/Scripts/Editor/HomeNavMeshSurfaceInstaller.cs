using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class HomeNavMeshSurfaceInstaller
{
    private const string MenuRoot = "PawFriends/Navigation/";
    private const string ConfigureMenuPath = MenuRoot + "Configure Home NavMesh Surface";
    private const string ConfigureAndBakeMenuPath = MenuRoot + "Configure And Bake Home NavMesh Surface";
    private const string HomeScenePath = "Assets/Scenes/David_Test.unity";
    private const string DirectorObjectName = "DogSocialDirector";
    private const string ObstacleRootName = "GeneratedNavMeshObstacles";

    private static readonly Vector3 SurfaceSize = new Vector3(7f, 2.6f, 6f);
    private static readonly Vector3 SurfaceCenter = new Vector3(0f, 1.3f, -0.75f);

    private const float SurfaceVoxelSize = 0.08f;
    private const float SurfaceMinRegionArea = 0.05f;
    private const int SurfaceTileSize = 128;

    [MenuItem(ConfigureMenuPath)]
    public static void ConfigureHomeNavMeshSurface()
    {
        ConfigureHomeSceneSurface(bakeNavMesh: false);
    }

    [MenuItem(ConfigureAndBakeMenuPath)]
    public static void ConfigureAndBakeHomeNavMeshSurface()
    {
        ConfigureHomeSceneSurface(bakeNavMesh: true);
    }

    private static void ConfigureHomeSceneSurface(bool bakeNavMesh)
    {
        Scene scene = EnsureHomeSceneOpen();
        if (!scene.IsValid())
        {
            Debug.LogError("Could not open the home scene to configure its NavMesh surface.");
            return;
        }

        GameObject directorObject = FindRootObject(scene, DirectorObjectName);
        if (directorObject == null)
        {
            Debug.LogError("Could not find DogSocialDirector in the home scene.");
            return;
        }

        NavMeshSurface surface = directorObject.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = Undo.AddComponent<NavMeshSurface>(directorObject);
        }

        RemoveGeneratedObstacleRoot(directorObject);
        ConfigureSurface(surface);
        EditorUtility.SetDirty(surface);

        if (bakeNavMesh)
        {
            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = directorObject;

        Debug.Log(bakeNavMesh
            ? "Configured and baked the home NavMesh surface on DogSocialDirector."
            : "Configured the home NavMesh surface on DogSocialDirector.");
    }

    private static void ConfigureSurface(NavMeshSurface surface)
    {
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.Volume;
        surface.size = SurfaceSize;
        surface.center = SurfaceCenter;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.overrideTileSize = true;
        surface.tileSize = SurfaceTileSize;
        surface.overrideVoxelSize = true;
        surface.voxelSize = SurfaceVoxelSize;
        surface.minRegionArea = SurfaceMinRegionArea;
        surface.buildHeightMesh = false;
    }

    private static void RemoveGeneratedObstacleRoot(GameObject directorObject)
    {
        Transform existingRoot = directorObject.transform.Find(ObstacleRootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot.gameObject);
        }
    }

    private static Scene EnsureHomeSceneOpen()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.path == HomeScenePath)
        {
            return activeScene;
        }

        return EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
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
