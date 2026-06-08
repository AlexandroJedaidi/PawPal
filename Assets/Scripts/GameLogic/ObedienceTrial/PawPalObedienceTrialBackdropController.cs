using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PawPalObedienceTrialBackdropController : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedObedienceTrialParkBackdrop";
    private const string DayResourcePath = "WindowBackdrops/Park_day";
    private const string NightResourcePath = "WindowBackdrops/Park_night";
    private const string AgilitySceneName = "TrialAgility";
    private const float BackdropDepthScale = 0.86742f;

    [SerializeField] private bool useDeviceLocalTime = true;
    [SerializeField] private bool usePreviewHour;
    [SerializeField, Range(0f, 24f)] private float previewHour = 12f;
    [SerializeField, Range(0f, 24f)] private float sunriseHour = 7f;
    [SerializeField, Range(0f, 24f)] private float sunsetHour = 20f;
    [SerializeField, Range(0.1f, 6f)] private float transitionHours = 1f;

    private Renderer[] screenRenderers = new Renderer[0];
    private Texture2D dayTexture;
    private Texture2D nightTexture;
    private Material dayMaterial;
    private Material nightMaterial;
    private int lastAppliedMinuteStamp = int.MinValue;
    private static Mesh quadMesh;

    public static bool IsSupportedScene(Scene scene)
    {
        return scene.IsValid() && IsSupportedSceneName(scene.name);
    }

    public static bool IsSupportedSceneName(string sceneName)
    {
        return string.Equals(sceneName, PawPalObedienceTrialSceneFlow.TrialSceneName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sceneName, AgilitySceneName, StringComparison.OrdinalIgnoreCase);
    }

    public static Bounds ResolveSceneBounds(Scene scene)
    {
        bool foundTerrain = false;
        Bounds terrainBounds = new Bounds(Vector3.zero, Vector3.zero);
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null || terrain.gameObject.scene != scene)
            {
                continue;
            }

            Vector3 size = terrain.terrainData.size;
            Bounds current = new Bounds(terrain.transform.position + (size * 0.5f), size);
            if (!foundTerrain)
            {
                terrainBounds = current;
                foundTerrain = true;
            }
            else
            {
                terrainBounds.Encapsulate(current);
            }
        }

        return foundTerrain ? terrainBounds : new Bounds(Vector3.zero, new Vector3(5.2f, 1f, 4.6f));
    }

    public void Configure(Bounds bounds)
    {
        Rebuild();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        ClearGenerated();
        ReleaseMaterials();
    }

    private void Update()
    {
        if (!ShouldRenderInCurrentScene())
        {
            ClearGenerated();
            ReleaseMaterials();
            return;
        }

        ApplyTimeOfDayState(false);
    }

    private void Rebuild()
    {
        if (!ShouldRenderInCurrentScene())
        {
            ClearGenerated();
            ReleaseMaterials();
            return;
        }

        ClearGenerated();
        ReleaseMaterials();
        dayTexture = Resources.Load<Texture2D>(DayResourcePath);
        nightTexture = Resources.Load<Texture2D>(NightResourcePath);
        if (dayTexture == null && nightTexture == null)
        {
            Debug.LogWarning("Obedience Trial park backdrop textures are missing from Resources/WindowBackdrops.");
            return;
        }

        dayMaterial = CreateMaterial(dayTexture, "ObedienceTrialParkDay");
        nightMaterial = CreateMaterial(nightTexture, "ObedienceTrialParkNight");

        GameObject generatedObject = new GameObject(GeneratedRootName);
        generatedObject.hideFlags = HideFlags.DontSave;
        Transform generatedRoot = generatedObject.transform;
        generatedRoot.SetParent(transform, false);

        IntroBackdropScreenLayout[] layouts = BuildLayouts();
        screenRenderers = new Renderer[layouts.Length];
        for (int i = 0; i < layouts.Length; i++)
        {
            screenRenderers[i] = CreateScreenRenderer(
                generatedRoot,
                "ParkBackdropScreen_" + i,
                layouts[i].LocalPosition,
                Quaternion.Euler(layouts[i].LocalEulerAngles),
                layouts[i].Size);
        }

        ApplyTimeOfDayState(true);
    }

    private IntroBackdropScreenLayout[] BuildLayouts()
    {
        return new[]
        {
            new IntroBackdropScreenLayout
            {
                LocalPosition = new Vector3(-0.02f, 302.8f, 17.71f),
                LocalEulerAngles = new Vector3(0f, -180f, 0f),
                Size = new Vector2(12.31736f, 5.031036f)
            },
            new IntroBackdropScreenLayout
            {
                LocalPosition = new Vector3(-6.24f, 302.8f, 12.53f),
                LocalEulerAngles = new Vector3(0f, -90f, 0f),
                Size = new Vector2(10.58252f, 5.031036f)
            },
            new IntroBackdropScreenLayout
            {
                LocalPosition = new Vector3(6.08f, 302.8f, 12.53f),
                LocalEulerAngles = new Vector3(0f, 90f, 0f),
                Size = new Vector2(10.58252f, 5.031036f)
            }
        };
    }

    private void ApplyTimeOfDayState(bool force)
    {
        if (screenRenderers == null || screenRenderers.Length == 0)
        {
            return;
        }

        PawPalTimeOfDayState state = PawPalTimeOfDayEvaluator.Evaluate(
            useDeviceLocalTime,
            usePreviewHour,
            previewHour,
            sunriseHour,
            sunsetHour,
            transitionHours);
        int minuteStamp = PawPalTimeOfDayEvaluator.ToMinuteStamp(state);
        if (!force && minuteStamp == lastAppliedMinuteStamp)
        {
            return;
        }

        lastAppliedMinuteStamp = minuteStamp;
        Material activeMaterial = state.Daylight01 <= 0.001f ? nightMaterial : dayMaterial;
        if (activeMaterial == null)
        {
            activeMaterial = state.Daylight01 <= 0.001f ? dayMaterial : nightMaterial;
        }

        for (int i = 0; i < screenRenderers.Length; i++)
        {
            Renderer renderer = screenRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = activeMaterial != null;
            if (activeMaterial != null)
            {
                renderer.sharedMaterial = activeMaterial;
            }
        }
    }

    private static Renderer CreateScreenRenderer(Transform parent, string screenName, Vector3 localPosition, Quaternion localRotation, Vector2 size)
    {
        GameObject screen = new GameObject(screenName, typeof(MeshFilter), typeof(MeshRenderer));
        screen.hideFlags = HideFlags.DontSave;
        screen.transform.SetParent(parent, false);
        screen.transform.localPosition = localPosition;
        screen.transform.localRotation = localRotation;
        screen.transform.localScale = new Vector3(size.x, size.y, BackdropDepthScale);

        MeshFilter filter = screen.GetComponent<MeshFilter>();
        filter.sharedMesh = GetQuadMesh();

        MeshRenderer renderer = screen.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return renderer;
    }

    private static Material CreateMaterial(Texture2D texture, string materialName)
    {
        if (texture == null)
        {
            return null;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.hideFlags = HideFlags.DontSave;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.renderQueue = (int)RenderQueue.Geometry;
        return material;
    }

    private void ClearGenerated()
    {
        screenRenderers = new Renderer[0];
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != GeneratedRootName)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void ReleaseMaterials()
    {
        ReleaseMaterial(ref dayMaterial);
        ReleaseMaterial(ref nightMaterial);
    }

    private static void ReleaseMaterial(ref Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }

        material = null;
    }

    private static Mesh GetQuadMesh()
    {
        if (quadMesh != null)
        {
            return quadMesh;
        }

        quadMesh = new Mesh();
        quadMesh.name = "ObedienceTrialParkBackdropQuad";
        quadMesh.hideFlags = HideFlags.DontSave;
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        quadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.normals = new[]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };
        quadMesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.01f));
        return quadMesh;
    }

    private bool ShouldRenderInCurrentScene()
    {
        return IsSupportedScene(gameObject.scene);
    }
}

public static class PawPalTrialParkBackdropBootstrap
{
    private const string BackdropObjectName = "PawPalTrialParkBackdrop";
    private static bool runtimeInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallRuntime()
    {
        if (!runtimeInstalled)
        {
            runtimeInstalled = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        EnsureBackdrop(SceneManager.GetActiveScene(), false);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureBackdrop(scene, false);
    }

    public static PawPalObedienceTrialBackdropController EnsureBackdrop(Scene scene, bool editorPreview)
    {
        if (!PawPalObedienceTrialBackdropController.IsSupportedScene(scene))
        {
            return null;
        }

#if UNITY_EDITOR
        if (editorPreview)
        {
            ClearLegacyNullMaterialScreens(scene);
        }
#endif

        PawPalObedienceTrialBackdropController[] existing = UnityEngine.Object.FindObjectsByType<PawPalObedienceTrialBackdropController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            PawPalObedienceTrialBackdropController controller = existing[i];
            if (controller != null && controller.gameObject.scene == scene)
            {
                controller.Configure(PawPalObedienceTrialBackdropController.ResolveSceneBounds(scene));
                return controller;
            }
        }

        GameObject root = new GameObject(BackdropObjectName);
#if UNITY_EDITOR
        if (editorPreview)
        {
            root.hideFlags = HideFlags.DontSaveInEditor;
        }
#endif
        PawPalObedienceTrialBackdropController created = root.AddComponent<PawPalObedienceTrialBackdropController>();
        created.Configure(PawPalObedienceTrialBackdropController.ResolveSceneBounds(scene));
        return created;
    }

#if UNITY_EDITOR
    private static void ClearLegacyNullMaterialScreens(Scene scene)
    {
        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || renderer.gameObject.scene != scene)
            {
                continue;
            }

            if (!renderer.gameObject.name.StartsWith("BackdropScreen_", StringComparison.Ordinal)
                && !renderer.gameObject.name.StartsWith("ParkBackdropScreen_", StringComparison.Ordinal))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            bool hasMaterial = materials != null && materials.Length > 0 && materials[0] != null;
            if (hasMaterial)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(renderer.gameObject);
        }
    }
#endif
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class PawPalTrialParkBackdropEditorBootstrap
{
    static PawPalTrialParkBackdropEditorBootstrap()
    {
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorApplication.delayCall += EnsureActiveScenePreview;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        PawPalTrialParkBackdropBootstrap.EnsureBackdrop(scene, true);
    }

    private static void EnsureActiveScenePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        PawPalTrialParkBackdropBootstrap.EnsureBackdrop(SceneManager.GetActiveScene(), true);
    }
}
#endif
