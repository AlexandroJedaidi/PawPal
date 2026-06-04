using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class PawPalCatRoomAgent : MonoBehaviour
{
    private const float DefaultArrivalDistance = 0.18f;
    private const float SitStartDuration = 0.28f;
    private const float SitEndDuration = 0.24f;
    private const float MovementDebugLogCooldown = 0.75f;
    private const float AnimatorStallThreshold = 0.35f;
    private const float MinimumAmbientVocalCooldown = 10f;
    private const float InteractionIdleMinSeconds = 2.2f;
    private const float InteractionIdleMaxSeconds = 4.1f;
    private const float InteractionIdleBusyRetrySeconds = 0.18f;
    private const float PetAvoidanceRepathInterval = 0.28f;
    private const float PetAvoidancePadding = 0.05f;
    private const float PetAvoidanceRerouteSearchRadius = 0.55f;
    private const int PetAvoidanceRerouteCandidateCount = 12;
    private static readonly HashSet<string> LoggedAnimationMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Transform> ClaimedToyTransforms = new HashSet<Transform>();
    private static readonly PawPalPhotoPoseId[] InteractionIdlePoses =
    {
        PawPalPhotoPoseId.Idle2,
        PawPalPhotoPoseId.Idle3,
        PawPalPhotoPoseId.Idle1,
        PawPalPhotoPoseId.Idle4,
        PawPalPhotoPoseId.Idle6,
        PawPalPhotoPoseId.Idle7
    };

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
    // Retained for scene/prefab compatibility; runtime locomotion now comes from PawPalPetMovementProfiles.
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
    [SerializeField] private float toyPickupAttachDelay = 0.32f;
    [SerializeField] private float toyPickupAnimationDuration = 0.8f;
    [SerializeField] private float toyPutDownReleaseDelay = 0.25f;
    [SerializeField] private float toyPutDownAnimationDuration = 0.65f;
    [SerializeField] private float minToyCarryDuration = 1.2f;
    [SerializeField] private float maxToyCarryDuration = 2.8f;
    [SerializeField] private bool carryToyToNewSpot = true;
    [SerializeField] private string[] toyNameKeywords = { "ball", "bone", "cattoy", "toy" };
    [SerializeField] private Vector3 toyDropOffset = new Vector3(0f, 0.035f, 0.32f);
    [SerializeField] private float socialCooldown = 3.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip vocalClip;
    [SerializeField, Range(0f, 1f)] private float vocalVolume = 0.78f;
    [SerializeField] private AudioClip toyPickupClip;
    [SerializeField, Range(0f, 1f)] private float toyPickupVolume = 0.58f;
    [SerializeField, Range(0f, 1f)] private float ambientVocalChance = 0.32f;
    [SerializeField] private float ambientVocalCooldown = 10f;
    [SerializeField] private Vector2 ambientVocalDurationRange = new Vector2(0.55f, 1.05f);

    private Animator animator;
    private NavMeshAgent agent;
    private PetAnimationSet animationSet;
    private SelectedPetSessionData sessionData;
    private ToyAttach toyAttach;
    private Transform headTransform;
    private Transform activeToyTarget;
    private Transform claimedToy;
    private GameObject heldToy;
    private PawPalRoomPetHandle socialPartner;
    private Coroutine interactionIdleRoutine;
    private Vector3 homePosition;
    private float waitUntil;
    private float nextSocialAt;
    private float nextToyAttemptAt;
    private float nextAllowedAmbientVocalTime;
    private float nextAllowedVocalAudioTime;
    private float currentMoveSpeed;
    private PawPalPetMovementProfile movementProfile = PawPalPetMovementProfiles.DefaultProfile;
    private bool hasMovementProfile;
    private float cachedNavigationFootprintRadius = -1f;
    private DogMovementPace currentPace = DogMovementPace.Walk;
    private bool externalWalkControl;
    private DogMovementPace externalWalkPace = DogMovementPace.Walk;
    private Coroutine activeActionRoutine;
    private bool directMovementRoutineActive;
    private bool socialPausePoseApplied;
    private bool socialPaused;
    private bool waitingForRoamDestination;
    private bool isResting;
    private bool isSleeping;
    private bool isPlayingOneShotAnimation;
    private bool trainingBusy;
    private bool wasLastTravelSuccessful = true;
    private Transform interactionLookTarget;
    private float interactionLookUntil;
    private int barkStateHash;
    private int neutralIdleStateHash;
    private int idle2StateHash;
    private int sitStartStateHash;
    private int sitLoopStateHash;
    private int sitEndStateHash;
    private int lieStateHash;
    private int scratchingStateHash;
    private int eatDrinkStartStateHash;
    private int eatLoopStateHash;
    private int drinkLoopStateHash;
    private int eatDrinkEndStateHash;
    private int jumpPlaceStateHash;
    private int locomotionStateHash;
    private int walkStateHash;
    private int trotStateHash;
    private int runStateHash;
    private int activeLocomotionStateHash;
    private string activeMovementContext = "Roam";
    private float nextMovementDebugLogAt;
    private float nextNavMeshSnapAttemptAt;
    private float nextPetAvoidanceRepathAt;
    private int lastObservedAnimatorStateHash;
    private float lastObservedAnimatorNormalizedTime;
    private float lastObservedAnimatorProgressAt;
    private int interactionIdlePoseCursor;
#if UNITY_EDITOR
    private PlayableGraph directLocomotionGraph;
    private AnimationClipPlayable activeDirectLocomotionPlayable;
    private AnimationClip activeDirectLocomotionClip;
#endif

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

    public bool CanJoinSocialInteraction
    {
        get
        {
            return isActiveAndEnabled
                && !IsBusy
                && !socialPaused
                && !isResting
                && !isSleeping
                && !isPlayingOneShotAnimation
                && !HasHeldToy;
        }
    }

    public bool HasHeldToy
    {
        get { return heldToy != null || (toyAttach != null && toyAttach.HasToy); }
    }

    public bool WasLastTravelSuccessful
    {
        get { return wasLastTravelSuccessful; }
    }

    public PawPalPetMovementProfile MovementProfile
    {
        get { return ResolveMovementProfile(); }
    }

    public float GetInteractionNavigationFootprintRadius()
    {
        return GetNavigationFootprintRadius();
    }

    public bool IsMoving
    {
        get
        {
            return agent != null
                && agent.isOnNavMesh
                && (agent.velocity.sqrMagnitude > 0.001f
                    || (agent.hasPath && !agent.pathPending && agent.remainingDistance > Mathf.Max(destinationReachedDistance, 0.08f)));
        }
    }

    public void Initialize(SelectedPetSessionData data)
    {
        sessionData = data;
        RefreshMovementProfile();
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
        CancelAmbientAction();
        PauseForSocial(!preserveHeldToy);
        trainingBusy = false;
        isPlayingOneShotAnimation = false;
        socialPartner = null;
        if (!preserveHeldToy)
        {
            DropHeldToyImmediately();
        }

        return isActiveAndEnabled && agent != null && agent.isOnNavMesh;
    }

    public void StartRoaming()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EndInteractionIdleLoop();

        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
            activeActionRoutine = null;
        }

        socialPaused = false;
        socialPausePoseApplied = false;
        activeMovementContext = "Roam";
        waitingForRoamDestination = false;
        trainingBusy = false;
        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = false;
        DropHeldToyImmediately();
        ReleaseClaimedToy();
        activeToyTarget = null;
        socialPartner = null;
        EnsureAgentMoving();
        nextAllowedAmbientVocalTime = Mathf.Max(
            nextAllowedAmbientVocalTime,
            Time.time + GetAmbientVocalCooldown());

        PickNextDestination(false);
    }

    public void PauseForSocial(bool dropHeldToyImmediately)
    {
        socialPaused = true;
        socialPausePoseApplied = false;
        waitingForRoamDestination = false;
        StopDirectLocomotionPlayback();
        StopAgentMovement();
        SetMoving(false);
        TryCrossFadeToStableIdle();
        socialPausePoseApplied = true;

        if (dropHeldToyImmediately)
        {
            DropHeldToyImmediately();
        }
    }

    public void BeginInteractionIdleLoop()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (!socialPaused)
        {
            PauseForSocial(false);
        }

        if (interactionIdleRoutine == null)
        {
            interactionIdleRoutine = StartCoroutine(InteractionIdleLoopRoutine());
        }
    }

    public void EndInteractionIdleLoop()
    {
        if (interactionIdleRoutine != null)
        {
            StopCoroutine(interactionIdleRoutine);
            interactionIdleRoutine = null;
        }
    }

    public void SetExternalWalkControl(bool enabled)
    {
        externalWalkControl = enabled;
        if (enabled)
        {
            CancelAmbientAction();
            socialPaused = false;
            waitingForRoamDestination = false;
            isResting = false;
            isSleeping = false;
            SetExternalWalkPace(externalWalkPace);
            socialPausePoseApplied = false;
            StopAgentMovement();
        }
        else
        {
            StopAgentMovement();
            SetMoving(false);
            socialPausePoseApplied = false;
        }
    }

    public void SetExternalWalkPace(DogMovementPace pace)
    {
        externalWalkPace = pace;
        SetMovePace(pace);
    }

    public bool TryGetRoomSafePoint(Vector3 candidate, float radius, out Vector3 point)
    {
        if (TryGetReachableRoomPoint(candidate, Mathf.Max(radius, sampleRadius), out point)
            && IsPointComfortable(point, radius))
        {
            return true;
        }

        if (TryFindNearbyComfortablePoint(candidate, radius, out point))
        {
            return true;
        }

        point = ClampToRoom(candidate);
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

    public IEnumerator MoveNearPlayerInteraction(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        yield return MoveNearInternal(worldPosition, 0f, pace, Mathf.Max(0.02f, reachedDistance), true);
    }

    public bool TryYieldForPlayerInteractionPath(Vector3 protectedDestination, Transform caller)
    {
        if (!isActiveAndEnabled || caller == null || caller == transform || transform.IsChildOf(caller) || caller.IsChildOf(transform))
        {
            return false;
        }

        WakeForPlayerInteraction();
        CancelAmbientAction();
        EndInteractionIdleLoop();
        socialPaused = false;
        socialPausePoseApplied = false;
        waitingForRoamDestination = false;
        trainingBusy = false;
        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = false;
        socialPartner = null;

        Vector3 yieldPoint;
        if (!TryFindInteractionYieldPoint(protectedDestination, caller, out yieldPoint))
        {
            return false;
        }

        SetMovePace(DogMovementPace.Walk);
        EnsureAgentMoving();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || !agent.SetDestination(yieldPoint))
        {
            return false;
        }

        activeMovementContext = "Yield";
        SetMoving(true);
        return true;
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
        yield return PlayBowlUseRoutine(useDrinkLoop, loopDuration);
    }

    public IEnumerator PlayBark(float duration)
    {
        PlayVocalAudio();
        yield return RunOneShotRoutine(duration, "Vocal");
    }

    public bool TryPlayPreviewVocal()
    {
        if (!CanJoinSocialInteraction)
        {
            return false;
        }

        StartCoroutine(PlayBark(0.7f));
        return true;
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
        if (!isActiveAndEnabled || trainingBusy || isPlayingOneShotAnimation)
        {
            return false;
        }

        CancelAmbientAction();
        PauseForSocial(true);
        StartCoroutine(SitPoseRoutine(0.9f, false));
        return true;
    }

    public bool CanPerformTrainingAnimation()
    {
        return isActiveAndEnabled && !IsBusy && !IsMoving;
    }

    public IEnumerator PlayTrainingTrick(PawPalTrickDefinition definition, Camera camera)
    {
        yield return PlayTrainingTrick(definition, camera, true);
    }

    public IEnumerator PlayInteractionTrainingTrick(PawPalTrickDefinition definition, Camera camera)
    {
        yield return PlayTrainingTrick(definition, camera, false);
    }

    private IEnumerator PlayTrainingTrick(PawPalTrickDefinition definition, Camera camera, bool resumeRoamingAfter)
    {
        CancelAmbientAction();
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
                case PawPalTrickId.Sit:
                    yield return SitPoseRoutine(1.15f, !resumeRoamingAfter);
                    break;
                case PawPalTrickId.Spin:
                    yield return SpinRoutine();
                    break;
                case PawPalTrickId.Jump:
                    yield return HopRoutine();
                    break;
                case PawPalTrickId.Lie:
                    yield return RestPoseRoutine(1.55f, false);
                    break;
                case PawPalTrickId.Shake:
                    yield return SitPoseRoutine(0.95f, !resumeRoamingAfter);
                    break;
                default:
                    yield return SitPoseRoutine(1.0f, !resumeRoamingAfter);
                    break;
            }
        }

        trainingBusy = false;
        if (resumeRoamingAfter)
        {
            StartRoaming();
        }
        else
        {
            socialPaused = true;
            if (agent != null)
            {
                agent.ResetPath();
            }
        }
    }

    public bool CanPlayPhotoPose(PawPalPhotoPoseId poseId)
    {
        return isActiveAndEnabled;
    }

    public IEnumerator PlayPhotoPose(PawPalPhotoPoseId poseId, float duration)
    {
        switch (poseId)
        {
            case PawPalPhotoPoseId.Bark:
                yield return PlayBark(duration);
                yield break;
            case PawPalPhotoPoseId.Idle1:
            case PawPalPhotoPoseId.Idle2:
            case PawPalPhotoPoseId.Idle3:
            case PawPalPhotoPoseId.Idle4:
            case PawPalPhotoPoseId.Idle6:
            case PawPalPhotoPoseId.Idle7:
                yield return PlayPhotoStandingIdle(poseId, duration);
                yield break;
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
        if (heldToy != null)
        {
            toy = heldToy.transform;
            return true;
        }

        if (toyAttach != null && toyAttach.CurrentToy != null)
        {
            toy = toyAttach.CurrentToy.transform;
            return true;
        }

        toy = null;
        return toy != null;
    }

    public IEnumerator PutDownHeldToyForInteraction()
    {
        if (!HasHeldToy)
        {
            yield break;
        }

        yield return PlayPutDownToy();
    }

    public void TryPlayImmediateInteractionPantingVocal()
    {
    }

    public void TryPlayImmediateInteractionAnnoyedVocal()
    {
    }

    private void Awake()
    {
        PawPalAudioResources.AssignIfMissing(ref vocalClip, PawPalAudioResources.CatMeow);
        PawPalAudioResources.AssignIfMissing(ref toyPickupClip, PawPalAudioResources.ToyPickup);
        toyAttach = GetComponent<ToyAttach>();
        InitializeSharedPresentation();
    }

    private void OnDisable()
    {
        directMovementRoutineActive = false;
        StopDirectLocomotionPlayback();
        activeLocomotionStateHash = 0;
        DropHeldToyImmediately();
        ReleaseClaimedToy();
    }

    private void OnDestroy()
    {
        StopDirectLocomotionPlayback();
        DropHeldToyImmediately();
        ReleaseClaimedToy();
    }

    private void Update()
    {
        if (externalWalkControl)
        {
            if (isPlayingOneShotAnimation)
            {
                StopAgentMovement();
                SetMoving(false);
                return;
            }

            EnsureAgentMoving();

            SetMovePace(externalWalkPace);
            SetMoving(true);
            return;
        }

        if (directMovementRoutineActive)
        {
            return;
        }

        if (!isActiveAndEnabled || sessionData == null || socialPaused || trainingBusy)
        {
            if (!socialPausePoseApplied)
            {
                StopAgentMovement();
                SetMoving(false);
                socialPausePoseApplied = true;
            }

            TickMovementDebug(false, "Suppressed");
            return;
        }

        if (activeActionRoutine != null)
        {
            bool shouldUseLocomotion = ShouldUseLocomotion();
            if (!shouldUseLocomotion)
            {
                StopAgentMovement();
            }
            else
            {
                EnsureAgentMoving();
                FaceSteering();
            }

            TickMovementDebug(shouldUseLocomotion, "Action");
            SetMoving(shouldUseLocomotion);
            return;
        }

        if (Time.time < waitUntil)
        {
            StopAgentMovement();
            TickMovementDebug(false, "Waiting");
            SetMoving(false);
            return;
        }

        if (waitingForRoamDestination)
        {
            waitingForRoamDestination = false;
            PickNextDestination(true);
            return;
        }

        if (TryStartAmbientAction())
        {
            return;
        }

        if (agent == null || !agent.isOnNavMesh)
        {
            if (Time.time >= nextNavMeshSnapAttemptAt)
            {
                nextNavMeshSnapAttemptAt = Time.time + 1f;
                if (SnapToNavMesh())
                {
                    PickNextDestination(false);
                    return;
                }
            }

            TickMovementDebug(false, "NoNavMesh");
            SetMoving(false);
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= destinationReachedDistance)
        {
            PickNextDestination(false);
            return;
        }

        Vector3 activeDestination = agent.destination;
        TryRedirectAroundBlockingPet(ref activeDestination, PetAvoidancePadding);

        EnsureAgentMoving();
        bool shouldMove = ShouldUseLocomotion();
        FaceSteering();
        TickMovementDebug(shouldMove, "Roam");
        SetMoving(shouldMove);
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

        if (socialPaused && !isPlayingOneShotAnimation)
        {
            FacePosition(lookPoint);
            return;
        }

        ApplyHeadLookRotation(direction.normalized);
    }

    private void ApplyHeadLookRotation(Vector3 lookDirection)
    {
        if (headTransform == null || lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 aimAxis = GetBestHeadAimAxis();
        Quaternion delta = Quaternion.FromToRotation(aimAxis, lookDirection);
        Quaternion targetRotation = delta * headTransform.rotation;
        headTransform.rotation = Quaternion.Slerp(headTransform.rotation, targetRotation, Time.deltaTime * 5.5f);
    }

    private Vector3 GetBestHeadAimAxis()
    {
        Vector3 petForward = transform.forward;
        petForward.y = 0f;
        if (petForward.sqrMagnitude < 0.001f)
        {
            petForward = Vector3.forward;
        }

        petForward.Normalize();

        Vector3 bestAxis = headTransform.forward;
        float bestDot = Vector3.Dot(FlattenAxis(bestAxis), petForward);
        TestHeadAimAxis(headTransform.forward, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(-headTransform.forward, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(headTransform.up, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(-headTransform.up, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(headTransform.right, petForward, ref bestAxis, ref bestDot);
        TestHeadAimAxis(-headTransform.right, petForward, ref bestAxis, ref bestDot);
        return bestAxis.normalized;
    }

    private static void TestHeadAimAxis(Vector3 candidateAxis, Vector3 petForward, ref Vector3 bestAxis, ref float bestDot)
    {
        Vector3 flatCandidate = FlattenAxis(candidateAxis);
        float dot = Vector3.Dot(flatCandidate, petForward);
        if (dot > bestDot)
        {
            bestDot = dot;
            bestAxis = candidateAxis;
        }
    }

    private static Vector3 FlattenAxis(Vector3 axis)
    {
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        return axis.normalized;
    }

    private void InitializeSharedPresentation()
    {
        animator = GetComponentInChildren<Animator>(true);
#if UNITY_EDITOR
        if (animator == null)
        {
            animator = EnsureEditorAnimator();
        }
#endif
        agent = GetComponent<NavMeshAgent>();
        RefreshMovementProfile();
        if (agent != null)
        {
            agent.speed = GetAgentSpeed(DogMovementPace.Walk);
            agent.angularSpeed = 420f;
            agent.acceleration = 7f;
            agent.stoppingDistance = stoppingDistance;
            agent.height = Mathf.Max(0.28f, agent.height);
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = Mathf.Clamp(40 + Mathf.Abs(GetInstanceID()) % 35, 0, 99);
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

        ConfigureAgentNavigationRadius();

        if (animationSet != null)
        {
            animationSet.ApplyTo(animator, sessionData != null ? sessionData.Definition : null);
        }

#if UNITY_EDITOR
        ApplyPreferredEditorAvatar();
#endif

        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);
        }

        CacheAnimationStateHashes();

        headTransform = PawPalRoomPetRuntime.ResolveHeadTransform(transform);
        PetVariantApplier.EnsureTapCollider(gameObject);
        homePosition = transform.position;
        currentPace = DogMovementPace.Walk;
        currentMoveSpeed = GetAgentSpeed(currentPace);
    }

#if UNITY_EDITOR
    private Animator EnsureEditorAnimator()
    {
        PawPalPetAnimationEntry entry;
        if (!PawPalPetAnimationRegistry.TryResolveEntry(
            sessionData != null ? sessionData.Definition : null,
            sessionData != null ? sessionData.BreedName : null,
            name,
            out entry))
        {
            return null;
        }

        Animator createdAnimator = gameObject.GetComponent<Animator>();
        if (createdAnimator == null)
        {
            createdAnimator = gameObject.AddComponent<Animator>();
        }

        Avatar avatar = PawPalPetAnimationRegistry.GetPreferredEditorAvatar(entry);
        createdAnimator.avatar = avatar;
        createdAnimator.applyRootMotion = false;
        createdAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return createdAnimator;
    }

    private void ApplyPreferredEditorAvatar()
    {
        if (animator == null)
        {
            return;
        }

        PawPalPetAnimationEntry entry;
        if (!PawPalPetAnimationRegistry.TryResolveEntry(
            sessionData != null ? sessionData.Definition : null,
            sessionData != null ? sessionData.BreedName : null,
            name,
            out entry))
        {
            return;
        }

        Avatar preferredAvatar = PawPalPetAnimationRegistry.GetPreferredEditorAvatar(entry);
        if (preferredAvatar == null)
        {
            animator.avatar = null;
            animator.Rebind();
            animator.Update(0f);
            return;
        }

        if (animator.avatar == preferredAvatar)
        {
            return;
        }

        animator.avatar = preferredAvatar;
        animator.Rebind();
        animator.Update(0f);
    }
#endif

    private void CancelAmbientAction()
    {
        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
            activeActionRoutine = null;
        }

        activeMovementContext = "Roam";
        socialPartner = null;
        if (HasHeldToy)
        {
            DropHeldToyImmediately();
        }

        ReleaseClaimedToy();
    }

    private bool SnapToNavMesh()
    {
        if (agent == null)
        {
            return false;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 4f, NavMesh.AllAreas)
            || NavMesh.SamplePosition(roomBoundsCenter, out hit, 4f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            homePosition = hit.position;
            return true;
        }

        return agent.isOnNavMesh;
    }

    private bool TryStartAmbientAction()
    {
        if (Time.time >= nextAllowedAmbientVocalTime && Random.value < ambientVocalChance * Time.deltaTime)
        {
            activeActionRoutine = StartCoroutine(AmbientVocalRoutine());
            return true;
        }

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

    private IEnumerator AmbientVocalRoutine()
    {
        activeMovementContext = "AmbientVocal";
        nextAllowedAmbientVocalTime = Time.time
            + GetAmbientVocalCooldown()
            + Random.Range(0f, Mathf.Max(0.1f, GetAmbientVocalCooldown() * 0.45f));

        StopAgentMovement();
        SetMoving(false);
        TryCrossFadeToStableIdle();

        PlayVocalAudio();
        float vocalDurationMin = Mathf.Min(ambientVocalDurationRange.x, ambientVocalDurationRange.y);
        float vocalDurationMax = Mathf.Max(ambientVocalDurationRange.x, ambientVocalDurationRange.y);
        yield return RunOneShotRoutine(Random.Range(vocalDurationMin, vocalDurationMax), "Vocal");

        activeActionRoutine = null;
        activeMovementContext = "Roam";
        PickNextDestination(false);
    }

    private IEnumerator ToyRoutine(Transform toy)
    {
        toy = ResolvePickupToyRoot(toy);
        if (!TryClaimToy(toy))
        {
            activeActionRoutine = null;
            activeMovementContext = "Roam";
            PickNextDestination(false);
            yield break;
        }

        activeMovementContext = toy != null ? "Toy:" + toy.name : "Toy";
        activeToyTarget = toy;
        try
        {
            Vector3 approachPoint = GetDesiredToyPickupRootPosition(toy);
            yield return MoveNearInternal(approachPoint, 4f, DogMovementPace.Trot, 0.18f);
            if (wasLastTravelSuccessful && toy != null)
            {
                yield return FaceTarget(toy, 0.25f);
                yield return PlayPickupToy(toy.gameObject);
            }

            if (HasHeldToy)
            {
                yield return new WaitForSeconds(Random.Range(toyPlayDurationMin, toyPlayDurationMax));
            }

            if (HasHeldToy && carryToyToNewSpot)
            {
                Vector3 carryDestination;
                if (TryFindNearbyComfortablePoint(transform.position + Random.insideUnitSphere * 1.25f, 0.12f, out carryDestination))
                {
                    yield return MoveNearInternal(carryDestination, 3f, DogMovementPace.Trot, DefaultArrivalDistance);
                }
            }

            if (HasHeldToy)
            {
                yield return new WaitForSeconds(Random.Range(minToyCarryDuration, maxToyCarryDuration));
                yield return PlayPutDownToy();
            }
        }
        finally
        {
            DropHeldToyImmediately();
            ReleaseClaimedToy();
            activeToyTarget = null;
            nextToyAttemptAt = Time.time + Random.Range(2f, 4.5f);
            activeActionRoutine = null;
            activeMovementContext = "Roam";
            if (!socialPaused && !externalWalkControl)
            {
                PickNextDestination(false);
            }
        }
    }

    private IEnumerator SocialRoutine(PawPalRoomPetHandle partner)
    {
        activeMovementContext = "Social:" + partner.DisplayName;
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
        activeMovementContext = "Roam";
        PickNextDestination(false);
    }

    private IEnumerator MoveNearInternal(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        yield return MoveNearInternal(worldPosition, timeout, pace, reachedDistance, false);
    }

    private IEnumerator MoveNearInternal(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance, bool persistentInteractionArrival)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            wasLastTravelSuccessful = false;
            yield break;
        }

        string previousMovementContext = activeMovementContext;
        directMovementRoutineActive = true;
        activeMovementContext = socialPaused ? "Interaction" : "Directed";
        try
        {
            SetMovePace(pace);
            Vector3 safePoint;
            if (!TryGetRoomSafePoint(worldPosition, sampleRadius, out safePoint))
            {
                safePoint = ClampToRoom(worldPosition);
            }

            EnsureAgentMoving();
            agent.ResetPath();
            float arrivalDistance = Mathf.Max(0.02f, reachedDistance);
            Vector3 startToTarget = safePoint - transform.position;
            startToTarget.y = 0f;
            if (startToTarget.magnitude <= arrivalDistance)
            {
                wasLastTravelSuccessful = true;
                StopAgentMovement();
                SetMoving(false);
                socialPausePoseApplied = socialPaused;
                waitUntil = Time.time + preInteractionPause;
                yield break;
            }

            if (!agent.SetDestination(safePoint))
            {
                wasLastTravelSuccessful = false;
                StopAgentMovement();
                SetMoving(false);
                socialPausePoseApplied = socialPaused;
                waitUntil = Time.time + preInteractionPause;
                yield break;
            }

            float elapsed = 0f;
            wasLastTravelSuccessful = false;
            bool hasReceivedPath = false;
            float nextInteractionYieldRequestAt = 0f;

            while (timeout <= 0f || elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                if (!agent.pathPending)
                {
                    if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    {
                        if (!persistentInteractionArrival)
                        {
                            break;
                        }

                        if (TryGetRoomSafePoint(worldPosition, sampleRadius, out safePoint)
                            && agent.SetDestination(safePoint))
                        {
                            hasReceivedPath = false;
                            yield return null;
                            continue;
                        }
                    }

                    hasReceivedPath = hasReceivedPath || agent.hasPath || agent.remainingDistance > arrivalDistance;
                    if (hasReceivedPath && agent.remainingDistance <= arrivalDistance)
                    {
                        wasLastTravelSuccessful = true;
                        break;
                    }
                }

                if (TryRedirectAroundBlockingPet(ref safePoint, PetAvoidancePadding))
                {
                    hasReceivedPath = false;
                }
                else if (persistentInteractionArrival && Time.time >= nextInteractionYieldRequestAt)
                {
                    Transform blocker;
                    bool personalSpaceBlocked;
                    if (IsPathBlockedByAnotherPet(safePoint, PetAvoidancePadding, true, out blocker, out personalSpaceBlocked))
                    {
                        TryRequestInteractionPathYield(blocker, safePoint);
                        nextInteractionYieldRequestAt = Time.time + 0.65f;
                    }
                }

                FaceSteering();
                SetMoving(ShouldUseLocomotion());
                yield return null;
            }

            StopAgentMovement();
            SetMoving(false);
            socialPausePoseApplied = socialPaused;
            waitUntil = Time.time + preInteractionPause;
        }
        finally
        {
            directMovementRoutineActive = false;
            activeMovementContext = previousMovementContext;
        }
    }

    private IEnumerator RunOneShotRoutine(float duration, string label)
    {
        isPlayingOneShotAnimation = true;
        StopMovementForOneShot();

        float waitDuration = Mathf.Max(0.1f, duration);
        bool playedExplicitState = TryPlayExplicitStateForLabel(label);
        if (!playedExplicitState && animationSet != null)
        {
            animationSet.TryPlaySelectedReaction(animator);
        }

        if (playedExplicitState || animationSet != null)
        {
            yield return null;
            waitDuration = ResolveActiveOneShotDuration(waitDuration);
        }

        if (label == "Scratch")
        {
            isResting = true;
        }

        yield return new WaitForSeconds(waitDuration);
        if (label == "Vocal")
        {
            TryCrossFadeState(idle2StateHash);
        }

        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = false;
        ResumeMovementAfterOneShot();
    }

    private IEnumerator PlayPhotoStandingIdle(PawPalPhotoPoseId poseId, float duration)
    {
        isPlayingOneShotAnimation = true;
        StopMovementForOneShot();
        TryApplyStableIdleParameters();

        string suffix = GetPhotoStandingIdleClipSuffix(poseId);
        string[] stateNames = GetCatPhotoStandingIdleStateNames(suffix);
        int stateHash = ResolveStateHash(stateNames);
        float targetDuration = Mathf.Max(0.1f, duration);

        if (stateHash != 0)
        {
            TryCrossFadeState(stateHash);
            yield return new WaitForSeconds(targetDuration);
        }
        else
        {
#if UNITY_EDITOR
            AnimationClip clip = ResolveEditorClipByNames(suffix, stateNames);
            if (clip != null)
            {
                yield return PlayDirectEditorClip(clip, targetDuration, true, "CatPhoto" + suffix);
            }
            else
#endif
            {
                TryCrossFadeToStableIdle();
                yield return new WaitForSeconds(targetDuration);
            }
        }

        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = false;
        TryApplyStableIdleParameters();
        TryCrossFadeToStableIdle();
        ResumeMovementAfterOneShot();
    }

    private IEnumerator InteractionIdleLoopRoutine()
    {
        while (true)
        {
            if (!socialPaused || trainingBusy || isPlayingOneShotAnimation || activeActionRoutine != null || IsMoving)
            {
                yield return new WaitForSeconds(InteractionIdleBusyRetrySeconds);
                continue;
            }

            PlayPassiveInteractionIdlePose(NextInteractionIdlePose());

            float waitDuration = Random.Range(InteractionIdleMinSeconds, InteractionIdleMaxSeconds);
            float waitUntilTime = Time.time + waitDuration;
            while (Time.time < waitUntilTime)
            {
                if (!socialPaused || trainingBusy || isPlayingOneShotAnimation || activeActionRoutine != null || IsMoving)
                {
                    break;
                }

                yield return null;
            }
        }
    }

    private PawPalPhotoPoseId NextInteractionIdlePose()
    {
        if (InteractionIdlePoses == null || InteractionIdlePoses.Length == 0)
        {
            return PawPalPhotoPoseId.Idle2;
        }

        int index = Mathf.Abs(interactionIdlePoseCursor) % InteractionIdlePoses.Length;
        interactionIdlePoseCursor++;
        return InteractionIdlePoses[index];
    }

    private bool PlayPassiveInteractionIdlePose(PawPalPhotoPoseId poseId)
    {
        TryApplyStableIdleParameters();

        string suffix = GetPhotoStandingIdleClipSuffix(poseId);
        string[] stateNames = GetCatPhotoStandingIdleStateNames(suffix);
        int stateHash = ResolveStateHash(stateNames);
        if (stateHash != 0)
        {
            StopDirectLocomotionPlayback();
            if (!IsAnimatorInOrTransitioningTo(stateHash))
            {
                animator.CrossFadeInFixedTime(stateHash, 0.12f, 0, 0f);
            }

            return true;
        }

#if UNITY_EDITOR
        if (ShouldPreferDirectClipLocomotion())
        {
            AnimationClip clip = ResolveEditorClipByNames(suffix, stateNames);
            if (clip != null && TryPlayDirectLoopingClip(clip, "CatInteractionIdle"))
            {
                return true;
            }
        }
#endif

        return TryCrossFadeToStableIdle();
    }

    private static string GetPhotoStandingIdleClipSuffix(PawPalPhotoPoseId poseId)
    {
        switch (poseId)
        {
            case PawPalPhotoPoseId.Idle1:
                return "Idle_1";
            case PawPalPhotoPoseId.Idle3:
                return "Idle_3";
            case PawPalPhotoPoseId.Idle4:
                return "Idle_4";
            case PawPalPhotoPoseId.Idle6:
                return "Idle_6";
            case PawPalPhotoPoseId.Idle7:
                return "Idle_7";
            case PawPalPhotoPoseId.Idle2:
            default:
                return "Idle_2";
        }
    }

    private static string[] GetCatPhotoStandingIdleStateNames(string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
        {
            suffix = "Idle_2";
        }

        return new[]
        {
            "CatSimple_" + suffix,
            "Arm_Cat|" + suffix,
            "Arm_Kitten|" + suffix,
            suffix,
            suffix.Replace("_", string.Empty)
        };
    }

    private bool TryPlayExplicitStateForLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return false;
        }

        switch (label)
        {
            case "Vocal":
                return TryCrossFadeState(barkStateHash) || TryCrossFadeState(idle2StateHash);
            case "Scratch":
                return TryCrossFadeState(scratchingStateHash);
            case "Rest":
            case "Sleep":
                return TryCrossFadeState(lieStateHash);
            case "Idle":
            case "Social":
            case "Training":
            case "Paw":
            case "Toy":
                return TryCrossFadeState(idle2StateHash);
            case "Petting":
                return TryCrossFadeState(sitLoopStateHash) || TryCrossFadeState(idle2StateHash);
            case "Eat":
                return TryCrossFadeState(eatLoopStateHash) || TryCrossFadeState(idle2StateHash);
            case "Drink":
                return TryCrossFadeState(drinkLoopStateHash) || TryCrossFadeState(idle2StateHash);
            case "EatDrink_start":
                return TryCrossFadeState(eatDrinkStartStateHash) || TryCrossFadeState(idle2StateHash);
            case "EatDrink_end":
                return TryCrossFadeState(eatDrinkEndStateHash) || TryCrossFadeState(idle2StateHash);
            case "EatDrink":
                return TryCrossFadeState(idle2StateHash);
            default:
                return false;
        }
    }

    private IEnumerator PlayBowlUseRoutine(bool useDrinkLoop, float loopDuration)
    {
        isPlayingOneShotAnimation = true;
        StopMovementForOneShot();
        TryApplyStableIdleParameters();

        string[] startNames = GetCatBowlStartStateNames();
        string[] loopNames = useDrinkLoop ? GetCatBowlDrinkLoopStateNames() : GetCatBowlEatLoopStateNames();
        string[] endNames = GetCatBowlEndStateNames();
        int loopStateHash = useDrinkLoop ? drinkLoopStateHash : eatLoopStateHash;
        string loopLabel = useDrinkLoop ? "Drink_loop" : "Eat_loop";

        yield return PlayCatBowlState(eatDrinkStartStateHash, "EatDrink_start", startNames, 0.28f);
        yield return PlayCatBowlLoop(loopStateHash, loopLabel, loopNames, loopDuration);
        yield return PlayCatBowlState(eatDrinkEndStateHash, "EatDrink_end", endNames, 0.28f);

        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = false;
        TryApplyStableIdleParameters();
        TryCrossFadeToStableIdle();
        ResumeMovementAfterOneShot();
    }

    private IEnumerator PlayCatBowlState(int stateHash, string stateLabel, string[] clipNames, float fallbackDuration)
    {
        if (stateHash != 0)
        {
            TryCrossFadeState(stateHash);
            yield return null;
            yield return new WaitForSeconds(ResolveActiveOneShotDuration(fallbackDuration));
            yield break;
        }

#if UNITY_EDITOR
        AnimationClip clip = ResolveEditorClipByNames(stateLabel, clipNames);
        if (clip != null)
        {
            yield return PlayDirectEditorClip(clip, Mathf.Max(0.2f, clip.length * 0.95f), false, "Cat" + stateLabel);
            yield break;
        }
#endif

        LogAnimationMessageOnce(
            "missing-cat-bowl-state-" + stateLabel,
            "PawPal cat animation audit: could not resolve cat bowl animation '"
            + stateLabel
            + "' for '"
            + DisplayName
            + "'. Waiting without that transition.");
        yield return new WaitForSeconds(Mathf.Max(0.05f, fallbackDuration));
    }

    private IEnumerator PlayCatBowlLoop(int stateHash, string stateLabel, string[] clipNames, float duration)
    {
        float targetDuration = Mathf.Max(0.05f, duration);
        if (stateHash != 0)
        {
            TryCrossFadeState(stateHash);
            yield return new WaitForSeconds(targetDuration);
            yield break;
        }

#if UNITY_EDITOR
        AnimationClip clip = ResolveEditorClipByNames(stateLabel, clipNames);
        if (clip != null)
        {
            yield return PlayDirectEditorClip(clip, targetDuration, true, "Cat" + stateLabel);
            yield break;
        }
#endif

        LogAnimationMessageOnce(
            "missing-cat-bowl-loop-" + stateLabel,
            "PawPal cat animation audit: could not resolve cat bowl loop '"
            + stateLabel
            + "' for '"
            + DisplayName
            + "'. Waiting without the loop animation.");
        yield return new WaitForSeconds(targetDuration);
    }

    private float ResolveActiveOneShotDuration(float fallbackDuration)
    {
        if (animator == null)
        {
            return Mathf.Max(0.1f, fallbackDuration);
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.loop || state.length <= 0.01f)
        {
            return Mathf.Max(0.1f, fallbackDuration);
        }

        return Mathf.Max(0.1f, Mathf.Min(fallbackDuration, state.length + 0.02f));
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
        StopMovementForOneShot();
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
        ResumeMovementAfterOneShot();
    }

    private IEnumerator SitPoseRoutine(float duration, bool holdPoseAfter)
    {
        isPlayingOneShotAnimation = true;
        StopMovementForOneShot();

        bool started = TryCrossFadeState(sitStartStateHash)
            || TryCrossFadeState(sitLoopStateHash)
            || TryCrossFadeState(idle2StateHash);

        if (!started && animationSet != null)
        {
            animationSet.TryPlaySelectedReaction(animator);
        }

        yield return null;

        float totalDuration = Mathf.Max(0.3f, duration);
        float startDuration = started && sitStartStateHash != 0 ? Mathf.Min(SitStartDuration, totalDuration * 0.35f) : 0f;
        if (startDuration > 0f)
        {
            yield return new WaitForSeconds(startDuration);
        }

        if (TryCrossFadeState(sitLoopStateHash))
        {
            float loopDuration = Mathf.Max(0.1f, totalDuration - startDuration - (sitEndStateHash != 0 ? SitEndDuration : 0f));
            yield return new WaitForSeconds(loopDuration);
        }
        else
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, ResolveActiveOneShotDuration(totalDuration)));
        }

        if (!holdPoseAfter && sitEndStateHash != 0)
        {
            TryCrossFadeState(sitEndStateHash);
            yield return new WaitForSeconds(SitEndDuration);
        }

        isPlayingOneShotAnimation = false;
        ResumeMovementAfterOneShot();
    }

    private IEnumerator HopRoutine()
    {
        isPlayingOneShotAnimation = true;
        StopMovementForOneShot();

        if (TryCrossFadeState(jumpPlaceStateHash))
        {
            yield return null;
            yield return new WaitForSeconds(ResolveActiveOneShotDuration(0.55f));
        }
        else
        {
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
        }

        isPlayingOneShotAnimation = false;
        ResumeMovementAfterOneShot();
    }

    private void PickNextDestination(bool immediate)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (!immediate)
        {
            waitingForRoamDestination = true;
            waitUntil = Time.time + Random.Range(minRoamWait, maxRoamWait);
            StopAgentMovement();
            return;
        }

        waitingForRoamDestination = false;
        EnsureAgentMoving();
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
        waitUntil = 0f;
    }

    private void CacheAnimationStateHashes()
    {
        barkStateHash = ResolveStateHash("Bark", "ProxBark");
        neutralIdleStateHash = ResolveStateHash("CatSimple_Idle_1", "Arm_Cat|Idle_1", "Idle_1", "Idle1");
        idle2StateHash = ResolveStateHash("CatSimple_Idle_2", "Arm_Cat|Idle_2", "Idle_2", "Idle2");
        sitStartStateHash = ResolveStateHash("CatSimple_Sit_start", "Arm_Cat|Sit_start", "Sit_start", "Sit start");
        sitLoopStateHash = ResolveStateHash("CatSimple_Sit_loop_1", "Arm_Cat|Sit_loop_1", "Sit_loop_1", "Sit loop", "Sit");
        sitEndStateHash = ResolveStateHash("CatSimple_Sit_end", "Arm_Cat|Sit_end", "Sit_end", "Sit end");
        lieStateHash = ResolveStateHash("CatSimple_Lie_belly_loop_1", "Arm_Cat|Lie_belly_loop_1", "Lie_belly_loop_1", "Lie");
        scratchingStateHash = ResolveStateHash("CatSimple_Scratching", "Arm_Cat|Scratching", "Scratching");
        eatDrinkStartStateHash = ResolveStateHash(GetCatBowlStartStateNames());
        eatLoopStateHash = ResolveStateHash(GetCatBowlEatLoopStateNames());
        drinkLoopStateHash = ResolveStateHash(GetCatBowlDrinkLoopStateNames());
        eatDrinkEndStateHash = ResolveStateHash(GetCatBowlEndStateNames());
        jumpPlaceStateHash = ResolveStateHash("CatSimple_JumpPlace_RM", "Arm_Cat|JumpPlace_RM");
        locomotionStateHash = ResolveStateHash("Locomotion");
        walkStateHash = ResolveStateHash(GetCatLocomotionStateNames(DogMovementPace.Walk));
        trotStateHash = ResolveStateHash(GetCatLocomotionStateNames(DogMovementPace.Trot));
        runStateHash = ResolveStateHash(GetCatLocomotionStateNames(DogMovementPace.Run));
        if (sessionData != null && animator != null && animator.runtimeAnimatorController != null)
        {
            LogAnimationAuditOnce();
        }
    }

    private bool TryCrossFadeState(int stateHash)
    {
        if (animator == null || stateHash == 0)
        {
            return false;
        }

        StopDirectLocomotionPlayback();
        activeLocomotionStateHash = 0;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(stateHash, 0.08f, 0, 0f);
        return true;
    }

    private int ResolveStateHash(params string[] stateNames)
    {
        if (animator == null || stateNames == null || animator.runtimeAnimatorController == null || animator.layerCount <= 0)
        {
            return 0;
        }

        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrEmpty(stateName))
            {
                continue;
            }

            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash))
            {
                return hash;
            }

            string layerName = animator.GetLayerName(0);
            string[] candidates =
            {
                layerName + "." + stateName,
                "Base Layer." + stateName,
                layerName + ".SitSM." + stateName,
                "Base Layer.SitSM." + stateName,
                layerName + ".IdleSM." + stateName,
                "Base Layer.IdleSM." + stateName,
                layerName + ".IdleSM.SitSM." + stateName,
                "Base Layer.IdleSM.SitSM." + stateName
            };

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                hash = Animator.StringToHash(candidates[candidateIndex]);
                if (animator.HasState(0, hash))
                {
                    return hash;
                }
            }
        }

        return 0;
    }

    private void SetMovePace(DogMovementPace pace)
    {
        currentPace = pace;
        currentMoveSpeed = GetAgentSpeed(pace);

        if (agent != null)
        {
            agent.speed = currentMoveSpeed;
        }
    }

    private Transform FindToyTarget()
    {
        PawPalToyRuntimeMetadata.AutoRegisterSceneLargeToys();
        HashSet<Transform> unique = new HashSet<Transform>();
        List<Transform> candidates = new List<Transform>();

        PawPalToyRuntimeMetadata[] runtimeToys = FindObjectsByType<PawPalToyRuntimeMetadata>(FindObjectsSortMode.None);
        for (int i = 0; i < runtimeToys.Length; i++)
        {
            AddToyCandidate(runtimeToys[i] != null ? runtimeToys[i].transform : null, unique, candidates);
        }

        IntroPetToyAnchor[] introToys = FindObjectsByType<IntroPetToyAnchor>(FindObjectsSortMode.None);
        for (int i = 0; i < introToys.Length; i++)
        {
            AddToyCandidate(introToys[i] != null ? introToys[i].transform : null, unique, candidates);
        }

        try
        {
            GameObject[] taggedToys = GameObject.FindGameObjectsWithTag("Toy");
            for (int i = 0; i < taggedToys.Length; i++)
            {
                AddToyCandidate(taggedToys[i] != null ? taggedToys[i].transform : null, unique, candidates);
            }
        }
        catch (UnityException)
        {
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            AddToyCandidate(sceneTransforms[i], unique, candidates);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void AddToyCandidate(Transform candidate, HashSet<Transform> unique, List<Transform> candidates)
    {
        Transform toyRoot = ResolvePickupToyRoot(candidate);
        if (!IsPickupToyCandidate(toyRoot) || unique == null || candidates == null || !unique.Add(toyRoot))
        {
            return;
        }

        if (!CanReachPickupToyCandidate(toyRoot))
        {
            return;
        }

        candidates.Add(toyRoot);
    }

    private Transform ResolvePickupToyRoot(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        PawPalToyRuntimeMetadata metadata = candidate.GetComponentInParent<PawPalToyRuntimeMetadata>();
        if (metadata != null)
        {
            return metadata.transform;
        }

        IntroPetToyAnchor introAnchor = candidate.GetComponentInParent<IntroPetToyAnchor>();
        if (introAnchor != null)
        {
            return introAnchor.transform;
        }

        Transform current = candidate;
        Transform bestNamedToyRoot = IsToyNamed(current.name) ? current : null;
        while (current.parent != null
            && current.parent.GetComponentInParent<DogRoomAgent>() == null
            && current.parent.GetComponentInParent<PawPalCatRoomAgent>() == null)
        {
            current = current.parent;
            if (IsToyNamed(current.name))
            {
                bestNamedToyRoot = current;
            }
        }

        return bestNamedToyRoot != null ? bestNamedToyRoot : candidate;
    }

    private bool IsPickupToyCandidate(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (candidate == transform || candidate.IsChildOf(transform))
        {
            return false;
        }

        if (candidate.GetComponentInParent<DogRoomAgent>() != null
            || candidate.GetComponentInParent<PawPalCatRoomAgent>() != null
            || candidate.GetComponentInParent<PawPalPlayerHeldToyMarker>() != null
            || ClaimedToyTransforms.Contains(candidate))
        {
            return false;
        }

        PawPalToyRuntimeMetadata metadata = candidate.GetComponentInParent<PawPalToyRuntimeMetadata>();
        if (metadata != null && metadata.InteractionMode != PawPalToyInteractionMode.CarryInMouth)
        {
            return false;
        }

        if (candidate.GetComponentInChildren<Renderer>(true) == null)
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        if (lowerName.Contains("ballhole") || lowerName.Contains("toyterrier"))
        {
            return false;
        }

        if (candidate.GetComponentInParent<IntroPetToyAnchor>() != null || HasToyTag(candidate.gameObject))
        {
            return true;
        }

        return IsToyNamed(lowerName);
    }

    private static bool HasToyTag(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        try
        {
            return candidate.CompareTag("Toy");
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private bool IsToyNamed(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        string lowerName = objectName.ToLowerInvariant();
        if (PawPalToyRuntimeMetadata.IsPawHitRollToyName(lowerName))
        {
            return false;
        }

        for (int i = 0; i < toyNameKeywords.Length; i++)
        {
            string keyword = toyNameKeywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) && lowerName.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanReachPickupToyCandidate(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Vector3 point;
        return TryGetReachableRoomPoint(GetDesiredToyPickupRootPosition(candidate), sampleRadius, out point);
    }

    private bool TryClaimToy(Transform toy)
    {
        if (toy == null || ClaimedToyTransforms.Contains(toy))
        {
            return false;
        }

        ClaimedToyTransforms.Add(toy);
        claimedToy = toy;
        return true;
    }

    private void ReleaseClaimedToy()
    {
        if (claimedToy != null)
        {
            ClaimedToyTransforms.Remove(claimedToy);
            claimedToy = null;
        }
    }

    private IEnumerator PlayPickupToy(GameObject toy)
    {
        if (HasHeldToy || toy == null)
        {
            yield break;
        }

        isPlayingOneShotAnimation = true;
        StopMovementForOneShot();
        TryPlayExplicitStateForLabel("Toy");

        yield return new WaitForSeconds(Mathf.Max(0f, toyPickupAttachDelay));
        if (toy == null)
        {
            isPlayingOneShotAnimation = false;
            yield break;
        }

        if (AttachToy(toy))
        {
            activeToyTarget = toy.transform;
        }

        float remainingDuration = Mathf.Max(0.05f, toyPickupAnimationDuration - Mathf.Max(0f, toyPickupAttachDelay));
        yield return new WaitForSeconds(remainingDuration);
        isPlayingOneShotAnimation = false;
        ResumeMovementAfterOneShot();
    }

    private bool AttachToy(GameObject toy)
    {
        if (toyAttach == null)
        {
            toyAttach = GetComponent<ToyAttach>();
        }

        if (toyAttach == null)
        {
            toyAttach = gameObject.AddComponent<ToyAttach>();
        }

        if (toyAttach == null || toy == null || !toyAttach.TryAttachToy(toy))
        {
            return false;
        }

        heldToy = toy;
        PlayToyPickupAudio(toy.transform.position);
        return true;
    }

    private IEnumerator PlayPutDownToy()
    {
        if (!HasHeldToy)
        {
            yield break;
        }

        isPlayingOneShotAnimation = true;
        StopMovementForOneShot();
        TryPlayExplicitStateForLabel("Toy");

        yield return new WaitForSeconds(Mathf.Max(0f, toyPutDownReleaseDelay));
        DropHeldToyImmediately();

        float remainingDuration = Mathf.Max(0.05f, toyPutDownAnimationDuration - Mathf.Max(0f, toyPutDownReleaseDelay));
        yield return new WaitForSeconds(remainingDuration);
        isPlayingOneShotAnimation = false;
        ResumeMovementAfterOneShot();
    }

    private void DropHeldToyImmediately()
    {
        if (heldToy == null && (toyAttach == null || !toyAttach.HasToy))
        {
            activeToyTarget = null;
            return;
        }

        Vector3 dropPosition = GetToyDropPosition();
        Quaternion dropRotation = GetHeldToyRotation();
        if (toyAttach != null && toyAttach.HasToy)
        {
            GameObject releasedToy = toyAttach.ReleaseToy(dropPosition, dropRotation);
            if (releasedToy != null)
            {
                heldToy = releasedToy;
            }
        }
        else if (heldToy != null)
        {
            heldToy.transform.SetParent(null, true);
            heldToy.transform.position = dropPosition;
            heldToy.transform.rotation = dropRotation;
        }

        heldToy = null;
        activeToyTarget = null;
        ReleaseClaimedToy();
    }

    private Vector3 GetDesiredToyPickupRootPosition(Transform toy)
    {
        Vector3 interactionOrigin = GetToyInteractionOrigin();
        Vector3 pickupPoint = GetToyPickupReferencePosition(toy);
        Vector3 interactionOffset = interactionOrigin - transform.position;
        interactionOffset.y = 0f;
        Vector3 desiredRootPosition = pickupPoint - interactionOffset;
        desiredRootPosition.y = transform.position.y;
        return desiredRootPosition;
    }

    private Vector3 GetToyInteractionOrigin()
    {
        if (toyAttach != null)
        {
            return toyAttach.GetAnchorWorldPosition();
        }

        return transform.position + transform.forward * Mathf.Max(0.1f, stoppingDistance);
    }

    private Vector3 GetToyPickupReferencePosition(Transform toy)
    {
        if (toy == null)
        {
            return transform.position;
        }

        Bounds bounds;
        return TryGetToyWorldBounds(toy, out bounds) ? bounds.center : toy.position;
    }

    private bool TryGetToyWorldBounds(Transform toy, out Bounds bounds)
    {
        bounds = new Bounds(toy != null ? toy.position : transform.position, Vector3.zero);
        if (toy == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = toy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
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

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = toy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private Vector3 GetToyDropPosition()
    {
        Vector3 desiredDropOrigin = GetHeldToyWorldPosition();
        desiredDropOrigin = ClampToRoom(desiredDropOrigin);
        Vector3 rayOrigin = desiredDropOrigin + Vector3.up * Mathf.Max(0.5f, sampleRadius);
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            sampleRadius + 2f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        Vector3 bestPoint = desiredDropOrigin;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null
                || hitCollider.transform.IsChildOf(transform)
                || (heldToy != null && hitCollider.transform.IsChildOf(heldToy.transform))
                || hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestPoint = hit.point;
        }

        return bestDistance < float.PositiveInfinity ? bestPoint : desiredDropOrigin;
    }

    private Vector3 GetHeldToyWorldPosition()
    {
        if (toyAttach != null && toyAttach.HasToy && toyAttach.CurrentToy != null)
        {
            return toyAttach.CurrentToy.transform.position;
        }

        if (heldToy != null)
        {
            return heldToy.transform.position;
        }

        return transform.position
            + transform.forward * toyDropOffset.z
            + transform.right * toyDropOffset.x
            + Vector3.up * toyDropOffset.y;
    }

    private Quaternion GetHeldToyRotation()
    {
        if (toyAttach != null && toyAttach.HasToy && toyAttach.CurrentToy != null)
        {
            return toyAttach.CurrentToy.transform.rotation;
        }

        if (heldToy != null)
        {
            return heldToy.transform.rotation;
        }

        return Quaternion.LookRotation(transform.forward, Vector3.up);
    }

    private void PlayToyPickupAudio(Vector3 position)
    {
        float volume = PawPalAudioSettings.ApplySoundEffectsVolume(toyPickupVolume);
        if (toyPickupClip == null || volume <= 0f)
        {
            return;
        }

        GameObject audioObject = new GameObject(name + "_CatToyPickupAudio");
        audioObject.hideFlags = HideFlags.DontSave;
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.25f;
        source.maxDistance = 6f;
        source.PlayOneShot(toyPickupClip, volume);
        Destroy(audioObject, toyPickupClip.length + 0.1f);
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

    private bool PlayVocalAudio()
    {
        if (vocalClip == null)
        {
            return false;
        }

        if (Time.time < nextAllowedVocalAudioTime)
        {
            return false;
        }

        float volume = PawPalAudioSettings.ApplySoundEffectsVolume(vocalVolume);
        if (volume <= 0f)
        {
            return false;
        }

        GameObject audioObject = new GameObject(name + "_CatVocalAudio");
        audioObject.transform.SetParent(transform, false);
        audioObject.transform.localPosition = Vector3.zero;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.PlayOneShot(vocalClip, 1f);
        nextAllowedVocalAudioTime = Time.time + GetAmbientVocalCooldown();

        Destroy(audioObject, vocalClip.length + 0.1f);
        return true;
    }

    private float GetAmbientVocalCooldown()
    {
        return Mathf.Max(MinimumAmbientVocalCooldown, ambientVocalCooldown);
    }

    private void FaceSteering()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
        {
            return;
        }

        Vector3 desiredDirection = Vector3.zero;
        if (agent.hasPath)
        {
            Vector3 steeringOffset = agent.steeringTarget - transform.position;
            steeringOffset.y = 0f;
            if (steeringOffset.sqrMagnitude > 0.0004f)
            {
                desiredDirection = steeringOffset.normalized;
            }
        }

        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            desiredDirection = velocity.normalized;
        }

        FacePosition(transform.position + desiredDirection);
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

    private bool ShouldUseLocomotion()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
        {
            return false;
        }

        if (agent.pathPending)
        {
            return activeLocomotionStateHash != 0;
        }

        if (!agent.hasPath || float.IsInfinity(agent.remainingDistance))
        {
            return false;
        }

        return agent.remainingDistance > Mathf.Max(agent.stoppingDistance, destinationReachedDistance);
    }

    private void SetMoving(bool moving)
    {
        if (!moving && (trainingBusy || isPlayingOneShotAnimation))
        {
            StopDirectLocomotionPlayback();
            activeLocomotionStateHash = 0;
            if (animator != null)
            {
                animator.speed = 1f;
            }

            return;
        }

        if (moving && TryPlayLocomotionForCurrentPace())
        {
            if (animator != null)
            {
                animator.speed = 1f;
            }

            return;
        }

        if (!moving)
        {
            StopDirectLocomotionPlayback();
            activeLocomotionStateHash = 0;
            bool appliedIdleParameters = TryApplyStableIdleParameters();
            bool playedStableIdle = TryCrossFadeToStableIdle();

#if UNITY_EDITOR
            if (!playedStableIdle && ShouldPreferDirectClipLocomotion() && TryPlayDirectIdleClip())
            {
                if (animator != null)
                {
                    animator.speed = 1f;
                }

                activeLocomotionStateHash = 0;
                return;
            }
#endif

            if (appliedIdleParameters || playedStableIdle)
            {
                if (animator != null)
                {
                    animator.speed = 1f;
                }

                return;
            }
        }

        if (animationSet != null && animationSet.TrySetMoving(animator, moving, moving ? Mathf.Max(0f, currentMoveSpeed) : 0f))
        {
            if (animator != null)
            {
                animator.speed = moving ? 1f : 0.85f;
            }

            return;
        }

        if (animator != null)
        {
            animator.speed = moving ? 1f : 0.85f;
        }
    }

    private bool TryPlayLocomotionForCurrentPace()
    {
        if (animator == null)
        {
            return false;
        }

#if UNITY_EDITOR
        if (ShouldPreferDirectClipLocomotion() && TryPlayDirectLocomotionClip(currentPace))
        {
            if (animationSet != null)
            {
                animationSet.TrySetMoving(animator, true, Mathf.Max(0f, currentMoveSpeed));
            }

            activeLocomotionStateHash = 0;
            return true;
        }
#endif

        int stateHash = GetLocomotionStateHash(currentPace);
        if (stateHash != 0)
        {
            StopDirectLocomotionPlayback();
            if (animationSet != null)
            {
                animationSet.TrySetMoving(animator, true, Mathf.Max(0f, currentMoveSpeed));
            }

            if (activeLocomotionStateHash != stateHash)
            {
                animator.CrossFadeInFixedTime(stateHash, 0.08f, 0, 0f);
                activeLocomotionStateHash = stateHash;
            }

            return true;
        }

#if UNITY_EDITOR
        if (TryPlayDirectLocomotionClip(currentPace))
        {
            if (animationSet != null)
            {
                animationSet.TrySetMoving(animator, true, Mathf.Max(0f, currentMoveSpeed));
            }

            activeLocomotionStateHash = 0;
            return true;
        }
#endif

        if (locomotionStateHash != 0)
        {
            LogAnimationMessageOnce(
                "ignored-generic-locomotion",
                "PawPal cat animation audit: ignoring generic 'Locomotion' state on '"
                + DisplayName
                + "' because the copied cat controller's blend tree is not trusted for cat locomotion.");
        }

        LogAnimationMessageOnce(
            "missing-locomotion-" + currentPace,
            "PawPal cat animation failed to resolve locomotion for '"
            + DisplayName
            + "' at pace "
            + currentPace
            + ". Controller='"
            + GetControllerName()
            + "', walk="
            + (walkStateHash != 0)
            + ", trot="
            + (trotStateHash != 0)
            + ", run="
            + (runStateHash != 0)
            + ".");

        return false;
    }

    private bool TryApplyStableIdleParameters()
    {
        if (animator == null)
        {
            return false;
        }

        bool applied = false;
        if (animationSet != null && animationSet.TrySetMoving(animator, false, 0f))
        {
            applied = true;
        }

        if (TrySetAnimatorBool("Move", false))
        {
            applied = true;
        }

        if (TrySetAnimatorFloat("Speed", 0f))
        {
            applied = true;
        }

        if (TrySetAnimatorFloat("Direction", 0f))
        {
            applied = true;
        }

        if (TrySetAnimatorInteger("IdleIndex", 0))
        {
            applied = true;
        }

        return applied;
    }

    private bool TryCrossFadeToStableIdle()
    {
        int stableIdleStateHash = neutralIdleStateHash != 0 ? neutralIdleStateHash : idle2StateHash;
        if (animator == null || stableIdleStateHash == 0)
        {
            return false;
        }

        if (IsAnimatorInOrTransitioningTo(stableIdleStateHash))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(stableIdleStateHash, 0.08f, 0, 0f);
        return true;
    }

    private bool IsAnimatorInOrTransitioningTo(int stateHash)
    {
        if (animator == null || stateHash == 0)
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.fullPathHash == stateHash || currentState.shortNameHash == stateHash)
        {
            return true;
        }

        if (!animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
        return nextState.fullPathHash == stateHash || nextState.shortNameHash == stateHash;
    }

    private bool TrySetAnimatorBool(string parameterName, bool value)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            return false;
        }

        animator.SetBool(parameterName, value);
        return true;
    }

    private bool TrySetAnimatorFloat(string parameterName, float value)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
        {
            return false;
        }

        animator.SetFloat(parameterName, value);
        return true;
    }

    private bool TrySetAnimatorInteger(string parameterName, int value)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Int))
        {
            return false;
        }

        animator.SetInteger(parameterName, value);
        return true;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void StopMovementForOneShot()
    {
        StopDirectLocomotionPlayback();
        activeLocomotionStateHash = 0;

        if (agent == null)
        {
            return;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }
    }

    private void ResumeMovementAfterOneShot()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    private int GetLocomotionStateHash(DogMovementPace pace)
    {
        switch (pace)
        {
            case DogMovementPace.Run:
                return runStateHash;
            case DogMovementPace.Trot:
                return trotStateHash;
            default:
                return walkStateHash;
        }
    }

    private static string[] GetCatLocomotionStateNames(DogMovementPace pace)
    {
        switch (pace)
        {
            case DogMovementPace.Run:
                return new[] { "CatSimple_Run_F_RM", "Arm_Cat|Run_F_RM", "Run_F_RM" };
            case DogMovementPace.Trot:
                return new[] { "CatSimple_Trot_F_RM", "Arm_Cat|Trot_F_RM", "Trot_F_RM" };
            default:
                return new[] { "CatSimple_Walk_F_RM", "Arm_Cat|Walk_F_RM", "Walk_F_RM" };
        }
    }

    private static string[] GetCatBowlStartStateNames()
    {
        return new[] { "CatSimple_EatDrink_start", "Arm_Cat|EatDrink_start", "Arm_Kitten|EatDrink_start", "EatDrink_start" };
    }

    private static string[] GetCatBowlEatLoopStateNames()
    {
        return new[] { "CatSimple_Eating", "Arm_Cat|Eating", "Arm_Kitten|Eating", "Eating", "CatSimple_Eat_loop", "Arm_Cat|Eat_loop", "Arm_Kitten|Eat_loop", "Eat_loop" };
    }

    private static string[] GetCatBowlDrinkLoopStateNames()
    {
        return new[] { "CatSimple_Drinking", "Arm_Cat|Drinking", "Arm_Kitten|Drinking", "Drinking", "CatSimple_Drink_loop", "Arm_Cat|Drink_loop", "Arm_Kitten|Drink_loop", "Drink_loop" };
    }

    private static string[] GetCatBowlEndStateNames()
    {
        return new[] { "CatSimple_EatDrink_end", "Arm_Cat|EatDrink_end", "Arm_Kitten|EatDrink_end", "EatDrink_end" };
    }

#if UNITY_EDITOR
    private IEnumerator PlayDirectEditorClip(AnimationClip clip, float duration, bool loop, string graphLabel)
    {
        if (clip == null || animator == null)
        {
            yield break;
        }

        StopDirectLocomotionPlayback();

        directLocomotionGraph = PlayableGraph.Create(name + "_" + graphLabel);
        directLocomotionGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        PlayableGraph ownedGraph = directLocomotionGraph;
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(directLocomotionGraph, graphLabel, animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(directLocomotionGraph, clip);
        playable.SetApplyFootIK(true);
        playable.SetDuration(loop ? double.PositiveInfinity : clip.length);
        playable.SetDone(false);
        playable.SetSpeed(1d);
        output.SetSourcePlayable(playable);
        directLocomotionGraph.Play();
        activeDirectLocomotionPlayable = playable;
        activeDirectLocomotionClip = clip;

        float elapsed = 0f;
        float targetDuration = Mathf.Max(0.05f, duration);
        while (elapsed < targetDuration)
        {
            if (!ownedGraph.IsValid() || !playable.IsValid())
            {
                yield break;
            }

            if (loop && clip.length > 0.001f && playable.GetTime() >= clip.length)
            {
                playable.SetTime(0d);
                playable.SetDone(false);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (directLocomotionGraph.Equals(ownedGraph))
        {
            StopDirectLocomotionPlayback();
        }
    }

    private bool TryPlayDirectLocomotionClip(DogMovementPace pace)
    {
        AnimationClip clip = ResolveEditorLocomotionClip(pace);
        if (clip == null)
        {
            StopDirectLocomotionPlayback();
            return false;
        }

        return TryPlayDirectLoopingClip(clip, "CatLocomotion");
    }

    private bool TryPlayDirectIdleClip()
    {
        AnimationClip clip = ResolveEditorIdleClip();
        if (clip == null)
        {
            StopDirectLocomotionPlayback();
            return false;
        }

        return TryPlayDirectLoopingClip(clip, "CatIdle");
    }

    private bool TryPlayDirectLoopingClip(AnimationClip clip, string graphLabel)
    {
        if (clip == null)
        {
            return false;
        }

        if (directLocomotionGraph.IsValid() && activeDirectLocomotionClip == clip)
        {
            KeepDirectLocomotionClipLooping();
            return true;
        }

        StopDirectLocomotionPlayback();

        directLocomotionGraph = PlayableGraph.Create(name + "_" + graphLabel);
        directLocomotionGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(directLocomotionGraph, graphLabel, animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(directLocomotionGraph, clip);
        playable.SetApplyFootIK(true);
        playable.SetDuration(double.PositiveInfinity);
        playable.SetDone(false);
        playable.SetSpeed(1d);
        output.SetSourcePlayable(playable);
        directLocomotionGraph.Play();
        activeDirectLocomotionPlayable = playable;
        activeDirectLocomotionClip = clip;
        return true;
    }

    private bool ShouldPreferDirectClipLocomotion()
    {
        return animator != null && animator.avatar == null;
    }

    private void KeepDirectLocomotionClipLooping()
    {
        if (activeDirectLocomotionClip == null || !activeDirectLocomotionPlayable.IsValid())
        {
            return;
        }

        float clipLength = activeDirectLocomotionClip.length;
        if (clipLength <= 0.01f)
        {
            return;
        }

        double currentTime = activeDirectLocomotionPlayable.GetTime();
        if (currentTime < clipLength - 0.02f)
        {
            return;
        }

        activeDirectLocomotionPlayable.SetTime(currentTime % clipLength);
        activeDirectLocomotionPlayable.SetDone(false);
    }

    private AnimationClip ResolveEditorLocomotionClip(DogMovementPace pace)
    {
        Dictionary<string, AnimationClip> clips = ResolveEditorClipDictionary(pace.ToString());
        if (clips == null || clips.Count == 0)
        {
            return null;
        }

        string[] clipNames = GetCatLocomotionStateNames(pace);
        for (int i = 0; i < clipNames.Length; i++)
        {
            AnimationClip clip;
            if (!string.IsNullOrEmpty(clipNames[i]) && clips.TryGetValue(clipNames[i], out clip) && clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private AnimationClip ResolveEditorIdleClip()
    {
        Dictionary<string, AnimationClip> clips = ResolveEditorClipDictionary("idle");
        if (clips == null || clips.Count == 0)
        {
            return null;
        }

        string[] idleClipNames =
        {
            "CatSimple_Idle_2",
            "Arm_Cat|Idle_2",
            "Idle_2",
            "CatSimple_Idle_1",
            "Arm_Cat|Idle_1",
            "Idle_1"
        };

        for (int i = 0; i < idleClipNames.Length; i++)
        {
            AnimationClip clip;
            if (!string.IsNullOrEmpty(idleClipNames[i]) && clips.TryGetValue(idleClipNames[i], out clip) && clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private AnimationClip ResolveEditorClipByNames(string purpose, string[] clipNames)
    {
        Dictionary<string, AnimationClip> clips = ResolveEditorClipDictionary(purpose);
        if (clips == null || clips.Count == 0 || clipNames == null || clipNames.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < clipNames.Length; i++)
        {
            AnimationClip clip;
            if (!string.IsNullOrEmpty(clipNames[i]) && clips.TryGetValue(clipNames[i], out clip) && clip != null)
            {
                return clip;
            }
        }

        foreach (KeyValuePair<string, AnimationClip> pair in clips)
        {
            if (pair.Value == null)
            {
                continue;
            }

            for (int i = 0; i < clipNames.Length; i++)
            {
                string candidate = clipNames[i];
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                if (pair.Key.EndsWith(candidate, StringComparison.OrdinalIgnoreCase)
                    || pair.Key.EndsWith("|" + candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }
        }

        return null;
    }

    private Dictionary<string, AnimationClip> ResolveEditorClipDictionary(string purpose)
    {
        PawPalPetAnimationEntry entry;
        if (!PawPalPetAnimationRegistry.TryResolveEntry(
            animator,
            sessionData != null ? sessionData.Definition : null,
            sessionData != null ? sessionData.BreedName : null,
            name,
            out entry))
        {
            LogAnimationMessageOnce(
                "unresolved-entry-" + purpose,
                "PawPal cat animation audit: could not resolve a registry entry for '"
                + DisplayName
                + "' while resolving "
                + purpose
                + " clip playback.");
            return null;
        }

        Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
        if (clips == null || clips.Count == 0)
        {
            LogAnimationMessageOnce(
                "missing-imported-clips-" + purpose,
                "PawPal cat animation audit: registry entry '"
                + entry.CanonicalKey
                + "' has no imported clips available for '"
                + DisplayName
                + "'.");
            return null;
        }

        return clips;
    }
#endif

    private void StopDirectLocomotionPlayback()
    {
#if UNITY_EDITOR
        if (directLocomotionGraph.IsValid())
        {
            directLocomotionGraph.Destroy();
        }

        directLocomotionGraph = default;
        activeDirectLocomotionClip = null;
        activeDirectLocomotionPlayable = default;
#endif
    }

    private void TickMovementDebug(bool shouldMove, string phase)
    {
        if (animator == null)
        {
            return;
        }

        TrackAnimatorProgress();

        if (Time.time < nextMovementDebugLogAt || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        bool hasPathIntent = agent.hasPath
            && !agent.pathPending
            && !agent.isStopped
            && !float.IsInfinity(agent.remainingDistance)
            && agent.remainingDistance > Mathf.Max(agent.stoppingDistance, destinationReachedDistance);
        bool hasVelocityIntent = !agent.isStopped
            && (agent.velocity.sqrMagnitude > 0.0025f || agent.desiredVelocity.sqrMagnitude > 0.0025f);
        if (!hasPathIntent && !hasVelocityIntent)
        {
            return;
        }

        if (!shouldMove)
        {
            nextMovementDebugLogAt = Time.time + MovementDebugLogCooldown;
            Debug.LogWarning(
                "PawPal cat movement anomaly: locomotion suppressed while movement intent exists. pet='"
                + DisplayName
                + "', phase='"
                + phase
                + "', context='"
                + activeMovementContext
                + "', pace="
                + currentPace
                + ", remaining="
                + agent.remainingDistance.ToString("F2")
                + ", velocity="
                + agent.velocity.magnitude.ToString("F2")
                + ", desired="
                + agent.desiredVelocity.magnitude.ToString("F2")
                + ", stopped="
                + agent.isStopped
                + ", socialPaused="
                + socialPaused
                + ", trainingBusy="
                + trainingBusy
                + ", stateHash="
                + lastObservedAnimatorStateHash
                + ", normalizedTime="
                + lastObservedAnimatorNormalizedTime.ToString("F2")
                + ", controller='"
                + GetControllerName()
                + "', avatar='"
                + GetAvatarName()
                + "'.",
                this);
            return;
        }

        if (Time.time - lastObservedAnimatorProgressAt < AnimatorStallThreshold)
        {
            return;
        }

        nextMovementDebugLogAt = Time.time + MovementDebugLogCooldown;
        Debug.LogWarning(
            "PawPal cat movement anomaly: animator stalled while movement remained active. pet='"
            + DisplayName
            + "', phase='"
            + phase
            + "', context='"
            + activeMovementContext
            + "', pace="
            + currentPace
            + ", remaining="
            + agent.remainingDistance.ToString("F2")
            + ", velocity="
            + agent.velocity.magnitude.ToString("F2")
            + ", desired="
            + agent.desiredVelocity.magnitude.ToString("F2")
            + ", stopped="
            + agent.isStopped
            + ", socialPaused="
            + socialPaused
            + ", trainingBusy="
            + trainingBusy
            + ", stateHash="
            + lastObservedAnimatorStateHash
            + ", normalizedTime="
            + lastObservedAnimatorNormalizedTime.ToString("F2")
            + ", controller='"
            + GetControllerName()
            + "', avatar='"
            + GetAvatarName()
            + "'.",
            this);
    }

    private void TrackAnimatorProgress()
    {
        if (animator == null)
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        int stateHash = state.fullPathHash != 0 ? state.fullPathHash : state.shortNameHash;
        float normalizedTime = state.normalizedTime;
        bool stateChanged = stateHash != lastObservedAnimatorStateHash;
        bool timeAdvanced = Mathf.Abs(normalizedTime - lastObservedAnimatorNormalizedTime) > 0.0001f;
        if (stateChanged || timeAdvanced)
        {
            lastObservedAnimatorStateHash = stateHash;
            lastObservedAnimatorNormalizedTime = normalizedTime;
            lastObservedAnimatorProgressAt = Time.time;
        }
    }

    private void StopAgentMovement()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private void EnsureAgentMoving()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = false;
    }

    private void LogAnimationAuditOnce()
    {
        string definitionPetId = sessionData != null && sessionData.Definition != null
            ? sessionData.Definition.PetId
            : null;
        string[] candidateIds =
        {
            definitionPetId,
            sessionData != null ? sessionData.BreedName : null,
            name
        };

        PawPalPetAnimationEntry entry;
        bool hasEntry = PawPalPetAnimationRegistry.TryResolveEntryFromRawValues(out entry, candidateIds);

        string message = "PawPal cat animation audit: pet='"
            + DisplayName
            + "', controller='"
            + GetControllerName()
            + "', avatar='"
            + GetAvatarName()
            + "', initialized="
            + (animator != null && animator.isInitialized)
            + ", registryEntry='"
            + (hasEntry && entry != null ? entry.CanonicalKey : "<unresolved>")
            + "', explicitStates={walk:"
            + (walkStateHash != 0)
            + ", trot:"
            + (trotStateHash != 0)
            + ", run:"
            + (runStateHash != 0)
            + ", jump:"
            + (jumpPlaceStateHash != 0)
            + "}, genericLocomotion="
            + (locomotionStateHash != 0)
            + " (ignored for cats).";

#if UNITY_EDITOR
        if (hasEntry && entry != null)
        {
            Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
            int clipCount = clips != null ? clips.Count : 0;
            message += " Imported clips=" + clipCount + ".";
        }
#endif

        LogAnimationMessageOnce("startup-audit", message);
    }

    private void LogAnimationMessageOnce(string suffix, string message)
    {
        string key = (RuntimePetId + "|" + DisplayName + "|" + suffix).ToLowerInvariant();
        if (!LoggedAnimationMessages.Add(key))
        {
            return;
        }

        Debug.LogWarning(message, this);
    }

    private string GetControllerName()
    {
        return animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.name
            : "<none>";
    }

    private string GetAvatarName()
    {
        return animator != null && animator.avatar != null
            ? animator.avatar.name
            : "<none>";
    }

    private void RefreshMovementProfile()
    {
        movementProfile = PawPalPetMovementProfiles.Resolve(sessionData, name);
        hasMovementProfile = true;
        currentPace = DogMovementPace.Walk;
        currentMoveSpeed = movementProfile.GetSpeed(currentPace);
    }

    private PawPalPetMovementProfile ResolveMovementProfile()
    {
        if (!hasMovementProfile)
        {
            RefreshMovementProfile();
        }

        return movementProfile;
    }

    private float GetAgentSpeed(DogMovementPace pace)
    {
        float profileSpeed = ResolveMovementProfile().GetSpeed(pace);
        if (profileSpeed > 0f)
        {
            return profileSpeed;
        }

        switch (pace)
        {
            case DogMovementPace.Run:
                return runSpeed;
            case DogMovementPace.Trot:
                return trotSpeed;
            default:
                return walkSpeed;
        }
    }

    private float GetNavigationFootprintRadius()
    {
        if (cachedNavigationFootprintRadius > 0f)
        {
            return cachedNavigationFootprintRadius;
        }

        cachedNavigationFootprintRadius = PawPalPetNavigationAvoidance.EstimateMovementClearanceRadius(
            transform,
            agent,
            0.18f,
            0.01f,
            0.08f,
            0.18f);
        return cachedNavigationFootprintRadius;
    }

    private void ConfigureAgentNavigationRadius()
    {
        if (agent == null)
        {
            return;
        }

        cachedNavigationFootprintRadius = -1f;
        agent.radius = GetNavigationFootprintRadius();
    }

    private bool TryGetReachableRoomPoint(Vector3 candidate, float radius, out Vector3 point)
    {
        Vector3 clampedCandidate = ClampToRoom(candidate);
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(clampedCandidate, out hit, Mathf.Max(0.05f, radius), NavMesh.AllAreas))
        {
            point = clampedCandidate;
            return false;
        }

        point = hit.position;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return true;
        }

        NavMeshPath path = new NavMeshPath();
        return agent.CalculatePath(point, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private bool IsPointComfortable(Vector3 point, float radius)
    {
        return PawPalPetNavigationAvoidance.IsPointComfortable(
            transform,
            point,
            GetNavigationFootprintRadius(),
            Mathf.Max(0f, radius),
            true);
    }

    private bool IsPathBlockedByAnotherPet(Vector3 destination, float radius, bool includeSteeringTarget, out Transform blocker, out bool personalSpaceBlocked)
    {
        return PawPalPetNavigationAvoidance.IsPathBlocked(
            transform,
            agent,
            destination,
            GetNavigationFootprintRadius(),
            Mathf.Max(0f, radius),
            includeSteeringTarget,
            true,
            0.08f,
            0.02f,
            out blocker,
            out personalSpaceBlocked);
    }

    private bool TryFindRerouteAroundBlocker(Transform blocker, Vector3 destination, float radius, out Vector3 point)
    {
        if (blocker == null)
        {
            point = ClampToRoom(destination);
            return false;
        }

        Vector3 pathDirection = destination - transform.position;
        pathDirection.y = 0f;
        if (pathDirection.sqrMagnitude <= 0.0001f)
        {
            pathDirection = transform.forward;
            pathDirection.y = 0f;
        }

        if (pathDirection.sqrMagnitude <= 0.0001f)
        {
            pathDirection = Vector3.forward;
        }

        pathDirection.Normalize();
        Vector3 sideDirection = Vector3.Cross(Vector3.up, pathDirection);
        if (sideDirection.sqrMagnitude <= 0.0001f)
        {
            sideDirection = transform.right;
            sideDirection.y = 0f;
        }

        sideDirection.Normalize();
        float baseDistance = Mathf.Max(
            0.22f,
            GetNavigationFootprintRadius() + Mathf.Max(0f, radius) + 0.12f);
        float[] distanceMultipliers = { 1f, 1.5f, 2f, 2.75f };
        float[] forwardOffsets = { 0.06f, -0.06f, 0.18f };

        for (int distanceIndex = 0; distanceIndex < distanceMultipliers.Length; distanceIndex++)
        {
            float sideDistance = baseDistance * distanceMultipliers[distanceIndex];
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float sideSign = sideIndex == 0 ? 1f : -1f;
                for (int forwardIndex = 0; forwardIndex < forwardOffsets.Length; forwardIndex++)
                {
                    Vector3 candidate = blocker.position
                        + sideDirection * sideSign * sideDistance
                        + pathDirection * forwardOffsets[forwardIndex];
                    candidate = ClampToRoom(candidate);
                    if (TryGetReachableRoomPoint(candidate, sampleRadius, out point)
                        && IsPointComfortable(point, radius))
                    {
                        Transform nestedBlocker;
                        bool personalSpaceBlocked;
                        if (!IsPathBlockedByAnotherPet(point, radius, false, out nestedBlocker, out personalSpaceBlocked))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        point = ClampToRoom(destination);
        return false;
    }

    private bool TryFindNearbyComfortablePoint(Vector3 desiredPoint, float radius, out Vector3 point)
    {
        float searchDistance = Mathf.Max(
            PetAvoidanceRerouteSearchRadius,
            GetNavigationFootprintRadius() + Mathf.Max(0f, radius));
        int candidateCount = Mathf.Max(6, PetAvoidanceRerouteCandidateCount);
        float startAngle = Random.Range(0f, 360f);

        for (int i = 0; i < candidateCount; i++)
        {
            float angle = startAngle + (360f / candidateCount) * i;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * searchDistance;
            Vector3 candidate = ClampToRoom(desiredPoint + offset);
            if (TryGetReachableRoomPoint(candidate, sampleRadius, out point)
                && IsPointComfortable(point, radius))
            {
                Transform blocker;
                bool personalSpaceBlocked;
                if (!IsPathBlockedByAnotherPet(point, radius, false, out blocker, out personalSpaceBlocked))
                {
                    return true;
                }
            }
        }

        point = ClampToRoom(desiredPoint);
        return false;
    }

    private bool TryFindInteractionYieldPoint(Vector3 protectedDestination, Transform caller, out Vector3 point)
    {
        Vector3 awayFromCaller = transform.position - (caller != null ? caller.position : protectedDestination);
        awayFromCaller.y = 0f;
        if (awayFromCaller.sqrMagnitude <= 0.0001f)
        {
            awayFromCaller = transform.position - protectedDestination;
            awayFromCaller.y = 0f;
        }

        if (awayFromCaller.sqrMagnitude <= 0.0001f)
        {
            awayFromCaller = transform.right;
            awayFromCaller.y = 0f;
        }

        if (awayFromCaller.sqrMagnitude <= 0.0001f)
        {
            awayFromCaller = Vector3.right;
        }

        awayFromCaller.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, awayFromCaller);
        if (side.sqrMagnitude <= 0.0001f)
        {
            side = Vector3.forward;
        }

        side.Normalize();
        float baseDistance = Mathf.Max(0.32f, GetNavigationFootprintRadius() + PetAvoidancePadding + 0.18f);
        float[] forwardMultipliers = { 1f, 1.45f, 2f, 2.7f };
        float[] sideMultipliers = { 0f, 0.85f, -0.85f, 1.45f, -1.45f };

        for (int distanceIndex = 0; distanceIndex < forwardMultipliers.Length; distanceIndex++)
        {
            for (int sideIndex = 0; sideIndex < sideMultipliers.Length; sideIndex++)
            {
                Vector3 candidate = transform.position
                    + awayFromCaller * (baseDistance * forwardMultipliers[distanceIndex])
                    + side * (baseDistance * sideMultipliers[sideIndex]);
                candidate = ClampToRoom(candidate);
                if (!TryGetReachableRoomPoint(candidate, sampleRadius, out point)
                    || !IsPointComfortable(point, PetAvoidancePadding))
                {
                    continue;
                }

                if (PawPalPetNavigationAvoidance.DistanceToPlanarSegment(point, caller != null ? caller.position : transform.position, protectedDestination)
                    <= GetNavigationFootprintRadius() + PetAvoidancePadding)
                {
                    continue;
                }

                return true;
            }
        }

        return TryFindNearbyComfortablePoint(transform.position + awayFromCaller * baseDistance, PetAvoidancePadding, out point);
    }

    private bool TryRedirectAroundBlockingPet(ref Vector3 destination, float radius)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || Time.time < nextPetAvoidanceRepathAt)
        {
            return false;
        }

        Transform blocker;
        bool personalSpaceBlocked;
        if (!IsPathBlockedByAnotherPet(destination, radius, true, out blocker, out personalSpaceBlocked))
        {
            return false;
        }

        Vector3 reroutePoint;
        if (!TryFindRerouteAroundBlocker(blocker, destination, radius, out reroutePoint)
            && !TryFindNearbyComfortablePoint(destination, radius, out reroutePoint))
        {
            nextPetAvoidanceRepathAt = Time.time + PetAvoidanceRepathInterval;
            return false;
        }

        Vector3 delta = reroutePoint - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= 0.0025f)
        {
            nextPetAvoidanceRepathAt = Time.time + PetAvoidanceRepathInterval;
            return false;
        }

        if (!agent.SetDestination(reroutePoint))
        {
            nextPetAvoidanceRepathAt = Time.time + PetAvoidanceRepathInterval;
            return false;
        }

        destination = reroutePoint;
        nextPetAvoidanceRepathAt = Time.time + PetAvoidanceRepathInterval;
        return true;
    }

    private bool TryRequestInteractionPathYield(Transform blocker, Vector3 protectedDestination)
    {
        if (blocker == null)
        {
            return false;
        }

        DogRoomAgent dog = blocker.GetComponentInParent<DogRoomAgent>();
        if (dog != null)
        {
            return dog.TryYieldForPlayerInteractionPath(protectedDestination, transform);
        }

        PawPalCatRoomAgent cat = blocker.GetComponentInParent<PawPalCatRoomAgent>();
        return cat != null && cat != this && cat.TryYieldForPlayerInteractionPath(protectedDestination, transform);
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
