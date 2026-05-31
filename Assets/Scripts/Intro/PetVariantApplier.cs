using UnityEngine;

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
}
