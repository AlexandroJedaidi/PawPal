using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalWalkingSceneBindings : MonoBehaviour
{
    private static readonly string[] DefaultAllowedRoadNames =
    {
        "Spline Road 3",
        "Spline Road 6",
        "Spline Road 8"
    };

    [SerializeField] private Transform spawnPoint;
    [SerializeField] private PawPalWalkGraph graph;
    [SerializeField] private List<Transform> routeMarkers = new List<Transform>();
    [SerializeField] private List<Collider> allowedRoadColliders = new List<Collider>();
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.35f, -3.2f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 0.7f, 0.35f);
    [SerializeField] private Vector2 pauseIntervalRange = new Vector2(5f, 9f);
    [SerializeField] private Vector2 pauseDurationRange = new Vector2(1.1f, 2f);
    [SerializeField, Min(0.1f)] private float runSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float endHoldDuration = 1f;
    [SerializeField, Min(0.01f)] private float cameraFollowSmoothTime = 0.12f;
    [SerializeField, Min(0f)] private float minimumRemainingDistanceForStop = 2.25f;

    public Transform SpawnPoint => spawnPoint;
    public PawPalWalkGraph Graph => graph;
    public Vector3 CameraOffset => cameraOffset;
    public Vector3 LookAtOffset => lookAtOffset;
    public float RunSpeedMultiplier => Mathf.Max(0.1f, runSpeedMultiplier);
    public float EndHoldDuration => Mathf.Max(0f, endHoldDuration);
    public float CameraFollowSmoothTime => Mathf.Max(0.01f, cameraFollowSmoothTime);
    public float MinimumRemainingDistanceForStop => Mathf.Max(0f, minimumRemainingDistanceForStop);

    public Vector2 PauseIntervalRange
    {
        get
        {
            float min = Mathf.Max(0.25f, pauseIntervalRange.x);
            float max = Mathf.Max(min, pauseIntervalRange.y);
            return new Vector2(min, max);
        }
    }

    public Vector2 PauseDurationRange
    {
        get
        {
            float min = Mathf.Max(0.25f, pauseDurationRange.x);
            float max = Mathf.Max(min, pauseDurationRange.y);
            return new Vector2(min, max);
        }
    }

    public static PawPalWalkingSceneBindings FindInScene()
    {
        PawPalWalkingSceneBindings[] bindings = Object.FindObjectsByType<PawPalWalkingSceneBindings>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return bindings != null && bindings.Length > 0 ? bindings[0] : null;
    }

    public static PawPalWalkingSceneBindings FindInSceneOrCreateRuntimeFallback()
    {
        PawPalWalkingSceneBindings existing = FindInScene();
        if (existing != null)
        {
            existing.AutoAssignAllowedRoadsFromScene();
            return existing;
        }

        GameObject bindingsObject = new GameObject("WalkingRuntimeBindings");
        PawPalWalkingSceneBindings runtimeBindings = bindingsObject.AddComponent<PawPalWalkingSceneBindings>();
        runtimeBindings.AutoAssignAllowedRoadsFromScene();
        return runtimeBindings;
    }

    public bool AutoAssignAllowedRoadsFromScene()
    {
        HashSet<Collider> unique = new HashSet<Collider>();
        List<Collider> resolved = new List<Collider>();
        for (int i = 0; i < DefaultAllowedRoadNames.Length; i++)
        {
            string targetName = DefaultAllowedRoadNames[i];
            Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider candidate = colliders[colliderIndex];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                if (!string.Equals(candidate.gameObject.name, targetName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (unique.Add(candidate))
                {
                    resolved.Add(candidate);
                }
            }
        }

        if (resolved.Count > 0)
        {
            allowedRoadColliders = resolved;
            return true;
        }

        return allowedRoadColliders != null && allowedRoadColliders.Count > 0;
    }

    public void GetResolvedAllowedRoads(List<Collider> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (allowedRoadColliders == null)
        {
            return;
        }

        for (int i = 0; i < allowedRoadColliders.Count; i++)
        {
            Collider collider = allowedRoadColliders[i];
            if (collider != null)
            {
                destination.Add(collider);
            }
        }
    }

    public bool TryBuildRoutePoints(List<Vector3> destination)
    {
        if (destination == null)
        {
            return false;
        }

        destination.Clear();
        if (routeMarkers != null)
        {
            for (int i = 0; i < routeMarkers.Count; i++)
            {
                Transform marker = routeMarkers[i];
                if (marker != null)
                {
                    AddRoutePointIfDistinct(destination, marker.position);
                }
            }
        }

        if (destination.Count >= 2)
        {
            return true;
        }

        return TryBuildFallbackRoutePoints(destination);
    }

    [ContextMenu("Auto Assign Default Walk Roads")]
    private void AutoAssignAllowedRoadsFromSceneContextMenu()
    {
        AutoAssignAllowedRoadsFromScene();
    }

    private bool TryBuildFallbackRoutePoints(List<Vector3> destination)
    {
        if (destination == null)
        {
            return false;
        }

        List<Collider> roads = new List<Collider>();
        GetResolvedAllowedRoads(roads);
        if (roads.Count == 0)
        {
            return false;
        }

        roads.Sort(delegate(Collider first, Collider second)
        {
            return first.bounds.center.x.CompareTo(second.bounds.center.x);
        });

        for (int i = 0; i < roads.Count; i++)
        {
            Collider collider = roads[i];
            if (collider == null)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            float minX = bounds.min.x + bounds.size.x * 0.12f;
            float centerX = bounds.center.x;
            float maxX = bounds.max.x - bounds.size.x * 0.12f;
            float z = bounds.center.z;

            AddProjectedFallbackPoint(destination, collider, new Vector3(minX, bounds.max.y + 3f, z));
            AddProjectedFallbackPoint(destination, collider, new Vector3(centerX, bounds.max.y + 3f, z));
            AddProjectedFallbackPoint(destination, collider, new Vector3(maxX, bounds.max.y + 3f, z));
        }

        if (destination.Count < 2)
        {
            return false;
        }

        destination.Sort(delegate(Vector3 first, Vector3 second)
        {
            return first.x.CompareTo(second.x);
        });

        if (spawnPoint != null)
        {
            AddRoutePointIfDistinct(destination, spawnPoint.position, true);
        }

        return destination.Count >= 2;
    }

    private static void AddProjectedFallbackPoint(List<Vector3> destination, Collider collider, Vector3 candidate)
    {
        if (collider == null)
        {
            return;
        }

        if (TryProjectToCollider(collider, candidate, out Vector3 projected))
        {
            AddRoutePointIfDistinct(destination, projected);
            return;
        }

        Vector3 closest = collider.ClosestPoint(candidate);
        AddRoutePointIfDistinct(destination, closest);
    }

    private static bool TryProjectToCollider(Collider collider, Vector3 candidate, out Vector3 projected)
    {
        projected = candidate;
        Ray downRay = new Ray(candidate, Vector3.down);
        if (collider.Raycast(downRay, out RaycastHit hit, 12f))
        {
            projected = hit.point;
            return true;
        }

        return false;
    }

    private static void AddRoutePointIfDistinct(List<Vector3> destination, Vector3 point, bool insertAtStart = false)
    {
        if (destination == null)
        {
            return;
        }

        if (insertAtStart)
        {
            if (destination.Count == 0 || Vector3.Distance(destination[0], point) > 0.35f)
            {
                destination.Insert(0, point);
            }

            return;
        }

        if (destination.Count == 0 || Vector3.Distance(destination[destination.Count - 1], point) > 0.35f)
        {
            destination.Add(point);
        }
    }

    private void OnValidate()
    {
        pauseIntervalRange.x = Mathf.Max(0.25f, pauseIntervalRange.x);
        pauseIntervalRange.y = Mathf.Max(pauseIntervalRange.x, pauseIntervalRange.y);
        pauseDurationRange.x = Mathf.Max(0.25f, pauseDurationRange.x);
        pauseDurationRange.y = Mathf.Max(pauseDurationRange.x, pauseDurationRange.y);
        runSpeedMultiplier = Mathf.Max(0.1f, runSpeedMultiplier);
        cameraFollowSmoothTime = Mathf.Max(0.01f, cameraFollowSmoothTime);
        minimumRemainingDistanceForStop = Mathf.Max(0f, minimumRemainingDistanceForStop);

        if (routeMarkers != null)
        {
            routeMarkers.RemoveAll(delegate(Transform marker) { return marker == null; });
        }

        if (allowedRoadColliders != null)
        {
            allowedRoadColliders.RemoveAll(delegate(Collider collider) { return collider == null; });
        }
    }
}
