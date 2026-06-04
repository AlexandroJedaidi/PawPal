using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DogVoiceCommandDirector : MonoBehaviour
{
    private const float CameraApproachDistance = 1.15f;
    private const float CameraApproachSampleRadius = 1.6f;
    private const float CameraApproachTimeout = 5.5f;
    private const float CameraFaceDuration = 0.45f;
    private const float ArrivalBarkDuration = 0.45f;

    private Coroutine activeRoutine;

    public bool IsCommandRunning
    {
        get { return activeRoutine != null; }
    }

    public bool TryCallActiveDogToCamera()
    {
        PawPalRoomPetHandle pet = ResolveActivePet();
        if (!PreparePetForVoiceInteraction(pet) || !CanStartVoiceAnimation(pet))
        {
            return false;
        }

        StartVoiceRoutine(CallToCameraRoutine(pet));
        return true;
    }

    public bool TryCallDogToCamera(string dogId)
    {
        PawPalRoomPetHandle pet;
        if (!TryResolvePetTarget(dogId, out pet))
        {
            return false;
        }

        if (!PreparePetForVoiceInteraction(pet) || !CanStartVoiceAnimation(pet))
        {
            return false;
        }

        StartVoiceRoutine(CallToCameraRoutine(pet));
        return true;
    }

    public bool TryPerformTrick(PawPalVoiceTrick trick)
    {
        return TryPerformTrick(PawPalTrickId.Sit);
    }

    public bool TryPerformTrick(PawPalTrickId trick)
    {
        PawPalRoomPetHandle pet = ResolveActivePet();
        if (!PreparePetForVoiceInteraction(pet) || !CanStartVoiceAnimation(pet))
        {
            return false;
        }

        StartVoiceRoutine(PerformTrickRoutine(pet, trick));
        return true;
    }

    public bool TryPerformTrick(string dogId, PawPalTrickId trick)
    {
        PawPalRoomPetHandle pet;
        if (!TryResolvePetTarget(dogId, out pet))
        {
            return false;
        }

        if (!PreparePetForVoiceInteraction(pet) || !CanStartVoiceAnimation(pet))
        {
            return false;
        }

        StartVoiceRoutine(PerformTrickRoutine(pet, trick));
        return true;
    }

    private void StartVoiceRoutine(IEnumerator routine)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        activeRoutine = StartCoroutine(WrapRoutine(routine));
    }

    private IEnumerator WrapRoutine(IEnumerator routine)
    {
        yield return routine;
        activeRoutine = null;
    }

    private IEnumerator CallToCameraRoutine(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid)
        {
            yield break;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            yield break;
        }

        pet.PauseForSocial(false);

        Vector3 approachPoint;
        if (TryResolveCameraApproachPoint(pet, camera, out approachPoint))
        {
            yield return StartCoroutine(pet.MoveNear(approachPoint, CameraApproachTimeout, DogMovementPace.Run));
        }

        yield return StartCoroutine(pet.FaceTarget(camera.transform, CameraFaceDuration));

        if (!pet.HasHeldToy)
        {
            yield return StartCoroutine(pet.PlayBark(ArrivalBarkDuration));
        }

        pet.StartRoaming();
    }

    private IEnumerator PerformTrickRoutine(PawPalRoomPetHandle pet, PawPalVoiceTrick trick)
    {
        yield return PerformTrickRoutine(pet, PawPalTrickId.Sit);
    }

    private IEnumerator PerformTrickRoutine(PawPalRoomPetHandle pet, PawPalTrickId trick)
    {
        if (pet == null || !pet.IsValid)
        {
            yield break;
        }

        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trick);
        if (definition != null)
        {
            yield return StartCoroutine(pet.PlayTrainingTrick(definition, true, Camera.main));
        }
    }

    private static bool CanStartVoiceAnimation(PawPalRoomPetHandle pet)
    {
        return pet != null
            && pet.IsValid
            && !pet.IsBusy
            && !pet.IsPlayingOneShotAnimation;
    }

    private static bool PreparePetForVoiceInteraction(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid)
        {
            return false;
        }

        pet.WakeForPlayerInteraction();
        return pet.PrepareForPlayerInteraction(true);
    }

    private static bool TryResolveCameraApproachPoint(PawPalRoomPetHandle pet, Camera camera, out Vector3 approachPoint)
    {
        approachPoint = pet != null && pet.RootTransform != null ? pet.RootTransform.position : Vector3.zero;
        if (pet == null || !pet.IsValid || pet.RootTransform == null || camera == null)
        {
            return false;
        }

        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = pet.RootTransform.position - camera.transform.position;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = -pet.RootTransform.forward;
        }

        Vector3 candidate = camera.transform.position + forward.normalized * CameraApproachDistance;
        candidate.y = pet.RootTransform.position.y;
        if (pet.TryGetRoomSafePoint(candidate, CameraApproachSampleRadius, out approachPoint))
        {
            return true;
        }

        candidate = camera.transform.position;
        candidate.y = pet.RootTransform.position.y;
        return pet.TryGetRoomSafePoint(candidate, CameraApproachSampleRadius, out approachPoint);
    }

    private static bool TryResolvePetTarget(string dogId, out PawPalRoomPetHandle pet)
    {
        pet = PawPalRoomPetRuntime.ResolvePetById(dogId, PawPalGameRuntime.Instance != null ? PawPalGameRuntime.Instance.GetPetSpecies(dogId) : IntroPetSpecies.Dog);
        if (pet == null || !pet.IsValid)
        {
            return false;
        }

        PawPalRoomPetRuntime.TrySelectRuntimePet(pet);
        DogCycleCamera.TryForceFocusRuntimeActiveDogFromSelection();
        return true;
    }

    private static PawPalRoomPetHandle ResolveActivePet()
    {
        return PawPalRoomPetRuntime.ResolveActivePet();
    }
}
