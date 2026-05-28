using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class DogContactShadow : MonoBehaviour
{
    private const string ShadowObjectName = "Generated Contact Shadow";
    private const int ShadowTextureSize = 64;

    [SerializeField, Range(0f, 1f)] private float opacity = 0.3f;
    [SerializeField] private Vector2 scaleMultiplier = new Vector2(1.15f, 0.68f);
    [SerializeField] private float yOffset = 0.025f;
    [SerializeField] private Vector3 rotationEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private float minWidth = 0.45f;
    [SerializeField] private float minLength = 0.28f;
    [SerializeField] private float maxWidth = 1.35f;
    [SerializeField] private float maxLength = 0.9f;

    private Transform shadowTransform;
    private MeshRenderer shadowRenderer;
    private Material shadowMaterial;
    private Mesh shadowMesh;
    private Texture2D shadowTexture;
    private Renderer[] cachedRenderers;

    private void OnEnable()
    {
        EnsureShadowObjects();
        CacheRenderers();
        UpdateShadow();
    }

    private void LateUpdate()
    {
        UpdateShadow();
    }

    private void OnDisable()
    {
        DestroyGeneratedObjects();
    }

    public void Configure(float targetOpacity, Vector2 targetScaleMultiplier, float targetYOffset)
    {
        opacity = Mathf.Clamp01(targetOpacity);
        scaleMultiplier = targetScaleMultiplier;
        yOffset = Mathf.Max(0f, targetYOffset);

        if (shadowMaterial != null)
        {
            ApplyMaterialColor();
        }
    }

    private void EnsureShadowObjects()
    {
        if (shadowTransform != null && shadowRenderer != null)
        {
            return;
        }

        GameObject shadowObject = new GameObject(ShadowObjectName);
        shadowObject.hideFlags = HideFlags.DontSave;
        shadowObject.transform.SetParent(transform, false);
        shadowTransform = shadowObject.transform;

        MeshFilter meshFilter = shadowObject.AddComponent<MeshFilter>();
        shadowRenderer = shadowObject.AddComponent<MeshRenderer>();
        shadowMesh = CreateQuadMesh();
        shadowTexture = CreateShadowTexture();
        shadowMaterial = CreateShadowMaterial(shadowTexture);

        meshFilter.sharedMesh = shadowMesh;
        shadowRenderer.sharedMaterial = shadowMaterial;
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void UpdateShadow()
    {
        EnsureShadowObjects();

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            CacheRenderers();
        }

        Bounds bounds;
        if (!TryGetVisibleBounds(out bounds))
        {
            shadowRenderer.enabled = false;
            return;
        }

        shadowRenderer.enabled = true;

        Vector3 center = bounds.center;
        Vector3 position = new Vector3(center.x, bounds.min.y + yOffset, center.z);
        float width = Mathf.Clamp(bounds.size.x * scaleMultiplier.x, minWidth, maxWidth);
        float length = Mathf.Clamp(bounds.size.z * scaleMultiplier.y, minLength, maxLength);

        shadowTransform.SetPositionAndRotation(position, Quaternion.Euler(rotationEuler));
        shadowTransform.localScale = CompensateParentScale(new Vector3(width, 1f, length));

        if (shadowMaterial != null)
        {
            ApplyMaterialColor();
        }
    }

    private bool TryGetVisibleBounds(out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer candidate = cachedRenderers[i];
            if (candidate == null || candidate == shadowRenderer || !candidate.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(candidate.bounds);
            }
        }

        return hasBounds;
    }

    private Vector3 CompensateParentScale(Vector3 targetWorldScale)
    {
        Vector3 parentScale = transform.lossyScale;
        return new Vector3(
            DivideSafely(targetWorldScale.x, parentScale.x),
            DivideSafely(targetWorldScale.y, parentScale.y),
            DivideSafely(targetWorldScale.z, parentScale.z));
    }

    private float DivideSafely(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
    }

    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Generated Contact Shadow Quad";
        mesh.hideFlags = HideFlags.DontSave;
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Texture2D CreateShadowTexture()
    {
        Texture2D texture = new Texture2D(ShadowTextureSize, ShadowTextureSize, TextureFormat.RGBA32, false);
        texture.name = "Generated Contact Shadow Texture";
        texture.hideFlags = HideFlags.DontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[ShadowTextureSize * ShadowTextureSize];
        for (int y = 0; y < ShadowTextureSize; y++)
        {
            for (int x = 0; x < ShadowTextureSize; x++)
            {
                float u = (x + 0.5f) / ShadowTextureSize * 2f - 1f;
                float v = (y + 0.5f) / ShadowTextureSize * 2f - 1f;
                float distance = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.SmoothStep(1f, 0f, distance);
                alpha *= alpha;
                pixels[y * ShadowTextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private Material CreateShadowMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("PawFriends/Contact Shadow");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        material.name = "Generated Contact Shadow Material";
        material.hideFlags = HideFlags.DontSave;
        material.color = new Color(0f, 0f, 0f, opacity);
        if (material.HasProperty("_MainTex"))
        {
            material.mainTexture = texture;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, opacity));
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        return material;
    }

    private void ApplyMaterialColor()
    {
        Color shadowColor = new Color(0f, 0f, 0f, opacity);
        shadowMaterial.color = shadowColor;

        if (shadowMaterial.HasProperty("_BaseColor"))
        {
            shadowMaterial.SetColor("_BaseColor", shadowColor);
        }
    }

    private void DestroyGeneratedObjects()
    {
        DestroyGeneratedObject(shadowTransform != null ? shadowTransform.gameObject : null);
        DestroyGeneratedObject(shadowMaterial);
        DestroyGeneratedObject(shadowMesh);
        DestroyGeneratedObject(shadowTexture);

        shadowTransform = null;
        shadowRenderer = null;
        shadowMaterial = null;
        shadowMesh = null;
        shadowTexture = null;
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
