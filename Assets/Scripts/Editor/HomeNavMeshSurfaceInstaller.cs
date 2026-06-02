using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

public static class HomeNavMeshSurfaceInstaller
{
    private const string MenuRoot = "PawFriends/Navigation/";
    private const string ConfigureMenuPath = MenuRoot + "Configure Home NavMesh Surface";
    private const string ConfigureAndBakeMenuPath = MenuRoot + "Configure And Bake Home NavMesh Surface";
    private const string HomeScenePath = "Assets/Scenes/David_Test.unity";
    private const string DirectorObjectName = "DogSocialDirector";
    private const string ObstacleRootName = "GeneratedNavMeshObstacles";
    private const string RuntimeMatchedNavMeshAssetPath = "Assets/Scenes/David_Test/NavMesh-DogSocialDirectorRuntimeMatched.asset";

    private static readonly Vector3 SurfaceSize = new Vector3(8.4f, 2.6f, 7.2f);
    private static readonly Vector3 SurfaceCenter = new Vector3(0f, 1.3f, -0.75f);
    private static readonly Vector3 DefaultRuntimeNavMeshCenter = Vector3.zero;
    private static readonly Vector3 DefaultRuntimeNavMeshSize = new Vector3(7.68f, 0.12f, 6.72f);

    private const float SurfaceVoxelSize = 0.08f;
    private const float SurfaceMinRegionArea = 0.05f;
    private const int SurfaceTileSize = 128;
    private const float RuntimeObstaclePadding = 0.04f;
    private const float RuntimeGroundedObstacleClearance = 0.18f;
    private const float RuntimeMinimumObstacleHeight = 0.2f;
    private const float RuntimeAgentRadius = 0.26f;
    private const float RuntimeAgentHeight = 0.6f;
    private const float RuntimeAgentClimb = 0.2f;
    private const float RuntimeAgentSlope = 45f;
    private const float RuntimeMinRegionArea = 0.1f;

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
            NavMeshData runtimeMatchedData = BuildRuntimeMatchedNavMeshData(directorObject, surface);
            if (runtimeMatchedData == null)
            {
                Debug.LogError("Could not build the runtime-matched home NavMesh preview.");
                return;
            }

            SaveRuntimeMatchedNavMeshAsset(surface, runtimeMatchedData);
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

    private static NavMeshData BuildRuntimeMatchedNavMeshData(GameObject directorObject, NavMeshSurface surface)
    {
        if (directorObject == null || surface == null)
        {
            return null;
        }

        NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(surface.agentTypeID);
        buildSettings.agentRadius = RuntimeAgentRadius;
        buildSettings.agentHeight = RuntimeAgentHeight;
        buildSettings.agentClimb = RuntimeAgentClimb;
        buildSettings.agentSlope = RuntimeAgentSlope;
        buildSettings.minRegionArea = RuntimeMinRegionArea;

        Vector3 navMeshCenter = GetPrivateVector3Field(directorObject, "runtimeNavMeshCenter", DefaultRuntimeNavMeshCenter);
        Vector3 navMeshSize = GetPrivateVector3Field(directorObject, "runtimeNavMeshSize", DefaultRuntimeNavMeshSize);
        bool carveGroundedObstacleFootprints = GetPrivateBoolField(directorObject, "carveGroundedObstacleFootprintsInRuntimeNavMesh", true);
        DogRoomAgent dogAgent = GetPrivateObjectField<DogRoomAgent>(directorObject, "dogA");
        Bounds dogRoomBounds;
        if (dogAgent != null && dogAgent.TryGetRoomBounds(out dogRoomBounds))
        {
            navMeshCenter = dogRoomBounds.center;
            navMeshSize = new Vector3(dogRoomBounds.size.x, navMeshSize.y, dogRoomBounds.size.z);
        }

        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        NavMeshBuildSource floorSource = new NavMeshBuildSource();
        floorSource.shape = NavMeshBuildSourceShape.Box;
        floorSource.transform = Matrix4x4.TRS(navMeshCenter, Quaternion.identity, Vector3.one);
        floorSource.size = navMeshSize;
        floorSource.area = 0;
        sources.Add(floorSource);

        float runtimeFloorY = dogAgent != null && dogAgent.TryGetRoomBounds(out dogRoomBounds)
            ? dogRoomBounds.min.y
            : navMeshCenter.y;
        Bounds obstacleBounds = new Bounds(navMeshCenter, navMeshSize);
        if (carveGroundedObstacleFootprints)
        {
            AddRuntimeMatchedObstacleSources(sources, runtimeFloorY, obstacleBounds, directorObject.transform);
        }

        Bounds buildBounds = new Bounds(
            navMeshCenter + Vector3.up,
            new Vector3(navMeshSize.x + 1f, 3f, navMeshSize.z + 1f));

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(
            buildSettings,
            sources,
            buildBounds,
            Vector3.zero,
            Quaternion.identity);

        if (data != null)
        {
            data.name = DirectorObjectName;
        }

        return data;
    }

    private static void AddRuntimeMatchedObstacleSources(
        List<NavMeshBuildSource> sources,
        float floorY,
        Bounds navMeshBounds,
        Transform directorTransform)
    {
        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (!ShouldUseRuntimeMatchedObstacleCollider(collider, floorY, navMeshBounds, directorTransform))
            {
                continue;
            }

            Bounds obstacleBounds = collider.bounds;
            float obstacleHeight = Mathf.Max(
                Mathf.Max(0f, obstacleBounds.max.y - floorY),
                Mathf.Max(0.05f, RuntimeMinimumObstacleHeight));

            NavMeshBuildSource obstacleSource = new NavMeshBuildSource();
            obstacleSource.shape = NavMeshBuildSourceShape.Box;
            obstacleSource.transform = Matrix4x4.TRS(
                new Vector3(
                    obstacleBounds.center.x,
                    floorY + obstacleHeight * 0.5f,
                    obstacleBounds.center.z),
                Quaternion.identity,
                Vector3.one);
            obstacleSource.size = new Vector3(
                Mathf.Max(0.05f, obstacleBounds.size.x + RuntimeObstaclePadding * 2f),
                obstacleHeight,
                Mathf.Max(0.05f, obstacleBounds.size.z + RuntimeObstaclePadding * 2f));
            obstacleSource.area = 1;
            sources.Add(obstacleSource);
        }
    }

    private static bool ShouldUseRuntimeMatchedObstacleCollider(
        Collider collider,
        float floorY,
        Bounds navMeshBounds,
        Transform directorTransform)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
        {
            return false;
        }

        if (collider.transform.IsChildOf(directorTransform))
        {
            return false;
        }

        if (!collider.bounds.Intersects(navMeshBounds))
        {
            return false;
        }

        if (collider.bounds.min.y > floorY + Mathf.Max(0f, RuntimeGroundedObstacleClearance))
        {
            return false;
        }

        if (collider.bounds.size.y < Mathf.Max(0.01f, RuntimeMinimumObstacleHeight))
        {
            return false;
        }

        if (collider.bounds.size.x < 0.05f || collider.bounds.size.z < 0.05f)
        {
            return false;
        }

        if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)
        {
            return false;
        }

        if (collider.GetComponentInParent<DogRoomAgent>() != null
            || collider.GetComponentInParent<PawPalToyRuntimeMetadata>() != null)
        {
            return false;
        }

        return true;
    }

    private static void SaveRuntimeMatchedNavMeshAsset(NavMeshSurface surface, NavMeshData navMeshData)
    {
        if (surface == null || navMeshData == null)
        {
            return;
        }

        string targetDirectory = Path.GetDirectoryName(RuntimeMatchedNavMeshAssetPath);
        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        surface.RemoveData();
        surface.navMeshData = null;

        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(RuntimeMatchedNavMeshAssetPath) != null)
        {
            AssetDatabase.DeleteAsset(RuntimeMatchedNavMeshAssetPath);
        }

        AssetDatabase.CreateAsset(navMeshData, RuntimeMatchedNavMeshAssetPath);
        surface.navMeshData = navMeshData;

        if (surface.isActiveAndEnabled)
        {
            surface.AddData();
        }
    }

    private static Vector3 GetPrivateVector3Field(GameObject gameObject, string fieldName, Vector3 fallback)
    {
        FieldInfo field = GetPrivateInstanceField(gameObject, fieldName);
        return field != null && field.FieldType == typeof(Vector3)
            ? (Vector3)field.GetValue(gameObject.GetComponent(field.DeclaringType))
            : fallback;
    }

    private static bool GetPrivateBoolField(GameObject gameObject, string fieldName, bool fallback)
    {
        FieldInfo field = GetPrivateInstanceField(gameObject, fieldName);
        return field != null && field.FieldType == typeof(bool)
            ? (bool)field.GetValue(gameObject.GetComponent(field.DeclaringType))
            : fallback;
    }

    private static T GetPrivateObjectField<T>(GameObject gameObject, string fieldName) where T : class
    {
        FieldInfo field = GetPrivateInstanceField(gameObject, fieldName);
        return field != null
            ? field.GetValue(gameObject.GetComponent(field.DeclaringType)) as T
            : null;
    }

    private static FieldInfo GetPrivateInstanceField(GameObject gameObject, string fieldName)
    {
        DogSocialDirector director = gameObject != null ? gameObject.GetComponent<DogSocialDirector>() : null;
        if (director == null)
        {
            return null;
        }

        return typeof(DogSocialDirector).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
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
