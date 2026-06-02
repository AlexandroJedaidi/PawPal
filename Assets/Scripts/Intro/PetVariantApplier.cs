using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class PetVariantApplier
{
    public static GameObject GetPrefabForVariant(IntroPetDefinition definition, FurVariantDefinition variant)
    {
        if (variant != null && variant.VariantPrefab != null)
        {
            return variant.VariantPrefab;
        }

        return definition != null ? definition.BasePrefab : null;
    }

    public static bool ApplyMaterial(GameObject root, FurVariantDefinition variant)
    {
        if (root == null || variant == null || variant.ReplacementMaterial == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool changed = false;
        string match = variant.MaterialNameContains;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material current = materials[materialIndex];
                if (!string.IsNullOrWhiteSpace(match)
                    && current != null
                    && current.name.IndexOf(match, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                materials[materialIndex] = variant.ReplacementMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }

        return changed;
    }

    public static bool TryInstantiatePrefab(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        out GameObject instance,
        out string errorMessage)
    {
        instance = null;
        errorMessage = string.Empty;
        if (prefab == null)
        {
            errorMessage = "Prefab was null.";
            return false;
        }

        TryInstantiateObject(prefab, position, rotation, out instance, out errorMessage);

        if (instance == null)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = "Instantiate returned null.";
            }

            return false;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        if (parent != null)
        {
            instance.transform.SetParent(parent, true);
        }

        SanitizeRuntimeMaterials(instance);
        return true;
    }

    private static void TryInstantiateObject(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        out GameObject instance,
        out string errorMessage)
    {
        instance = null;
        errorMessage = string.Empty;

        try
        {
            instance = Object.Instantiate(prefab, position, rotation);
            if (instance != null)
            {
                return;
            }
        }
        catch (System.Exception exception)
        {
            errorMessage = exception.Message;
        }

        if (instance != null)
        {
            return;
        }

#if UNITY_EDITOR
        try
        {
            GameObject editorPrefab = ResolveEditorPrefabRoot(prefab);
            if (editorPrefab != null)
            {
                Object clone = PrefabUtility.InstantiatePrefab(editorPrefab);
                if (clone is GameObject editorGameObject)
                {
                    instance = editorGameObject;
                    return;
                }
            }
        }
        catch (System.Exception exception)
        {
            errorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? exception.Message
                : errorMessage + " " + exception.Message;
        }

        if (instance != null)
        {
            return;
        }
#endif

        try
        {
            instance = Object.Instantiate(prefab);
            if (instance != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = "Instantiate returned null for prefab '" + prefab.name + "'.";
            }
        }
        catch (System.Exception exception)
        {
            errorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? exception.Message
                : errorMessage + " " + exception.Message;
        }
    }

#if UNITY_EDITOR
    private static GameObject ResolveEditorPrefabRoot(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return prefab;
        }

        GameObject rootAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        return rootAsset != null ? rootAsset : prefab;
    }
#endif

    public static Collider EnsureTapCollider(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        Collider existing = root.GetComponentInChildren<Collider>();
        if (existing != null)
        {
            return existing;
        }

        Bounds bounds = CalculateBounds(root);
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.height = Mathf.Max(0.4f, bounds.size.y);
        collider.radius = Mathf.Max(0.18f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        return collider;
    }

    public static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        if (renderers.Length == 0)
        {
            Vector3 center = root != null ? root.transform.position + Vector3.up * 0.4f : Vector3.up * 0.4f;
            return new Bounds(center, Vector3.one * 0.8f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    public static void SanitizeRuntimeMaterials(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Lit");
        if (fallbackShader == null)
        {
            fallbackShader = Shader.Find("Standard");
        }

        if (fallbackShader == null)
        {
            fallbackShader = Shader.Find("Sprites/Default");
        }

        if (fallbackShader == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (!NeedsFallbackMaterial(material))
                {
                    continue;
                }

                materials[materialIndex] = BuildFallbackMaterial(material, fallbackShader);
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static bool NeedsFallbackMaterial(Material material)
    {
        if (material == null)
        {
            return true;
        }

        Shader shader = material.shader;
        if (shader == null)
        {
            return true;
        }

        string shaderName = shader.name ?? string.Empty;
        return shaderName.Equals("Hidden/InternalErrorShader", System.StringComparison.OrdinalIgnoreCase)
            || shaderName.Equals("Standard", System.StringComparison.OrdinalIgnoreCase)
            || shaderName.StartsWith("Legacy Shaders/", System.StringComparison.OrdinalIgnoreCase);
    }

    private static Material BuildFallbackMaterial(Material source, Shader shader)
    {
        Material material = new Material(shader);
        material.name = source != null ? source.name + "_RuntimeFallback" : "RuntimeFallbackMaterial";

        Texture baseTexture = null;
        Color baseColor = Color.white;

        if (source != null)
        {
            if (source.HasProperty("_BaseMap"))
            {
                baseTexture = source.GetTexture("_BaseMap");
            }
            else if (source.HasProperty("_MainTex"))
            {
                baseTexture = source.GetTexture("_MainTex");
            }

            if (source.HasProperty("_BaseColor"))
            {
                baseColor = source.GetColor("_BaseColor");
            }
            else if (source.HasProperty("_Color"))
            {
                baseColor = source.GetColor("_Color");
            }
        }

        if (material.HasProperty("_BaseMap") && baseTexture != null)
        {
            material.SetTexture("_BaseMap", baseTexture);
        }
        else if (material.HasProperty("_MainTex") && baseTexture != null)
        {
            material.SetTexture("_MainTex", baseTexture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        return material;
    }
}
