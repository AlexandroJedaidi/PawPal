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
    [SerializeField] private Vector3 cameraFollowWorldOffset = new Vector3(0.021f, 0.623f, -1.551f);
    [SerializeField] private Vector3 cameraFixedEulerAngles = new Vector3(8.904f, 0f, 0f);
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.35f, -3.2f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 0.7f, 0.35f);
    [SerializeField] private Vector2 pauseIntervalRange = new Vector2(5f, 9f);
    [SerializeField] private Vector2 pauseDurationRange = new Vector2(1.1f, 2f);
    [SerializeField, Min(0.1f)] private float runSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float endHoldDuration = 1f;
    [SerializeField, Min(0.01f)] private float cameraFollowSmoothTime = 0.12f;
    [SerializeField, Min(0f)] private float minimumRemainingDistanceForStop = 2.25f;
    [Header("Present Encounter")]
    [SerializeField] private GameObject presentEncounterPrefab;
    [SerializeField] private string editorPresentPrefabAssetPath = "Assets/3rd Party Packs/Presents/Sample Scene/Prefabs/present_02.prefab";
    [SerializeField] private string editorPresentModelAssetPath = "Assets/3rd Party Packs/Presents/Models/present_02.fbx";
    [SerializeField, Min(0.25f)] private float presentRunOffscreenDistance = 4.5f;
    [SerializeField, Min(0.25f)] private float presentApproachDistanceFromCamera = 1.15f;
    [SerializeField, Min(0.05f)] private float presentCinematicRunSpeed = 2.2f;
    [SerializeField, Min(0.05f)] private float presentCinematicApproachSpeed = 1.4f;
    [SerializeField] private Vector3 presentLocalPosition = new Vector3(0f, 0.0064f, 0.0279f);
    [SerializeField] private Vector3 presentLocalScale = new Vector3(0.08f, 0.08f, 0.08f);
    [SerializeField] private Vector3 presentLocalEulerAngles = new Vector3(0f, 187.255f, 0f);
    [Header("Pet Encounter")]
    [SerializeField] private Vector3 petEncounterCameraOffset = new Vector3(0.35f, 1.15f, -3.2f);
    [SerializeField] private Vector3 petEncounterLookAtOffset = new Vector3(0f, 0.55f, 0f);
    [SerializeField, Min(0.5f)] private float petEncounterVisitorSpawnDistance = 3.2f;
    [SerializeField, Min(0.25f)] private float petEncounterMeetDistance = 1.25f;
    [SerializeField, Min(0.5f)] private float petEncounterVisitorExitDistance = 3.5f;
    [SerializeField, Min(0.05f)] private float petEncounterRunSpeed = 1.75f;
    [SerializeField] private Vector2 petEncounterInteractionDurationRange = new Vector2(5f, 10f);
    [SerializeField] private string[] petEncounterVisitorDefinitionKeys =
    {
        "dog_toyterrier",
        "dog_beagle",
        "cat_simple",
        "cat_chubby"
    };
    [SerializeField, TextArea(2, 4)] private string[] petEncounterAdviceTexts =
    {
        "City walks are full of new smells. Let your pet pause sometimes so every route feels familiar.",
        "Friendly greetings are easier when the leash stays loose and both pets have room to look away.",
        "Some streets are busiest near corners. A calm pace helps your pet notice bikes, doors, and other walkers.",
        "Short, happy encounters can build confidence without making the walk feel too crowded."
    };

    public Transform SpawnPoint => spawnPoint;
    public PawPalWalkGraph Graph => graph;
    public Vector3 CameraFollowWorldOffset => cameraFollowWorldOffset;
    public Vector3 CameraFixedEulerAngles => cameraFixedEulerAngles;
    public Vector3 CameraOffset => cameraOffset;
    public Vector3 LookAtOffset => lookAtOffset;
    public float RunSpeedMultiplier => Mathf.Max(0.1f, runSpeedMultiplier);
    public float EndHoldDuration => Mathf.Max(0f, endHoldDuration);
    public float CameraFollowSmoothTime => Mathf.Max(0.01f, cameraFollowSmoothTime);
    public float MinimumRemainingDistanceForStop => Mathf.Max(0f, minimumRemainingDistanceForStop);
    public GameObject PresentEncounterPrefab => presentEncounterPrefab;
    public string EditorPresentPrefabAssetPath => editorPresentPrefabAssetPath;
    public string EditorPresentModelAssetPath => editorPresentModelAssetPath;
    public float PresentRunOffscreenDistance => Mathf.Max(0.25f, presentRunOffscreenDistance);
    public float PresentApproachDistanceFromCamera => Mathf.Max(0.25f, presentApproachDistanceFromCamera);
    public float PresentCinematicRunSpeed => Mathf.Max(0.05f, presentCinematicRunSpeed);
    public float PresentCinematicApproachSpeed => Mathf.Max(0.05f, presentCinematicApproachSpeed);
    public Vector3 PresentLocalPosition => presentLocalPosition;
    public Vector3 PresentLocalScale => presentLocalScale == Vector3.zero ? new Vector3(0.08f, 0.08f, 0.08f) : presentLocalScale;
    public Vector3 PresentLocalEulerAngles => presentLocalEulerAngles;
    public Vector3 PetEncounterCameraOffset => petEncounterCameraOffset;
    public Vector3 PetEncounterLookAtOffset => petEncounterLookAtOffset;
    public float PetEncounterVisitorSpawnDistance => Mathf.Max(0.5f, petEncounterVisitorSpawnDistance);
    public float PetEncounterMeetDistance => Mathf.Max(0.25f, petEncounterMeetDistance);
    public float PetEncounterVisitorExitDistance => Mathf.Max(0.5f, petEncounterVisitorExitDistance);
    public float PetEncounterRunSpeed => Mathf.Max(0.05f, petEncounterRunSpeed);
    public string[] PetEncounterVisitorDefinitionKeys => petEncounterVisitorDefinitionKeys;
    public string[] PetEncounterAdviceTexts => petEncounterAdviceTexts;

    public Vector2 PetEncounterInteractionDurationRange
    {
        get
        {
            float min = Mathf.Max(0.25f, petEncounterInteractionDurationRange.x);
            float max = Mathf.Max(min, petEncounterInteractionDurationRange.y);
            return new Vector2(min, max);
        }
    }

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
        presentRunOffscreenDistance = Mathf.Max(0.25f, presentRunOffscreenDistance);
        presentApproachDistanceFromCamera = Mathf.Max(0.25f, presentApproachDistanceFromCamera);
        presentCinematicRunSpeed = Mathf.Max(0.05f, presentCinematicRunSpeed);
        presentCinematicApproachSpeed = Mathf.Max(0.05f, presentCinematicApproachSpeed);
        if (presentLocalScale == Vector3.zero)
        {
            presentLocalScale = new Vector3(0.08f, 0.08f, 0.08f);
        }

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
