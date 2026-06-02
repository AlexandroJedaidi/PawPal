using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalDogInteractionDirector : MonoBehaviour
{
    private const float MinimumDogNavigationRadius = 0.14f;
    private const float MaximumDogNavigationRadius = 0.3f;
    private const float MinimumCameraApproachDistance = 0.64f;
    private const float MaximumCameraApproachDistance = 0.96f;
    private const float MinimumCameraApproachSampleRadius = 1.3f;
    private const float MaximumCameraApproachSampleRadius = 1.8f;
    private const float CameraApproachTimeout = 5.5f;
    private const float CameraApproachTimeoutPerMeter = 2.35f;
    private const float CameraApproachTimeoutPadding = 2.8f;
    private const float CameraApproachTimeoutMax = 12f;
    private const float CameraFaceDuration = 0.45f;
    private const float ArrivalBarkDuration = 0.35f;
    private const float MinimumCameraApproachSideOffset = 0.48f;
    private const float MaximumCameraApproachSideOffset = 0.68f;
    private const float MinimumCameraApproachWideSideOffset = 0.88f;
    private const float MaximumCameraApproachWideSideOffset = 1.12f;
    private const float CameraApproachToyPadding = 0.14f;
    private const float CameraApproachReachedDistance = 0.16f;
    private const float CameraApproachAlternativePointDistance = 0.35f;
    private const float MinimumCameraApproachDogClearanceRadius = 0.3f;
    private const float MaximumCameraApproachDogClearanceRadius = 0.46f;
    private const float CameraApproachBlockedCandidatePenalty = 6f;
    private const float CameraApproachBusyBlockerPenalty = 3.5f;
    private const float CameraApproachRestingBlockerPenalty = 12f;
    private const float MinimumCameraApproachExtraWideSideOffset = 1.04f;
    private const float MaximumCameraApproachExtraWideSideOffset = 1.3f;
    private const float BlockerYieldCorridorRadius = 0.78f;
    private const float BlockerYieldPointClearance = 0.42f;
    private const float BlockerYieldTimeout = 2.8f;
    private const float BlockerYieldSideOffset = 0.95f;
    private const float BlockerYieldWideSideOffset = 1.25f;
    private const float BlockerYieldForwardOffset = 0.32f;
    private const float LargeToySuspensionRadius = 0.85f;
    private static int debugSessionCounter;

    private Coroutine activeRoutine;
    private DogRoomAgent activeDog;
    private string activeDebugContext;
    private readonly List<DogRoomAgent> yieldedBlockers = new List<DogRoomAgent>();
    private readonly List<SuspendedLargeToyBlocker> suspendedLargeToyBlockers = new List<SuspendedLargeToyBlocker>();

    private struct SuspendedLargeToyBlocker
    {
        public PawPalToyRuntimeMetadata Metadata;
        public bool BlocksNavigation;
        public float BlockRadius;
        public UnityEngine.AI.NavMeshObstacle Obstacle;
        public bool ObstacleEnabled;
    }

    public bool IsRunning
    {
        get { return activeRoutine != null; }
    }

    public void Cancel(bool resumeDog)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        ResumeYieldedBlockers();
        RestoreSuspendedLargeToyBlockers();

        if (activeDog != null)
        {
            Debug.Log("[DogMoveDebug] " + activeDog.name + " | " + activeDebugContext + " | DIRECTOR_CANCEL resume=" + resumeDog, activeDog);
            activeDog.EndMovementDebugSession(activeDebugContext);
        }

        if (resumeDog && activeDog != null)
        {
            activeDog.StartRoaming();
        }

        activeDog = null;
        activeDebugContext = null;
    }

    public bool TryCallDogToInteraction(DogRoomAgent dog, Camera camera, out string failureReason)
    {
        Cancel(true);

        if (dog != null)
        {
            activeDebugContext = "interaction_whistle_" + (++debugSessionCounter);
            dog.BeginMovementDebugSession(activeDebugContext);
            Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_TRY_CALL", dog);
            dog.WakeForPlayerInteraction();
            dog.PrepareForPlayerInteraction(true);
        }

        if (!CanStartInteraction(dog, out failureReason))
        {
            if (dog != null)
            {
                Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_START_FAILED reason=" + failureReason, dog);
                dog.EndMovementDebugSession(activeDebugContext);
            }

            activeDebugContext = null;
            return false;
        }

        Vector3 approachPoint;
        if (!TryResolveCameraApproachPoint(dog, camera, out approachPoint))
        {
            failureReason = "Interaction mode needs a reachable spot.";
            if (dog != null)
            {
                Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_APPROACH_RESOLVE_FAILED", dog);
                dog.EndMovementDebugSession(activeDebugContext);
            }

            activeDebugContext = null;
            return false;
        }

        Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_APPROACH point=" + approachPoint.ToString("F3"), dog);
        activeRoutine = StartCoroutine(CallRoutine(dog, camera, approachPoint));
        failureReason = string.Empty;
        return true;
    }

    private IEnumerator CallRoutine(DogRoomAgent dog, Camera camera, Vector3 approachPoint)
    {
        activeDog = dog;
        try
        {
            dog.PauseForSocial(false);
            yield return StartCoroutine(ApproachDogToCamera(dog, camera, approachPoint));
            RestoreSuspendedLargeToyBlockers();
            if (!dog.WasLastTravelSuccessful)
            {
                Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_APPROACH_FAILED", dog);
                yield break;
            }

            Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_APPROACH_COMPLETE", dog);
            if (camera != null)
            {
                yield return StartCoroutine(dog.FaceTarget(camera.transform, CameraFaceDuration));
            }

            RequestCameraAttention(dog, PawPalDogInteractionTuning.GetInactivityTimeoutSeconds(GetPersonality(dog)));

            if (!dog.HasHeldToy)
            {
                yield return StartCoroutine(dog.PlayBark(ArrivalBarkDuration));
            }

            dog.PauseForSocial(false);
        }
        finally
        {
            ResumeYieldedBlockers();
            RestoreSuspendedLargeToyBlockers();
            if (dog != null)
            {
                dog.EndMovementDebugSession(activeDebugContext);
            }

            activeRoutine = null;
            activeDog = null;
            activeDebugContext = null;
        }
    }

    private IEnumerator ApproachDogToCamera(DogRoomAgent dog, Camera camera, Vector3 initialApproachPoint)
    {
        Vector3 approachPoint = initialApproachPoint;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            SuspendLargeToyBlockersOnApproach(dog, approachPoint);
            yield return YieldBlockingDogsForApproach(dog, approachPoint);

            float timeout = GetCameraApproachTimeout(dog, approachPoint);
            Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_APPROACH_ATTEMPT " + (attempt + 1) + " timeout=" + timeout.ToString("F2") + " point=" + approachPoint.ToString("F3"), dog);
            IEnumerator approachRoutine = dog.HasHeldToy
                ? dog.MoveNearCarryingHeldToy(approachPoint, timeout, DogMovementPace.Trot)
                : dog.MoveNearInteraction(approachPoint, timeout, DogMovementPace.Trot, CameraApproachReachedDistance);
            yield return StartCoroutine(approachRoutine);
            RestoreSuspendedLargeToyBlockers();
            if (dog.WasLastTravelSuccessful)
            {
                yield break;
            }

            if (camera == null
                || attempt >= 2
                || !TryResolveCameraApproachPoint(dog, camera, approachPoint, out approachPoint))
            {
                yield break;
            }

            Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | DIRECTOR_APPROACH_RETRY point=" + approachPoint.ToString("F3"), dog);
            dog.PauseForSocial(false);
        }
    }

    private IEnumerator YieldBlockingDogsForApproach(DogRoomAgent calledDog, Vector3 approachPoint)
    {
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (calledDog == null || dogs == null || dogs.Length == 0)
        {
            yield break;
        }

        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent blocker = dogs[i];
            if (!CanYieldBlockerForApproach(blocker, calledDog, approachPoint)
                || yieldedBlockers.Contains(blocker))
            {
                continue;
            }

            Vector3 yieldPoint;
            if (!TryResolveBlockerYieldPoint(blocker, calledDog, approachPoint, out yieldPoint))
            {
                continue;
            }

            yield return StartCoroutine(MoveBlockerToYieldPoint(blocker, yieldPoint));
        }
    }

    private IEnumerator MoveBlockerToYieldPoint(DogRoomAgent blocker, Vector3 yieldPoint)
    {
        if (blocker == null)
        {
            yield break;
        }

        if (!blocker.PrepareForPlayerInteraction(true))
        {
            yield break;
        }

        blocker.PauseForSocial(false);
        Debug.Log("[DogMoveDebug] " + blocker.name + " | " + activeDebugContext + " | BLOCKER_YIELD point=" + yieldPoint.ToString("F3"), blocker);
        yield return StartCoroutine(blocker.MoveNearSocial(
            yieldPoint,
            BlockerYieldTimeout,
            DogMovementPace.Trot,
            CameraApproachReachedDistance));

        if (blocker.WasLastTravelSuccessful)
        {
            blocker.PauseForSocial(false);
            yieldedBlockers.Add(blocker);
            yield break;
        }

        blocker.StartRoaming();
    }

    private void ResumeYieldedBlockers()
    {
        for (int i = 0; i < yieldedBlockers.Count; i++)
        {
            DogRoomAgent blocker = yieldedBlockers[i];
            if (blocker != null && blocker.isActiveAndEnabled)
            {
                blocker.StartRoaming();
            }
        }

        yieldedBlockers.Clear();
    }

    private void SuspendLargeToyBlockersOnApproach(DogRoomAgent dog, Vector3 approachPoint)
    {
        RestoreSuspendedLargeToyBlockers();
        if (dog == null)
        {
            return;
        }

        PawPalToyRuntimeMetadata[] blockers = FindObjectsByType<PawPalToyRuntimeMetadata>(FindObjectsSortMode.None);
        if (blockers == null || blockers.Length == 0)
        {
            return;
        }

        Vector3 start = dog.transform.position;
        start.y = 0f;
        Vector3 end = approachPoint;
        end.y = 0f;
        for (int i = 0; i < blockers.Length; i++)
        {
            PawPalToyRuntimeMetadata metadata = blockers[i];
            if (metadata == null || !metadata.isActiveAndEnabled || !metadata.BlocksDogNavigation)
            {
                continue;
            }

            if (metadata.InteractionMode != PawPalToyInteractionMode.PawHitRoll)
            {
                continue;
            }

            Vector3 center = metadata.DogNavigationBlockWorldCenter;
            center.y = 0f;
            float threshold = metadata.DogNavigationBlockRadius + LargeToySuspensionRadius;
            if (DistanceToSegment(center, start, end) > threshold)
            {
                continue;
            }

            SuspendedLargeToyBlocker suspended = new SuspendedLargeToyBlocker
            {
                Metadata = metadata,
                BlocksNavigation = metadata.BlocksDogNavigation,
                BlockRadius = metadata.DogNavigationBlockRadius,
                Obstacle = metadata.GetComponent<UnityEngine.AI.NavMeshObstacle>()
            };
            suspended.ObstacleEnabled = suspended.Obstacle != null && suspended.Obstacle.enabled;
            if (suspended.Obstacle != null)
            {
                suspended.Obstacle.enabled = false;
            }

            metadata.SetBlocksDogNavigation(false, suspended.BlockRadius);
            suspendedLargeToyBlockers.Add(suspended);
            if (dog != null)
            {
                Debug.Log("[DogMoveDebug] " + dog.name + " | " + activeDebugContext + " | SUSPEND_BIG_BALL " + metadata.name, dog);
            }
        }
    }

    private void RestoreSuspendedLargeToyBlockers()
    {
        for (int i = 0; i < suspendedLargeToyBlockers.Count; i++)
        {
            SuspendedLargeToyBlocker suspended = suspendedLargeToyBlockers[i];
            if (suspended.Metadata != null)
            {
                suspended.Metadata.SetBlocksDogNavigation(suspended.BlocksNavigation, suspended.BlockRadius);
            }

            if (suspended.Obstacle != null)
            {
                suspended.Obstacle.enabled = suspended.ObstacleEnabled;
            }
        }

        suspendedLargeToyBlockers.Clear();
    }

    private static float GetCameraApproachTimeout(DogRoomAgent dog, Vector3 approachPoint)
    {
        if (dog == null)
        {
            return CameraApproachTimeout;
        }

        Vector3 delta = approachPoint - dog.transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        return Mathf.Clamp(
            distance * CameraApproachTimeoutPerMeter + CameraApproachTimeoutPadding,
            CameraApproachTimeout,
            CameraApproachTimeoutMax);
    }

    private static bool CanYieldBlockerForApproach(DogRoomAgent blocker, DogRoomAgent calledDog, Vector3 approachPoint)
    {
        if (blocker == null
            || blocker == calledDog
            || calledDog == null
            || !blocker.isActiveAndEnabled
            || blocker.IsSleeping
            || blocker.IsResting)
        {
            return false;
        }

        if (blocker.IsBusy && !blocker.IsPlayingOneShotAnimation)
        {
            return false;
        }

        return IsDogBlockingInteractionApproach(blocker, calledDog, approachPoint);
    }

    private static bool IsDogBlockingInteractionApproach(DogRoomAgent blocker, DogRoomAgent calledDog, Vector3 approachPoint)
    {
        Vector3 blockerPosition = blocker.transform.position;
        blockerPosition.y = 0f;
        Vector3 start = calledDog.transform.position;
        start.y = 0f;
        Vector3 end = approachPoint;
        end.y = 0f;

        float clearance = Mathf.Max(0.2f, BlockerYieldCorridorRadius);
        float sqrClearance = clearance * clearance;
        if ((blockerPosition - start).sqrMagnitude <= sqrClearance
            || (blockerPosition - end).sqrMagnitude <= sqrClearance)
        {
            return true;
        }

        return DistanceToSegment(blockerPosition, start, end) <= clearance;
    }

    private static bool TryResolveBlockerYieldPoint(DogRoomAgent blocker, DogRoomAgent calledDog, Vector3 approachPoint, out Vector3 point)
    {
        point = blocker != null ? blocker.transform.position : Vector3.zero;
        if (blocker == null || calledDog == null)
        {
            return false;
        }

        Vector3 start = calledDog.transform.position;
        start.y = 0f;
        Vector3 end = approachPoint;
        end.y = 0f;
        Vector3 corridorDirection = end - start;
        corridorDirection.y = 0f;
        if (corridorDirection.sqrMagnitude < 0.001f)
        {
            corridorDirection = calledDog.transform.forward;
            corridorDirection.y = 0f;
        }

        if (corridorDirection.sqrMagnitude < 0.001f)
        {
            corridorDirection = Vector3.forward;
        }

        corridorDirection.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, corridorDirection).normalized;
        float[] sideOffsets = { BlockerYieldSideOffset, -BlockerYieldSideOffset, BlockerYieldWideSideOffset, -BlockerYieldWideSideOffset };
        float[] forwardOffsets = { 0f, BlockerYieldForwardOffset, -BlockerYieldForwardOffset };

        float bestScore = float.MaxValue;
        bool foundPoint = false;
        for (int sideIndex = 0; sideIndex < sideOffsets.Length; sideIndex++)
        {
            for (int forwardIndex = 0; forwardIndex < forwardOffsets.Length; forwardIndex++)
            {
                Vector3 candidate = blocker.transform.position
                    + lateral * sideOffsets[sideIndex]
                    + corridorDirection * forwardOffsets[forwardIndex];
                candidate.y = blocker.transform.position.y;

                Vector3 safePoint;
                if (!blocker.TryGetRoomSafePoint(candidate, 1.1f, out safePoint))
                {
                    continue;
                }

                if (DistanceToSegment(safePoint, start, end) <= BlockerYieldCorridorRadius + 0.08f)
                {
                    continue;
                }

                if (!IsPointClearOfOtherDogs(safePoint, blocker, calledDog, BlockerYieldPointClearance))
                {
                    continue;
                }

                float score = Vector3.Distance(blocker.transform.position, safePoint)
                    + Mathf.Abs(forwardOffsets[forwardIndex]) * 0.35f
                    + Mathf.Abs(sideOffsets[sideIndex]) * 0.1f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                point = safePoint;
                foundPoint = true;
            }
        }

        return foundPoint;
    }

    private static bool IsPointClearOfOtherDogs(Vector3 point, DogRoomAgent blocker, DogRoomAgent calledDog, float radius)
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
            if (otherDog == null || otherDog == blocker || otherDog == calledDog)
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


    private static float DistanceToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
        {
            return Vector3.Distance(point, segmentStart);
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / lengthSquared);
        Vector3 projection = segmentStart + segment * t;
        return Vector3.Distance(point, projection);
    }

    private static PawPalDogPersonality GetPersonality(DogRoomAgent dog)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null && runtime.ActiveDog != null)
        {
            return runtime.ActiveDog.Personality;
        }

        return PawPalDogPersonality.Clever;
    }

    private static void RequestCameraAttention(DogRoomAgent dog, float duration)
    {
        if (dog == null)
        {
            return;
        }

        DogCameraAttention attention = dog.GetComponentInChildren<DogCameraAttention>(true);
        if (attention != null)
        {
            attention.RequestCameraAttention(duration);
        }
    }

    private static bool CanStartInteraction(DogRoomAgent dog, out string failureReason)
    {
        if (dog == null || !dog.isActiveAndEnabled)
        {
            failureReason = "No active dog for interaction mode.";
            return false;
        }

        if (dog.IsSleeping || dog.IsResting)
        {
            failureReason = "Your dog is resting right now.";
            return false;
        }

        if (dog.IsBusy || dog.IsPlayingOneShotAnimation)
        {
            failureReason = "Your dog is busy right now.";
            return false;
        }

        Vector3 safePoint;
        if (!dog.TryGetRoomSafePoint(dog.transform.position, GetCameraApproachSampleRadius(dog), out safePoint))
        {
            failureReason = "Your dog needs a NavMesh spot.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryResolveCameraApproachPoint(DogRoomAgent dog, Camera camera, out Vector3 approachPoint)
    {
        return TryResolveCameraApproachPoint(dog, camera, null, out approachPoint);
    }

    private static bool TryResolveCameraApproachPoint(DogRoomAgent dog, Camera camera, Vector3? excludedPoint, out Vector3 approachPoint)
    {
        approachPoint = dog != null ? dog.transform.position : Vector3.zero;
        if (dog == null || camera == null)
        {
            return false;
        }

        float approachDistance = GetCameraApproachDistance(dog);
        float sampleRadius = GetCameraApproachSampleRadius(dog);
        float sideOffset = GetCameraApproachSideOffset(dog);
        float wideSideOffset = GetCameraApproachWideSideOffset(dog);
        float extraWideSideOffset = GetCameraApproachExtraWideSideOffset(dog);

        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = dog.transform.position - camera.transform.position;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = -dog.transform.forward;
        }

        Vector3 right = camera.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }
        else
        {
            right.Normalize();
        }

        Vector3 fallbackPoint = dog.transform.position;
        float bestScore = float.MaxValue;
        bool foundCandidate = false;
        float[] sideOffsets =
        {
            0f,
            -sideOffset,
            sideOffset,
            -wideSideOffset,
            wideSideOffset,
            -extraWideSideOffset,
            extraWideSideOffset
        };
        float[] forwardDistances =
        {
            approachDistance,
            Mathf.Max(MinimumCameraApproachDistance - 0.02f, approachDistance - 0.14f),
            approachDistance + 0.18f
        };

        for (int distanceIndex = 0; distanceIndex < forwardDistances.Length; distanceIndex++)
        {
            for (int sideIndex = 0; sideIndex < sideOffsets.Length; sideIndex++)
            {
                Vector3 candidate = camera.transform.position
                    + forward.normalized * forwardDistances[distanceIndex]
                    + right * sideOffsets[sideIndex];
                candidate.y = dog.transform.position.y;
                Vector3 safePoint;
                if (!dog.TryGetRoomSafePoint(candidate, sampleRadius, out safePoint))
                {
                    continue;
                }

                if (sideIndex == 0 && distanceIndex == 0)
                {
                    fallbackPoint = safePoint;
                }

                if (excludedPoint.HasValue
                    && Vector3.Distance(excludedPoint.Value, safePoint) < CameraApproachAlternativePointDistance)
                {
                    continue;
                }

                if (IsCameraApproachBlockedByImmovableDog(dog, safePoint))
                {
                    continue;
                }

                if (dog.IsToyBlockingPathTo(safePoint, CameraApproachToyPadding))
                {
                    continue;
                }

                float dogPenalty = GetCameraApproachDogPenalty(dog, safePoint);
                float pathDistance;
                float pathScore = dog.TryEstimateRoomPathDistance(safePoint, out pathDistance)
                    ? pathDistance
                    : Vector3.Distance(dog.transform.position, safePoint) * 1.5f;
                float score = pathScore
                    + dogPenalty
                    + Mathf.Abs(sideOffsets[sideIndex]) * 0.08f
                    + Mathf.Abs(forwardDistances[distanceIndex] - approachDistance) * 0.2f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                approachPoint = safePoint;
                foundCandidate = true;
            }
        }

        if (foundCandidate)
        {
            return true;
        }

        Vector3 fallbackCandidate = Vector3.Lerp(dog.transform.position, camera.transform.position, 0.35f);
        fallbackCandidate.y = dog.transform.position.y;
        if (dog.TryGetRoomSafePoint(fallbackCandidate, sampleRadius, out approachPoint)
            && (!excludedPoint.HasValue || Vector3.Distance(excludedPoint.Value, approachPoint) >= CameraApproachAlternativePointDistance)
            && !dog.IsToyBlockingPathTo(approachPoint, CameraApproachToyPadding)
            && !IsCameraApproachBlockedByImmovableDog(dog, approachPoint)
            && GetCameraApproachDogPenalty(dog, approachPoint) < CameraApproachRestingBlockerPenalty)
        {
            return true;
        }

        if (fallbackPoint != dog.transform.position
            && !dog.IsToyBlockingPathTo(fallbackPoint, CameraApproachToyPadding)
            && !IsCameraApproachBlockedByImmovableDog(dog, fallbackPoint))
        {
            approachPoint = fallbackPoint;
            return true;
        }

        approachPoint = dog.transform.position;
        return false;
    }

    private static float GetCameraApproachDogPenalty(DogRoomAgent dog, Vector3 approachPoint)
    {
        if (dog == null)
        {
            return 0f;
        }

        float clearanceRadius = GetCameraApproachDogClearanceRadius(dog);
        float penalty = 0f;
        if (!IsPointClearOfOtherDogs(approachPoint, null, dog, clearanceRadius))
        {
            penalty += CameraApproachBlockedCandidatePenalty;
        }

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null || dogs.Length == 0)
        {
            return penalty;
        }

        Vector3 start = dog.transform.position;
        start.y = 0f;
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent blocker = dogs[i];
            if (blocker == null || blocker == dog || !blocker.isActiveAndEnabled)
            {
                continue;
            }

            if (!IsDogBlockingInteractionApproach(blocker, dog, approachPoint))
            {
                continue;
            }

            Vector3 blockerPosition = blocker.transform.position;
            blockerPosition.y = 0f;
            float blockerDistance = Vector3.Distance(start, blockerPosition);
            float proximityPenalty = Mathf.Clamp01(1f - (blockerDistance / 2.25f)) * 2f;

            if (blocker.IsSleeping || blocker.IsResting)
            {
                penalty += CameraApproachRestingBlockerPenalty + proximityPenalty;
                continue;
            }

            float blockerPenalty = CameraApproachBlockedCandidatePenalty + proximityPenalty;
            if (blocker.IsBusy && !blocker.IsPlayingOneShotAnimation)
            {
                blockerPenalty += CameraApproachBusyBlockerPenalty;
            }

            penalty += blockerPenalty;
        }

        return penalty;
    }

    private static bool IsCameraApproachBlockedByImmovableDog(DogRoomAgent dog, Vector3 approachPoint)
    {
        if (dog == null)
        {
            return false;
        }

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null || dogs.Length == 0)
        {
            return false;
        }

        float closestDistance = float.MaxValue;
        Vector3 start = dog.transform.position;
        start.y = 0f;
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent candidate = dogs[i];
            if (candidate == null
                || candidate == dog
                || !candidate.isActiveAndEnabled
                || (!candidate.IsSleeping && !candidate.IsResting))
            {
                continue;
            }

            if (!IsDogBlockingInteractionApproach(candidate, dog, approachPoint))
            {
                continue;
            }

            Vector3 candidatePosition = candidate.transform.position;
            candidatePosition.y = 0f;
            float distance = Vector3.Distance(start, candidatePosition);
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
        }

        return closestDistance < float.MaxValue;
    }

    private static float GetCameraApproachDistance(DogRoomAgent dog)
    {
        return GetCameraApproachDistanceForFootprintRadius(dog != null ? dog.GetInteractionNavigationFootprintRadius() : 0.2f);
    }

    private static float GetCameraApproachDistanceForFootprintRadius(float dogFootprintRadius)
    {
        return Mathf.Lerp(MinimumCameraApproachDistance, MaximumCameraApproachDistance, GetInteractionSize01ForFootprintRadius(dogFootprintRadius));
    }

    private static float GetCameraApproachSampleRadius(DogRoomAgent dog)
    {
        return Mathf.Lerp(MinimumCameraApproachSampleRadius, MaximumCameraApproachSampleRadius, GetInteractionSize01ForFootprintRadius(dog != null ? dog.GetInteractionNavigationFootprintRadius() : 0.2f));
    }

    private static float GetCameraApproachSideOffset(DogRoomAgent dog)
    {
        return Mathf.Lerp(MinimumCameraApproachSideOffset, MaximumCameraApproachSideOffset, GetInteractionSize01ForFootprintRadius(dog != null ? dog.GetInteractionNavigationFootprintRadius() : 0.2f));
    }

    private static float GetCameraApproachWideSideOffset(DogRoomAgent dog)
    {
        return Mathf.Lerp(MinimumCameraApproachWideSideOffset, MaximumCameraApproachWideSideOffset, GetInteractionSize01ForFootprintRadius(dog != null ? dog.GetInteractionNavigationFootprintRadius() : 0.2f));
    }

    private static float GetCameraApproachExtraWideSideOffset(DogRoomAgent dog)
    {
        return Mathf.Lerp(MinimumCameraApproachExtraWideSideOffset, MaximumCameraApproachExtraWideSideOffset, GetInteractionSize01ForFootprintRadius(dog != null ? dog.GetInteractionNavigationFootprintRadius() : 0.2f));
    }

    private static float GetCameraApproachDogClearanceRadius(DogRoomAgent dog)
    {
        return Mathf.Lerp(MinimumCameraApproachDogClearanceRadius, MaximumCameraApproachDogClearanceRadius, GetInteractionSize01ForFootprintRadius(dog != null ? dog.GetInteractionNavigationFootprintRadius() : 0.2f));
    }

    private static float GetInteractionSize01ForFootprintRadius(float dogFootprintRadius)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(MinimumDogNavigationRadius, MaximumDogNavigationRadius, dogFootprintRadius));
    }
}
