using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class DogNeedInteractionDirector : MonoBehaviour
{
    private const string EditorFoodBowlAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Bowl_1_food_1.prefab";
    private const string EditorWaterBowlAssetPath = "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Dog_Object/Prefabs/Bowl_2_water.prefab";
    private const string EditorEatingAudioAssetPath = "Assets/Audio/dog_eating.mp3";
    private const string EditorDrinkingAudioAssetPath = "Assets/Audio/dog_drinking.mp3";
    private const float MinRuntimeBowlApproachDistance = 0.14f;
    private const float MaxRuntimeBowlApproachDistance = 0.5f;

    [Header("Bowl Prefabs")]
    [SerializeField] private GameObject foodBowlPrefab;
    [SerializeField] private GameObject waterBowlPrefab;

    [Header("Scene References")]
    [SerializeField] private Camera roomCamera;
    [SerializeField] private DogCycleCamera dogCamera;

    [Header("Placement")]
    [SerializeField] private float spawnDistanceFromCamera = 1f;
    [SerializeField] private float groundRayHeight = 3f;
    [SerializeField] private float groundRayDistance = 8f;
    [SerializeField] private float bowlFloorPadding = 0.025f;
    [SerializeField] private float dogApproachDistance = 0.28f;
    [SerializeField] private float smallDogApproachDistance = 0.2f;
    [SerializeField] private float largeDogApproachDistance = 0.38f;
    [SerializeField] private Vector2 dogSizeForBowlApproachRange = new Vector2(0.75f, 1.55f);
    [SerializeField] private float preciseArrivalDistance = 0.04f;
    [SerializeField] private float maxBowlUsePlanarDistance = 0.48f;
    [SerializeField] private float moveTimeout = 7f;
    [SerializeField] private float faceDuration = 0.45f;
    [SerializeField] private float useLoopDuration = 5f;
    [SerializeField] private Vector3 cameraFocusOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private float otherDogClearanceRadius = 0.85f;
    [SerializeField] private float otherDogMoveAwayDistance = 1.15f;
    [SerializeField] private float otherDogMoveTimeout = 3f;
    [SerializeField] private int approachCandidateCount = 12;

    [Header("Audio")]
    [SerializeField] private AudioClip eatingClip;
    [SerializeField] private AudioClip drinkingClip;
    [SerializeField, Range(0f, 1f)] private float bowlUseVolume = 0.75f;

    private Coroutine activeRoutine;
    private GameObject activeBowl;

    public static DogNeedInteractionDirector Instance { get; private set; }

    public bool IsInteractionActive
    {
        get { return activeRoutine != null; }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AutoAssignEditorAudioClips();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AutoAssignEditorAudioClips();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        CleanupActiveBowl();
    }

    public bool TryStartInteraction(PawPalDogNeed need, Action onCompleted)
    {
        if (activeRoutine != null)
        {
            return false;
        }

        if (need != PawPalDogNeed.Food && need != PawPalDogNeed.Water)
        {
            return false;
        }

        DogRoomAgent dog = ResolveActiveDog();
        if (dog == null)
        {
            Debug.LogWarning("DogNeedInteractionDirector could not start because no active DogRoomAgent was found.");
            return false;
        }

        GameObject prefab = ResolveBowlPrefab(need);
        if (prefab == null)
        {
            Debug.LogWarning("DogNeedInteractionDirector has no " + need + " bowl prefab assigned.");
            return false;
        }

        activeRoutine = StartCoroutine(InteractionRoutine(dog, prefab, need, onCompleted));
        return true;
    }

    private GameObject ResolveBowlPrefab(PawPalDogNeed need)
    {
        GameObject assignedPrefab = need == PawPalDogNeed.Food ? foodBowlPrefab : waterBowlPrefab;
        if (assignedPrefab != null)
        {
            return assignedPrefab;
        }

#if UNITY_EDITOR
        string assetPath = need == PawPalDogNeed.Food ? EditorFoodBowlAssetPath : EditorWaterBowlAssetPath;
        GameObject editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (editorPrefab != null)
        {
            if (need == PawPalDogNeed.Food)
            {
                foodBowlPrefab = editorPrefab;
            }
            else
            {
                waterBowlPrefab = editorPrefab;
            }

            return editorPrefab;
        }
#endif

        return null;
    }

    private IEnumerator InteractionRoutine(DogRoomAgent dog, GameObject bowlPrefab, PawPalDogNeed need, Action onCompleted)
    {
        bool completed = false;
        int cameraFocusId = 0;
        List<DogRoomAgent> pausedBlockers = new List<DogRoomAgent>();
        try
        {
            dog.PauseForSocial();

            activeBowl = Instantiate(bowlPrefab);
            activeBowl.name = bowlPrefab.name;
            PositionBowl(activeBowl, dog);

            DogCycleCamera resolvedDogCamera = ResolveDogCamera();
            if (resolvedDogCamera != null)
            {
                cameraFocusId = resolvedDogCamera.BeginPairFocus(dog.transform, activeBowl.transform, DogCameraFocusPriority.NeedInteraction, cameraFocusOffset);
            }

            Vector3 approachPoint = ResolveApproachPoint(dog, activeBowl.transform.position);
            yield return ClearOtherDogsFromBowlArea(dog, activeBowl.transform.position, approachPoint, pausedBlockers);
            approachPoint = ResolveApproachPoint(dog, activeBowl.transform.position);
            yield return dog.MoveNearPrecise(approachPoint, moveTimeout, DogMovementPace.Walk, preciseArrivalDistance);
            yield return EnsureDogCloseEnoughForBowlUse(dog, activeBowl.transform.position, approachPoint);
            yield return dog.FaceTarget(activeBowl.transform, faceDuration);
            StartCoroutine(PlayBowlUseAudio(dog, activeBowl.transform, need, useLoopDuration));
            yield return dog.PlayBowlUse(need == PawPalDogNeed.Water, useLoopDuration);

            completed = true;
            if (onCompleted != null)
            {
                onCompleted();
            }
        }
        finally
        {
            DogCycleCamera resolvedDogCamera = ResolveDogCamera();
            if (resolvedDogCamera != null && cameraFocusId != 0)
            {
                resolvedDogCamera.EndFocus(cameraFocusId);
            }

            CleanupActiveBowl();

            if (dog != null && dog.isActiveAndEnabled)
            {
                dog.StartRoaming();
            }

            ResumePausedBlockers(pausedBlockers);
            activeRoutine = null;
        }

        if (!completed)
        {
            yield break;
        }
    }

    private DogRoomAgent ResolveActiveDog()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null || dogs.Length == 0)
        {
            return null;
        }

        if (runtime != null && runtime.ActiveDog != null && !string.IsNullOrEmpty(runtime.ActiveDog.Id))
        {
            string activeDogId = runtime.ActiveDog.Id;
            for (int i = 0; i < dogs.Length; i++)
            {
                DogRoomAgent candidate = dogs[i];
                if (candidate != null
                    && candidate.HasExplicitDogId
                    && string.Equals(candidate.DogId, activeDogId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        if (runtime != null)
        {
            int index = Mathf.Clamp(runtime.ActiveDogIndex, 0, dogs.Length - 1);
            if (dogs[index] != null)
            {
                return dogs[index];
            }
        }

        for (int i = 0; i < dogs.Length; i++)
        {
            if (dogs[i] != null)
            {
                return dogs[i];
            }
        }

        return null;
    }

    private void PositionBowl(GameObject bowl, DogRoomAgent dog)
    {
        Vector3 requestedPosition = ResolveCameraSpawnPosition(dog);
        Vector3 roomPoint;
        if (dog.TryGetRoomSafePoint(requestedPosition, 1.25f, out roomPoint))
        {
            requestedPosition = roomPoint;
        }

        Vector3 floorPosition = ResolveFloorPosition(bowl, requestedPosition);
        bowl.transform.position = floorPosition;

        Vector3 lookDirection = dog.transform.position - floorPosition;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            bowl.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }

    private Vector3 ResolveCameraSpawnPosition(DogRoomAgent dog)
    {
        Camera camera = ResolveRoomCamera();
        if (camera == null)
        {
            return dog.transform.position + dog.transform.forward * 0.85f;
        }

        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = dog.transform.position - camera.transform.position;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = dog.transform.forward;
        }

        return camera.transform.position + forward.normalized * Mathf.Max(0.2f, spawnDistanceFromCamera);
    }

    private Vector3 ResolveFloorPosition(GameObject bowl, Vector3 requestedPosition)
    {
        float bottomOffset = GetBottomOffset(bowl);
        RaycastHit hit;
        Vector3 rayOrigin = requestedPosition + Vector3.up * Mathf.Max(0.1f, groundRayHeight);
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, Mathf.Max(0.1f, groundRayDistance), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * (bottomOffset + bowlFloorPadding);
        }

        return requestedPosition + Vector3.up * (bottomOffset + bowlFloorPadding);
    }

    private Vector3 ResolveApproachPoint(DogRoomAgent dog, Vector3 bowlPosition)
    {
        float approachDistance = GetEffectiveBowlApproachDistance(dog);
        Vector3 preferredDirection = ResolvePreferredApproachDirection(dog, bowlPosition);
        Vector3 preferredPoint;
        if (TryResolveApproachCandidate(dog, bowlPosition, preferredDirection, approachDistance, out preferredPoint))
        {
            return preferredPoint;
        }

        int candidateCount = Mathf.Max(4, approachCandidateCount);
        float preferredYaw = Mathf.Atan2(preferredDirection.x, preferredDirection.z) * Mathf.Rad2Deg;
        for (int i = 0; i < candidateCount; i++)
        {
            int side = i % 2 == 0 ? 1 : -1;
            int step = (i / 2) + 1;
            float angle = preferredYaw + side * step * (180f / candidateCount);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 point;
            if (TryResolveApproachCandidate(dog, bowlPosition, direction, approachDistance, out point))
            {
                return point;
            }
        }

        Vector3 requested = bowlPosition + preferredDirection * approachDistance;
        Vector3 safePoint;
        if (dog.TryGetRoomSafePoint(requested, 0.25f, out safePoint))
        {
            return safePoint;
        }

        return requested;
    }

    private bool TryResolveApproachCandidate(DogRoomAgent dog, Vector3 bowlPosition, Vector3 direction, float approachDistance, out Vector3 point)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            point = bowlPosition;
            return false;
        }

        Vector3 requested = bowlPosition + direction.normalized * approachDistance;
        if (!dog.TryGetRoomSafePoint(requested, 0.25f, out point))
        {
            return false;
        }

        return IsPointClearOfOtherDogs(point, dog, Mathf.Max(0.25f, otherDogClearanceRadius * 0.55f));
    }

    private Vector3 ResolvePreferredApproachDirection(DogRoomAgent dog, Vector3 bowlPosition)
    {
        Camera camera = ResolveRoomCamera();
        if (camera != null)
        {
            Vector3 fromCameraToBowl = bowlPosition - camera.transform.position;
            fromCameraToBowl.y = 0f;
            if (fromCameraToBowl.sqrMagnitude > 0.001f)
            {
                return fromCameraToBowl.normalized;
            }
        }

        Vector3 fromBowlToDog = dog.transform.position - bowlPosition;
        fromBowlToDog.y = 0f;
        if (fromBowlToDog.sqrMagnitude > 0.001f)
        {
            return fromBowlToDog.normalized;
        }

        Vector3 fallback = -dog.transform.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.back;
    }

    private IEnumerator EnsureDogCloseEnoughForBowlUse(DogRoomAgent dog, Vector3 bowlPosition, Vector3 approachPoint)
    {
        if (dog == null || IsDogCloseEnoughForBowlUse(dog, bowlPosition, approachPoint))
        {
            yield break;
        }

        Vector3 retryPoint = ResolveApproachPoint(dog, bowlPosition);
        yield return dog.MoveNearPrecise(retryPoint, Mathf.Max(1f, moveTimeout * 0.45f), DogMovementPace.Walk, preciseArrivalDistance);

        if (!IsDogCloseEnoughForBowlUse(dog, bowlPosition, retryPoint))
        {
            Debug.LogWarning("DogNeedInteractionDirector: " + dog.name + " is still too far from the bowl for a natural " + "eat/drink animation.");
        }
    }

    private bool IsDogCloseEnoughForBowlUse(DogRoomAgent dog, Vector3 bowlPosition, Vector3 approachPoint)
    {
        Vector3 dogPosition = dog.transform.position;
        dogPosition.y = 0f;
        Vector3 flatBowlPosition = bowlPosition;
        flatBowlPosition.y = 0f;
        Vector3 flatApproachPoint = approachPoint;
        flatApproachPoint.y = 0f;

        float approachDistance = Vector3.Distance(dogPosition, flatApproachPoint);
        float bowlDistance = Vector3.Distance(dogPosition, flatBowlPosition);
        return approachDistance <= Mathf.Max(0.08f, preciseArrivalDistance * 3f)
            && bowlDistance <= Mathf.Max(GetEffectiveBowlApproachDistance(dog) + 0.18f, maxBowlUsePlanarDistance);
    }

    private float GetEffectiveBowlApproachDistance(DogRoomAgent dog)
    {
        float mediumDistance = dogApproachDistance > 0f ? dogApproachDistance : 0.28f;
        float smallDistance = Mathf.Min(mediumDistance, smallDogApproachDistance > 0f ? smallDogApproachDistance : 0.2f);
        float largeDistance = Mathf.Max(mediumDistance, largeDogApproachDistance > 0f ? largeDogApproachDistance : 0.38f);

        float size01;
        if (!TryGetDogBowlApproachSize01(dog, out size01))
        {
            return Mathf.Clamp(mediumDistance, MinRuntimeBowlApproachDistance, MaxRuntimeBowlApproachDistance);
        }

        float distance = size01 <= 0.5f
            ? Mathf.Lerp(smallDistance, mediumDistance, size01 * 2f)
            : Mathf.Lerp(mediumDistance, largeDistance, (size01 - 0.5f) * 2f);

        return Mathf.Clamp(distance, MinRuntimeBowlApproachDistance, MaxRuntimeBowlApproachDistance);
    }

    private bool TryGetDogBowlApproachSize01(DogRoomAgent dog, out float size01)
    {
        size01 = 0.5f;
        if (dog == null)
        {
            return false;
        }

        Bounds bounds;
        if (TryGetRendererBounds(dog.gameObject, out bounds))
        {
            float planarSize = Mathf.Max(bounds.size.x, bounds.size.z);
            float heightAdjustedSize = bounds.size.y * 1.15f;
            float sizeMetric = Mathf.Max(planarSize, heightAdjustedSize);
            float minSize = Mathf.Min(dogSizeForBowlApproachRange.x, dogSizeForBowlApproachRange.y);
            float maxSize = Mathf.Max(dogSizeForBowlApproachRange.x, dogSizeForBowlApproachRange.y);
            size01 = Mathf.InverseLerp(minSize, maxSize, sizeMetric);
        }

        string dogName = dog.name;
        if (ContainsIgnoreCase(dogName, "puppy"))
        {
            size01 = Mathf.Min(size01, 0.18f);
        }
        else if (ContainsIgnoreCase(dogName, "corgi"))
        {
            size01 = Mathf.Clamp(size01, 0.45f, 0.75f);
        }

        return true;
    }

    private IEnumerator ClearOtherDogsFromBowlArea(DogRoomAgent activeDog, Vector3 bowlPosition, Vector3 approachPoint, List<DogRoomAgent> pausedBlockers)
    {
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null || dogs.Length == 0)
        {
            yield break;
        }

        List<Coroutine> moveRoutines = new List<Coroutine>();
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent otherDog = dogs[i];
            if (otherDog == null || otherDog == activeDog || !IsDogBlockingBowlUse(otherDog, activeDog, bowlPosition, approachPoint))
            {
                continue;
            }

            Vector3 moveAwayPoint;
            if (!TryResolveOtherDogMoveAwayPoint(otherDog, activeDog, bowlPosition, approachPoint, out moveAwayPoint))
            {
                continue;
            }

            otherDog.PauseForSocial();
            pausedBlockers.Add(otherDog);
            moveRoutines.Add(StartCoroutine(otherDog.MoveNear(moveAwayPoint, otherDogMoveTimeout, DogMovementPace.Trot)));
        }

        for (int i = 0; i < moveRoutines.Count; i++)
        {
            yield return moveRoutines[i];
        }
    }

    private bool IsDogBlockingBowlUse(DogRoomAgent otherDog, DogRoomAgent activeDog, Vector3 bowlPosition, Vector3 approachPoint)
    {
        Vector3 dogPosition = otherDog.transform.position;
        dogPosition.y = 0f;
        Vector3 activeDogPosition = activeDog.transform.position;
        activeDogPosition.y = 0f;
        Vector3 flatBowlPosition = bowlPosition;
        flatBowlPosition.y = 0f;
        Vector3 flatApproachPoint = approachPoint;
        flatApproachPoint.y = 0f;

        float clearance = Mathf.Max(0.2f, otherDogClearanceRadius);
        float sqrClearance = clearance * clearance;
        if ((dogPosition - flatBowlPosition).sqrMagnitude <= sqrClearance
            || (dogPosition - flatApproachPoint).sqrMagnitude <= sqrClearance
            || (dogPosition - activeDogPosition).sqrMagnitude <= sqrClearance * 0.65f)
        {
            return true;
        }

        return DistanceToSegment(dogPosition, flatBowlPosition, flatApproachPoint) <= clearance * 0.65f;
    }

    private bool TryResolveOtherDogMoveAwayPoint(DogRoomAgent otherDog, DogRoomAgent activeDog, Vector3 bowlPosition, Vector3 approachPoint, out Vector3 point)
    {
        Vector3 awayDirection = otherDog.transform.position - bowlPosition;
        awayDirection.y = 0f;
        if (awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = otherDog.transform.position - activeDog.transform.position;
            awayDirection.y = 0f;
        }

        if (awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = -ResolvePreferredApproachDirection(activeDog, bowlPosition);
        }

        awayDirection.Normalize();
        float distance = Mathf.Max(0.4f, otherDogMoveAwayDistance);
        for (int i = 0; i < 8; i++)
        {
            float angle = i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f) * ((i + 1) / 2) * 35f;
            Vector3 candidateDirection = Quaternion.Euler(0f, angle, 0f) * awayDirection;
            Vector3 candidate = otherDog.transform.position + candidateDirection * distance;
            if (otherDog.TryGetRoomSafePoint(candidate, 0.45f, out point)
                && IsPointClearOfOtherDogs(point, otherDog, Mathf.Max(0.35f, otherDogClearanceRadius * 0.65f))
                && !IsPointNearBowlUseArea(point, bowlPosition, approachPoint))
            {
                return true;
            }
        }

        point = otherDog.transform.position + awayDirection * distance;
        return otherDog.TryGetRoomSafePoint(point, 0.45f, out point);
    }

    private bool IsPointClearOfOtherDogs(Vector3 point, DogRoomAgent ignoredDog, float radius)
    {
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null)
        {
            return true;
        }

        float sqrRadius = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        Vector3 flatPoint = point;
        flatPoint.y = 0f;
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent otherDog = dogs[i];
            if (otherDog == null || otherDog == ignoredDog)
            {
                continue;
            }

            Vector3 otherPosition = otherDog.transform.position;
            otherPosition.y = 0f;
            if ((otherPosition - flatPoint).sqrMagnitude < sqrRadius)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPointNearBowlUseArea(Vector3 point, Vector3 bowlPosition, Vector3 approachPoint)
    {
        Vector3 flatPoint = point;
        flatPoint.y = 0f;
        Vector3 flatBowlPosition = bowlPosition;
        flatBowlPosition.y = 0f;
        Vector3 flatApproachPoint = approachPoint;
        flatApproachPoint.y = 0f;

        float clearance = Mathf.Max(0.2f, otherDogClearanceRadius);
        return (flatPoint - flatBowlPosition).sqrMagnitude <= clearance * clearance
            || (flatPoint - flatApproachPoint).sqrMagnitude <= clearance * clearance;
    }

    private void ResumePausedBlockers(List<DogRoomAgent> pausedBlockers)
    {
        if (pausedBlockers == null)
        {
            return;
        }

        for (int i = 0; i < pausedBlockers.Count; i++)
        {
            DogRoomAgent blocker = pausedBlockers[i];
            if (blocker != null && blocker.isActiveAndEnabled)
            {
                blocker.StartRoaming();
            }
        }
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        segment.y = 0f;
        if (segment.sqrMagnitude < 0.001f)
        {
            return Vector3.Distance(point, start);
        }

        Vector3 toPoint = point - start;
        toPoint.y = 0f;
        float t = Mathf.Clamp01(Vector3.Dot(toPoint, segment) / segment.sqrMagnitude);
        return Vector3.Distance(point, start + segment * t);
    }

    private IEnumerator PlayBowlUseAudio(DogRoomAgent dog, Transform bowl, PawPalDogNeed need, float duration)
    {
        AudioClip clip = need == PawPalDogNeed.Water ? drinkingClip : eatingClip;
        if (clip == null || dog == null)
        {
            yield break;
        }

        GameObject audioObject = new GameObject(dog.name + "_" + need + "Audio");
        audioObject.transform.position = bowl != null ? bowl.position : dog.transform.position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(bowlUseVolume);
        source.spatialBlend = 0f;
        source.loop = true;
        source.playOnAwake = false;
        source.Play();

        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

        Destroy(audioObject);
    }

    private Camera ResolveRoomCamera()
    {
        if (roomCamera != null)
        {
            return roomCamera;
        }

        roomCamera = Camera.main;
        return roomCamera;
    }

    private DogCycleCamera ResolveDogCamera()
    {
        if (dogCamera != null)
        {
            return dogCamera;
        }

        Camera camera = ResolveRoomCamera();
        if (camera != null)
        {
            dogCamera = camera.GetComponent<DogCycleCamera>();
        }

        if (dogCamera == null)
        {
            dogCamera = FindFirstObjectByType<DogCycleCamera>();
        }

        return dogCamera;
    }

    private void AutoAssignEditorAudioClips()
    {
#if UNITY_EDITOR
        if (eatingClip == null)
        {
            eatingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorEatingAudioAssetPath);
        }

        if (drinkingClip == null)
        {
            drinkingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorDrinkingAudioAssetPath);
        }
#endif
    }

    private void CleanupActiveBowl()
    {
        if (activeBowl != null)
        {
            Destroy(activeBowl);
            activeBowl = null;
        }
    }

    private static float GetBottomOffset(GameObject instance)
    {
        if (instance == null)
        {
            return 0.05f;
        }

        Bounds bounds;
        if (!TryGetRendererBounds(instance, out bounds))
        {
            return 0.05f;
        }

        return Mathf.Max(0.02f, instance.transform.position.y - bounds.min.y);
    }

    private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
    {
        bounds = instance != null ? new Bounds(instance.transform.position, Vector3.zero) : new Bounds();
        if (instance == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
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

    private static bool ContainsIgnoreCase(string value, string fragment)
    {
        return !string.IsNullOrEmpty(value)
            && !string.IsNullOrEmpty(fragment)
            && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
