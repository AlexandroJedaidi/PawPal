using System.Collections;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class IntroPetSelectionBootstrap : MonoBehaviour
{
    private const float FieldHalfSize = 6f;
    private static IntroPetSelectionBootstrap instance;

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
        Bounds fieldBounds = BuildFieldEnvironment();

        GameObject controllerObject = new GameObject("IntroPetSelectionController");
        IntroPetSelectionController controller = controllerObject.AddComponent<IntroPetSelectionController>();
        controller.Initialize(fieldBounds, sceneCamera);
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

    private static Bounds BuildFieldEnvironment()
    {
        IntroSceneAssetCatalog catalog = Resources.Load<IntroSceneAssetCatalog>("PawPal/IntroPets/IntroSceneAssetCatalog");
        GameObject root = new GameObject("IntroSunnyField");

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GrassField";
        ground.transform.SetParent(root.transform, false);
        ground.transform.localScale = new Vector3(1.45f, 1f, 1.45f);
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material grass = BuildColorMaterial("IntroGrassRuntime", new Color32(111, 177, 91, 255));
            grass.name = "IntroGrassRuntime";
            renderer.sharedMaterial = grass;
        }

        SpawnFence(catalog, root.transform);
        SpawnTrees(catalog, root.transform);
        SpawnToys(catalog, root.transform);
        SpawnAmbientAudio(catalog, root.transform);

        return new Bounds(Vector3.zero, new Vector3(FieldHalfSize * 1.65f, 1f, FieldHalfSize * 1.35f));
    }

    private static void SpawnFence(IntroSceneAssetCatalog catalog, Transform root)
    {
        GameObject prefab = catalog != null ? catalog.FencePrefab : null;
        for (int side = 0; side < 4; side++)
        {
            for (int i = -3; i <= 3; i++)
            {
                Vector3 position;
                Quaternion rotation;
                if (side < 2)
                {
                    position = new Vector3(i * 1.7f, 0f, side == 0 ? 5.15f : -5.15f);
                    rotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
                }
                else
                {
                    position = new Vector3(side == 2 ? 5.75f : -5.75f, 0f, i * 1.55f);
                    rotation = Quaternion.Euler(0f, side == 2 ? 90f : -90f, 0f);
                }

                GameObject fence = prefab != null ? InstantiatePrefabRoot(prefab, position, rotation, root, "FencePrefab") : null;
                if (fence == null)
                {
                    fence = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fence.name = "GardenFence";
                    fence.transform.SetParent(root, false);
                    fence.transform.position = position;
                    fence.transform.rotation = rotation;
                    fence.transform.localScale = new Vector3(1.55f, 0.42f, 0.08f);
                    Renderer renderer = fence.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = BuildColorMaterial("IntroFenceFallback", new Color32(166, 113, 70, 255));
                    }

                    continue;
                }

                fence.name = "GardenFence";
            }
        }
    }

    private static void SpawnTrees(IntroSceneAssetCatalog catalog, Transform root)
    {
        Vector3[] positions =
        {
            new Vector3(-5.7f, 0f, 4.3f),
            new Vector3(5.2f, 0f, 4.7f),
            new Vector3(-5.4f, 0f, -4.4f),
            new Vector3(5.5f, 0f, -4.1f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject prefab = catalog != null && i % 2 == 0 ? catalog.BirchTreePrefab : catalog != null ? catalog.AshTreePrefab : null;
            if (prefab != null)
            {
                GameObject tree = InstantiatePrefabRoot(prefab, positions[i], Quaternion.Euler(0f, i * 63f, 0f), root, i % 2 == 0 ? "BirchTreePrefab" : "AshTreePrefab");
                if (tree == null)
                {
                    SpawnFallbackTree(root, positions[i], i * 63f);
                    continue;
                }

                tree.name = "IntroTree";
                tree.transform.localScale = Vector3.one * 0.72f;
            }
            else
            {
                SpawnFallbackTree(root, positions[i], i * 63f);
            }
        }
    }

    private static void SpawnFallbackTree(Transform root, Vector3 position, float yaw)
    {
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "IntroTreeTrunk";
        trunk.transform.SetParent(root, false);
        trunk.transform.position = position + Vector3.up * 0.65f;
        trunk.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        trunk.transform.localScale = new Vector3(0.18f, 0.65f, 0.18f);
        Renderer trunkRenderer = trunk.GetComponent<Renderer>();
        if (trunkRenderer != null)
        {
            trunkRenderer.sharedMaterial = BuildColorMaterial("IntroTreeTrunkFallback", new Color32(112, 78, 49, 255));
        }

        GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        canopy.name = "IntroTreeCanopy";
        canopy.transform.SetParent(root, false);
        canopy.transform.position = position + Vector3.up * 1.65f;
        canopy.transform.localScale = new Vector3(1.05f, 0.85f, 1.05f);
        Renderer canopyRenderer = canopy.GetComponent<Renderer>();
        if (canopyRenderer != null)
        {
            canopyRenderer.sharedMaterial = BuildColorMaterial("IntroTreeCanopyFallback", new Color32(80, 153, 88, 255));
        }
    }

    private static void SpawnToys(IntroSceneAssetCatalog catalog, Transform root)
    {
        if (catalog != null && catalog.BigBallPrefab != null)
        {
            GameObject ball = InstantiatePrefabRoot(catalog.BigBallPrefab, new Vector3(-2.6f, 0.12f, -1.7f), Quaternion.identity, root, "BigBallPrefab");
            if (ball != null)
            {
                ball.name = "IntroToy_Ball";
                ball.transform.localScale = Vector3.one * 0.45f;
            }
        }

        if (catalog != null && catalog.BonePrefab != null)
        {
            GameObject bone = InstantiatePrefabRoot(catalog.BonePrefab, new Vector3(2.7f, 0.05f, -1.2f), Quaternion.Euler(0f, 36f, 0f), root, "BonePrefab");
            if (bone != null)
            {
                bone.name = "IntroToy_Bone";
                bone.transform.localScale = Vector3.one * 0.55f;
            }
        }
    }

    private static void SpawnAmbientAudio(IntroSceneAssetCatalog catalog, Transform root)
    {
        if (catalog == null || catalog.BirdsAmbientClip == null)
        {
            return;
        }

        AudioSource source = root.gameObject.AddComponent<AudioSource>();
        source.clip = catalog.BirdsAmbientClip;
        source.loop = true;
        source.playOnAwake = true;
        source.volume = 0.18f;
        source.spatialBlend = 0f;
        source.Play();
    }

    private static Material BuildColorMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = name;
        material.color = color;
        return material;
    }

    private static GameObject InstantiatePrefabRoot(UnityEngine.Object prefab, Vector3 position, Quaternion rotation, Transform parent, string fieldName)
    {
        if (prefab == null)
        {
            return null;
        }

        try
        {
            UnityEngine.Object instance = Instantiate(prefab, position, rotation, parent);
            GameObject root = ExtractGameObject(instance);
            if (root != null)
            {
                return root;
            }

            Debug.LogWarning("IntroPetSelectionBootstrap could not resolve a GameObject root for " + fieldName + ".");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("IntroPetSelectionBootstrap failed to instantiate " + fieldName + ": " + exception.Message);
        }

        return null;
    }

    private static GameObject ExtractGameObject(UnityEngine.Object instance)
    {
        if (instance == null)
        {
            return null;
        }

        GameObject gameObject = instance as GameObject;
        if (gameObject != null)
        {
            return gameObject;
        }

        Component component = instance as Component;
        return component != null ? component.gameObject : null;
    }

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
