using System;
using System.Collections;
using UnityEngine;

public sealed class PawPalRoomPetHandle
{
    public PawPalRoomPetHandle(DogRoomAgent dogAgent)
    {
        DogAgent = dogAgent;
        CatAgent = null;
    }

    public PawPalRoomPetHandle(PawPalCatRoomAgent catAgent)
    {
        DogAgent = null;
        CatAgent = catAgent;
    }

    public DogRoomAgent DogAgent { get; }

    public PawPalCatRoomAgent CatAgent { get; }

    public bool IsValid
    {
        get { return DogAgent != null || CatAgent != null; }
    }

    public bool IsDog
    {
        get { return DogAgent != null; }
    }

    public IntroPetSpecies Species
    {
        get { return DogAgent != null ? IntroPetSpecies.Dog : IntroPetSpecies.Cat; }
    }

    public string RuntimePetId
    {
        get
        {
            if (DogAgent != null)
            {
                return DogAgent.DogId;
            }

            return CatAgent != null ? CatAgent.RuntimePetId : string.Empty;
        }
    }

    public string DisplayName
    {
        get
        {
            if (DogAgent != null)
            {
                return DogAgent.name;
            }

            return CatAgent != null ? CatAgent.DisplayName : "Pet";
        }
    }

    public Transform RootTransform
    {
        get { return DogAgent != null ? DogAgent.transform : CatAgent != null ? CatAgent.transform : null; }
    }

    public Transform FocusTransform
    {
        get
        {
            if (DogAgent != null)
            {
                return PawPalRoomPetRuntime.ResolveHeadTransform(DogAgent.transform);
            }

            return CatAgent != null ? CatAgent.FocusTransform : null;
        }
    }

    public Vector3 HomeCameraOffset
    {
        get
        {
            if (DogAgent != null)
            {
                return DogAgent.HomeCameraOffset;
            }

            if (CatAgent != null)
            {
                return CatAgent.HomeCameraOffset;
            }

            return new Vector3(0f, 0.85f, -2.6f);
        }
    }

    public bool IsBusy
    {
        get { return DogAgent != null ? DogAgent.IsBusy : CatAgent != null && CatAgent.IsBusy; }
    }

    public bool IsResting
    {
        get { return DogAgent != null ? DogAgent.IsResting : CatAgent != null && CatAgent.IsResting; }
    }

    public bool IsSleeping
    {
        get { return DogAgent != null ? DogAgent.IsSleeping : CatAgent != null && CatAgent.IsSleeping; }
    }

    public bool IsPlayingOneShotAnimation
    {
        get { return DogAgent != null ? DogAgent.IsPlayingOneShotAnimation : CatAgent != null && CatAgent.IsPlayingOneShotAnimation; }
    }

    public bool HasHeldToy
    {
        get { return DogAgent != null ? DogAgent.HasHeldToy : CatAgent != null && CatAgent.HasHeldToy; }
    }

    public bool CanJoinSocialInteraction
    {
        get
        {
            if (DogAgent != null)
            {
                return DogAgent.CanJoinSocialInteraction;
            }

            return CatAgent != null && CatAgent.CanJoinSocialInteraction;
        }
    }

    public bool WasLastTravelSuccessful
    {
        get { return DogAgent != null ? DogAgent.WasLastTravelSuccessful : CatAgent != null && CatAgent.WasLastTravelSuccessful; }
    }

    public void WakeForPlayerInteraction()
    {
        if (DogAgent != null)
        {
            DogAgent.WakeForPlayerInteraction();
        }
        else if (CatAgent != null)
        {
            CatAgent.WakeForPlayerInteraction();
        }
    }

    public bool PrepareForPlayerInteraction(bool preserveHeldToy)
    {
        if (DogAgent != null)
        {
            return DogAgent.PrepareForPlayerInteraction(preserveHeldToy);
        }

        return CatAgent != null && CatAgent.PrepareForPlayerInteraction(preserveHeldToy);
    }

    public void StartRoaming()
    {
        if (DogAgent != null)
        {
            DogAgent.StartRoaming();
        }
        else if (CatAgent != null)
        {
            CatAgent.StartRoaming();
        }
    }

    public void PauseForSocial(bool dropHeldToyImmediately)
    {
        if (DogAgent != null)
        {
            DogAgent.PauseForSocial(dropHeldToyImmediately);
        }
        else if (CatAgent != null)
        {
            CatAgent.PauseForSocial(dropHeldToyImmediately);
        }
    }

    public bool TryGetRoomSafePoint(Vector3 candidate, float radius, out Vector3 point)
    {
        if (DogAgent != null)
        {
            return DogAgent.TryGetRoomSafePoint(candidate, radius, out point);
        }

        if (CatAgent != null)
        {
            return CatAgent.TryGetRoomSafePoint(candidate, radius, out point);
        }

        point = candidate;
        return false;
    }

    public IEnumerator MoveNear(Vector3 worldPosition, float timeout, DogMovementPace pace)
    {
        if (DogAgent != null)
        {
            return DogAgent.MoveNear(worldPosition, timeout, pace);
        }

        return CatAgent != null ? CatAgent.MoveNear(worldPosition, timeout, pace) : EmptyRoutine();
    }

    public IEnumerator MoveNearPrecise(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        if (DogAgent != null)
        {
            return DogAgent.MoveNearPrecise(worldPosition, timeout, pace, reachedDistance);
        }

        return CatAgent != null ? CatAgent.MoveNearPrecise(worldPosition, timeout, pace, reachedDistance) : EmptyRoutine();
    }

    public IEnumerator MoveNearPlayerInteraction(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        if (DogAgent != null)
        {
            return DogAgent.MoveNearInteraction(worldPosition, timeout, pace, reachedDistance);
        }

        return CatAgent != null ? CatAgent.MoveNearPlayerInteraction(worldPosition, timeout, pace, reachedDistance) : EmptyRoutine();
    }

    public IEnumerator FaceTarget(Transform target, float duration)
    {
        if (DogAgent != null)
        {
            return DogAgent.FaceTarget(target, duration);
        }

        return CatAgent != null ? CatAgent.FaceTarget(target, duration) : EmptyRoutine();
    }

    public IEnumerator PlayBowlUse(bool useDrinkLoop, float loopDuration)
    {
        if (DogAgent != null)
        {
            return DogAgent.PlayBowlUse(useDrinkLoop, loopDuration);
        }

        return CatAgent != null ? CatAgent.PlayBowlUse(useDrinkLoop, loopDuration) : EmptyRoutine();
    }

    public IEnumerator PlayBark(float duration)
    {
        if (DogAgent != null)
        {
            return DogAgent.PlayBark(duration);
        }

        return CatAgent != null ? CatAgent.PlayBark(duration) : EmptyRoutine();
    }

    public IEnumerator PlaySocialVocal(float duration)
    {
        if (DogAgent != null)
        {
            return DogAgent.PlaySocialInteractionVocal(duration);
        }

        return CatAgent != null ? CatAgent.PlayBark(duration) : EmptyRoutine();
    }

    public bool CanPlayPhotoPose(PawPalPhotoPoseId poseId)
    {
        if (DogAgent != null)
        {
            return DogAgent.CanPlayPhotoPose(poseId);
        }

        return CatAgent != null && CatAgent.CanPlayPhotoPose(poseId);
    }

    public IEnumerator PlayPhotoPose(PawPalPhotoPoseId poseId, float duration)
    {
        if (DogAgent != null)
        {
            return DogAgent.PlayPhotoPose(poseId, duration);
        }

        return CatAgent != null ? CatAgent.PlayPhotoPose(poseId, duration) : EmptyRoutine();
    }

    public IEnumerator PlayTrainingTrick(PawPalTrickDefinition definition, bool useFallback, Camera camera)
    {
        if (DogAgent != null)
        {
            return DogAgent.PlayTrainingTrick(definition, useFallback);
        }

        return CatAgent != null ? CatAgent.PlayTrainingTrick(definition, camera) : EmptyRoutine();
    }

    public IEnumerator PlayInteractionTrainingTrick(PawPalTrickDefinition definition, bool useFallback, Camera camera)
    {
        if (DogAgent != null)
        {
            return DogAgent.PlayInteractionTrainingTrick(definition, useFallback);
        }

        return CatAgent != null ? CatAgent.PlayInteractionTrainingTrick(definition, camera) : EmptyRoutine();
    }

    public bool TryPlayPettingReaction()
    {
        if (DogAgent != null)
        {
            return DogAgent.TryPlayPettingReaction();
        }

        return CatAgent != null && CatAgent.TryPlayPettingReaction();
    }

    public bool TryPlayPreviewVocal()
    {
        if (DogAgent != null)
        {
            return DogAgent.TryPlayPreviewVocal();
        }

        return CatAgent != null && CatAgent.TryPlayPreviewVocal();
    }

    public void StartHeldToyTugAnimation()
    {
        if (DogAgent != null)
        {
            DogAgent.StartHeldToyTugAnimation();
        }
        else if (CatAgent != null)
        {
            CatAgent.StartHeldToyTugAnimation();
        }
    }

    public void StopHeldToyTugAnimation()
    {
        if (DogAgent != null)
        {
            DogAgent.StopHeldToyTugAnimation();
        }
        else if (CatAgent != null)
        {
            CatAgent.StopHeldToyTugAnimation();
        }
    }

    public bool TryGetHeldToyTransform(out Transform toy)
    {
        if (DogAgent != null)
        {
            return DogAgent.TryGetHeldToyTransform(out toy);
        }

        if (CatAgent != null)
        {
            return CatAgent.TryGetHeldToyTransform(out toy);
        }

        toy = null;
        return false;
    }

    public IEnumerator PutDownHeldToyForInteraction()
    {
        if (DogAgent != null)
        {
            return DogAgent.PutDownHeldToyForInteraction();
        }

        return CatAgent != null ? CatAgent.PutDownHeldToyForInteraction() : EmptyRoutine();
    }

    public void TryPlayImmediateInteractionPantingVocal()
    {
        if (DogAgent != null)
        {
            DogAgent.TryPlayImmediateInteractionPantingVocal();
        }
        else if (CatAgent != null)
        {
            CatAgent.TryPlayImmediateInteractionPantingVocal();
        }
    }

    public void TryPlayImmediateInteractionAnnoyedVocal()
    {
        if (DogAgent != null)
        {
            DogAgent.TryPlayImmediateInteractionAnnoyedVocal();
        }
        else if (CatAgent != null)
        {
            CatAgent.TryPlayImmediateInteractionAnnoyedVocal();
        }
    }

    public bool CanPerformTrainingAnimation()
    {
        if (DogAgent != null)
        {
            return DogAgent.CanPerformTrainingAnimation();
        }

        return CatAgent != null && CatAgent.CanPerformTrainingAnimation();
    }

    private static IEnumerator EmptyRoutine()
    {
        yield break;
    }
}

public static class PawPalRoomPetRuntime
{
    public static PawPalRoomPetHandle ResolveActivePet()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null && runtime.ActiveDog != null)
        {
            return ResolvePetById(runtime.ActiveDog.Id, runtime.ActivePetSpecies);
        }

        return ResolveAnyPet();
    }

    public static PawPalRoomPetHandle ResolvePetById(string petId, IntroPetSpecies preferredSpecies)
    {
        if (!string.IsNullOrWhiteSpace(petId))
        {
            if (preferredSpecies == IntroPetSpecies.Cat)
            {
                PawPalCatRoomAgent[] cats = UnityEngine.Object.FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
                for (int i = 0; i < cats.Length; i++)
                {
                    if (cats[i] != null && string.Equals(cats[i].RuntimePetId, petId, StringComparison.OrdinalIgnoreCase))
                    {
                        return new PawPalRoomPetHandle(cats[i]);
                    }
                }
            }

            DogRoomAgent[] dogs = UnityEngine.Object.FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < dogs.Length; i++)
            {
                DogRoomAgent dog = dogs[i];
                if (dog == null)
                {
                    continue;
                }

                if (dog.HasExplicitDogId && string.Equals(dog.DogId, petId, StringComparison.OrdinalIgnoreCase))
                {
                    return new PawPalRoomPetHandle(dog);
                }
            }

            if (preferredSpecies != IntroPetSpecies.Cat)
            {
                PawPalCatRoomAgent[] cats = UnityEngine.Object.FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
                for (int i = 0; i < cats.Length; i++)
                {
                    if (cats[i] != null && string.Equals(cats[i].RuntimePetId, petId, StringComparison.OrdinalIgnoreCase))
                    {
                        return new PawPalRoomPetHandle(cats[i]);
                    }
                }
            }
        }

        return ResolveAnyPet();
    }

    public static PawPalRoomPetHandle ResolveFromTransform(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        DogRoomAgent dog = target.GetComponentInParent<DogRoomAgent>();
        if (dog != null)
        {
            return new PawPalRoomPetHandle(dog);
        }

        PawPalCatRoomAgent cat = target.GetComponentInParent<PawPalCatRoomAgent>();
        if (cat != null)
        {
            return new PawPalRoomPetHandle(cat);
        }

        return null;
    }

    public static bool TrySelectRuntimePet(PawPalRoomPetHandle pet)
    {
        if (pet == null || !pet.IsValid)
        {
            return false;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || string.IsNullOrWhiteSpace(pet.RuntimePetId))
        {
            return false;
        }

        return runtime.SelectDogById(pet.RuntimePetId, false);
    }

    public static Transform ResolveHeadTransform(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        DogCameraAttention attention = root.GetComponentInChildren<DogCameraAttention>(true);
        if (attention != null)
        {
            Transform ownHead = attention.GetOwnHeadLookTarget();
            if (ownHead != null)
            {
                return ownHead;
            }
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate != null && string.Equals(candidate.name, "head", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.Contains("head") && !lowerName.Contains("aim") && !lowerName.Contains("target") && !lowerName.Contains("helper"))
            {
                return candidate;
            }
        }

        return root;
    }

    private static PawPalRoomPetHandle ResolveAnyPet()
    {
        PawPalCatRoomAgent cat = UnityEngine.Object.FindFirstObjectByType<PawPalCatRoomAgent>();
        if (cat != null)
        {
            return new PawPalRoomPetHandle(cat);
        }

        DogRoomAgent dog = UnityEngine.Object.FindFirstObjectByType<DogRoomAgent>();
        return dog != null ? new PawPalRoomPetHandle(dog) : null;
    }
}
