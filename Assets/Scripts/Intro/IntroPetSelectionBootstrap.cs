using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed class IntroPetSelectionBootstrap : MonoBehaviour
{
    private const string RuntimeRootName = "IntroSceneRuntime";
    private const string RuntimeNavMeshRootName = "IntroRuntimeNavMesh";
    private const string RuntimeWalkSurfaceName = "IntroWalkSurface";
    private const string EditorPreviewRootName = RuntimeRootName;
    private const string EditorPreviewCameraName = "Main Camera";
    private const string EditorPreviewLightName = "Warm Sun";
    private static readonly string[] IntroToyNames =
    {
        "Toy_BallLeft",
        "Toy_BallRight",
        "Toy_BoneCenter",
        "Big_ball_1",
        "BallHole_1",
        "Bone_2"
    };
    private static IntroPetSelectionBootstrap instance;
#if UNITY_EDITOR
    private static IntroBackdropRingController editorPreviewController;
    private static bool editorPreviewInstalled;
#endif

    private struct EnvironmentBuildResult
    {
        public Bounds FieldBounds;
        public Transform Root;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("IntroPetSelectionBootstrap");
        DontDestroyOnLoad(bootstrap);
        instance = bootstrap.AddComponent<IntroPetSelectionBootstrap>();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void InstallEditorPreview()
    {
        if (editorPreviewInstalled)
        {
            return;
        }

        editorPreviewInstalled = true;
        EditorApplication.delayCall += RefreshEditorPreview;
        EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChangedInEditMode;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }
#endif

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!PawPalIntroSceneFlow.IsIntroScene(scene))
        {
            CleanupNonIntroSceneArtifacts();
            return;
        }

        StartCoroutine(BuildIntroSceneRoutine());
    }

    private IEnumerator BuildIntroSceneRoutine()
    {
        yield return null;

        if (FindFirstObjectByType<IntroPetSelectionController>(FindObjectsInactive.Include) != null)
        {
            yield break;
        }

        EnsureEventSystem();
        Camera sceneCamera = EnsureCamera();
        BuildLighting();
        EnvironmentBuildResult environment = BuildFieldEnvironment(sceneCamera);

        GameObject controllerObject = new GameObject("IntroPetSelectionController");
        controllerObject.transform.SetParent(environment.Root, false);
        IntroPetSelectionController controller = controllerObject.AddComponent<IntroPetSelectionController>();
        controller.Initialize(environment.FieldBounds, sceneCamera);
    }

    private static Camera EnsureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 35f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 80f;
        camera.transform.SetPositionAndRotation(
            PetSelectionCameraController.IntroSceneCameraPosition,
            Quaternion.Euler(PetSelectionCameraController.IntroSceneCameraEulerAngles));

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }

        DisableOtherAudioListeners(camera);

        return camera;
    }

    private static void BuildLighting()
    {
        Light existingSun = ResolveDirectionalLight();
        if (existingSun == null)
        {
            existingSun = new GameObject("Warm Sun").AddComponent<Light>();
            existingSun.type = LightType.Directional;
        }

        RenderSettings.sun = existingSun;
    }

    private static EnvironmentBuildResult BuildFieldEnvironment(Camera sceneCamera)
    {
        IntroSceneAssetCatalog catalog = Resources.Load<IntroSceneAssetCatalog>("PawPal/IntroPets/IntroSceneAssetCatalog");
        GameObject root = GameObject.Find(RuntimeRootName);
        if (root == null)
        {
            root = new GameObject(RuntimeRootName);
        }

        Bounds fieldBounds = IntroPetPlayfieldResolver.Resolve(sceneCamera);

        IntroBackdropRingController backdropRingController = root.GetComponent<IntroBackdropRingController>();
        if (backdropRingController == null)
        {
            backdropRingController = root.AddComponent<IntroBackdropRingController>();
        }

        backdropRingController.Configure(catalog, fieldBounds);

        IntroTimeOfDayAudioController audioController = root.GetComponent<IntroTimeOfDayAudioController>();
        if (audioController == null)
        {
            audioController = root.AddComponent<IntroTimeOfDayAudioController>();
        }

        audioController.Configure(catalog, backdropRingController);
        IntroTimeOfDayLightingController lightingController = root.GetComponent<IntroTimeOfDayLightingController>();
        if (lightingController == null)
        {
            lightingController = root.AddComponent<IntroTimeOfDayLightingController>();
        }

        lightingController.Configure(backdropRingController, ResolveDirectionalLight());
        IntroTimeOfDayPracticalLightsController practicalLightsController = root.GetComponent<IntroTimeOfDayPracticalLightsController>();
        if (practicalLightsController == null)
        {
            practicalLightsController = root.AddComponent<IntroTimeOfDayPracticalLightsController>();
        }

        practicalLightsController.Configure(backdropRingController);
        EnsureRuntimeNavMesh(root, fieldBounds);
        CleanupIntroSceneToys(root.transform);

        return new EnvironmentBuildResult
        {
            FieldBounds = fieldBounds,
            Root = root.transform
        };
    }

    private static void EnsureRuntimeNavMesh(GameObject root, Bounds fieldBounds)
    {
        if (root == null)
        {
            return;
        }

        NavMeshSurface legacySurface = root.GetComponent<NavMeshSurface>();
        if (legacySurface != null)
        {
            if (Application.isPlaying)
            {
                Destroy(legacySurface);
            }
            else
            {
                DestroyImmediate(legacySurface);
            }
        }

        Transform navMeshRoot = root.transform.Find(RuntimeNavMeshRootName);
        if (navMeshRoot == null)
        {
            navMeshRoot = new GameObject(RuntimeNavMeshRootName).transform;
            navMeshRoot.SetParent(root.transform, false);
        }

        EnsureRuntimeWalkSurface(navMeshRoot, fieldBounds);

        NavMeshSurface surface = navMeshRoot.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = navMeshRoot.gameObject.AddComponent<NavMeshSurface>();
        }

        surface.collectObjects = CollectObjects.Children;
        surface.center = Vector3.zero;
        surface.size = new Vector3(fieldBounds.size.x, 1f, fieldBounds.size.z);
        surface.layerMask = Physics.DefaultRaycastLayers;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
    }

    private static void EnsureRuntimeWalkSurface(Transform navMeshRoot, Bounds fieldBounds)
    {
        if (navMeshRoot == null)
        {
            return;
        }

        Transform existing = navMeshRoot.Find(RuntimeWalkSurfaceName);
        GameObject walkSurface = existing != null ? existing.gameObject : new GameObject(RuntimeWalkSurfaceName);
        if (walkSurface.transform.parent != navMeshRoot)
        {
            walkSurface.transform.SetParent(navMeshRoot, false);
        }

        walkSurface.transform.SetPositionAndRotation(
            new Vector3(fieldBounds.center.x, fieldBounds.min.y - 0.05f, fieldBounds.center.z),
            Quaternion.identity);
        walkSurface.transform.localScale = Vector3.one;

        BoxCollider collider = walkSurface.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = walkSurface.AddComponent<BoxCollider>();
        }

        collider.isTrigger = false;
        collider.center = Vector3.zero;
        collider.size = new Vector3(fieldBounds.size.x, 0.1f, fieldBounds.size.z);
    }

#if UNITY_EDITOR
    private static void HandleActiveSceneChangedInEditMode(Scene previousScene, Scene nextScene)
    {
        RefreshEditorPreview();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            editorPreviewController = null;
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            RefreshEditorPreview();
        }
    }

    private static void RefreshEditorPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!PawPalIntroSceneFlow.IsIntroScene(activeScene))
        {
            editorPreviewController = null;
            return;
        }

        EnsureEditorPreview(activeScene);
    }

    private static void EnsureEditorPreview(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (editorPreviewController == null)
        {
            GameObject existingPreview = GameObject.Find(EditorPreviewRootName);
            if (existingPreview != null)
            {
                editorPreviewController = existingPreview.GetComponent<IntroBackdropRingController>();
            }
        }

        if (editorPreviewController == null)
        {
            GameObject previewRoot = new GameObject(EditorPreviewRootName);
            SceneManager.MoveGameObjectToScene(previewRoot, scene);
            editorPreviewController = previewRoot.AddComponent<IntroBackdropRingController>();
        }

        editorPreviewController.gameObject.hideFlags = HideFlags.None;

        IntroSceneAssetCatalog catalog = Resources.Load<IntroSceneAssetCatalog>("PawPal/IntroPets/IntroSceneAssetCatalog");
        Bounds fieldBounds = IntroPetPlayfieldResolver.Resolve(Camera.main);
        editorPreviewController.Configure(catalog, fieldBounds);
        CleanupIntroSceneToys(editorPreviewController.transform);
        EnsureEditorPreviewCamera(scene);
        EnsureEditorPreviewLighting(scene);
    }

    private static void EnsureEditorPreviewCamera(Scene scene)
    {
        Camera camera = Camera.main;
        if (camera == null || camera.gameObject.scene != scene)
        {
            GameObject previewRoot = editorPreviewController != null ? editorPreviewController.gameObject : null;
            Transform parent = previewRoot != null ? previewRoot.transform : null;
            Transform existingChild = parent != null ? parent.Find(EditorPreviewCameraName) : null;
            GameObject cameraObject = existingChild != null ? existingChild.gameObject : new GameObject(EditorPreviewCameraName);
            if (parent != null && cameraObject.transform.parent != parent)
            {
                cameraObject.transform.SetParent(parent, false);
            }

            cameraObject.tag = "MainCamera";

            camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }
        }

        camera.gameObject.hideFlags = HideFlags.None;

        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 35f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 80f;
        camera.transform.SetPositionAndRotation(
            PetSelectionCameraController.IntroSceneCameraPosition,
            Quaternion.Euler(PetSelectionCameraController.IntroSceneCameraEulerAngles));

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null)
        {
            listener = camera.gameObject.AddComponent<AudioListener>();
        }

        listener.enabled = true;
        DisableOtherAudioListeners(camera);

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            sceneView.LookAtDirect(
                PetSelectionCameraController.IntroSceneCameraPosition,
                Quaternion.Euler(PetSelectionCameraController.IntroSceneCameraEulerAngles),
                4.5f);
            sceneView.Repaint();
        }
    }

    private static void EnsureEditorPreviewLighting(Scene scene)
    {
        GameObject previewRoot = editorPreviewController != null ? editorPreviewController.gameObject : null;
        Transform parent = previewRoot != null ? previewRoot.transform : null;

        Light light = null;
        if (parent != null)
        {
            Transform existingChild = parent.Find(EditorPreviewLightName);
            if (existingChild != null)
            {
                light = existingChild.GetComponent<Light>();
            }
        }

        if (light == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional && lights[i].gameObject.scene == scene)
                {
                    light = lights[i];
                    break;
                }
            }
        }

        if (light == null)
        {
            GameObject lightObject = new GameObject(EditorPreviewLightName);
            if (parent != null)
            {
                lightObject.transform.SetParent(parent, false);
            }

            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.gameObject.hideFlags = HideFlags.None;
        IntroTimeOfDayLightingController lightingController = previewRoot != null
            ? previewRoot.GetComponent<IntroTimeOfDayLightingController>()
            : null;
        if (previewRoot != null && lightingController == null)
        {
            lightingController = previewRoot.AddComponent<IntroTimeOfDayLightingController>();
        }

        if (lightingController != null)
        {
            lightingController.Configure(editorPreviewController, light);
        }

        IntroTimeOfDayPracticalLightsController practicalLightsController = previewRoot != null
            ? previewRoot.GetComponent<IntroTimeOfDayPracticalLightsController>()
            : null;
        if (previewRoot != null && practicalLightsController == null)
        {
            practicalLightsController = previewRoot.AddComponent<IntroTimeOfDayPracticalLightsController>();
        }

        if (practicalLightsController != null)
        {
            practicalLightsController.Configure(editorPreviewController);
        }
    }

    private static void DestroyEditorPreview()
    {
        if (editorPreviewController == null)
        {
            GameObject existingPreview = GameObject.Find(EditorPreviewRootName);
            if (existingPreview == null)
            {
                return;
            }

            editorPreviewController = existingPreview.GetComponent<IntroBackdropRingController>();
            if (editorPreviewController == null)
            {
                UnityEngine.Object.DestroyImmediate(existingPreview);
                return;
            }
        }

        GameObject previewObject = editorPreviewController.gameObject;
        editorPreviewController = null;
        if (previewObject != null)
        {
            UnityEngine.Object.DestroyImmediate(previewObject);
        }
    }
#endif

    private static void DisableOtherAudioListeners(Camera primaryCamera)
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
            {
                continue;
            }

            listener.enabled = primaryCamera != null && listener.gameObject == primaryCamera.gameObject;
        }
    }

    private static Light ResolveDirectionalLight()
    {
        if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
        {
            return RenderSettings.sun;
        }

        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
            {
                return lights[i];
            }
        }

        return null;
    }

    private static void CleanupIntroSceneToys(Transform runtimeRoot)
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || candidate == runtimeRoot)
            {
                continue;
            }

            GameObject candidateObject = candidate.gameObject;
            if (!IsIntroSceneToyObject(candidateObject))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(candidateObject);
            }
            else
            {
                DestroyImmediate(candidateObject);
            }
        }
    }

    private static void CleanupNonIntroSceneArtifacts()
    {
        IntroBackdropRingController[] backdropControllers = FindObjectsByType<IntroBackdropRingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < backdropControllers.Length; i++)
        {
            IntroBackdropRingController controller = backdropControllers[i];
            if (controller == null || PawPalIntroSceneFlow.IsIntroScene(controller.gameObject.scene))
            {
                continue;
            }

            DestroySceneObject(controller.gameObject);
        }

        GameObject runtimeRoot = GameObject.Find(RuntimeRootName);
        if (runtimeRoot != null && !PawPalIntroSceneFlow.IsIntroScene(runtimeRoot.scene))
        {
            DestroySceneObject(runtimeRoot);
        }
    }

    private static void DestroySceneObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static bool IsIntroSceneToyObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.GetComponent<IntroPetToyAnchor>() != null
            || candidate.GetComponent<PawPalToyRuntimeMetadata>() != null)
        {
            return true;
        }

        string name = candidate.name;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (int i = 0; i < IntroToyNames.Length; i++)
        {
            if (string.Equals(name, IntroToyNames[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureEventSystem()
    {
        PawPalEventSystemUtility.EnsureSingleEventSystem(false);
    }
}
