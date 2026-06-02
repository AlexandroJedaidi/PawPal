using System;
using System.Collections.Generic;
using UnityEngine;

public static class IntroPetPlayfieldResolver
{
    private const float DefaultFieldHeight = 3.5f;
    private const float FenceInsetPadding = 0.65f;
    private const float MinimumPlayableWidth = 4f;
    private const float MinimumPlayableDepth = 4f;

    public static Bounds Resolve(Camera referenceCamera)
    {
        Bounds fenceBounds;
        if (TryResolveFenceBounds(out fenceBounds))
        {
            return fenceBounds;
        }

        Vector3 center = referenceCamera != null
            ? referenceCamera.transform.position + Vector3.ProjectOnPlane(referenceCamera.transform.forward, Vector3.up).normalized * 6f
            : new Vector3(0f, DefaultFieldHeight * 0.5f, 6f);

        float groundY = ResolveGroundHeight(center);
        center.y = groundY + DefaultFieldHeight * 0.5f;
        return new Bounds(center, new Vector3(10.4f, DefaultFieldHeight, 12.6f));
    }

    public static bool TryResolveFenceBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        List<Transform> fenceRoots = CollectFenceRoots();
        if (fenceRoots.Count < 4)
        {
            return false;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        float minY = float.PositiveInfinity;

        for (int i = 0; i < fenceRoots.Count; i++)
        {
            Transform fence = fenceRoots[i];
            Bounds fenceBounds;
            if (TryGetFenceGeometryBounds(fence, out fenceBounds))
            {
                minX = Mathf.Min(minX, fenceBounds.min.x);
                maxX = Mathf.Max(maxX, fenceBounds.max.x);
                minZ = Mathf.Min(minZ, fenceBounds.min.z);
                maxZ = Mathf.Max(maxZ, fenceBounds.max.z);
                minY = Mathf.Min(minY, fenceBounds.min.y);
                continue;
            }

            Vector3 position = fence.position;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minZ = Mathf.Min(minZ, position.z);
            maxZ = Mathf.Max(maxZ, position.z);
            minY = Mathf.Min(minY, position.y);
        }

        if (!float.IsFinite(minX) || !float.IsFinite(maxX) || !float.IsFinite(minZ) || !float.IsFinite(maxZ))
        {
            return false;
        }

        float width = Mathf.Max(MinimumPlayableWidth, (maxX - minX) - FenceInsetPadding * 2f);
        float depth = Mathf.Max(MinimumPlayableDepth, (maxZ - minZ) - FenceInsetPadding * 2f);
        Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        float groundY = ResolveGroundHeight(center);
        if (!float.IsFinite(groundY))
        {
            groundY = float.IsFinite(minY) ? minY : 0f;
        }

        center.y = groundY + DefaultFieldHeight * 0.5f;
        bounds = new Bounds(center, new Vector3(width, DefaultFieldHeight, depth));
        return true;
    }

    private static List<Transform> CollectFenceRoots()
    {
        Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<Transform> fenceRoots = new List<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!candidate.name.StartsWith("Fence", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidate.parent != null && candidate.parent.name.StartsWith("Fence", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fenceRoots.Add(candidate);
        }

        return fenceRoots;
    }

    private static bool TryGetFenceGeometryBounds(Transform fenceRoot, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        Collider[] colliders = fenceRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = fenceRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
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

        return hasBounds;
    }

    private static float ResolveGroundHeight(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            return terrain.SampleHeight(position) + terrain.transform.position.y;
        }

        RaycastHit hit;
        if (Physics.Raycast(position + Vector3.up * 8f, Vector3.down, out hit, 24f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return position.y;
    }
}
