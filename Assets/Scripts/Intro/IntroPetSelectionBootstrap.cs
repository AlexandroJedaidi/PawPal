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
    private const string EditorPreviewRootName = "IntroBackdropPreview";
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
        GameObject root = new GameObject("IntroSceneRuntime");
        Bounds fieldBounds = new Bounds(Vector3.zero, new Vector3(FieldHalfSize * 1.65f, 1f, FieldHalfSize * 1.35f));

        IntroBackdropRingController backdropRingController = root.AddComponent<IntroBackdropRingController>();
        backdropRingController.Configure(catalog, fieldBounds);

        IntroTimeOfDayAudioController audioController = root.AddComponent<IntroTimeOfDayAudioController>();
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
            DestroyEditorPreview();
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
            DestroyEditorPreview();
            return;
        }

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!PawPalIntroSceneFlow.IsIntroScene(activeScene))
        {
            DestroyEditorPreview();
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
            previewRoot.hideFlags = HideFlags.DontSaveInEditor;
            SceneManager.MoveGameObjectToScene(previewRoot, scene);
            editorPreviewController = previewRoot.AddComponent<IntroBackdropRingController>();
        }

        IntroSceneAssetCatalog catalog = Resources.Load<IntroSceneAssetCatalog>("PawPal/IntroPets/IntroSceneAssetCatalog");
        Bounds fieldBounds = new Bounds(Vector3.zero, new Vector3(FieldHalfSize * 1.65f, 1f, FieldHalfSize * 1.35f));
        editorPreviewController.Configure(catalog, fieldBounds);
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
