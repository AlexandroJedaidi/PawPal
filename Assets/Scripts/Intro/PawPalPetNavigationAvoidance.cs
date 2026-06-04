using UnityEngine;
using UnityEngine.AI;

internal static class PawPalPetNavigationAvoidance
{
    private const float MinimumMoverClearance = 0.035f;
    private const float MaximumMoverClearanceFallback = 0.16f;
    private const float StationaryPetEdgePadding = 0.015f;

    private struct PlanarFootprint
    {
        public Vector3 Center;
        public Vector2 HalfExtents;
    }

    public static bool IsPointComfortable(
        Transform self,
        Vector3 point,
        float selfRadius,
        float extraPadding,
        bool includeMovingPets)
    {
        Transform blocker;
        return !TryFindPointBlocker(self, point, selfRadius, extraPadding, includeMovingPets, out blocker);
    }

    public static bool TryFindPointBlocker(
        Transform self,
        Vector3 point,
        float selfRadius,
        float extraPadding,
        bool includeMovingPets,
        out Transform blocker)
    {
        blocker = null;
        float clearance = GetMoverClearance(self, selfRadius) + Mathf.Max(0f, extraPadding);

        DogRoomAgent[] dogs = UnityEngine.Object.FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (!ShouldConsiderDog(self, dog, includeMovingPets))
            {
                continue;
            }

            if (IsPointBlockedByFootprint(point, dog.transform, dog.GetInteractionNavigationFootprintRadius(), clearance, IsStationary(dog)))
            {
                blocker = dog.transform;
                return true;
            }
        }

        PawPalCatRoomAgent[] cats = UnityEngine.Object.FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < cats.Length; i++)
        {
            PawPalCatRoomAgent cat = cats[i];
            if (!ShouldConsiderCat(self, cat, includeMovingPets))
            {
                continue;
            }

            if (IsPointBlockedByFootprint(point, cat.transform, cat.GetInteractionNavigationFootprintRadius(), clearance, IsStationary(cat)))
            {
                blocker = cat.transform;
                return true;
            }
        }

        return false;
    }

    public static bool IsPathBlocked(
        Transform self,
        NavMeshAgent agent,
        Vector3 destination,
        float selfRadius,
        float extraPadding,
        bool includeSteeringTarget,
        bool includeMovingPets,
        float proactiveStopPadding,
        float minimumProgressPadding,
        out Transform blocker,
        out bool personalSpaceBlocked)
    {
        float moverClearance = GetMoverClearance(self, selfRadius);
        float pointClearance = moverClearance + Mathf.Max(0f, extraPadding);
        personalSpaceBlocked = TryFindPointBlocker(
            self,
            self != null ? self.position : destination,
            selfRadius,
            extraPadding,
            includeMovingPets,
            out blocker);
        if (personalSpaceBlocked)
        {
            return true;
        }

        blocker = null;
        if (self == null)
        {
            return false;
        }

        Vector3 start = self.position;
        Vector3 steeringTarget = destination;
        if (includeSteeringTarget && agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
        {
            steeringTarget = agent.steeringTarget;
            if (PlanarDistance(start, steeringTarget) <= 0.05f)
            {
                steeringTarget = destination;
            }
        }

        float pathClearance = pointClearance
            + Mathf.Max(0f, proactiveStopPadding)
            + Mathf.Max(0f, minimumProgressPadding);

        DogRoomAgent[] dogs = UnityEngine.Object.FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (!ShouldConsiderDog(self, dog, includeMovingPets))
            {
                continue;
            }

            if (IsFootprintBlockingPath(
                dog.transform,
                dog.GetInteractionNavigationFootprintRadius(),
                IsStationary(dog),
                start,
                steeringTarget,
                destination,
                pathClearance,
                includeSteeringTarget))
            {
                blocker = dog.transform;
                return true;
            }
        }

        PawPalCatRoomAgent[] cats = UnityEngine.Object.FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < cats.Length; i++)
        {
            PawPalCatRoomAgent cat = cats[i];
            if (!ShouldConsiderCat(self, cat, includeMovingPets))
            {
                continue;
            }

            if (IsFootprintBlockingPath(
                cat.transform,
                cat.GetInteractionNavigationFootprintRadius(),
                IsStationary(cat),
                start,
                steeringTarget,
                destination,
                pathClearance,
                includeSteeringTarget))
            {
                blocker = cat.transform;
                return true;
            }
        }

        return false;
    }

    public static float EstimateVisualFootprintRadius(Transform root, NavMeshAgent agent, float fallbackRadius, float padding, float minRadius, float maxRadius)
    {
        PlanarFootprint footprint;
        float radius = TryGetPlanarFootprint(root, fallbackRadius, out footprint)
            ? Mathf.Max(footprint.HalfExtents.x, footprint.HalfExtents.y)
            : (agent != null ? Mathf.Max(0.08f, agent.radius) : fallbackRadius);

        return Mathf.Clamp(radius + Mathf.Max(0f, padding), Mathf.Max(0.05f, minRadius), Mathf.Max(minRadius, maxRadius));
    }

    public static float EstimateMovementClearanceRadius(Transform root, NavMeshAgent agent, float fallbackRadius, float padding, float minRadius, float maxRadius)
    {
        PlanarFootprint footprint;
        float radius = TryGetPlanarFootprint(root, fallbackRadius, out footprint)
            ? Mathf.Min(footprint.HalfExtents.x, footprint.HalfExtents.y)
            : (agent != null ? Mathf.Max(0.08f, agent.radius) : fallbackRadius);

        return Mathf.Clamp(radius + Mathf.Max(0f, padding), Mathf.Max(0.03f, minRadius), Mathf.Max(minRadius, maxRadius));
    }

    public static float DistanceToPlanarSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        point.y = 0f;
        start.y = 0f;
        end.y = 0f;
        Vector3 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= 0.0001f)
        {
            return Vector3.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSquared);
        Vector3 projection = start + segment * t;
        return Vector3.Distance(point, projection);
    }

    private static bool IsPointBlockedByFootprint(Vector3 point, Transform other, float fallbackRadius, float clearance, bool stationary)
    {
        PlanarFootprint footprint;
        if (!TryGetPlanarFootprint(other, fallbackRadius, out footprint))
        {
            return IsPointBlockedByFallbackCircle(point, other, fallbackRadius, clearance, stationary);
        }

        Vector2 halfExtents = ExpandHalfExtents(footprint.HalfExtents, clearance, stationary);
        Vector2 localPoint = new Vector2(point.x - footprint.Center.x, point.z - footprint.Center.z);
        return Mathf.Abs(localPoint.x) <= halfExtents.x
            && Mathf.Abs(localPoint.y) <= halfExtents.y;
    }

    private static bool IsFootprintBlockingPath(
        Transform other,
        float fallbackRadius,
        bool stationary,
        Vector3 start,
        Vector3 steeringTarget,
        Vector3 goal,
        float clearance,
        bool includeSteeringTarget)
    {
        PlanarFootprint footprint;
        if (!TryGetPlanarFootprint(other, fallbackRadius, out footprint))
        {
            return IsFallbackCircleBlockingPath(other, fallbackRadius, stationary, start, steeringTarget, goal, clearance, includeSteeringTarget);
        }

        Vector2 halfExtents = ExpandHalfExtents(footprint.HalfExtents, clearance, stationary);
        return (includeSteeringTarget && SegmentIntersectsAabb(start, steeringTarget, footprint.Center, halfExtents))
            || SegmentIntersectsAabb(start, goal, footprint.Center, halfExtents);
    }

    private static bool TryGetPlanarFootprint(Transform root, float fallbackRadius, out PlanarFootprint footprint)
    {
        footprint = new PlanarFootprint
        {
            Center = root != null ? root.position : Vector3.zero,
            HalfExtents = Vector2.one * Mathf.Max(MinimumMoverClearance, fallbackRadius)
        };

        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(root.position, Vector3.zero);
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

        if (!hasBounds)
        {
            return false;
        }

        footprint.Center = bounds.center;
        footprint.HalfExtents = new Vector2(
            Mathf.Max(MinimumMoverClearance, bounds.extents.x),
            Mathf.Max(MinimumMoverClearance, bounds.extents.z));
        return true;
    }

    private static float GetMoverClearance(Transform self, float fallbackRadius)
    {
        PlanarFootprint footprint;
        if (TryGetPlanarFootprint(self, fallbackRadius, out footprint))
        {
            return Mathf.Max(MinimumMoverClearance, Mathf.Min(footprint.HalfExtents.x, footprint.HalfExtents.y));
        }

        return Mathf.Clamp(Mathf.Max(MinimumMoverClearance, fallbackRadius), MinimumMoverClearance, MaximumMoverClearanceFallback);
    }

    private static Vector2 ExpandHalfExtents(Vector2 halfExtents, float clearance, bool stationary)
    {
        float padding = Mathf.Max(0f, clearance) + (stationary ? StationaryPetEdgePadding : 0f);
        return new Vector2(
            Mathf.Max(MinimumMoverClearance, halfExtents.x) + padding,
            Mathf.Max(MinimumMoverClearance, halfExtents.y) + padding);
    }

    private static bool SegmentIntersectsAabb(Vector3 start, Vector3 end, Vector3 center, Vector2 halfExtents)
    {
        Vector2 localStart = new Vector2(start.x - center.x, start.z - center.z);
        Vector2 localEnd = new Vector2(end.x - center.x, end.z - center.z);
        if (IsInsideAabb(localStart, halfExtents) || IsInsideAabb(localEnd, halfExtents))
        {
            return true;
        }

        Vector2 delta = localEnd - localStart;
        float tMin = 0f;
        float tMax = 1f;
        return ClipSegmentAxis(localStart.x, delta.x, -halfExtents.x, halfExtents.x, ref tMin, ref tMax)
            && ClipSegmentAxis(localStart.y, delta.y, -halfExtents.y, halfExtents.y, ref tMin, ref tMax);
    }

    private static bool IsInsideAabb(Vector2 point, Vector2 halfExtents)
    {
        return Mathf.Abs(point.x) <= halfExtents.x
            && Mathf.Abs(point.y) <= halfExtents.y;
    }

    private static bool ClipSegmentAxis(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(direction) <= 0.0001f)
        {
            return origin >= min && origin <= max;
        }

        float inverse = 1f / direction;
        float first = (min - origin) * inverse;
        float second = (max - origin) * inverse;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }

        tMin = Mathf.Max(tMin, first);
        tMax = Mathf.Min(tMax, second);
        return tMin <= tMax;
    }

    private static bool IsPointBlockedByFallbackCircle(Vector3 point, Transform other, float fallbackRadius, float clearance, bool stationary)
    {
        if (other == null)
        {
            return false;
        }

        Vector3 otherPosition = other.position;
        otherPosition.y = 0f;
        point.y = 0f;
        float requiredDistance = Mathf.Max(MinimumMoverClearance, fallbackRadius)
            + Mathf.Max(0f, clearance)
            + (stationary ? StationaryPetEdgePadding : 0f);
        return (otherPosition - point).sqrMagnitude < requiredDistance * requiredDistance;
    }

    private static bool IsFallbackCircleBlockingPath(
        Transform other,
        float fallbackRadius,
        bool stationary,
        Vector3 start,
        Vector3 steeringTarget,
        Vector3 goal,
        float clearance,
        bool includeSteeringTarget)
    {
        if (other == null)
        {
            return false;
        }

        Vector3 otherPosition = other.position;
        otherPosition.y = 0f;
        float threshold = Mathf.Max(MinimumMoverClearance, fallbackRadius)
            + Mathf.Max(0f, clearance)
            + (stationary ? StationaryPetEdgePadding : 0f);
        return (includeSteeringTarget && DistanceToPlanarSegment(otherPosition, start, steeringTarget) <= threshold)
            || DistanceToPlanarSegment(otherPosition, start, goal) <= threshold;
    }

    private static bool ShouldConsiderDog(Transform self, DogRoomAgent dog, bool includeMovingPets)
    {
        return dog != null
            && dog.isActiveAndEnabled
            && !IsSelf(self, dog.transform)
            && IsSameScene(self, dog.transform)
            && (includeMovingPets || IsStationary(dog));
    }

    private static bool ShouldConsiderCat(Transform self, PawPalCatRoomAgent cat, bool includeMovingPets)
    {
        return cat != null
            && cat.isActiveAndEnabled
            && !IsSelf(self, cat.transform)
            && IsSameScene(self, cat.transform)
            && (includeMovingPets || IsStationary(cat));
    }

    private static bool IsSelf(Transform self, Transform other)
    {
        return self != null
            && other != null
            && (self == other || self.IsChildOf(other) || other.IsChildOf(self));
    }

    private static bool IsSameScene(Transform self, Transform other)
    {
        return self == null
            || other == null
            || self.gameObject.scene == other.gameObject.scene;
    }

    private static bool IsStationary(DogRoomAgent dog)
    {
        return dog == null || dog.IsResting || dog.IsSleeping || dog.IsBusy || !dog.IsMoving;
    }

    private static bool IsStationary(PawPalCatRoomAgent cat)
    {
        return cat == null || cat.IsResting || cat.IsSleeping || cat.IsBusy || !cat.IsMoving;
    }

    private static float PlanarDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }
}
