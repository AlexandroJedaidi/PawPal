using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed class IntroPetSelectionBootstrap : MonoBehaviour
{
    private const float FieldHalfSize = 6f;
    private const string RuntimeRootName = "IntroSceneRuntime";
    private const string EditorPreviewRootName = RuntimeRootName;
    private const string EditorPreviewCameraName = "Main Camera";
    private const string EditorPreviewLightName = "Warm Sun";
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
        EnvironmentBuildResult environment = BuildFieldEnvironment();

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
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(172, 210, 255, 255);
        RenderSettings.ambientEquatorColor = new Color32(197, 214, 178, 255);
        RenderSettings.ambientGroundColor = new Color32(111, 132, 91, 255);

        Light existingSun = null;
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
            {
                existingSun = lights[i];
                break;
            }
        }

        if (existingSun == null)
        {
            existingSun = new GameObject("Warm Sun").AddComponent<Light>();
            existingSun.type = LightType.Directional;
        }

        existingSun.transform.rotation = Quaternion.Euler(47f, -28f, 0f);
        existingSun.color = new Color32(255, 232, 190, 255);
        existingSun.intensity = 1.35f;
    }

    private static EnvironmentBuildResult BuildFieldEnvironment()
    {
        IntroSceneAssetCatalog catalog = Resources.Load<IntroSceneAssetCatalog>("PawPal/IntroPets/IntroSceneAssetCatalog");
        GameObject root = GameObject.Find(RuntimeRootName);
        if (root == null)
        {
            root = new GameObject(RuntimeRootName);
        }

        Bounds fieldBounds = new Bounds(Vector3.zero, new Vector3(FieldHalfSize * 1.65f, 1f, FieldHalfSize * 1.35f));

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

        return new EnvironmentBuildResult
        {
            FieldBounds = fieldBounds,
            Root = root.transform
        };
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
        Bounds fieldBounds = new Bounds(Vector3.zero, new Vector3(FieldHalfSize * 1.65f, 1f, FieldHalfSize * 1.35f));
        editorPreviewController.Configure(catalog, fieldBounds);
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

        Vector3 previewPosition = new Vector3(0f, PetSelectionCameraController.HomeSceneCameraHeight, -3.2f);
        Vector3 focusPoint = new Vector3(0f, 0.55f, 0f);
        camera.transform.SetPositionAndRotation(previewPosition, Quaternion.LookRotation(focusPoint - previewPosition, Vector3.up));

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null)
        {
            listener = camera.gameObject.AddComponent<AudioListener>();
        }

        listener.enabled = true;
        DisableOtherAudioListeners(camera);
    }

    private static void EnsureEditorPreviewLighting(Scene scene)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(172, 210, 255, 255);
        RenderSettings.ambientEquatorColor = new Color32(197, 214, 178, 255);
        RenderSettings.ambientGroundColor = new Color32(111, 132, 91, 255);

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

        light.transform.rotation = Quaternion.Euler(47f, -28f, 0f);
        light.color = new Color32(255, 232, 190, 255);
        light.intensity = 1.35f;
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

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
