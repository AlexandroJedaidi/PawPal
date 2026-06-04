using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PawPalWalkPetGraphicsEnhancer : MonoBehaviour
{
    private const string FillLightName = "WalkPetFillLightRuntime";

    [SerializeField, Range(0f, 1f)] private float contactShadowOpacity = 0.36f;
    [SerializeField] private Vector2 contactShadowScale = new Vector2(1.22f, 0.74f);
    [SerializeField] private float contactShadowYOffset = 0.018f;
    [SerializeField] private Color fillLightColor = new Color(1f, 0.93f, 0.82f, 1f);
    [SerializeField, Range(0f, 3f)] private float fillLightIntensity = 0.72f;
    [SerializeField, Min(0.1f)] private float fillLightRange = 3.2f;
    [SerializeField] private Vector3 fillLightCameraOffset = new Vector3(0.35f, 0.85f, -0.7f);
    [SerializeField] private Vector3 shadowKeyLightEuler = new Vector3(46f, -32f, 0f);
    [SerializeField, Range(0f, 1f)] private float shadowKeyStrength = 0.42f;

    private Transform petRoot;
    private Camera walkCamera;
    private Light fillLight;
    private Light createdShadowKeyLight;
    private Renderer[] cachedRenderers;
    private bool capturedRenderSettings;
    private Light previousSun;
    private AmbientMode previousAmbientMode;
    private Color previousAmbientLight;
    private Color previousAmbientSkyColor;
    private Color previousAmbientEquatorColor;
    private Color previousAmbientGroundColor;
    private float previousAmbientIntensity;
    private float previousReflectionIntensity;
    private int previousReflectionBounces;
    private bool previousFog;
    private Color previousFogColor;
    private FogMode previousFogMode;
    private float previousFogDensity;
    private float previousFogStartDistance;
    private float previousFogEndDistance;

    public void Configure(Transform targetPetRoot, Camera targetWalkCamera, List<Collider> receivingRoads)
    {
        petRoot = targetPetRoot;
        walkCamera = targetWalkCamera;
        if (petRoot == null)
        {
            return;
        }

        CacheRenderers();
        ConfigurePetRenderers();
        ConfigureReceivingSurfaces(receivingRoads);
        EnsureContactShadow();
        CaptureRenderSettingsIfNeeded();
        EnsureSceneShadowLight();
        EnsureFillLight();
        UpdateFillLight();
    }

    private void LateUpdate()
    {
        if (petRoot == null)
        {
            return;
        }

        UpdateFillLight();
    }

    private void OnDisable()
    {
        RestoreRenderSettingsIfCaptured();

        if (fillLight != null)
        {
            DestroyGeneratedObject(fillLight.gameObject);
            fillLight = null;
        }

        if (createdShadowKeyLight != null)
        {
            DestroyGeneratedObject(createdShadowKeyLight.gameObject);
            createdShadowKeyLight = null;
        }
    }

    private void CacheRenderers()
    {
        cachedRenderers = petRoot != null
            ? petRoot.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];
    }

    private void ConfigurePetRenderers()
    {
        if (cachedRenderers == null)
        {
            CacheRenderers();
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (!IsPetMeshRenderer(renderer))
            {
                continue;
            }

            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        }
    }

    private bool IsPetMeshRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled)
        {
            return false;
        }

        if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
        {
            return false;
        }

        string name = renderer.gameObject.name;
        return name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) < 0;
    }

    private void ConfigureReceivingSurfaces(List<Collider> receivingRoads)
    {
        if (receivingRoads == null)
        {
            return;
        }

        for (int i = 0; i < receivingRoads.Count; i++)
        {
            Collider road = receivingRoads[i];
            if (road == null)
            {
                continue;
            }

            Renderer[] roadRenderers = road.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < roadRenderers.Length; rendererIndex++)
            {
                Renderer renderer = roadRenderers[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                renderer.receiveShadows = true;
            }
        }
    }

    private void EnsureSceneShadowLight()
    {
        if (createdShadowKeyLight == null)
        {
            GameObject lightObject = new GameObject("WalkPetShadowKeyRuntime");
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.transform.SetParent(transform, false);
            createdShadowKeyLight = lightObject.AddComponent<Light>();
        }

        createdShadowKeyLight.type = LightType.Directional;
        createdShadowKeyLight.intensity = 0.85f;
        createdShadowKeyLight.color = new Color(1f, 0.95f, 0.84f, 1f);
        createdShadowKeyLight.transform.rotation = Quaternion.Euler(shadowKeyLightEuler);
        createdShadowKeyLight.shadows = LightShadows.Soft;
        createdShadowKeyLight.shadowStrength = shadowKeyStrength;
        createdShadowKeyLight.shadowBias = 0.04f;
        createdShadowKeyLight.shadowNormalBias = 0.35f;
        createdShadowKeyLight.renderMode = LightRenderMode.ForcePixel;
        RenderSettings.sun = createdShadowKeyLight;
    }

    private void CaptureRenderSettingsIfNeeded()
    {
        if (capturedRenderSettings)
        {
            return;
        }

        capturedRenderSettings = true;
        previousSun = RenderSettings.sun;
        previousAmbientMode = RenderSettings.ambientMode;
        previousAmbientLight = RenderSettings.ambientLight;
        previousAmbientSkyColor = RenderSettings.ambientSkyColor;
        previousAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        previousAmbientGroundColor = RenderSettings.ambientGroundColor;
        previousAmbientIntensity = RenderSettings.ambientIntensity;
        previousReflectionIntensity = RenderSettings.reflectionIntensity;
        previousReflectionBounces = RenderSettings.reflectionBounces;
        previousFog = RenderSettings.fog;
        previousFogColor = RenderSettings.fogColor;
        previousFogMode = RenderSettings.fogMode;
        previousFogDensity = RenderSettings.fogDensity;
        previousFogStartDistance = RenderSettings.fogStartDistance;
        previousFogEndDistance = RenderSettings.fogEndDistance;
    }

    private void RestoreRenderSettingsIfCaptured()
    {
        if (!capturedRenderSettings)
        {
            return;
        }

        RenderSettings.sun = previousSun;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientLight = previousAmbientLight;
        RenderSettings.ambientSkyColor = previousAmbientSkyColor;
        RenderSettings.ambientEquatorColor = previousAmbientEquatorColor;
        RenderSettings.ambientGroundColor = previousAmbientGroundColor;
        RenderSettings.ambientIntensity = previousAmbientIntensity;
        RenderSettings.reflectionIntensity = previousReflectionIntensity;
        RenderSettings.reflectionBounces = previousReflectionBounces;
        RenderSettings.fog = previousFog;
        RenderSettings.fogColor = previousFogColor;
        RenderSettings.fogMode = previousFogMode;
        RenderSettings.fogDensity = previousFogDensity;
        RenderSettings.fogStartDistance = previousFogStartDistance;
        RenderSettings.fogEndDistance = previousFogEndDistance;
        capturedRenderSettings = false;
    }

    private void EnsureContactShadow()
    {
        DogContactShadow contactShadow = petRoot.GetComponent<DogContactShadow>();
        if (contactShadow == null)
        {
            contactShadow = petRoot.gameObject.AddComponent<DogContactShadow>();
        }

        contactShadow.Configure(contactShadowOpacity, contactShadowScale, contactShadowYOffset);
    }

    private void EnsureFillLight()
    {
        if (fillLight != null)
        {
            return;
        }

        GameObject lightObject = new GameObject(FillLightName);
        lightObject.hideFlags = HideFlags.DontSave;
        lightObject.transform.SetParent(transform, false);
        fillLight = lightObject.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.color = fillLightColor;
        fillLight.intensity = fillLightIntensity;
        fillLight.range = fillLightRange;
        fillLight.shadows = LightShadows.None;
        fillLight.renderMode = LightRenderMode.ForcePixel;
    }

    private void UpdateFillLight()
    {
        if (fillLight == null || petRoot == null)
        {
            return;
        }

        Transform reference = walkCamera != null ? walkCamera.transform : petRoot;
        Vector3 offset = reference.TransformDirection(fillLightCameraOffset);
        fillLight.transform.position = petRoot.position + offset;
        fillLight.color = fillLightColor;
        fillLight.intensity = fillLightIntensity;
        fillLight.range = fillLightRange;
    }

    private void DestroyGeneratedObject(Object target)
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
}
