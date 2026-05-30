using System.Collections.Generic;
using UnityEngine;

public sealed class PawPalWalkMapLocationDefinition
{
    public readonly string Id;
    public readonly string DisplayName;
    public readonly PawPalMapLocationType LocationType;
    public readonly Vector2 Position;
    public readonly float TriggerRadius;

    public PawPalWalkMapLocationDefinition(string id, string displayName, PawPalMapLocationType locationType, Vector2 position, float triggerRadius)
    {
        Id = id;
        DisplayName = displayName;
        LocationType = locationType;
        Position = position;
        TriggerRadius = triggerRadius;
    }
}

public static class PawPalWalkRouteGraph
{
    public const float MapWidth = 393f;
    public const float MapHeight = 786f;
    public const float DistanceToStaminaScale = 0.125f;
    public const float SnapDistance = 46f;
    public const float HomeRadius = 48f;
    public const float MinimumRouteDistance = 8f;
    private const float SegmentTolerance = 2.5f;

    private static readonly Vector2 Home = new Vector2(76.5f, 625f);

    private static readonly PawPalWalkMapLocationDefinition[] Locations =
    {
        new PawPalWalkMapLocationDefinition("home", "Home", PawPalMapLocationType.Home, Home, HomeRadius),
        new PawPalWalkMapLocationDefinition("dog_park", "Dog Park", PawPalMapLocationType.DogPark, new Vector2(162f, 324f), 62f),
        new PawPalWalkMapLocationDefinition("kennel", "Kennel", PawPalMapLocationType.Kennel, new Vector2(74f, 272f), 52f),
        new PawPalWalkMapLocationDefinition("competition_center", "Competition Center", PawPalMapLocationType.CompetitionCenter, new Vector2(163f, 173f), 72f)
    };

    private static readonly Vector2[] Nodes =
    {
        Home,
        new Vector2(76f, 548f),
        new Vector2(76f, 468f),
        new Vector2(76f, 370f),
        new Vector2(76f, 272f),
        new Vector2(76f, 173f),
        new Vector2(162f, 625f),
        new Vector2(162f, 548f),
        new Vector2(162f, 468f),
        new Vector2(162f, 370f),
        new Vector2(162f, 324f),
        new Vector2(162f, 240f),
        new Vector2(162f, 173f),
        new Vector2(260f, 625f),
        new Vector2(260f, 548f),
        new Vector2(260f, 468f),
        new Vector2(260f, 370f),
        new Vector2(260f, 324f),
        new Vector2(260f, 240f),
        new Vector2(260f, 173f),
        new Vector2(335f, 625f),
        new Vector2(335f, 548f),
        new Vector2(335f, 468f),
        new Vector2(335f, 370f),
        new Vector2(335f, 240f),
        new Vector2(335f, 173f)
    };

    private static readonly int[,] Edges =
    {
        { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 4 }, { 4, 5 },
        { 6, 7 }, { 7, 8 }, { 8, 9 }, { 9, 10 }, { 10, 11 }, { 11, 12 },
        { 13, 14 }, { 14, 15 }, { 15, 16 }, { 16, 17 }, { 17, 18 }, { 18, 19 },
        { 20, 21 }, { 21, 22 }, { 22, 23 }, { 23, 24 }, { 24, 25 },
        { 0, 6 }, { 6, 13 }, { 13, 20 },
        { 1, 7 }, { 7, 14 }, { 14, 21 },
        { 2, 8 }, { 8, 15 }, { 15, 22 },
        { 3, 9 }, { 9, 16 }, { 16, 23 },
        { 4, 11 }, { 11, 18 }, { 18, 24 },
        { 5, 12 }, { 12, 19 }, { 19, 25 },
        { 9, 11 }, { 16, 18 }
    };

    public static Vector2 HomePosition
    {
        get { return Home; }
    }

    public static IReadOnlyList<PawPalWalkMapLocationDefinition> MapLocations
    {
        get { return Locations; }
    }

    public static bool IsNearHome(Vector2 mapPoint)
    {
        return Vector2.Distance(mapPoint, Home) <= HomeRadius;
    }

    public static bool TrySnapToWalkablePath(Vector2 mapPoint, out Vector2 snappedPoint)
    {
        int edgeIndex;
        return TrySnapToWalkablePath(mapPoint, out snappedPoint, out edgeIndex);
    }

    public static bool TrySnapToWalkablePath(Vector2 mapPoint, out Vector2 snappedPoint, out int edgeIndex)
    {
        snappedPoint = mapPoint;
        edgeIndex = -1;
        float bestDistance = float.MaxValue;
        Vector2 bestPoint = mapPoint;
        int bestEdgeIndex = -1;

        for (int i = 0; i < Edges.GetLength(0); i++)
        {
            Vector2 a = Nodes[Edges[i, 0]];
            Vector2 b = Nodes[Edges[i, 1]];
            Vector2 candidate = ProjectToSegment(mapPoint, a, b);
            float distance = Vector2.Distance(mapPoint, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPoint = candidate;
                bestEdgeIndex = i;
            }
        }

        if (bestDistance > SnapDistance)
        {
            return false;
        }

        snappedPoint = IsNearHome(bestPoint) ? Home : bestPoint;
        edgeIndex = bestEdgeIndex;
        return true;
    }

    public static bool IsWalkableSegment(Vector2 start, Vector2 end)
    {
        if (Vector2.Distance(start, end) <= 0.001f)
        {
            return true;
        }

        for (int i = 0; i < Edges.GetLength(0); i++)
        {
            if (IsSegmentOnEdge(start, end, i))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGetSharedNode(int firstEdgeIndex, int secondEdgeIndex, out Vector2 node)
    {
        node = Vector2.zero;
        if (!IsValidEdgeIndex(firstEdgeIndex) || !IsValidEdgeIndex(secondEdgeIndex))
        {
            return false;
        }

        int firstA = Edges[firstEdgeIndex, 0];
        int firstB = Edges[firstEdgeIndex, 1];
        int secondA = Edges[secondEdgeIndex, 0];
        int secondB = Edges[secondEdgeIndex, 1];

        if (firstA == secondA || firstA == secondB)
        {
            node = Nodes[firstA];
            return true;
        }

        if (firstB == secondA || firstB == secondB)
        {
            node = Nodes[firstB];
            return true;
        }

        return false;
    }

    public static PawPalWalkRoutePlan BuildPlan(IList<Vector2> routePoints)
    {
        PawPalWalkRoutePlan plan = new PawPalWalkRoutePlan();
        if (routePoints == null)
        {
            return plan;
        }

        for (int i = 0; i < routePoints.Count; i++)
        {
            plan.RoutePoints.Add(new PawPalWalkPointData(routePoints[i]));
        }

        plan.RouteDistance = CalculateDistance(routePoints);
        plan.StaminaCost = plan.RouteDistance;
        AddPlannedStops(plan, routePoints);
        return plan;
    }

    public static float CalculateDistance(IList<Vector2> routePoints)
    {
        if (routePoints == null || routePoints.Count < 2)
        {
            return 0f;
        }

        float pixels = 0f;
        for (int i = 1; i < routePoints.Count; i++)
        {
            pixels += Vector2.Distance(routePoints[i - 1], routePoints[i]);
        }

        return pixels * DistanceToStaminaScale;
    }

    public static string ValidatePlan(PawPalWalkRoutePlan plan, float availableStamina)
    {
        if (plan == null || plan.RoutePoints.Count < 2)
        {
            return "Draw a route from Home.";
        }

        if (!IsNearHome(plan.RoutePoints[0].ToVector2()))
        {
            return "Route must start at Home.";
        }

        if (!IsNearHome(plan.RoutePoints[plan.RoutePoints.Count - 1].ToVector2()))
        {
            return "Route must return Home.";
        }

        if (!IsWalkableRoute(plan.RoutePoints))
        {
            return "Stay on connected streets.";
        }

        if (plan.RouteDistance < MinimumRouteDistance)
        {
            return "Make the walk a little longer.";
        }

        if (plan.StaminaCost > availableStamina + 0.001f)
        {
            return "Too tired for this route.";
        }

        return string.Empty;
    }

    private static void AddPlannedStops(PawPalWalkRoutePlan plan, IList<Vector2> routePoints)
    {
        if (plan == null || routePoints == null || routePoints.Count < 2)
        {
            return;
        }

        float totalPixels = 0f;
        for (int i = 1; i < routePoints.Count; i++)
        {
            totalPixels += Vector2.Distance(routePoints[i - 1], routePoints[i]);
        }

        if (totalPixels <= 0.001f)
        {
            return;
        }

        for (int locationIndex = 0; locationIndex < Locations.Length; locationIndex++)
        {
            PawPalWalkMapLocationDefinition location = Locations[locationIndex];
            if (location.LocationType == PawPalMapLocationType.Home)
            {
                continue;
            }

            float progress;
            if (TryGetLocationProgress(routePoints, location, totalPixels, out progress))
            {
                plan.PlannedStops.Add(new PawPalWalkLocationData
                {
                    LocationId = location.Id,
                    DisplayName = location.DisplayName,
                    LocationType = location.LocationType,
                    Progress = progress
                });
            }
        }
    }

    private static bool IsWalkableRoute(IList<PawPalWalkPointData> routePoints)
    {
        if (routePoints == null || routePoints.Count < 2)
        {
            return false;
        }

        for (int i = 1; i < routePoints.Count; i++)
        {
            if (!IsWalkableSegment(routePoints[i - 1].ToVector2(), routePoints[i].ToVector2()))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSegmentOnEdge(Vector2 start, Vector2 end, int edgeIndex)
    {
        if (!IsValidEdgeIndex(edgeIndex))
        {
            return false;
        }

        Vector2 edgeStart = Nodes[Edges[edgeIndex, 0]];
        Vector2 edgeEnd = Nodes[Edges[edgeIndex, 1]];
        float startT;
        float endT;
        float startDistance = DistanceToSegment(start, edgeStart, edgeEnd, out startT);
        float endDistance = DistanceToSegment(end, edgeStart, edgeEnd, out endT);
        if (startDistance > SegmentTolerance || endDistance > SegmentTolerance)
        {
            return false;
        }

        float edgeLength = Vector2.Distance(edgeStart, edgeEnd);
        float alongEdgeDistance = Mathf.Abs(endT - startT) * edgeLength;
        return Mathf.Abs(alongEdgeDistance - Vector2.Distance(start, end)) <= SegmentTolerance;
    }

    private static bool IsValidEdgeIndex(int edgeIndex)
    {
        return edgeIndex >= 0 && edgeIndex < Edges.GetLength(0);
    }

    private static bool TryGetLocationProgress(IList<Vector2> routePoints, PawPalWalkMapLocationDefinition location, float totalPixels, out float progress)
    {
        progress = 0f;
        float walked = 0f;
        for (int i = 1; i < routePoints.Count; i++)
        {
            Vector2 a = routePoints[i - 1];
            Vector2 b = routePoints[i];
            float segmentLength = Vector2.Distance(a, b);
            if (segmentLength <= 0.001f)
            {
                continue;
            }

            float t;
            float distance = DistanceToSegment(location.Position, a, b, out t);
            if (distance <= location.TriggerRadius)
            {
                progress = Mathf.Clamp01((walked + segmentLength * t) / totalPixels);
                return true;
            }

            walked += segmentLength;
        }

        return false;
    }

    private static Vector2 ProjectToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float sqrMagnitude = segment.sqrMagnitude;
        if (sqrMagnitude <= 0.001f)
        {
            return start;
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / sqrMagnitude);
        return start + segment * t;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end, out float t)
    {
        Vector2 segment = end - start;
        float sqrMagnitude = segment.sqrMagnitude;
        if (sqrMagnitude <= 0.001f)
        {
            t = 0f;
            return Vector2.Distance(point, start);
        }

        t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / sqrMagnitude);
        return Vector2.Distance(point, start + segment * t);
    }
}
