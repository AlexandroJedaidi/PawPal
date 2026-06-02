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
    private PawPalRoomPetHandle activePet;
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

        if (resumeDog && activePet != null && activePet.IsValid)
        {
            activePet.StartRoaming();
        }

        activePet = null;
    }

    public bool TryWhistle(DogRoomAgent dog, Camera camera, out string failureReason)
    {
        return TryWhistle(dog != null ? new PawPalRoomPetHandle(dog) : null, camera, out failureReason);
    }

    public bool TryWhistle(PawPalRoomPetHandle pet, Camera camera, out string failureReason)
    {
        CancelActiveCommand(true);

        if (pet != null && pet.IsValid)
        {
            pet.WakeForPlayerInteraction();
            pet.PrepareForPlayerInteraction(true);
        }

        if (!CanStartPhotoCommand(pet, false, false, out failureReason))
        {
            return false;
        }

        StartPhotoRoutine(WhistleRoutine(pet, camera));
        failureReason = string.Empty;
        return true;
    }

    public bool TryPose(DogRoomAgent dog, Camera camera, out string failureReason)
    {
        return TryPose(dog != null ? new PawPalRoomPetHandle(dog) : null, camera, out failureReason);
    }

    public bool TryPose(PawPalRoomPetHandle pet, Camera camera, out string failureReason)
    {
        CancelActiveCommand(true);
        PreparePetForPhotoCommand(pet);

        if (!CanStartPhotoCommand(pet, true, true, out failureReason))
        {
            return false;
        }

        int poseIndex = nextPoseIndex;
        nextPoseIndex = (nextPoseIndex + 1) % 3;
        StartPhotoRoutine(PoseRoutine(pet, camera, poseIndex));
        failureReason = string.Empty;
        return true;
    }

    public bool TryPose(DogRoomAgent dog, Camera camera, PawPalTrickId trickId, out string failureReason)
    {
        return TryPose(dog != null ? new PawPalRoomPetHandle(dog) : null, camera, trickId, out failureReason);
    }

    public bool TryPose(PawPalRoomPetHandle pet, Camera camera, PawPalTrickId trickId, out string failureReason)
    {
        CancelActiveCommand(true);
        PreparePetForPhotoCommand(pet);

        if (!CanStartPhotoCommand(pet, true, true, out failureReason))
        {
            return false;
        }

        StartPhotoRoutine(PoseRoutine(pet, camera, trickId));
        failureReason = string.Empty;
        return true;
    }

    public bool TryPose(DogRoomAgent dog, Camera camera, PawPalPhotoPoseId poseId, out string failureReason)
    {
        return TryPose(dog != null ? new PawPalRoomPetHandle(dog) : null, camera, poseId, out failureReason);
    }

    public bool TryPose(PawPalRoomPetHandle pet, Camera camera, PawPalPhotoPoseId poseId, out string failureReason)
    {
        CancelActiveCommand(true);
        PreparePetForPhotoCommand(pet);

        if (!CanStartPhotoCommand(pet, false, true, out failureReason))
        {
            return false;
        }

        if (pet == null || !pet.CanPlayPhotoPose(poseId))
        {
            failureReason = "That pose is not available for this pet.";
            return false;
        }

        StartPhotoRoutine(PoseRoutine(pet, camera, poseId));
        failureReason = string.Empty;
        return true;
    }

    private void StartPhotoRoutine(IEnumerator routine)
    {
        activeRoutine = StartCoroutine(WrapRoutine(routine));
    }

    private static void PreparePetForPhotoCommand(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid)
        {
            return;
        }

        pet.WakeForPlayerInteraction();
        pet.PrepareForPlayerInteraction(true);
    }

    private IEnumerator WrapRoutine(IEnumerator routine)
    {
        yield return routine;
        if (activePet != null && activePet.IsValid)
        {
            activePet.StartRoaming();
        }

        activePet = null;
        activeRoutine = null;
    }

    private IEnumerator WhistleRoutine(PawPalRoomPetHandle pet, Camera camera)
    {
        activePet = pet;
        pet.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(pet, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(WhistleHoldDuration);
    }

    private IEnumerator PoseRoutine(PawPalRoomPetHandle pet, Camera camera, int poseIndex)
    {
        activePet = pet;
        pet.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(pet, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(PhotoPoseSettleDuration);

        switch (poseIndex)
        {
            case 0:
                yield return StartCoroutine(pet.PlayPhotoPose(PawPalPhotoPoseId.Idle1, PhotoPoseDuration));
                break;
            case 1:
                yield return StartCoroutine(pet.PlayPhotoPose(PawPalPhotoPoseId.Idle3, PhotoPoseDuration));
                break;
            default:
                if (!pet.HasHeldToy)
                {
                    yield return StartCoroutine(pet.PlayBark(WhistleBarkDuration));
                }
                break;
        }

        yield return FaceCameraAndRequestAttention(pet, camera, Mathf.Max(1.6f, CameraAttentionDuration * 0.5f));
    }

    private IEnumerator PoseRoutine(PawPalRoomPetHandle pet, Camera camera, PawPalTrickId trickId)
    {
        activePet = pet;
        pet.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(pet, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(PhotoPoseSettleDuration);

        switch (trickId)
        {
            case PawPalTrickId.Lie:
                yield return StartCoroutine(pet.PlayPhotoPose(PawPalPhotoPoseId.LieLoop1, PhotoPoseDuration));
                break;
            default:
                PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trickId);
                yield return StartCoroutine(pet.PlayTrainingTrick(definition, true, camera));
                break;
        }

        yield return FaceCameraAndRequestAttention(pet, camera, Mathf.Max(1.6f, CameraAttentionDuration * 0.5f));
    }

    private IEnumerator PoseRoutine(PawPalRoomPetHandle pet, Camera camera, PawPalPhotoPoseId poseId)
    {
        activePet = pet;
        pet.PauseForSocial(false);
        yield return FaceCameraAndRequestAttention(pet, camera, CameraAttentionDuration);
        yield return new WaitForSeconds(PhotoPoseSettleDuration);
        yield return StartCoroutine(pet.PlayPhotoPose(poseId, PhotoPoseDuration));
        yield return FaceCameraAndRequestAttention(pet, camera, Mathf.Max(1.6f, CameraAttentionDuration * 0.5f));
    }

    private IEnumerator FaceCameraAndRequestAttention(PawPalRoomPetHandle pet, Camera camera, float attentionDuration)
    {
        if (pet == null || !pet.IsValid)
        {
            yield break;
        }

        if (camera != null)
        {
            yield return StartCoroutine(pet.FaceTarget(camera.transform, CameraFaceDuration));
        }

        RequestCameraAttention(pet, attentionDuration);
    }

    private static void RequestCameraAttention(PawPalRoomPetHandle pet, float duration)
    {
        if (pet == null || !pet.IsValid)
        {
            return;
        }

        DogCameraAttention attention = pet.RootTransform != null ? pet.RootTransform.GetComponentInChildren<DogCameraAttention>(true) : null;
        if (attention != null)
        {
            attention.RequestCameraAttention(duration);
        }
    }

    private static bool CanStartPhotoCommand(PawPalRoomPetHandle pet, bool requireNavMeshSpot, bool requireFreeMouth, out string failureReason)
    {
        if (pet == null || !pet.IsValid || pet.RootTransform == null || !pet.RootTransform.gameObject.activeInHierarchy)
        {
            failureReason = "No active pet for photo mode.";
            return false;
        }

        if (pet.IsSleeping || pet.IsResting)
        {
            failureReason = "Your pet is resting right now.";
            return false;
        }

        if (requireFreeMouth && pet.HasHeldToy)
        {
            failureReason = "Your pet is holding a toy.";
            return false;
        }

        if (pet.IsBusy || pet.IsPlayingOneShotAnimation)
        {
            failureReason = "Your pet is busy right now.";
            return false;
        }

        if (requireNavMeshSpot)
        {
            Vector3 safePoint;
            if (!pet.TryGetRoomSafePoint(pet.RootTransform.position, CameraApproachSampleRadius, out safePoint))
            {
                failureReason = "Your pet needs a NavMesh spot.";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

}
