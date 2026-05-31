using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalDogInteractionDirector : MonoBehaviour
{
    private const float CameraApproachDistance = 0.8f;
    private const float CameraApproachSampleRadius = 1.6f;
    private const float CameraApproachTimeout = 5.5f;
    private const float CameraFaceDuration = 0.45f;
    private const float ArrivalBarkDuration = 0.35f;
    private const float CameraApproachSideOffset = 0.6f;
    private const float CameraApproachWideSideOffset = 1.05f;
    private const float CameraApproachToyPadding = 0.14f;

    private Coroutine activeRoutine;
    private DogRoomAgent activeDog;

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

        if (resumeDog && activeDog != null)
        {
            activeDog.StartRoaming();
        }

        activeDog = null;
    }

    public bool TryCallDogToInteraction(DogRoomAgent dog, Camera camera, out string failureReason)
    {
        Cancel(true);

        if (dog != null)
        {
            dog.PrepareForPlayerInteraction(true);
        }

        if (!CanStartInteraction(dog, out failureReason))
        {
            return false;
        }

        Vector3 approachPoint;
        if (!TryResolveCameraApproachPoint(dog, camera, out approachPoint))
        {
            failureReason = "Interaction mode needs a reachable spot.";
            return false;
        }

        activeRoutine = StartCoroutine(CallRoutine(dog, camera, approachPoint));
        failureReason = string.Empty;
        return true;
    }

    private IEnumerator CallRoutine(DogRoomAgent dog, Camera camera, Vector3 approachPoint)
    {
        activeDog = dog;
        dog.PauseForSocial(false);
        IEnumerator approachRoutine = dog.HasHeldToy
            ? dog.MoveNearCarryingHeldToy(approachPoint, CameraApproachTimeout, DogMovementPace.Trot)
            : dog.MoveNear(approachPoint, CameraApproachTimeout, DogMovementPace.Trot);
        yield return StartCoroutine(approachRoutine);
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
        activeRoutine = null;
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

                if (!dog.IsToyBlockingPathTo(safePoint, CameraApproachToyPadding))
                {
                    approachPoint = safePoint;
                    return true;
                }
            }
        }

        Vector3 fallbackCandidate = Vector3.Lerp(dog.transform.position, camera.transform.position, 0.35f);
        fallbackCandidate.y = dog.transform.position.y;
        if (dog.TryGetRoomSafePoint(fallbackCandidate, CameraApproachSampleRadius, out approachPoint)
            && !dog.IsToyBlockingPathTo(approachPoint, CameraApproachToyPadding))
        {
            return true;
        }

        approachPoint = fallbackPoint;
        return fallbackPoint != dog.transform.position;
    }
}
