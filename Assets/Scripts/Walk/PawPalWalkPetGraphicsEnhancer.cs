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
    private Transform petRoot;
    private Camera walkCamera;
    private Light fillLight;
    private Renderer[] cachedRenderers;

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
        if (fillLight != null)
        {
            DestroyGeneratedObject(fillLight.gameObject);
            fillLight = null;
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
