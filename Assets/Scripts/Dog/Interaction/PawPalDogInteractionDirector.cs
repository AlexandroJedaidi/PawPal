using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalDogInteractionDirector : MonoBehaviour
{
    private const float CameraApproachDistance = 0.8f;
    private const float CameraApproachSampleRadius = 1.6f;
    private const float CameraApproachTimeout = 5.5f;
    private const float CameraApproachTimeoutPerMeter = 2.35f;
    private const float CameraApproachTimeoutPadding = 2.8f;
    private const float CameraApproachTimeoutMax = 12f;
    private const float CameraFaceDuration = 0.45f;
    private const float ArrivalBarkDuration = 0.35f;
    private const float CameraApproachSideOffset = 0.6f;
    private const float CameraApproachWideSideOffset = 1.05f;
    private const float CameraApproachToyPadding = 0.14f;
    private const float CameraApproachReachedDistance = 0.16f;
    private const float CameraApproachAlternativePointDistance = 0.35f;
    private const float BlockerClearanceRadius = 0.72f;
    private const float BlockerMoveAwayDistance = 1.15f;
    private const float BlockerMoveTimeout = 2.8f;
    private const float LargeToySuspensionRadius = 0.85f;
    private static int debugSessionCounter;

    private Coroutine activeRoutine;
    private DogRoomAgent activeDog;
    private string activeDebugContext;
    private readonly List<DogRoomAgent> pausedBlockers = new List<DogRoomAgent>();
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

        ResumePausedBlockers();
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
            dog.BeginTemporaryNavigationAnchor();
            try
            {
                yield return ClearBlockingDogs(dog, approachPoint);
            }
            finally
            {
                dog.EndTemporaryNavigationAnchor();
            }

            dog.PauseForSocial(false);
            yield return StartCoroutine(ApproachDogToCamera(dog, camera, approachPoint));
            ResumePausedBlockers();
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
            ResumePausedBlockers();
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
            dog.BeginTemporaryNavigationAnchor();
            try
            {
                yield return ClearBlockingDogs(dog, approachPoint);
            }
            finally
            {
                dog.EndTemporaryNavigationAnchor();
            }

            dog.PauseForSocial(false);
        }
    }

    private IEnumerator ClearBlockingDogs(DogRoomAgent activeDog, Vector3 approachPoint)
    {
        ResumePausedBlockers();

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        if (dogs == null || dogs.Length == 0 || activeDog == null)
        {
            yield break;
        }

        List<Coroutine> moveRoutines = new List<Coroutine>();
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent otherDog = dogs[i];
            if (!IsDogBlockingInteractionApproach(otherDog, activeDog, approachPoint))
            {
                continue;
            }

            Vector3 moveAwayPoint;
            if (!TryResolveBlockerMoveAwayPoint(otherDog, activeDog, approachPoint, out moveAwayPoint))
            {
                continue;
            }

            Debug.Log("[DogMoveDebug] " + activeDog.name + " | " + activeDebugContext + " | BLOCKER " + otherDog.name + " moveAway=" + moveAwayPoint.ToString("F3"), activeDog);
            otherDog.PrepareForPlayerInteraction(false);
            otherDog.PauseForSocial();
            pausedBlockers.Add(otherDog);
            moveRoutines.Add(StartCoroutine(otherDog.MoveNearSocial(
                moveAwayPoint,
                BlockerMoveTimeout,
                DogMovementPace.Trot,
                CameraApproachReachedDistance)));
        }

        for (int i = 0; i < moveRoutines.Count; i++)
        {
            yield return moveRoutines[i];
        }
    }

    private void ResumePausedBlockers()
    {
        for (int i = 0; i < pausedBlockers.Count; i++)
        {
            DogRoomAgent blocker = pausedBlockers[i];
            if (blocker != null && blocker.isActiveAndEnabled)
            {
                blocker.StartRoaming();
            }
        }

        pausedBlockers.Clear();
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

    private static bool IsDogBlockingInteractionApproach(DogRoomAgent otherDog, DogRoomAgent activeDog, Vector3 approachPoint)
    {
        if (otherDog == null
            || otherDog == activeDog
            || !otherDog.isActiveAndEnabled)
        {
            return false;
        }

        Vector3 otherPosition = otherDog.transform.position;
        otherPosition.y = 0f;
        Vector3 activePosition = activeDog.transform.position;
        activePosition.y = 0f;
        Vector3 flatApproach = approachPoint;
        flatApproach.y = 0f;

        float clearance = Mathf.Max(0.2f, BlockerClearanceRadius);
        float sqrClearance = clearance * clearance;
        if ((otherPosition - activePosition).sqrMagnitude <= sqrClearance * 0.7f
            || (otherPosition - flatApproach).sqrMagnitude <= sqrClearance)
        {
            return true;
        }

        return DistanceToSegment(otherPosition, activePosition, flatApproach) <= clearance * 0.7f;
    }

    private static bool TryResolveBlockerMoveAwayPoint(DogRoomAgent otherDog, DogRoomAgent activeDog, Vector3 approachPoint, out Vector3 point)
    {
        point = otherDog != null ? otherDog.transform.position : Vector3.zero;
        if (otherDog == null || activeDog == null)
        {
            return false;
        }

        Vector3 awayDirection = otherDog.transform.position - approachPoint;
        awayDirection.y = 0f;
        if (awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = otherDog.transform.position - activeDog.transform.position;
            awayDirection.y = 0f;
        }

        if (awayDirection.sqrMagnitude < 0.001f)
        {
            awayDirection = Vector3.right;
        }

        awayDirection.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, awayDirection).normalized;
        float[] lateralOffsets = { 0f, 0.4f, -0.4f, 0.72f, -0.72f };

        for (int i = 0; i < lateralOffsets.Length; i++)
        {
            Vector3 candidate = otherDog.transform.position
                + awayDirection * BlockerMoveAwayDistance
                + lateral * lateralOffsets[i];
            candidate.y = otherDog.transform.position.y;
            Vector3 safePoint;
            if (otherDog.TryGetRoomSafePoint(candidate, 1.2f, out safePoint))
            {
                point = safePoint;
                return true;
            }
        }

        return false;
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
        if (!dog.TryGetRoomSafePoint(dog.transform.position, CameraApproachSampleRadius, out safePoint))
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
        float[] sideOffsets = { 0f, -CameraApproachSideOffset, CameraApproachSideOffset, -CameraApproachWideSideOffset, CameraApproachWideSideOffset };
        float[] forwardDistances = { CameraApproachDistance, Mathf.Max(0.55f, CameraApproachDistance - 0.16f), CameraApproachDistance + 0.18f };

        for (int distanceIndex = 0; distanceIndex < forwardDistances.Length; distanceIndex++)
        {
            for (int sideIndex = 0; sideIndex < sideOffsets.Length; sideIndex++)
            {
                Vector3 candidate = camera.transform.position
                    + forward.normalized * forwardDistances[distanceIndex]
                    + right * sideOffsets[sideIndex];
                candidate.y = dog.transform.position.y;
                Vector3 safePoint;
                if (!dog.TryGetRoomSafePoint(candidate, CameraApproachSampleRadius, out safePoint))
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

                if (dog.IsToyBlockingPathTo(safePoint, CameraApproachToyPadding))
                {
                    continue;
                }

                float pathDistance;
                float pathScore = dog.TryEstimateRoomPathDistance(safePoint, out pathDistance)
                    ? pathDistance
                    : Vector3.Distance(dog.transform.position, safePoint) * 1.5f;
                float score = pathScore
                    + Mathf.Abs(sideOffsets[sideIndex]) * 0.08f
                    + Mathf.Abs(forwardDistances[distanceIndex] - CameraApproachDistance) * 0.2f;
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
        if (dog.TryGetRoomSafePoint(fallbackCandidate, CameraApproachSampleRadius, out approachPoint)
            && (!excludedPoint.HasValue || Vector3.Distance(excludedPoint.Value, approachPoint) >= CameraApproachAlternativePointDistance)
            && !dog.IsToyBlockingPathTo(approachPoint, CameraApproachToyPadding))
        {
            return true;
        }

        approachPoint = fallbackPoint;
        return fallbackPoint != dog.transform.position;
    }
}
