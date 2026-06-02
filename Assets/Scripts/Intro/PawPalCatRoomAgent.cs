using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class PawPalCatRoomAgent : MonoBehaviour
{
    private const float DefaultArrivalDistance = 0.18f;

    [Header("Room Roaming")]
    [SerializeField] private float roamRadius = 2.8f;
    [SerializeField] private float minRoamWait = 1.2f;
    [SerializeField] private float maxRoamWait = 2.8f;
    [SerializeField] private float sampleRadius = 1.6f;
    [SerializeField] private float destinationReachedDistance = 0.18f;
    [SerializeField] private bool restrictToRoomBounds = true;
    [SerializeField] private Vector3 roomBoundsCenter = Vector3.zero;
    [SerializeField] private Vector2 roomBoundsSize = new Vector2(6.4f, 5.6f);
    [SerializeField] private float roomBoundsPadding = 0.25f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 0.72f;
    [SerializeField] private float trotSpeed = 1.08f;
    [SerializeField] private float runSpeed = 1.4f;
    [SerializeField] private float rotationSpeed = 7.5f;
    [SerializeField] private float stoppingDistance = 0.05f;
    [SerializeField] private float preInteractionPause = 0.18f;

    [Header("Ambient")]
    [SerializeField] private float toyInterestChance = 0.55f;
    [SerializeField] private float socialInterestChance = 0.24f;
    [SerializeField] private float toyPlayDurationMin = 1.5f;
    [SerializeField] private float toyPlayDurationMax = 3.2f;
    [SerializeField] private float socialCooldown = 3.2f;

    private Animator animator;
    private NavMeshAgent agent;
    private PetAnimationSet animationSet;
    private SelectedPetSessionData sessionData;
    private Transform headTransform;
    private Transform activeToyTarget;
    private PawPalRoomPetHandle socialPartner;
    private Vector3 homePosition;
    private float waitUntil;
    private float nextSocialAt;
    private float nextToyAttemptAt;
    private float currentMoveSpeed;
    private Coroutine activeActionRoutine;
    private bool socialPaused;
    private bool isResting;
    private bool isSleeping;
    private bool isPlayingOneShotAnimation;
    private bool trainingBusy;
    private bool wasLastTravelSuccessful = true;
    private Transform interactionLookTarget;
    private float interactionLookUntil;

    public string RuntimePetId
    {
        get { return sessionData != null ? sessionData.RuntimePetId : string.Empty; }
    }

    public string DisplayName
    {
        get { return sessionData != null ? sessionData.SafeName : name; }
    }

    public Transform FocusTransform
    {
        get { return headTransform != null ? headTransform : transform; }
    }

    public Vector3 HomeCameraOffset
    {
        get
        {
            return sessionData != null && sessionData.Definition != null
                ? sessionData.Definition.HomeCameraOffset
                : new Vector3(0f, 0.9f, -2.5f);
        }
    }

    public bool IsBusy
    {
        get { return activeActionRoutine != null || trainingBusy; }
    }

    public bool IsResting
    {
        get { return isResting; }
    }

    public bool IsSleeping
    {
        get { return isSleeping; }
    }

    public bool IsPlayingOneShotAnimation
    {
        get { return isPlayingOneShotAnimation; }
    }

    public bool HasHeldToy
    {
        get { return activeToyTarget != null; }
    }

    public bool WasLastTravelSuccessful
    {
        get { return wasLastTravelSuccessful; }
    }

    public void Initialize(SelectedPetSessionData data)
    {
        sessionData = data;
        if (sessionData != null)
        {
            name = "SelectedIntroCat_" + sessionData.SafeName;
        }

        InitializeSharedPresentation();
        SnapToNavMesh();
        StartRoaming();
    }

    public void InitializeIntroSelection(IntroPetRuntimeSelection selection)
    {
        if (selection == null)
        {
            return;
        }

        Initialize(selection.ToSessionData());
    }

    public void ConfigureRoomBounds(Bounds bounds)
    {
        restrictToRoomBounds = true;
        roomBoundsCenter = bounds.center;
        roomBoundsSize = new Vector2(bounds.size.x, bounds.size.z);
    }

    public void WakeForPlayerInteraction()
    {
        waitUntil = 0f;
        isSleeping = false;
        isResting = false;
    }

    public bool PrepareForPlayerInteraction(bool preserveHeldToy)
    {
        WakeForPlayerInteraction();
        PauseForSocial(!preserveHeldToy);
        if (!preserveHeldToy)
        {
            activeToyTarget = null;
        }

        return isActiveAndEnabled && agent != null && agent.isOnNavMesh;
    }

    public void StartRoaming()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
            activeActionRoutine = null;
        }

        socialPaused = false;
        trainingBusy = false;
        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = false;
        activeToyTarget = null;
        socialPartner = null;
        PickNextDestination(false);
    }

    public void PauseForSocial(bool dropHeldToyImmediately)
    {
        socialPaused = true;
        if (agent != null)
        {
            agent.ResetPath();
        }

        if (dropHeldToyImmediately)
        {
            activeToyTarget = null;
        }
    }

    public bool TryGetRoomSafePoint(Vector3 candidate, float radius, out Vector3 point)
    {
        candidate = ClampToRoom(candidate);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, Mathf.Max(radius, sampleRadius), NavMesh.AllAreas))
        {
            point = hit.position;
            return true;
        }

        point = candidate;
        return false;
    }

    public IEnumerator MoveNear(Vector3 worldPosition, float timeout, DogMovementPace pace)
    {
        yield return MoveNearInternal(worldPosition, timeout, pace, DefaultArrivalDistance);
    }

    public IEnumerator MoveNearPrecise(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        yield return MoveNearInternal(worldPosition, timeout, pace, Mathf.Max(0.02f, reachedDistance));
    }

    public IEnumerator FaceTarget(Transform target, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            FacePosition(target.position);
            yield return null;
        }
    }

    public IEnumerator PlayBowlUse(bool useDrinkLoop, float loopDuration)
    {
        yield return RunOneShotRoutine(loopDuration, useDrinkLoop ? "Drink" : "Eat");
    }

    public IEnumerator PlayBark(float duration)
    {
        yield return RunOneShotRoutine(duration, "Vocal");
    }

    public void RequestInteractionCameraLook(Transform target, float duration)
    {
        if (target == null)
        {
            interactionLookTarget = null;
            interactionLookUntil = 0f;
            return;
        }

        interactionLookTarget = target;
        interactionLookUntil = Mathf.Max(interactionLookUntil, Time.time + Mathf.Max(0.1f, duration));
    }

    public bool TryPlayPettingReaction()
    {
        if (IsBusy)
        {
            return false;
        }

        StartCoroutine(RunOneShotRoutine(0.9f, "Petting"));
        return true;
    }

    public bool CanPerformTrainingAnimation()
    {
        return isActiveAndEnabled && !IsBusy;
    }

    public IEnumerator PlayTrainingTrick(PawPalTrickDefinition definition, Camera camera)
    {
        PauseForSocial(true);
        trainingBusy = true;

        if (agent != null)
        {
            agent.ResetPath();
        }

        if (camera != null)
        {
            yield return FaceTarget(camera.transform, 0.24f);
        }

        if (definition != null)
        {
            switch (definition.Id)
            {
                case PawPalTrickId.Spin:
                    yield return SpinRoutine();
                    break;
                case PawPalTrickId.Jump:
                    yield return HopRoutine();
                    break;
                case PawPalTrickId.Lie:
                    yield return RestPoseRoutine(1.55f, true);
                    break;
                case PawPalTrickId.Shake:
                    yield return RunOneShotRoutine(1.0f, "Paw");
                    break;
                default:
                    yield return RunOneShotRoutine(1.15f, "Training");
                    break;
            }
        }

        trainingBusy = false;
        StartRoaming();
    }

    public bool CanPlayPhotoPose(PawPalPhotoPoseId poseId)
    {
        return isActiveAndEnabled;
    }

    public IEnumerator PlayPhotoPose(PawPalPhotoPoseId poseId, float duration)
    {
        switch (poseId)
        {
            case PawPalPhotoPoseId.LieSleep:
                yield return RestPoseRoutine(duration, true);
                yield break;
            case PawPalPhotoPoseId.LieLoop1:
            case PawPalPhotoPoseId.LieLoop2:
                yield return RestPoseRoutine(duration, false);
                yield break;
            case PawPalPhotoPoseId.Scratch:
                yield return RunOneShotRoutine(duration, "Scratch");
                yield break;
            case PawPalPhotoPoseId.Bark:
                yield return RunOneShotRoutine(duration, "Vocal");
                yield break;
            default:
                yield return RunOneShotRoutine(duration, "Idle");
                yield break;
        }
    }

    public void StartHeldToyTugAnimation()
    {
        isPlayingOneShotAnimation = true;
    }

    public void StopHeldToyTugAnimation()
    {
        isPlayingOneShotAnimation = false;
    }

    public bool TryGetHeldToyTransform(out Transform toy)
    {
        toy = activeToyTarget;
        return toy != null;
    }

    public IEnumerator PutDownHeldToyForInteraction()
    {
        activeToyTarget = null;
        yield return new WaitForSeconds(0.1f);
    }

    public void TryPlayImmediateInteractionPantingVocal()
    {
    }

    public void TryPlayImmediateInteractionAnnoyedVocal()
    {
    }

    private void Awake()
    {
        InitializeSharedPresentation();
    }

    private void Update()
    {
        if (!isActiveAndEnabled || sessionData == null || socialPaused || IsBusy)
        {
            SetMoving(false);
            return;
        }

        if (Time.time < waitUntil)
        {
            SetMoving(false);
            return;
        }

        if (TryStartAmbientAction())
        {
            return;
        }

        if (agent == null || !agent.isOnNavMesh)
        {
            SetMoving(false);
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= destinationReachedDistance)
        {
            PickNextDestination(false);
            return;
        }

        FaceVelocity();
        SetMoving(agent.velocity.sqrMagnitude > 0.002f);
    }

    private void LateUpdate()
    {
        if (interactionLookTarget == null || Time.time > interactionLookUntil || headTransform == null)
        {
            return;
        }

        Vector3 lookPoint = interactionLookTarget.position;
        Vector3 direction = lookPoint - headTransform.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        headTransform.rotation = Quaternion.Slerp(headTransform.rotation, targetRotation, Time.deltaTime * 5.5f);
    }

    private void InitializeSharedPresentation()
    {
        animator = GetComponentInChildren<Animator>(true);
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.angularSpeed = 420f;
            agent.acceleration = 7f;
            agent.stoppingDistance = stoppingDistance;
            agent.radius = Mathf.Max(0.12f, agent.radius);
            agent.height = Mathf.Max(0.28f, agent.height);
            agent.autoBraking = true;
        }

        if (sessionData != null && sessionData.Definition != null)
        {
            animationSet = sessionData.Definition.AnimationSet;
            if (sessionData.Definition.HomeScale != Vector3.zero)
            {
                transform.localScale = sessionData.Definition.HomeScale;
            }

            roamRadius = Mathf.Max(1.5f, sessionData.Definition.RoamRadius);
            if (sessionData.FurVariant != null && sessionData.FurVariant.ReplacementMaterial != null)
            {
                PetVariantApplier.ApplyMaterial(gameObject, sessionData.FurVariant);
            }
        }

        if (animationSet != null)
        {
            animationSet.ApplyTo(animator, sessionData != null ? sessionData.Definition : null);
        }

        headTransform = PawPalRoomPetRuntime.ResolveHeadTransform(transform);
        PetVariantApplier.EnsureTapCollider(gameObject);
        homePosition = transform.position;
    }

    private void SnapToNavMesh()
    {
        if (agent == null)
        {
            return;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 4f, NavMesh.AllAreas)
            || NavMesh.SamplePosition(roomBoundsCenter, out hit, 4f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            homePosition = hit.position;
        }
    }

    private bool TryStartAmbientAction()
    {
        if (Time.time >= nextToyAttemptAt && Random.value < toyInterestChance * Time.deltaTime)
        {
            Transform toy = FindToyTarget();
            if (toy != null)
            {
                activeActionRoutine = StartCoroutine(ToyRoutine(toy));
                return true;
            }
        }

        if (Time.time >= nextSocialAt && Random.value < socialInterestChance * Time.deltaTime)
        {
            PawPalRoomPetHandle partner = FindSocialPartner();
            if (partner != null && partner.IsValid)
            {
                activeActionRoutine = StartCoroutine(SocialRoutine(partner));
                return true;
            }
        }

        return false;
    }

    private IEnumerator ToyRoutine(Transform toy)
    {
        activeToyTarget = toy;
        yield return MoveNearInternal(toy.position, 4f, DogMovementPace.Trot, 0.24f);
        if (wasLastTravelSuccessful)
        {
            yield return FaceTarget(toy, 0.25f);
            yield return RunOneShotRoutine(Random.Range(toyPlayDurationMin, toyPlayDurationMax), "Toy");
        }

        activeToyTarget = null;
        nextToyAttemptAt = Time.time + Random.Range(2f, 4.5f);
        activeActionRoutine = null;
        PickNextDestination(false);
    }

    private IEnumerator SocialRoutine(PawPalRoomPetHandle partner)
    {
        socialPartner = partner;
        Transform target = partner.FocusTransform != null ? partner.FocusTransform : partner.RootTransform;
        if (target != null)
        {
            Vector3 offset = target.position - target.forward * 0.42f;
            yield return MoveNearInternal(offset, 4.5f, DogMovementPace.Walk, 0.34f);
            if (wasLastTravelSuccessful)
            {
                yield return FaceTarget(target, 0.35f);
                yield return RunOneShotRoutine(Random.Range(1.05f, 1.75f), "Social");
            }
        }

        socialPartner = null;
        nextSocialAt = Time.time + socialCooldown;
        activeActionRoutine = null;
        PickNextDestination(false);
    }

    private IEnumerator MoveNearInternal(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            wasLastTravelSuccessful = false;
            yield break;
        }

        SetMovePace(pace);
        Vector3 safePoint;
        if (!TryGetRoomSafePoint(worldPosition, sampleRadius, out safePoint))
        {
            safePoint = ClampToRoom(worldPosition);
        }

        agent.ResetPath();
        agent.SetDestination(safePoint);
        float elapsed = 0f;
        wasLastTravelSuccessful = false;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(0.02f, reachedDistance))
            {
                wasLastTravelSuccessful = true;
                break;
            }

            FaceVelocity();
            SetMoving(agent.velocity.sqrMagnitude > 0.001f);
            yield return null;
        }

        agent.ResetPath();
        SetMoving(false);
        waitUntil = Time.time + preInteractionPause;
    }

    private IEnumerator RunOneShotRoutine(float duration, string label)
    {
        isPlayingOneShotAnimation = true;
        if (agent != null)
        {
            agent.ResetPath();
        }

        if (animationSet != null)
        {
            animationSet.TryPlaySelectedReaction(animator);
        }

        if (label == "Scratch")
        {
            isResting = true;
        }

        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));
        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = false;
    }

    private IEnumerator RestPoseRoutine(float duration, bool sleeping)
    {
        isResting = true;
        isSleeping = sleeping;
        yield return RunOneShotRoutine(duration, sleeping ? "Sleep" : "Rest");
        isResting = false;
        isSleeping = false;
    }

    private IEnumerator SpinRoutine()
    {
        isPlayingOneShotAnimation = true;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 360f, 0f);
        float elapsed = 0f;
        const float duration = 0.8f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        isPlayingOneShotAnimation = false;
    }

    private IEnumerator HopRoutine()
    {
        isPlayingOneShotAnimation = true;
        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        const float duration = 0.55f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(progress * Mathf.PI) * 0.08f;
            transform.position = startPosition + transform.forward * (progress * 0.12f) + Vector3.up * arc;
            yield return null;
        }

        transform.position = startPosition;
        isPlayingOneShotAnimation = false;
    }

    private void PickNextDestination(bool immediate)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        Vector2 offset = Random.insideUnitCircle * roamRadius;
        Vector3 destination = homePosition + new Vector3(offset.x, 0f, offset.y);
        destination = ClampToRoom(destination);

        Vector3 safePoint;
        if (TryGetRoomSafePoint(destination, sampleRadius, out safePoint))
        {
            agent.SetDestination(safePoint);
        }
        else
        {
            agent.SetDestination(destination);
        }

        SetMovePace(Random.value < 0.35f ? DogMovementPace.Trot : DogMovementPace.Walk);
        waitUntil = immediate ? 0f : Time.time + Random.Range(minRoamWait, maxRoamWait);
    }

    private void SetMovePace(DogMovementPace pace)
    {
        switch (pace)
        {
            case DogMovementPace.Run:
                currentMoveSpeed = runSpeed;
                break;
            case DogMovementPace.Trot:
                currentMoveSpeed = trotSpeed;
                break;
            default:
                currentMoveSpeed = walkSpeed;
                break;
        }

        if (agent != null)
        {
            agent.speed = currentMoveSpeed;
        }
    }

    private Transform FindToyTarget()
    {
        List<Transform> candidates = new List<Transform>();
        ToyAttach[] toyObjects = FindObjectsByType<ToyAttach>(FindObjectsSortMode.None);
        for (int i = 0; i < toyObjects.Length; i++)
        {
            if (toyObjects[i] != null)
            {
                candidates.Add(toyObjects[i].transform);
            }
        }

        PawPalToyRuntimeMetadata[] runtimeToys = FindObjectsByType<PawPalToyRuntimeMetadata>(FindObjectsSortMode.None);
        for (int i = 0; i < runtimeToys.Length; i++)
        {
            if (runtimeToys[i] != null)
            {
                candidates.Add(runtimeToys[i].transform);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private PawPalRoomPetHandle FindSocialPartner()
    {
        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (dog == null || dog == GetComponent<DogRoomAgent>() || dog.IsBusy)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, dog.transform.position) <= 3.5f)
            {
                return new PawPalRoomPetHandle(dog);
            }
        }

        PawPalCatRoomAgent[] cats = FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < cats.Length; i++)
        {
            PawPalCatRoomAgent cat = cats[i];
            if (cat == null || cat == this || cat.IsBusy)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, cat.transform.position) <= 3.5f)
            {
                return new PawPalRoomPetHandle(cat);
            }
        }

        return null;
    }

    private void FaceVelocity()
    {
        if (agent == null || agent.velocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        FacePosition(transform.position + agent.velocity);
    }

    private void FacePosition(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    private void SetMoving(bool moving)
    {
        if (animationSet != null && animationSet.TrySetMoving(animator, moving, currentMoveSpeed > 0f ? currentMoveSpeed : walkSpeed))
        {
            return;
        }

        if (animator != null)
        {
            animator.speed = moving ? 1f : 0.85f;
        }
    }

    private Vector3 ClampToRoom(Vector3 point)
    {
        if (!restrictToRoomBounds)
        {
            return point;
        }

        float halfWidth = Mathf.Max(0.1f, roomBoundsSize.x * 0.5f - roomBoundsPadding);
        float halfDepth = Mathf.Max(0.1f, roomBoundsSize.y * 0.5f - roomBoundsPadding);
        point.x = Mathf.Clamp(point.x, roomBoundsCenter.x - halfWidth, roomBoundsCenter.x + halfWidth);
        point.z = Mathf.Clamp(point.z, roomBoundsCenter.z - halfDepth, roomBoundsCenter.z + halfDepth);
        return point;
    }
}
