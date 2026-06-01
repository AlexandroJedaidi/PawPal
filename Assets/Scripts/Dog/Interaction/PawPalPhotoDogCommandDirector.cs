using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalPhotoDogCommandDirector : MonoBehaviour
{
    private const float CameraApproachSampleRadius = 1.6f;
    private const float CameraFaceDuration = 0.45f;
    private const float CameraAttentionDuration = 4.5f;
    private const float PhotoPoseDuration = 3.2f;
    private const float PhotoPoseSettleDuration = 0.25f;
    private const float WhistleHoldDuration = 1.1f;
    private const float WhistleBarkDuration = 0.45f;

    private Coroutine activeRoutine;
    private DogRoomAgent activeDog;
    private int nextPoseIndex;

    public bool IsRunning
    {
        get { return activeRoutine != null; }
    }

    public void CancelActiveCommand(bool resumeDog)
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

    public bool TryWhistle(DogRoomAgent dog, Camera camera, out string failureReason)
    {
        CancelActiveCommand(true);

        if (dog != null)
        {
            dog.WakeForPlayerInteraction();
            dog.PrepareForPlayerInteraction(true);
        }

        if (!CanStartPhotoCommand(dog, false, false, out failureReason))
        {
            return false;
        }

        StartPhotoRoutine(WhistleRoutine(dog, camera));
        failureReason = string.Empty;
        return true;
    }

    public bool TryPose(DogRoomAgent dog, Camera camera, out string failureReason)
    {
        CancelActiveCommand(true);
        PrepareDogForPhotoCommand(dog);

        if (!CanStartPhotoCommand(dog, true, true, out failureReason))
        {
            return false;
        }

        int poseIndex = nextPoseIndex;
        nextPoseIndex = (nextPoseIndex + 1) % 3;
        StartPhotoRoutine(PoseRoutine(dog, camera, poseIndex));
        failureReason = string.Empty;
        return true;
    }

    public bool TryPose(DogRoomAgent dog, Camera camera, PawPalTrickId trickId, out string failureReason)
    {
        CancelActiveCommand(true);
        PrepareDogForPhotoCommand(dog);

        if (!CanStartPhotoCommand(dog, true, true, out failureReason))
        {
            return false;
        }

        StartPhotoRoutine(PoseRoutine(dog, camera, trickId));
        failureReason = string.Empty;
        return true;
    }

    public bool TryPose(DogRoomAgent dog, Camera camera, PawPalPhotoPoseId poseId, out string failureReason)
    {
        CancelActiveCommand(true);
        PrepareDogForPhotoCommand(dog);

        if (!CanStartPhotoCommand(dog, false, true, out failureReason))
        {
            return false;
        }

        if (!dog.CanPlayPhotoPose(poseId))
        {
            failureReason = "That pose is not available for this dog.";
            return false;
        }

        StartPhotoRoutine(PoseRoutine(dog, camera, poseId));
        failureReason = string.Empty;
        return true;
    }

    private void StartPhotoRoutine(IEnumerator routine)
    {
        activeRoutine = StartCoroutine(WrapRoutine(routine));
    }

    private static void PrepareDogForPhotoCommand(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return;
        }

        dog.WakeForPlayerInteraction();
        dog.PrepareForPlayerInteraction(true);
    }

    private IEnumerator WrapRoutine(IEnumerator routine)
    {
        yield return routine;
        if (activeDog != null)
        {
            activeDog.StartRoaming();
        }

        activeDog = null;
        activeRoutine = null;
    }

    private IEnumerator WhistleRoutine(DogRoomAgent dog, Camera camera)
    {
        activeDog = dog;
        dog.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(dog, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(WhistleHoldDuration);
    }

    private IEnumerator PoseRoutine(DogRoomAgent dog, Camera camera, int poseIndex)
    {
        activeDog = dog;
        dog.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(dog, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(PhotoPoseSettleDuration);

        switch (poseIndex)
        {
            case 0:
                yield return StartCoroutine(dog.PlaySit(PhotoPoseDuration));
                break;
            case 1:
                yield return StartCoroutine(dog.PlayTailWag());
                break;
            default:
                if (!dog.HasHeldToy)
                {
                    yield return StartCoroutine(dog.PlayBark(WhistleBarkDuration));
                }
                break;
        }

        yield return FaceCameraAndRequestAttention(dog, camera, Mathf.Max(1.6f, CameraAttentionDuration * 0.5f));
    }

    private IEnumerator PoseRoutine(DogRoomAgent dog, Camera camera, PawPalTrickId trickId)
    {
        activeDog = dog;
        dog.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(dog, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(PhotoPoseSettleDuration);

        switch (trickId)
        {
            case PawPalTrickId.Lie:
                yield return StartCoroutine(dog.PlayChillRest(PhotoPoseDuration));
                break;
            default:
                yield return StartCoroutine(dog.PlaySit(PhotoPoseDuration));
                break;
        }

        yield return FaceCameraAndRequestAttention(dog, camera, Mathf.Max(1.6f, CameraAttentionDuration * 0.5f));
    }

    private IEnumerator PoseRoutine(DogRoomAgent dog, Camera camera, PawPalPhotoPoseId poseId)
    {
        activeDog = dog;
        dog.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(dog, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(PhotoPoseSettleDuration);
        yield return StartCoroutine(dog.PlayPhotoPose(poseId, PhotoPoseDuration));
        yield return FaceCameraAndRequestAttention(dog, camera, Mathf.Max(1.6f, CameraAttentionDuration * 0.5f));
    }

    private IEnumerator FaceCameraAndRequestAttention(DogRoomAgent dog, Camera camera, float attentionDuration)
    {
        if (dog == null)
        {
            yield break;
        }

        if (camera != null)
        {
            yield return StartCoroutine(dog.FaceTarget(camera.transform, CameraFaceDuration));
        }

        RequestCameraAttention(dog, attentionDuration);
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

    private static bool CanStartPhotoCommand(DogRoomAgent dog, bool requireNavMeshSpot, bool requireFreeMouth, out string failureReason)
    {
        if (dog == null || !dog.isActiveAndEnabled)
        {
            failureReason = "No active dog for photo mode.";
            return false;
        }

        if (dog.IsSleeping || dog.IsResting)
        {
            failureReason = "Your dog is resting right now.";
            return false;
        }

        if (requireFreeMouth && dog.HasHeldToy)
        {
            failureReason = "Your dog is holding a toy.";
            return false;
        }

        if (dog.IsBusy || dog.IsPlayingOneShotAnimation)
        {
            failureReason = "Your dog is busy right now.";
            return false;
        }

        if (requireNavMeshSpot)
        {
            Vector3 safePoint;
            if (!dog.TryGetRoomSafePoint(dog.transform.position, CameraApproachSampleRadius, out safePoint))
            {
                failureReason = "Your dog needs a NavMesh spot.";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

}
