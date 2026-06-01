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
        DogRoomAgent dog = ResolveActiveDog();
        if (!PrepareDogForVoiceInteraction(dog) || !CanStartVoiceAnimation(dog))
        {
            return false;
        }

        StartVoiceRoutine(CallToCameraRoutine(dog));
        return true;
    }

    public bool TryCallDogToCamera(string dogId)
    {
        DogRoomAgent dog;
        if (!TryResolveDogTarget(dogId, out dog))
        {
            return false;
        }

        if (!PrepareDogForVoiceInteraction(dog) || !CanStartVoiceAnimation(dog))
        {
            return false;
        }

        StartVoiceRoutine(CallToCameraRoutine(dog));
        return true;
    }

    public bool TryPerformTrick(PawPalVoiceTrick trick)
    {
        return TryPerformTrick(PawPalTrickId.Sit);
    }

    public bool TryPerformTrick(PawPalTrickId trick)
    {
        DogRoomAgent dog = ResolveActiveDog();
        if (!PrepareDogForVoiceInteraction(dog) || !CanStartVoiceAnimation(dog))
        {
            return false;
        }

        StartVoiceRoutine(PerformTrickRoutine(dog, trick));
        return true;
    }

    public bool TryPerformTrick(string dogId, PawPalTrickId trick)
    {
        DogRoomAgent dog;
        if (!TryResolveDogTarget(dogId, out dog))
        {
            return false;
        }

        if (!PrepareDogForVoiceInteraction(dog) || !CanStartVoiceAnimation(dog))
        {
            return false;
        }

        StartVoiceRoutine(PerformTrickRoutine(dog, trick));
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

    private IEnumerator CallToCameraRoutine(DogRoomAgent dog)
    {
        if (dog == null)
        {
            yield break;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            yield break;
        }

        dog.PauseForSocial();

        Vector3 approachPoint;
        if (TryResolveCameraApproachPoint(dog, camera, out approachPoint))
        {
            yield return StartCoroutine(dog.MoveNear(approachPoint, CameraApproachTimeout, DogMovementPace.Trot));
        }

        yield return StartCoroutine(dog.FaceTarget(camera.transform, CameraFaceDuration));

        if (!dog.HasHeldToy)
        {
            yield return StartCoroutine(dog.PlayBark(ArrivalBarkDuration));
        }

        dog.StartRoaming();
    }

    private IEnumerator PerformTrickRoutine(DogRoomAgent dog, PawPalVoiceTrick trick)
    {
        yield return PerformTrickRoutine(dog, PawPalTrickId.Sit);
    }

    private IEnumerator PerformTrickRoutine(DogRoomAgent dog, PawPalTrickId trick)
    {
        if (dog == null)
        {
            yield break;
        }

        PawPalTrickDefinition definition = PawPalTrickCatalog.GetDefinition(trick);
        if (definition != null)
        {
            yield return StartCoroutine(dog.PlayTrainingTrick(definition, true));
        }
    }

    private static bool CanStartVoiceAnimation(DogRoomAgent dog)
    {
        return dog != null
            && dog.isActiveAndEnabled
            && !dog.IsBusy
            && !dog.IsPlayingOneShotAnimation;
    }

    private static bool PrepareDogForVoiceInteraction(DogRoomAgent dog)
    {
        if (dog == null || !dog.isActiveAndEnabled)
        {
            return false;
        }

        dog.WakeForPlayerInteraction();
        return dog.PrepareForPlayerInteraction(true);
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

        Vector3 candidate = camera.transform.position + forward.normalized * CameraApproachDistance;
        candidate.y = dog.transform.position.y;
        if (dog.TryGetRoomSafePoint(candidate, CameraApproachSampleRadius, out approachPoint))
        {
            return true;
        }

        candidate = camera.transform.position;
        candidate.y = dog.transform.position.y;
        return dog.TryGetRoomSafePoint(candidate, CameraApproachSampleRadius, out approachPoint);
    }

    private static bool TryResolveDogTarget(string dogId, out DogRoomAgent dog)
    {
        dog = ResolveDogById(dogId);
        if (dog == null)
        {
            return false;
        }

        SelectRuntimeDog(dog);
        DogCycleCamera.TryForceFocusRuntimeActiveDogFromSelection();
        return true;
    }

    private static void SelectRuntimeDog(DogRoomAgent dog)
    {
        if (dog == null)
        {
            return;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        if (dog.HasExplicitDogId && !string.IsNullOrEmpty(dog.DogId))
        {
            runtime.SelectDogById(dog.DogId, false);
            return;
        }

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < dogs.Length; i++)
        {
            if (dogs[i] == dog)
            {
                runtime.SelectDogIndex(i, false);
                return;
            }
        }
    }

    private static DogRoomAgent ResolveDogById(string dogId)
    {
        if (string.IsNullOrWhiteSpace(dogId))
        {
            return ResolveActiveDog();
        }

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent candidate = dogs[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.HasExplicitDogId && string.Equals(candidate.DogId, dogId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            for (int i = 0; i < runtime.Dogs.Count && i < dogs.Length; i++)
            {
                PawPalDogState dogState = runtime.Dogs[i];
                if (dogState != null
                    && string.Equals(dogState.Id, dogId, StringComparison.OrdinalIgnoreCase)
                    && dogs[i] != null)
                {
                    return dogs[i];
                }
            }
        }

        return null;
    }

    private static DogRoomAgent ResolveActiveDog()
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
}
