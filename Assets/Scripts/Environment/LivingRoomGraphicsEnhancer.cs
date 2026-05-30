using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LivingRoomGraphicsEnhancer : MonoBehaviour
{
    private const string AutoBootstrapSceneName = "David_Test";
    private const string RuntimeEnhancerObjectName = "Runtime Living Room Graphics Enhancer";
    private const string WindowFillLightName = "Window Soft Fill";
    private const string SofaFillLightName = "Sofa Warm Fill";
    private const string ReflectionProbeName = "Living Room Reflection Probe";
    private const float RoomFootprintPadding = 0.85f;
    private const float RoomRendererMaxFootprintMultiplier = 1.8f;
    private const float MinimumRoomBoundsHeight = 3.25f;
    private const float MinimumRoomBoundsCenterY = 1.4f;

    [Header("Apply")]
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool configureMainCamera = true;
    [SerializeField] private bool configureRenderSettings = true;
    [SerializeField] private bool configureSkybox;
    [SerializeField] private bool configurePostProcessing = true;
    [SerializeField] private bool configureReflectionProbe = true;
    [SerializeField] private bool configureQualityInPlayMode = true;

    [Header("Sun")]
    [SerializeField] private Light sun;
    [SerializeField] private Vector3 sunEulerAngles = new Vector3(45f, -32f, 0f);
    [SerializeField] private float sunIntensity = 1.25f;
    [SerializeField] private Color sunColor = new Color(1f, 0.94f, 0.84f);
    [SerializeField] private float sunColorTemperature = 6100f;
    [SerializeField, Range(0f, 1f)] private float sunShadowStrength = 0.78f;
    [SerializeField] private float sunShadowBias = 0.025f;
    [SerializeField] private float sunShadowNormalBias = 0.18f;

    [Header("Room Fill")]
    [SerializeField] private bool createFillLights = true;
    [SerializeField] private Light windowFillLight;
    [SerializeField] private Light sofaFillLight;
    [SerializeField] private float windowFillIntensity = 0.18f;
    [SerializeField] private float sofaFillIntensity = 0.12f;

    [Header("Ambient")]
    [SerializeField, Range(0f, 2f)] private float ambientIntensity = 0.55f;
    [SerializeField, Range(0f, 2f)] private float reflectionIntensity = 0.35f;
    [SerializeField] private Color ambientSky = new Color(0.70f, 0.72f, 0.72f);
    [SerializeField] private Color ambientEquator = new Color(0.45f, 0.43f, 0.39f);
    [SerializeField] private Color ambientGround = new Color(0.14f, 0.13f, 0.12f);

    [Header("Skybox")]
    [SerializeField] private Color skyboxTint = new Color(0.42f, 0.70f, 1f);
    [SerializeField] private Color skyboxGroundColor = new Color(0.32f, 0.47f, 0.60f);
    [SerializeField, Range(0f, 8f)] private float skyboxExposure = 1.08f;
    [SerializeField, Range(0f, 5f)] private float skyboxAtmosphereThickness = 0.85f;

    [Header("Look")]
    [SerializeField] private float exposure = -0.08f;
    [SerializeField, Range(-100f, 100f)] private float contrast = 2f;
    [SerializeField, Range(-100f, 100f)] private float saturation = -10f;
    [SerializeField, Range(-100f, 100f)] private float whiteBalanceTemperature = 0f;
    [SerializeField, Range(0f, 1f)] private float bloomIntensity = 0.025f;
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.025f;

    [Header("Shadows")]
    [SerializeField] private bool enablePetShadows = true;
    [SerializeField] private bool enableRoomReceiveShadows = true;
    [SerializeField] private bool makeOpaqueRoomObjectsCastShadows = true;
    [SerializeField] private bool addPetContactShadowFallback = true;
    [SerializeField, Range(0f, 1f)] private float contactShadowOpacity = 0.42f;
    [SerializeField] private Vector2 contactShadowScale = new Vector2(1.35f, 0.82f);
    [SerializeField] private float contactShadowYOffset = 0.035f;
    [SerializeField] private float playModeShadowDistance = 45f;
    [SerializeField]
    private string[] petNameKeywords =
    {
        "dog",
        "cat",
        "puppy",
        "labrador",
        "retriever",
        "shiba",
        "corgi",
        "husky",
        "beagle",
        "boxer",
        "bulldog",
        "doberman",
        "dalmatian",
        "shepherd",
        "terrier",
        "rottweiler",
        "pitbull",
        "pug",
        "spitz",
        "collie"
    };

    private Volume lookVolume;
    private Material runtimeSkyboxMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapInPlayMode()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != AutoBootstrapSceneName)
        {
            return;
        }

#if UNITY_2022_2_OR_NEWER
        LivingRoomGraphicsEnhancer[] enhancers = UnityEngine.Object.FindObjectsByType<LivingRoomGraphicsEnhancer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        LivingRoomGraphicsEnhancer[] enhancers = UnityEngine.Object.FindObjectsOfType<LivingRoomGraphicsEnhancer>(true);
#endif
        for (int i = 0; i < enhancers.Length; i++)
        {
            if (enhancers[i] != null && enhancers[i].gameObject.scene == activeScene)
            {
                enhancers[i].ApplyGraphics();
                return;
            }
        }

        GameObject enhancerObject = new GameObject(RuntimeEnhancerObjectName);
        LivingRoomGraphicsEnhancer enhancer = enhancerObject.AddComponent<LivingRoomGraphicsEnhancer>();
        enhancer.ApplyGraphics();
    }

    private void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyGraphics();
        }
    }

    private void Start()
    {
        if (applyOnEnable)
        {
            ApplyGraphics();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        sunIntensity = Mathf.Max(0f, sunIntensity);
        sunShadowBias = Mathf.Max(0f, sunShadowBias);
        sunShadowNormalBias = Mathf.Max(0f, sunShadowNormalBias);
        windowFillIntensity = Mathf.Max(0f, windowFillIntensity);
        sofaFillIntensity = Mathf.Max(0f, sofaFillIntensity);
        skyboxExposure = Mathf.Max(0f, skyboxExposure);
        skyboxAtmosphereThickness = Mathf.Max(0f, skyboxAtmosphereThickness);
        playModeShadowDistance = Mathf.Max(1f, playModeShadowDistance);
    }
#endif

    [ContextMenu("Apply Graphics Now")]
    public void ApplyGraphics()
    {
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return;
        }

        Bounds roomBounds = CalculateSceneBounds();

        if (configureRenderSettings)
        {
            ConfigureRenderSettings();
        }

        ConfigureSun();

        if (createFillLights)
        {
            ConfigureFillLights(roomBounds);
        }

        if (configureMainCamera)
        {
            ConfigureCameras();
        }

        if (configurePostProcessing)
        {
            ConfigureLookVolume();
        }

        if (configureReflectionProbe)
        {
            ConfigureRoomReflectionProbe(roomBounds);
        }

        if (enablePetShadows || enableRoomReceiveShadows)
        {
            ConfigureRendererShadows();
        }

        if (Application.isPlaying && configureQualityInPlayMode)
        {
            ConfigurePlayModeQuality();
        }
    }

    private void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.reflectionBounces = 1;

        if (configureSkybox)
        {
            ConfigureSkybox();
        }
    }

    private void ConfigureSkybox()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Material skybox = ResolveRuntimeSkyboxMaterial();
        if (skybox == null)
        {
            return;
        }

        SetMaterialColor(skybox, "_SkyTint", skyboxTint);
        SetMaterialColor(skybox, "_Tint", skyboxTint);
        SetMaterialColor(skybox, "_Color", skyboxTint);
        SetMaterialColor(skybox, "_GroundColor", skyboxGroundColor);
        SetMaterialFloat(skybox, "_Exposure", skyboxExposure);
        SetMaterialFloat(skybox, "_AtmosphereThickness", skyboxAtmosphereThickness);

        RenderSettings.skybox = skybox;
        DynamicGI.UpdateEnvironment();
    }

    private Material ResolveRuntimeSkyboxMaterial()
    {
        if (runtimeSkyboxMaterial != null && runtimeSkyboxMaterial.shader != null)
        {
            return runtimeSkyboxMaterial;
        }

        Material sourceSkybox = RenderSettings.skybox;
        if (sourceSkybox != null && sourceSkybox.shader != null)
        {
            runtimeSkyboxMaterial = new Material(sourceSkybox);
        }
        else
        {
            Shader proceduralSkybox = Shader.Find("Skybox/Procedural");
            if (proceduralSkybox == null)
            {
                return null;
            }

            runtimeSkyboxMaterial = new Material(proceduralSkybox);
        }

        runtimeSkyboxMaterial.name = "PawPal Blue Skybox";
        runtimeSkyboxMaterial.hideFlags = HideFlags.DontSave;
        return runtimeSkyboxMaterial;
    }

    private void SetMaterialColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void ConfigureSun()
    {
        Light resolvedSun = ResolveSun();
        if (resolvedSun == null)
        {
            return;
        }

        sun = resolvedSun;
        RenderSettings.sun = resolvedSun;

        resolvedSun.type = LightType.Directional;
        resolvedSun.transform.rotation = Quaternion.Euler(sunEulerAngles);
        resolvedSun.intensity = sunIntensity;
        resolvedSun.color = sunColor;
        resolvedSun.useColorTemperature = true;
        resolvedSun.colorTemperature = sunColorTemperature;
        resolvedSun.lightmapBakeType = LightmapBakeType.Realtime;
        resolvedSun.shadows = LightShadows.Soft;
        resolvedSun.shadowStrength = sunShadowStrength;
        resolvedSun.shadowBias = sunShadowBias;
        resolvedSun.shadowNormalBias = sunShadowNormalBias;
        resolvedSun.shadowNearPlane = 0.1f;
        resolvedSun.renderMode = LightRenderMode.ForcePixel;

        UniversalAdditionalLightData lightData = resolvedSun.GetUniversalAdditionalLightData();
        lightData.usePipelineSettings = false;
        lightData.softShadowQuality = SoftShadowQuality.High;
    }

    private Light ResolveSun()
    {
        if (IsSceneObject(sun) && sun.type == LightType.Directional)
        {
            return sun;
        }

        if (IsSceneObject(RenderSettings.sun) && RenderSettings.sun.type == LightType.Directional)
        {
            return RenderSettings.sun;
        }

        Light fallback = null;
        Light[] lights = FindSceneObjects<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (!IsSceneObject(candidate) || candidate.type != LightType.Directional)
            {
                continue;
            }

            if (fallback == null || candidate.name.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fallback = candidate;
            }
        }

        if (fallback != null)
        {
            return fallback;
        }

        GameObject sunObject = new GameObject("Sun");
        sunObject.transform.SetParent(transform, false);
        return sunObject.AddComponent<Light>();
    }

    private void ConfigureFillLights(Bounds roomBounds)
    {
        Light windowLight = ResolveOrCreateLight(ref windowFillLight, WindowFillLightName, LightType.Spot);
        Vector3 windowPosition = new Vector3(
            roomBounds.max.x - Mathf.Max(0.35f, roomBounds.size.x * 0.08f),
            Mathf.Max(roomBounds.center.y + 1.7f, roomBounds.max.y - 0.35f),
            Mathf.Lerp(roomBounds.min.z, roomBounds.max.z, 0.55f));

        windowLight.transform.position = windowPosition;
        AimAt(windowLight.transform, roomBounds.center + new Vector3(-roomBounds.extents.x * 0.25f, 0.25f, -roomBounds.extents.z * 0.15f));
        windowLight.type = LightType.Spot;
        windowLight.intensity = windowFillIntensity;
        windowLight.range = Mathf.Max(4f, roomBounds.extents.magnitude * 0.7f);
        windowLight.spotAngle = 82f;
        windowLight.innerSpotAngle = 48f;
        windowLight.color = new Color(1f, 0.80f, 0.55f);
        windowLight.shadows = LightShadows.None;
        windowLight.renderMode = LightRenderMode.Auto;

        Light sofaLight = ResolveOrCreateLight(ref sofaFillLight, SofaFillLightName, LightType.Point);
        sofaLight.transform.position = roomBounds.center + new Vector3(roomBounds.extents.x * 0.22f, Mathf.Max(1.25f, roomBounds.extents.y * 0.55f), roomBounds.extents.z * 0.2f);
        sofaLight.type = LightType.Point;
        sofaLight.intensity = sofaFillIntensity;
        sofaLight.range = Mathf.Max(2.5f, roomBounds.size.x * 0.35f);
        sofaLight.color = new Color(1f, 0.74f, 0.50f);
        sofaLight.shadows = LightShadows.None;
        sofaLight.renderMode = LightRenderMode.Auto;
    }

    private void ConfigureCameras()
    {
        Camera[] cameras = FindSceneObjects<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (!IsSceneObject(camera) || !camera.enabled)
            {
                continue;
            }

            if (configureSkybox)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = skyboxTint;
            }

            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.useOcclusionCulling = true;

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = true;
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            cameraData.requiresColorOption = CameraOverrideOption.On;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.stopNaN = true;
            cameraData.dithering = true;
        }
    }

    private void ConfigureLookVolume()
    {
        if (lookVolume == null)
        {
            lookVolume = GetComponent<Volume>();
            if (lookVolume == null)
            {
                lookVolume = gameObject.AddComponent<Volume>();
            }
        }

        lookVolume.isGlobal = true;
        lookVolume.priority = 60f;
        lookVolume.weight = 1f;

        VolumeProfile profile = lookVolume.profile;
        profile.name = "PawFriends Living Room Look";
        profile.hideFlags = HideFlags.DontSave;

        Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments colorAdjustments = GetOrAddVolumeComponent<ColorAdjustments>(profile);
        colorAdjustments.postExposure.Override(exposure);
        colorAdjustments.contrast.Override(contrast);
        colorAdjustments.saturation.Override(saturation);
        colorAdjustments.colorFilter.Override(Color.white);

        WhiteBalance whiteBalance = GetOrAddVolumeComponent<WhiteBalance>(profile);
        whiteBalance.temperature.Override(whiteBalanceTemperature);
        whiteBalance.tint.Override(0f);

        Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
        bloom.threshold.Override(1.4f);
        bloom.intensity.Override(bloomIntensity);
        bloom.scatter.Override(0.35f);
        bloom.tint.Override(Color.white);
        bloom.highQualityFiltering.Override(true);
        bloom.maxIterations.Override(6);

        Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.42f);
        vignette.rounded.Override(false);
    }

    private void ConfigureRoomReflectionProbe(Bounds roomBounds)
    {
        ReflectionProbe probe = ResolveOrCreateChildComponent<ReflectionProbe>(ReflectionProbeName);
        probe.transform.position = roomBounds.center + Vector3.up * 0.15f;
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution = 128;
        probe.intensity = reflectionIntensity;
        probe.boxProjection = true;
        probe.center = Vector3.zero;
        probe.size = new Vector3(
            Mathf.Max(3f, roomBounds.size.x + 0.6f),
            Mathf.Max(2.5f, roomBounds.size.y + 1.0f),
            Mathf.Max(3f, roomBounds.size.z + 0.6f));
        probe.cullingMask = -1;

        if (Application.isPlaying)
        {
            probe.RenderProbe();
        }
    }

    private void ConfigureRendererShadows()
    {
        Renderer[] renderers = FindSceneObjects<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsSceneObject(renderer) || !renderer.enabled || IsEffectRenderer(renderer))
            {
                continue;
            }

            bool isPet = IsPetRenderer(renderer);
            if (isPet && enablePetShadows)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                EnsurePetContactShadow(renderer);
                continue;
            }

            if (!enableRoomReceiveShadows)
            {
                continue;
            }

            renderer.receiveShadows = true;
            if (makeOpaqueRoomObjectsCastShadows && !UsesTransparentMaterial(renderer))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        if (enableRoomReceiveShadows)
        {
            ConfigureTerrainShadows();
        }
    }

    private void ConfigureTerrainShadows()
    {
        Terrain[] terrains = FindSceneObjects<Terrain>();
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (!IsSceneObject(terrain) || !terrain.enabled)
            {
                continue;
            }

            terrain.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    private void ConfigurePlayModeQuality()
    {
        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, playModeShadowDistance);
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
    }

    private Bounds CalculateSceneBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, new Vector3(6f, 3f, 6f));
        bool hasRoomFootprint = TryResolveRoomFootprint(out Bounds roomFootprint);
        Renderer[] renderers = FindSceneObjects<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsSceneObject(renderer) || !renderer.enabled || IsEffectRenderer(renderer))
            {
                continue;
            }

            if (hasRoomFootprint && !IsInsideRoomFootprint(renderer.bounds, roomFootprint))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            if (hasRoomFootprint)
            {
                return CreateFallbackRoomBounds(roomFootprint);
            }

            return bounds;
        }

        bounds.Expand(new Vector3(0.5f, 0.25f, 0.5f));
        bounds = EnsureMinimumRoomHeight(bounds);
        return bounds;
    }

    private bool TryResolveRoomFootprint(out Bounds footprint)
    {
        DogRoomAgent[] agents = FindSceneObjects<DogRoomAgent>();
        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent agent = agents[i];
            if (!IsSceneObject(agent) || !agent.TryGetRoomBounds(out footprint))
            {
                continue;
            }

            footprint.Expand(new Vector3(RoomFootprintPadding * 2f, 0f, RoomFootprintPadding * 2f));
            return true;
        }

        footprint = new Bounds();
        return false;
    }

    private static bool IsInsideRoomFootprint(Bounds bounds, Bounds footprint)
    {
        Vector3 center = bounds.center;
        if (center.x < footprint.min.x || center.x > footprint.max.x ||
            center.z < footprint.min.z || center.z > footprint.max.z)
        {
            return false;
        }

        return bounds.size.x <= footprint.size.x * RoomRendererMaxFootprintMultiplier &&
               bounds.size.z <= footprint.size.z * RoomRendererMaxFootprintMultiplier;
    }

    private static Bounds CreateFallbackRoomBounds(Bounds footprint)
    {
        Vector3 center = new Vector3(footprint.center.x, MinimumRoomBoundsCenterY, footprint.center.z);
        Vector3 size = new Vector3(
            Mathf.Max(6f, footprint.size.x),
            MinimumRoomBoundsHeight,
            Mathf.Max(6f, footprint.size.z));
        return new Bounds(center, size);
    }

    private static Bounds EnsureMinimumRoomHeight(Bounds bounds)
    {
        if (bounds.size.y >= MinimumRoomBoundsHeight)
        {
            return bounds;
        }

        Vector3 center = bounds.center;
        center.y = Mathf.Max(center.y, MinimumRoomBoundsCenterY);
        bounds.center = center;

        Vector3 size = bounds.size;
        size.y = MinimumRoomBoundsHeight;
        bounds.size = size;
        return bounds;
    }

    private Light ResolveOrCreateLight(ref Light cachedLight, string lightName, LightType lightType)
    {
        if (IsSceneObject(cachedLight))
        {
            return cachedLight;
        }

        Light foundLight = ResolveChildComponent<Light>(lightName);
        if (foundLight == null)
        {
            Light[] lights = FindSceneObjects<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (IsSceneObject(lights[i]) && string.Equals(lights[i].name, lightName, StringComparison.OrdinalIgnoreCase))
                {
                    foundLight = lights[i];
                    break;
                }
            }
        }

        if (foundLight == null)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(transform, false);
            foundLight = lightObject.AddComponent<Light>();
        }

        foundLight.type = lightType;
        cachedLight = foundLight;
        return foundLight;
    }

    private T ResolveOrCreateChildComponent<T>(string childName) where T : Component
    {
        T component = ResolveChildComponent<T>(childName);
        if (component != null)
        {
            return component;
        }

        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        return child.AddComponent<T>();
    }

    private T ResolveChildComponent<T>(string childName) where T : Component
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<T>();
    }

    private T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (!profile.TryGet(out component))
        {
            component = profile.Add<T>(true);
        }

        component.active = true;
        return component;
    }

    private bool IsPetRenderer(Renderer renderer)
    {
        Transform current = renderer.transform;
        while (current != null)
        {
            if (NameMatchesAnyPetKeyword(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        Animator animator = renderer.GetComponentInParent<Animator>();
        return animator != null && NameMatchesAnyPetKeyword(animator.gameObject.name);
    }

    private void EnsurePetContactShadow(Renderer renderer)
    {
        if (!addPetContactShadowFallback)
        {
            return;
        }

        Transform petRoot = ResolvePetRoot(renderer);
        if (petRoot == null)
        {
            return;
        }

        DogContactShadow contactShadow = petRoot.GetComponent<DogContactShadow>();
        if (contactShadow == null)
        {
            contactShadow = petRoot.gameObject.AddComponent<DogContactShadow>();
        }

        contactShadow.Configure(contactShadowOpacity, contactShadowScale, contactShadowYOffset);
    }

    private Transform ResolvePetRoot(Renderer renderer)
    {
        Animator animator = renderer.GetComponentInParent<Animator>();
        if (animator != null && NameMatchesAnyPetKeyword(animator.gameObject.name))
        {
            return animator.transform;
        }

        Transform current = renderer.transform;
        Transform bestMatch = null;
        while (current != null)
        {
            if (NameMatchesAnyPetKeyword(current.name))
            {
                bestMatch = current;
            }

            current = current.parent;
        }

        if (bestMatch != null)
        {
            return bestMatch;
        }

        return animator != null ? animator.transform : renderer.transform.root;
    }

    private bool NameMatchesAnyPetKeyword(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || petNameKeywords == null)
        {
            return false;
        }

        for (int i = 0; i < petNameKeywords.Length; i++)
        {
            string keyword = petNameKeywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) && objectName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool UsesTransparentMaterial(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            if (material.renderQueue >= 3000)
            {
                return true;
            }

            string materialName = material.name;
            if (materialName.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("alpha", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            Shader shader = material.shader;
            if (shader != null && shader.name.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEffectRenderer(Renderer renderer)
    {
        return renderer is ParticleSystemRenderer ||
               renderer is LineRenderer ||
               renderer is TrailRenderer;
    }

    private bool IsSceneObject(Component component)
    {
        return component != null && component.gameObject.scene == gameObject.scene;
    }

    private T[] FindSceneObjects<T>() where T : Component
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }

    private void AimAt(Transform source, Vector3 target)
    {
        Vector3 direction = target - source.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        source.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
