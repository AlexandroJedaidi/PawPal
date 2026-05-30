using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum DogMovementPace
{
    Walk,
    Trot,
    Run
}

internal enum DogAmbientActionType
{
    ShortIdle,
    Bark,
    ChillLie,
    SleepLie
}

internal enum DogRestFlavor
{
    Chill,
    Sleep
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class DogRoomAgent : MonoBehaviour
{
#if UNITY_EDITOR
    private const string EditorWalkAudioAssetPath = "Assets/Audio/dog_walk.mp3";
    private const string EditorBallBounceAudioAssetPath = "Assets/Audio/ball_bounce.mp3";
    private const string EditorToyPickupAudioAssetPath = "Assets/Audio/toy_pickup.mp3";
    private const string EditorSniffingAudioAssetPath = "Assets/Audio/sniffing.mp3";
    private const string EditorScratchAndShakingAudioAssetPath = "Assets/Audio/scratch-and-shaking.mp3";
    private const string EditorShakingX3AudioAssetPath = "Assets/Audio/shakingx3.mp3";
#endif
    private const int BaseLayerIndex = 0;
    private const int MovementIdleIndex = -1;
    private const int NeutralIdleIndex = 99;
    private const float BigBallHitImpulseScale = 0.75f;
    private const float BigBallHitTorqueScale = 0.75f;
    private static readonly HashSet<Transform> ClaimedToyTransforms = new HashSet<Transform>();
    private static readonly List<DogRoomAgent> ActiveAgents = new List<DogRoomAgent>();
    private static readonly Dictionary<DogRoomAgent, Vector3> ReservedDestinations = new Dictionary<DogRoomAgent, Vector3>();
    private static readonly string[] ImportedStandingIdleClipSuffixes =
    {
        "Idle_1",
        "Idle_2",
        "Idle_3",
        "Idle_4",
        "Idle_6",
        "Idle_7"
    };

    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly int SitEndTriggerHash = Animator.StringToHash("SitEndTrigger");
    private static readonly int PickupHash = Animator.StringToHash("Pickup");
    private static readonly int PutDownHash = Animator.StringToHash("PutDown");

    [Header("Room Roaming")]
    [SerializeField] private float roamRadius = 3.25f;
    [SerializeField] private float minRoamWait = 1.5f;
    [SerializeField] private float maxRoamWait = 4f;
    [SerializeField] private float sampleRadius = 1.5f;
    [SerializeField] private float destinationReachedDistance = 0.25f;

    [Header("Room Bounds")]
    [SerializeField] private bool restrictToRoomBounds = true;
    [SerializeField] private Vector3 roomBoundsCenter = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector2 roomBoundsSize = new Vector2(6.4f, 5.6f);
    [SerializeField] private float roomBoundsPadding = 0.25f;
    [SerializeField] private float outsideRoomRepathDistance = 0.05f;
    [SerializeField] private float navMeshRecoverySearchRadius = 4.5f;

    [Header("Movement")]
    [SerializeField] private float calmWalkSpeed = 0.38f;
    [SerializeField] private float trotSpeed = 0.65f;
    [SerializeField] private float runSpeed = 1.15f;
    [SerializeField] private float acceleration = 4.5f;
    [SerializeField] private float angularSpeed = 420f;
    [SerializeField] private float stoppingDistance = 0.15f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float animatorSpeedScale = 1f;
    [SerializeField] private float walkAnimatorSpeed = 0.5f;
    [SerializeField] private float trotAnimatorSpeed = 0.78f;
    [SerializeField] private float runAnimatorSpeed = 1f;
    [SerializeField] private float arrivalSlowdownDistance = 0.65f;
    [SerializeField] private float animatorSpeedDampTime = 0.16f;
    [SerializeField] private float animatorDirectionDampTime = 0.12f;
    [SerializeField] private float movingVelocityThreshold = 0.05f;
    [SerializeField] private float preMoveTurnSpeed = 180f;
    [SerializeField] private float preMoveMaxTurnTime = 1.1f;
    [SerializeField] private float preMoveTurnAngle = 8f;
    [SerializeField] private float preMovePauseDuration = 0.12f;
    [SerializeField] private string locomotionStateName = "Locomotion";
    [SerializeField] private string runStateName = "RunForward";
    [SerializeField] private float locomotionCrossFadeDuration = 0.12f;
    [SerializeField] private float locomotionLeadInDuration = 0.2f;

    [Header("Personal Space")]
    [SerializeField] private float personalSpaceRadius = 0.72f;
    [SerializeField] private float destinationReservationRadius = 0.8f;
    [SerializeField] private float personalSpaceYieldDistance = 0.58f;
    [SerializeField] private float minimumMovementCommitDuration = 0.75f;
    [SerializeField] private float crowdedMovementAbortDuration = 0.55f;
    [SerializeField] private float agentRadiusFromPersonalSpace = 0.36f;

    [Header("Social Animation")]
    [SerializeField] private Animator animatorOverride;
    [SerializeField] private int scratchIdleIndex = 0;
    [SerializeField] private int tailWagIdleIndex = 1;
    [SerializeField] private int barkIdleIndex = 2;
    [SerializeField] private int sitIdleIndex = 3;
    [SerializeField] private int idleResetIndex = 1;
    [SerializeField] private float socialIdleDuration = 2.25f;
    [SerializeField] private float barkIdleDuration = 0.9f;
    [SerializeField] private float locomotionSettleDuration = 0.3f;
    [SerializeField] private float sitEndRecoveryDuration = 1.15f;
    [SerializeField] private bool disableLegacyDogControllers = true;
    [SerializeField] private string[] standingIdleStateNames = { "Idle1", "Idle2", "Idle3", "Idle4" };
    [SerializeField] private bool useImportedStandingIdleClipsInEditor = true;
    [SerializeField] private float importedIdleFiveLoopDuration = 1.6f;
    [SerializeField] private string scratchStateName = "Scratching";
    [SerializeField] private string tailWagStateName = "TailWag";
    [SerializeField] private string barkStateName = "Bark";
    [SerializeField] private string pickupStateName = "Pickup";
    [SerializeField] private string putDownStateName = "PutDown";
    [SerializeField] private string attackLeftStateName = "Attack_L";
    [SerializeField] private string attackRightStateName = "Attack_R";
    [SerializeField] private float oneShotCrossFadeDuration = 0.08f;
    [SerializeField] private float animatorStateEntryTimeout = 0.45f;
    [SerializeField] private float oneShotAnimationMaxDuration = 3f;
    [SerializeField] private string eatDrinkStartStateName = "EatDrinkStart";
    [SerializeField] private string eatLoopStateName = "EatLoop";
    [SerializeField] private string drinkLoopStateName = "DrinkLoop";
    [SerializeField] private string eatDrinkEndStateName = "EatDrinkEnd";
    [SerializeField, Range(0f, 1f)] private float ambientIdleChanceAfterRoam = 0.7f;
    [SerializeField] private float minAmbientIdleDuration = 1.4f;
    [SerializeField] private float maxAmbientIdleDuration = 2.4f;
    [SerializeField, Range(0f, 1f)] private float ambientBarkChance = 0.16f;
    [SerializeField] private float ambientBarkCooldown = 20f;
    [SerializeField, Range(0f, 1f)] private float ambientChillChance = 0.45f;
    [SerializeField, Range(0f, 1f)] private float ambientSleepChance = 0.18f;
    [SerializeField] private float minAmbientChillDuration = 4.5f;
    [SerializeField] private float maxAmbientChillDuration = 7.5f;
    [SerializeField] private float minAmbientSleepDuration = 8f;
    [SerializeField] private float maxAmbientSleepDuration = 13f;
    [SerializeField] private float minimumSecondsBetweenAmbientSleeps = 180f;
    [SerializeField] private float minimumSecondsBetweenAmbientLieDowns = 150f;
    [SerializeField] private Vector2 initialAmbientLieDownDelayRange = new Vector2(60f, 140f);
    [SerializeField] private float sitToLieDelay = 0.85f;
    [SerializeField] private float restCrossFadeDuration = 0.18f;
    [SerializeField] private float wakePauseDuration = 0.45f;
    [SerializeField] private bool useImportedRestClipsInEditor = true;
    [SerializeField] private string lieStartStateName = "LieBellyStart";
    [SerializeField] private string lieLoopStateName = "CatSimple_Lie_side_loop_1";
    [SerializeField] private string lieSleepStartStateName = "LieSleepStart";
    [SerializeField] private string lieSleepLoopStateName = "LieSleepLoop";
    [SerializeField] private string lieSleepEndStateName = "LieSleepEnd";
    [SerializeField] private string lieEndStateName = "LieBellyEnd";
    [SerializeField] private string sitEndStateName = "Sit end";

    [Header("PawPal Bindings")]
    [SerializeField] private string dogId = string.Empty;
    [SerializeField] private Transform collarAnchor;

    [Header("Toy Pickup")]
    [SerializeField] private bool allowToyPickup = true;
    [SerializeField, Range(0f, 1f)] private float toyPickupChanceAfterRoam = 0.28f;
    [SerializeField] private float minimumSecondsBetweenToyPickups = 12f;
    [SerializeField] private string[] toyNameKeywords = { "ball", "bone", "cattoy" };
    [SerializeField] private float toySearchRadius = 4.25f;
    [SerializeField] private float toyApproachDistance = 0.45f;
    [SerializeField] private float maxToyPickupApproachDistance = 0.12f;
    [SerializeField] private float maxToyPickupPlanarDistance = 0.25f;
    [SerializeField] private float toyApproachTimeout = 6f;
    [SerializeField] private float fetchToyRepathInterval = 0.18f;
    [SerializeField] private float fetchToyRepathDistance = 0.18f;
    [SerializeField] private float fetchToyPickupReachMultiplier = 0.85f;
    [SerializeField] private float toyFaceDuration = 0.45f;
    [SerializeField] private float pickupAttachDelay = 0.55f;
    [SerializeField] private float pickupAnimationDuration = 1.15f;
    [SerializeField] private float minToyCarryDuration = 2f;
    [SerializeField] private float maxToyCarryDuration = 4f;
    [SerializeField] private bool carryToyToNewSpot = true;
    [SerializeField, Range(0f, 1f)] private float toyPlayBurstChance = 0.45f;
    [SerializeField] private int minToyPlayRuns = 1;
    [SerializeField] private int maxToyPlayRuns = 2;
    [SerializeField] private float toyPlayRunDistance = 1.85f;
    [SerializeField] private float minToyPlayRunDistance = 1.05f;
    [SerializeField] private float toyPlayRunTimeout = 3.5f;
    [SerializeField, Range(0f, 1f)] private float toyChasePartnerChance = 0.65f;
    [SerializeField] private float toyChaseFollowDistance = 0.85f;
    [SerializeField] private float putDownReleaseDelay = 0.45f;
    [SerializeField] private float putDownAnimationDuration = 0.9f;
    [SerializeField] private Vector3 toyDropOffset = new Vector3(0f, 0.04f, 0.45f);

    [Header("Big Ball Play")]
    [SerializeField] private bool useImportedAttackClipsInEditor = true;
    [SerializeField, Range(0f, 1f)] private float bigBallInterestChanceAfterRoam = 0.72f;
    [SerializeField] private int minBigBallHitRepeats = 1;
    [SerializeField] private int maxBigBallHitRepeats = 2;
    [SerializeField] private float bigBallApproachPadding = 0.22f;
    [SerializeField] private float bigBallPawSideOffset = 0.12f;
    [SerializeField] private float bigBallHitPlanarTolerance = 0.35f;
    [SerializeField] private float bigBallAttackHitDelay = 0.36f;
    [SerializeField] private float bigBallAttackFallbackDuration = 1.05f;
    [SerializeField] private float bigBallHitImpulse = 1.35f;
    [SerializeField] private float bigBallSideImpulseBias = 0.18f;
    [SerializeField] private float bigBallHitTorque = 0.65f;
    [SerializeField] private float bigBallPostHitWait = 0.45f;
    [SerializeField, Range(0f, 1f)] private float bigBallChaseChance = 0.75f;
    [SerializeField] private float bigBallChaseTimeout = 2.5f;
    [SerializeField] private float bigBallChaseFollowDistance = 0.55f;

    [Header("Camera Focus")]
    [SerializeField] private bool focusCameraDuringToyInteractions = true;
    [SerializeField] private Vector3 toyCameraFocusOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioClip walkingClip;
    [SerializeField, Range(0f, 1f)] private float walkingVolume = 0.18f;
    [SerializeField] private AudioClip ballBounceClip;
    [SerializeField, Range(0f, 1f)] private float ballBounceVolume = 0.65f;
    [SerializeField] private AudioClip toyPickupClip;
    [SerializeField, Range(0f, 1f)] private float toyPickupVolume = 0.7f;
    [SerializeField] private AudioClip sniffingClip;
    [SerializeField] private AudioClip scratchAndShakingClip;
    [SerializeField] private AudioClip shakingX3Clip;
    [SerializeField, Range(0f, 1f)] private float idleActionVolume = 0.75f;
    [SerializeField] private float minSecondsBetweenBallBounceSounds = 0.18f;

    private Animator animator;
    private NavMeshAgent agent;
    private ToyAttach toyAttach;
    private AudioSource walkingAudioSource;
    private Coroutine roamRoutine;
    private bool socialPaused;
    private bool warnedMissingNavMesh;
    private bool warnedMissingToyAttach;
    private Vector3 homePosition;
    private float animatorSpeedValue;
    private float animatorSpeedVelocity;
    private float animatorDirectionValue;
    private float animatorDirectionVelocity;
    private float movementLockedUntil;
    private int locomotionStateHash;
    private int runStateHash;
    private int scratchStateHash;
    private int tailWagStateHash;
    private int barkStateHash;
    private int pickupStateHash;
    private int putDownStateHash;
    private int attackLeftStateHash;
    private int attackRightStateHash;
    private int eatDrinkStartStateHash;
    private int eatLoopStateHash;
    private int drinkLoopStateHash;
    private int eatDrinkEndStateHash;
    private int[] standingIdleStateHashes = new int[0];
    private bool pathPrimedForWalk;
    private bool warnedMissingLocomotionState;
    private Coroutine releaseAgentRoutine;
    private bool isPlayingPreMoveTurnAnimation;
    private float preMoveTurnBlendDirection;
    private bool hasReservedDestination;
    private Transform claimedToy;
    private GameObject heldToy;
    private float nextAllowedToyPickupTime;
    private float nextAllowedAmbientBarkTime;
    private float nextAllowedAmbientLieDownTime;
    private float nextAllowedAmbientSleepTime;
    private int lieStartStateHash;
    private int lieLoopStateHash;
    private int lieSleepStartStateHash;
    private int lieSleepLoopStateHash;
    private int lieSleepEndStateHash;
    private int lieEndStateHash;
    private int sitEndStateHash;
    private bool isResting;
    private bool isSleeping;
    private bool needsStandBeforeMovement;
    private bool needsSleepWakeBeforeMovement;
    private bool isPlayingOneShotAnimation;
    private bool isToyRoutineActive;
    private Coroutine fetchRoutine;
    private float nextAllowedBallBounceAudioTime;
    private DogCycleCamera resolvedDogCamera;
    private int activeFetchCameraFocusId;
    private int activeToyCameraFocusId;
    private int activeToyPairCameraFocusId;
    private PlayableGraph directClipGraph;
#if UNITY_EDITOR
    private Dictionary<string, AnimationClip> editorImportedClips;
#endif

    public bool IsBusy { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsPreparingToMove { get; private set; }
    public DogMovementPace CurrentPace { get; private set; } = DogMovementPace.Walk;
    public bool IsSocialBusy => IsBusy;
    public bool IsResting => isResting;
    public bool IsSleeping => isSleeping;
    public bool IsPlayingOneShotAnimation => isPlayingOneShotAnimation;
    public bool HasHeldToy => heldToy != null || (toyAttach != null && toyAttach.HasToy);
    public bool CanJoinSocialInteraction => !IsBusy
        && !socialPaused
        && !isResting
        && !isSleeping
        && !isPlayingOneShotAnimation
        && !HasHeldToy
        && claimedToy == null
        && Time.time >= movementLockedUntil;
    public bool CanStartFetchToy => isActiveAndEnabled
        && !IsBusy
        && !socialPaused
        && !isResting
        && !isSleeping
        && !isPlayingOneShotAnimation
        && !isToyRoutineActive
        && !HasHeldToy
        && claimedToy == null
        && Time.time >= movementLockedUntil;
    public Vector3 HomePosition => homePosition;
    public string DogId => dogId;
    public bool HasExplicitDogId => !string.IsNullOrWhiteSpace(dogId);
    public Transform CollarAnchor => collarAnchor;

    private void Awake()
    {
        animator = ResolveAnimator();
        agent = GetComponent<NavMeshAgent>();
        toyAttach = GetComponent<ToyAttach>();

        if (animator == null)
        {
            Debug.LogWarning(name + " cannot animate because no Animator was found on the dog or its children.");
            enabled = false;
            return;
        }

        if (disableLegacyDogControllers)
        {
            DisableLegacyControllers();
        }

        ConfigureAgent();
        locomotionStateHash = ResolveAnimatorStateHash(locomotionStateName);
        runStateHash = ResolveAnimatorStateHash(runStateName);
        ResolveOneShotStateHashes();
        ResolveNeedInteractionStateHashes();
        ResolveRestStateHashes();
        sitEndStateHash = ResolveAnimatorStateHash(sitEndStateName);
        animator.applyRootMotion = false;
        homePosition = ClampToRoomBounds(transform.position);
        AutoAssignEditorAudioClips();
        EnsureWalkingAudioSource();
    }

    private void OnEnable()
    {
        if (!ActiveAgents.Contains(this))
        {
            ActiveAgents.Add(this);
        }

        DogSocialDirector.Register(this);
        AutoAssignEditorAudioClips();
    }

    private void Start()
    {
        PawPalToyRuntimeMetadata.AutoRegisterSceneLargeToys(true);
        ScheduleInitialAmbientLieDownWindow();
        StartRoaming();
    }

    private void OnDisable()
    {
        StopWalkingAudio();
        StopDirectClipGraph();
        ClearRestState();
        isPlayingOneShotAnimation = false;
        isToyRoutineActive = false;
        fetchRoutine = null;
        EndActiveDogCameraFocuses();
        DropHeldToyImmediately();
        ReleaseReservedDestination();
        ReleaseClaimedToy();
        ActiveAgents.Remove(this);
        DogSocialDirector.Unregister(this);
    }

    private void Update()
    {
        KeepInsideRoomBounds();
        UpdateAnimator();
        UpdateWalkingAudio();
    }

    public void StartRoaming()
    {
        if (roamRoutine != null)
        {
            StopCoroutine(roamRoutine);
        }

        socialPaused = false;
        IsBusy = false;
        IsPreparingToMove = false;
        isPlayingOneShotAnimation = false;
        isToyRoutineActive = false;
        ClearRestState();
        SetNeutralIdle();
        TryEnsureOnNavMesh(false);
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        roamRoutine = StartCoroutine(RoamRoutine());
    }

    public void PauseForSocial()
    {
        PauseForSocial(true);
    }

    public void PauseForSocial(bool dropHeldToyImmediately)
    {
        socialPaused = true;
        IsBusy = true;
        ClearRestState();
        if (dropHeldToyImmediately)
        {
            DropHeldToyImmediately();
            ReleaseClaimedToy();
        }

        if (roamRoutine != null)
        {
            StopCoroutine(roamRoutine);
            roamRoutine = null;
        }

        IsPreparingToMove = false;
        StopAgent();
    }

    public bool TryStartFetchToy(GameObject toy, Transform returnTarget, System.Action onCompleted)
    {
        if (toy == null || !CanStartFetchToy)
        {
            return false;
        }

        if (toy.GetComponentInParent<PawPalPlayerHeldToyMarker>() != null)
        {
            return false;
        }

        Transform toyRoot = ResolvePickupToyRoot(toy.transform);
        if (!IsPickupToyCandidate(toyRoot) || IsPawHitRollToy(toyRoot))
        {
            return false;
        }

        if (!TryClaimToy(toyRoot))
        {
            return false;
        }

        fetchRoutine = StartCoroutine(FetchToyRoutine(toyRoot.gameObject, returnTarget, onCompleted));
        return true;
    }

    public bool TrySetDestination(Vector3 destination)
    {
        return TrySetDestination(destination, DogMovementPace.Walk);
    }

    public bool TrySetDestination(Vector3 destination, DogMovementPace pace)
    {
        if (!TryEnsureOnNavMesh(true))
        {
            return false;
        }

        Vector3 roomPoint;
        if (!TryGetReachableRoomPoint(destination, sampleRadius, out roomPoint))
        {
            return false;
        }

        if (!PrimeDestination(roomPoint, pace))
        {
            return false;
        }

        StartReleaseAgentRoutine();
        return true;
    }

    public bool TryGetRoomSafePoint(Vector3 candidate, float radius, out Vector3 point)
    {
        if (!TryEnsureOnNavMesh(false))
        {
            point = ClampToRoomBounds(candidate);
            return false;
        }

        return TryGetReachableRoomPoint(candidate, radius, out point);
    }

    public bool TryGetRoomBounds(out Bounds bounds)
    {
        if (!restrictToRoomBounds)
        {
            bounds = new Bounds();
            return false;
        }

        Vector2 halfExtents = GetUsableRoomHalfExtents();
        Vector3 size = new Vector3(halfExtents.x * 2f, 0.2f, halfExtents.y * 2f);
        bounds = new Bounds(roomBoundsCenter, size);
        return true;
    }

    public IEnumerator MoveNear(Vector3 worldPosition, float timeout)
    {
        yield return MoveNear(worldPosition, timeout, DogMovementPace.Walk);
    }

    public IEnumerator MoveNear(Vector3 worldPosition, float timeout, DogMovementPace pace)
    {
        yield return TravelTo(worldPosition, timeout, pace, false);
    }

    public IEnumerator MoveNearPrecise(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        yield return TravelTo(worldPosition, timeout, pace, false, reachedDistance, true);
    }

    public IEnumerator MoveNearSocial(Vector3 worldPosition, float timeout, DogMovementPace pace, float reachedDistance)
    {
        yield return TravelTo(worldPosition, timeout, pace, false, reachedDistance, true, true, false);
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
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * turnSpeed);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public IEnumerator PlayBowlUse(bool useDrinkLoop, float loopDuration)
    {
        IsBusy = true;
        ClearRestState();
        StopAgent();
        yield return StandBeforeMovementIfNeeded();
        yield return PutDownHeldToyBeforeMovementIfNeeded();
        yield return WaitForLocomotionToSettle();

        isPlayingOneShotAnimation = true;
        animator.SetBool(MoveHash, false);
        SetNeutralIdle();

        yield return PlayNeedInteractionState(
            eatDrinkStartStateHash,
            eatDrinkStartStateName,
            "EatDrink_start",
            Mathf.Min(1.2f, oneShotAnimationMaxDuration));

        int loopStateHash = useDrinkLoop ? drinkLoopStateHash : eatLoopStateHash;
        string loopStateName = useDrinkLoop ? drinkLoopStateName : eatLoopStateName;
        string loopClipSuffix = useDrinkLoop ? "Drink_loop" : "Eat_loop";
        yield return PlayNeedInteractionLoop(loopStateHash, loopStateName, loopClipSuffix, loopDuration);

        yield return PlayNeedInteractionState(
            eatDrinkEndStateHash,
            eatDrinkEndStateName,
            "EatDrink_end",
            Mathf.Min(1.2f, oneShotAnimationMaxDuration));

        SetNeutralIdle();
        isPlayingOneShotAnimation = false;
        IsBusy = false;
    }

    public IEnumerator PlayTailWag()
    {
        yield return PlayOneShotAnimation(tailWagStateHash, tailWagStateName, tailWagIdleIndex, socialIdleDuration);
    }

    public IEnumerator PlayBark(float duration)
    {
        if (HasHeldToy)
        {
            yield break;
        }

        float barkDuration = duration > 0f ? duration : barkIdleDuration;
        yield return PlayOneShotAnimation(barkStateHash, barkStateName, barkIdleIndex, barkDuration);
    }

    public IEnumerator PlaySit()
    {
        yield return PlayRestIdle(socialIdleDuration, DogRestFlavor.Chill, false);
    }

    public IEnumerator PlayChillRest(float duration)
    {
        yield return PlayRestIdle(duration, DogRestFlavor.Chill, true);
    }

    private IEnumerator PlaySocialIdle(int idleIndex, float duration)
    {
        int stateHash = idleIndex == scratchIdleIndex ? scratchStateHash : tailWagStateHash;
        string stateName = idleIndex == scratchIdleIndex ? scratchStateName : tailWagStateName;
        AudioClip idleAudioClip = idleIndex == scratchIdleIndex ? scratchAndShakingClip : null;
        yield return PlayOneShotAnimation(stateHash, stateName, idleIndex, duration, idleAudioClip);
    }

    private Animator ResolveAnimator()
    {
        if (animatorOverride != null)
        {
            return animatorOverride;
        }

        Animator ownAnimator = GetComponent<Animator>();
        if (HasController(ownAnimator))
        {
            return ownAnimator;
        }

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < childAnimators.Length; i++)
        {
            if (HasController(childAnimators[i]))
            {
                return childAnimators[i];
            }
        }

        if (ownAnimator != null)
        {
            return ownAnimator;
        }

        return childAnimators.Length > 0 ? childAnimators[0] : null;
    }

    private bool HasController(Animator candidate)
    {
        return candidate != null && candidate.runtimeAnimatorController != null;
    }

    private void ResolveOneShotStateHashes()
    {
        scratchStateHash = ResolveAnimatorStateHash(scratchStateName);
        tailWagStateHash = ResolveAnimatorStateHash(tailWagStateName);
        barkStateHash = ResolveAnimatorStateHash(barkStateName);
        pickupStateHash = ResolveAnimatorStateHash(pickupStateName);
        putDownStateHash = ResolveAnimatorStateHash(putDownStateName);
        string prefix = GetImportedClipPrefix();
        attackLeftStateHash = ResolveAnimatorStateHash(
            attackLeftStateName,
            BuildPrefixedStateName(prefix, "Attack_L"),
            "Attack_L");
        attackRightStateHash = ResolveAnimatorStateHash(
            attackRightStateName,
            BuildPrefixedStateName(prefix, "Attack_R"),
            "Attack_R");

        if (standingIdleStateNames == null)
        {
            standingIdleStateHashes = new int[0];
            return;
        }

        standingIdleStateHashes = new int[standingIdleStateNames.Length];
        for (int i = 0; i < standingIdleStateNames.Length; i++)
        {
            standingIdleStateHashes[i] = ResolveAnimatorStateHash(standingIdleStateNames[i]);
        }
    }

    private void ResolveNeedInteractionStateHashes()
    {
        string prefix = GetImportedClipPrefix();
        eatDrinkStartStateHash = ResolveAnimatorStateHash(
            eatDrinkStartStateName,
            BuildPrefixedStateName(prefix, "EatDrink_start"),
            "EatDrink_start");
        eatLoopStateHash = ResolveAnimatorStateHash(
            eatLoopStateName,
            BuildPrefixedStateName(prefix, "Eat_loop"),
            "Eat_loop");
        drinkLoopStateHash = ResolveAnimatorStateHash(
            drinkLoopStateName,
            BuildPrefixedStateName(prefix, "Drink_loop"),
            "Drink_loop");
        eatDrinkEndStateHash = ResolveAnimatorStateHash(
            eatDrinkEndStateName,
            BuildPrefixedStateName(prefix, "EatDrink_end"),
            "EatDrink_end");
    }

    private void ResolveRestStateHashes()
    {
        lieStartStateHash = ResolveAnimatorStateHash(lieStartStateName);
        lieLoopStateHash = ResolveAnimatorStateHash("LieBellyLoop", lieLoopStateName);
        lieSleepStartStateHash = ResolveAnimatorStateHash(lieSleepStartStateName);
        lieSleepLoopStateHash = ResolveAnimatorStateHash(lieSleepLoopStateName);
        lieSleepEndStateHash = ResolveAnimatorStateHash(lieSleepEndStateName);
        lieEndStateHash = ResolveAnimatorStateHash(lieEndStateName);
    }

    private IEnumerator RoamRoutine()
    {
        yield return null;

        while (!socialPaused)
        {
            if (!TryEnsureOnNavMesh(true))
            {
                StopAgent();
                yield return new WaitForSeconds(1f);
                continue;
            }

            Transform toy = null;
            bool playedWithToy = ShouldTryToyPickup();
            if (playedWithToy)
            {
                playedWithToy = TryFindPickupToy(out toy);
            }

            if (playedWithToy)
            {
                yield return ToyPickupRoutine(toy);
            }
            else
            {
                Vector3 destination;
                if (TryGetRoamPoint(out destination))
                {
                    yield return TravelTo(destination, 0f, DogMovementPace.Walk, true);
                }
                else
                {
                    WarnMissingNavMesh();
                }
            }

            StopAgent();
            yield return PlayAmbientIdleAfterRoam();
            yield return new WaitForSeconds(Random.Range(minRoamWait, maxRoamWait));
        }
    }

    private IEnumerator PlayAmbientIdleAfterRoam()
    {
        if (socialPaused || HasHeldToy || Random.value > ambientIdleChanceAfterRoam)
        {
            yield break;
        }

        DogAmbientActionType ambientAction = SelectAmbientAction();
        if (ambientAction == DogAmbientActionType.ChillLie)
        {
            float chillDuration = Random.Range(minAmbientChillDuration, maxAmbientChillDuration);
            yield return PlayRestIdle(chillDuration, DogRestFlavor.Chill, true);
            yield break;
        }

        if (ambientAction == DogAmbientActionType.SleepLie)
        {
            float sleepDuration = Random.Range(minAmbientSleepDuration, maxAmbientSleepDuration);
            yield return PlayRestIdle(sleepDuration, DogRestFlavor.Sleep, true);
            yield break;
        }

        if (ambientAction == DogAmbientActionType.Bark)
        {
            yield return PlayAmbientBark();
            yield break;
        }

        float idleDuration = Random.Range(minAmbientIdleDuration, maxAmbientIdleDuration);
        yield return PlayStandingIdle(idleDuration);
    }

    private DogAmbientActionType SelectAmbientAction()
    {
        float shortIdleWeight = Mathf.Max(0f, 1f - ambientChillChance - ambientSleepChance);
        float barkWeight = !HasHeldToy && Time.time >= nextAllowedAmbientBarkTime
            ? Mathf.Max(0f, ambientBarkChance)
            : 0f;
        bool canLieDown = Time.time >= nextAllowedAmbientLieDownTime;
        float chillWeight = canLieDown
            ? Mathf.Max(0f, ambientChillChance)
            : 0f;
        float sleepWeight = canLieDown && Time.time >= nextAllowedAmbientSleepTime
            ? Mathf.Max(0f, ambientSleepChance)
            : 0f;

        float totalWeight = shortIdleWeight + barkWeight + chillWeight + sleepWeight;
        if (totalWeight <= 0.001f)
        {
            return DogAmbientActionType.ShortIdle;
        }

        float roll = Random.value * totalWeight;
        if (roll < shortIdleWeight)
        {
            return DogAmbientActionType.ShortIdle;
        }

        roll -= shortIdleWeight;
        if (roll < barkWeight)
        {
            return DogAmbientActionType.Bark;
        }

        roll -= barkWeight;
        if (roll < chillWeight)
        {
            return DogAmbientActionType.ChillLie;
        }

        return DogAmbientActionType.SleepLie;
    }

    private IEnumerator PlayAmbientBark()
    {
        if (HasHeldToy)
        {
            yield break;
        }

        nextAllowedAmbientBarkTime = Time.time + Mathf.Max(0f, ambientBarkCooldown);
        DogSocialDirector barkDirector = DogSocialDirector.Instance;
        if (barkDirector != null)
        {
            yield return barkDirector.PlayAmbientBarkForAgent(this);
            yield break;
        }

        yield return PlayBark(barkIdleDuration);
    }

    private IEnumerator PlayRestIdle(float duration, DogRestFlavor restFlavor, bool allowLieLoop)
    {
        isResting = true;
        isSleeping = restFlavor == DogRestFlavor.Sleep;
        needsStandBeforeMovement = false;
        needsSleepWakeBeforeMovement = false;
        isPlayingOneShotAnimation = true;
        StopAgent();
        yield return WaitForLocomotionToSettle();

        animator.SetBool(MoveHash, false);
        SetNeutralIdle();

        float clampedDuration = Mathf.Max(duration, sitToLieDelay);
        bool playedLieFlow = false;

        if (allowLieLoop && lieLoopStateHash != 0)
        {
            needsStandBeforeMovement = true;
            needsSleepWakeBeforeMovement = restFlavor == DogRestFlavor.Sleep;
            if (lieStartStateHash != 0)
            {
                yield return PlayStateForDuration(lieStartStateHash, lieStartStateName, sitToLieDelay, restCrossFadeDuration, false);
            }
            else
            {
                AnimationClip lieStartClip;
                if (TryGetImportedRestClip("Lie_belly_start", out lieStartClip))
                {
                    yield return PlayDirectClip(lieStartClip, Mathf.Max(sitToLieDelay, lieStartClip.length * 0.95f), false);
                }
                else
                {
                    yield return new WaitForSeconds(Mathf.Min(0.2f, sitToLieDelay));
                }
            }

            AnimationClip lieLoopClip;
            if (TryGetImportedRestClip("Lie_belly_loop_1", out lieLoopClip))
            {
                playedLieFlow = true;
                float remainingLieTime = Mathf.Max(0f, clampedDuration - sitToLieDelay);
                yield return PlayImportedLieLoop(restFlavor, remainingLieTime, lieLoopClip);
            }
            else
            {
                CrossFadeState(lieLoopStateHash, restCrossFadeDuration);
                playedLieFlow = true;

                float remainingLieTime = Mathf.Max(0f, clampedDuration - sitToLieDelay);
                if (restFlavor == DogRestFlavor.Sleep)
                {
                    float sleepLoopTime = Mathf.Max(0f, remainingLieTime - wakePauseDuration);
                    if (lieSleepStartStateHash != 0)
                    {
                        yield return PlayStateForDuration(lieSleepStartStateHash, lieSleepStartStateName, Mathf.Min(1.2f, Mathf.Max(0.4f, sleepLoopTime * 0.18f)), restCrossFadeDuration, false);
                    }

                    if (lieSleepLoopStateHash != 0)
                    {
                        CrossFadeState(lieSleepLoopStateHash, restCrossFadeDuration);
                    }

                    if (sleepLoopTime > 0f)
                    {
                        yield return new WaitForSeconds(sleepLoopTime);
                    }

                    if (lieSleepEndStateHash != 0)
                    {
                        yield return PlayStateForDuration(lieSleepEndStateHash, lieSleepEndStateName, wakePauseDuration, restCrossFadeDuration, false);
                    }
                }
                else if (remainingLieTime > 0f)
                {
                    yield return new WaitForSeconds(remainingLieTime);
                }
            }
        }
        else
        {
            AnimationClip lieStartClip;
            AnimationClip lieLoopClip;
            if (allowLieLoop
                && TryGetImportedRestClip("Lie_belly_loop_1", out lieLoopClip))
            {
                needsStandBeforeMovement = true;
                needsSleepWakeBeforeMovement = restFlavor == DogRestFlavor.Sleep;
                if (TryGetImportedRestClip("Lie_belly_start", out lieStartClip))
                {
                    yield return PlayDirectClip(lieStartClip, Mathf.Max(sitToLieDelay, lieStartClip.length * 0.95f), false);
                }

                playedLieFlow = true;
                yield return PlayImportedLieLoop(restFlavor, Mathf.Max(0f, clampedDuration - sitToLieDelay), lieLoopClip);
            }
            else
            {
                animator.SetInteger(IdleIndexHash, sitIdleIndex);
                yield return new WaitForSeconds(clampedDuration);
                SetNeutralIdle();
            }
        }

        if (playedLieFlow && lieEndStateHash != 0)
        {
            yield return PlayStateForDuration(lieEndStateHash, lieEndStateName, sitEndRecoveryDuration, restCrossFadeDuration, false);
        }
        else if (playedLieFlow)
        {
            AnimationClip lieEndClip;
            if (TryGetImportedRestClip("Lie_belly_end", out lieEndClip))
            {
                yield return PlayDirectClip(lieEndClip, Mathf.Max(0.25f, lieEndClip.length * 0.95f), false);
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Min(0.25f, sitEndRecoveryDuration));
            }
        }
        else if (!playedLieFlow && sitEndStateHash != 0)
        {
            CrossFadeState(sitEndStateHash, restCrossFadeDuration);
            yield return new WaitForSeconds(sitEndRecoveryDuration);
        }
        else if (!playedLieFlow)
        {
            animator.SetTrigger(SitEndTriggerHash);
            yield return new WaitForSeconds(sitEndRecoveryDuration);
        }

        movementLockedUntil = Time.time + sitEndRecoveryDuration;
        if (allowLieLoop && playedLieFlow)
        {
            nextAllowedAmbientLieDownTime = Time.time + minimumSecondsBetweenAmbientLieDowns;
        }

        if (restFlavor == DogRestFlavor.Sleep)
        {
            nextAllowedAmbientSleepTime = Time.time + minimumSecondsBetweenAmbientSleeps;
        }

        SetNeutralIdle();
        needsStandBeforeMovement = false;
        needsSleepWakeBeforeMovement = false;
        isPlayingOneShotAnimation = false;
        ClearRestState();
    }

    private void ScheduleInitialAmbientLieDownWindow()
    {
        float minDelay = Mathf.Max(0f, initialAmbientLieDownDelayRange.x);
        float maxDelay = Mathf.Max(minDelay, initialAmbientLieDownDelayRange.y);
        nextAllowedAmbientLieDownTime = Time.time + Random.Range(minDelay, maxDelay);
        nextAllowedAmbientSleepTime = Mathf.Max(nextAllowedAmbientSleepTime, nextAllowedAmbientLieDownTime);
    }

    private bool TryGetRoamPoint(out Vector3 point)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = ClampToRoomBounds(homePosition + new Vector3(randomCircle.x, 0f, randomCircle.y));

            Vector3 roomPoint;
            if (TryGetReachableRoomPoint(candidate, sampleRadius, out roomPoint)
                && IsPointComfortable(roomPoint, personalSpaceRadius, true))
            {
                point = roomPoint;
                return true;
            }
        }

        NavMeshHit fallbackHit;
        if (TrySampleRoomPosition(transform.position, Mathf.Max(sampleRadius, 0.75f), out fallbackHit)
            || TrySampleRoomPosition(homePosition, Mathf.Max(sampleRadius, 0.75f), out fallbackHit)
            || TryFindFallbackRoomNavMeshPoint(out fallbackHit))
        {
            point = fallbackHit.position;
            homePosition = ClampToRoomBounds(point);
            return true;
        }

        point = ClampToRoomBounds(transform.position);
        return false;
    }

    private bool HasReachedDestination()
    {
        return HasReachedDestination(destinationReachedDistance, false);
    }

    private bool HasReachedDestination(float reachedDistance)
    {
        return HasReachedDestination(reachedDistance, false);
    }

    private bool HasReachedDestination(float reachedDistance, bool preciseArrival)
    {
        if (agent.pathPending)
        {
            return false;
        }

        float targetDistance = Mathf.Max(0.01f, reachedDistance);
        if (!preciseArrival)
        {
            targetDistance = Mathf.Max(agent.stoppingDistance, targetDistance);
        }

        return agent.remainingDistance <= targetDistance;
    }

    private IEnumerator TravelTo(Vector3 worldPosition, float timeout, DogMovementPace pace, bool stopWhenSocialPaused)
    {
        yield return TravelTo(worldPosition, timeout, pace, stopWhenSocialPaused, destinationReachedDistance);
    }

    private IEnumerator TravelTo(Vector3 worldPosition, float timeout, DogMovementPace pace, bool stopWhenSocialPaused, float reachedDistance)
    {
        yield return TravelTo(worldPosition, timeout, pace, stopWhenSocialPaused, reachedDistance, false);
    }

    private IEnumerator TravelTo(Vector3 worldPosition, float timeout, DogMovementPace pace, bool stopWhenSocialPaused, float reachedDistance, bool preciseArrival)
    {
        yield return TravelTo(worldPosition, timeout, pace, stopWhenSocialPaused, reachedDistance, preciseArrival, false);
    }

    private IEnumerator TravelToIgnoringCrowding(Vector3 worldPosition, float timeout, DogMovementPace pace, bool stopWhenSocialPaused)
    {
        yield return TravelTo(worldPosition, timeout, pace, stopWhenSocialPaused, destinationReachedDistance, false, true);
    }

    private IEnumerator TravelTo(Vector3 worldPosition, float timeout, DogMovementPace pace, bool stopWhenSocialPaused, float reachedDistance, bool preciseArrival, bool ignoreCrowding)
    {
        yield return TravelTo(worldPosition, timeout, pace, stopWhenSocialPaused, reachedDistance, preciseArrival, ignoreCrowding, true);
    }

    private IEnumerator TravelTo(Vector3 worldPosition, float timeout, DogMovementPace pace, bool stopWhenSocialPaused, float reachedDistance, bool preciseArrival, bool ignoreCrowding, bool reserveDestination)
    {
        if (!TryEnsureOnNavMesh(true))
        {
            yield break;
        }

        Vector3 roomPoint;
        if (!TryGetReachableRoomPoint(worldPosition, sampleRadius, out roomPoint))
        {
            yield break;
        }

        if (reserveDestination && !TryReserveDestination(roomPoint))
        {
            if (!TryFindNearbyComfortablePoint(roomPoint, out roomPoint) || !TryReserveDestination(roomPoint))
            {
                yield break;
            }
        }

        float originalStoppingDistance = agent.stoppingDistance;
        if (preciseArrival)
        {
            agent.stoppingDistance = Mathf.Min(originalStoppingDistance, Mathf.Max(0.01f, reachedDistance));
        }

        yield return PrepareForMovement(roomPoint);

        if (stopWhenSocialPaused && socialPaused)
        {
            ReleaseReservedDestination();
            agent.stoppingDistance = originalStoppingDistance;
            yield break;
        }

        if (!PrimeDestination(roomPoint, pace))
        {
            ReleaseReservedDestination();
            agent.stoppingDistance = originalStoppingDistance;
            yield break;
        }

        yield return WaitForWalkAnimationLeadIn();
        ReleaseAgentForPrimedPath();

        float elapsed = 0f;
        float crowdedStoppedDuration = 0f;
        while ((timeout <= 0f || elapsed < timeout) && !HasReachedDestination(reachedDistance, preciseArrival))
        {
            if (stopWhenSocialPaused && socialPaused)
            {
                break;
            }

            if (!ignoreCrowding && IsCrowdedByAnotherDog(personalSpaceYieldDistance))
            {
                if (elapsed >= Mathf.Max(0f, minimumMovementCommitDuration)
                    && agent.velocity.sqrMagnitude <= movingVelocityThreshold * movingVelocityThreshold)
                {
                    crowdedStoppedDuration += Time.deltaTime;
                    if (crowdedStoppedDuration >= Mathf.Max(0.05f, crowdedMovementAbortDuration))
                    {
                        break;
                    }
                }
            }
            else
            {
                crowdedStoppedDuration = 0f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopAgent();
        agent.stoppingDistance = originalStoppingDistance;
    }

    private IEnumerator PrepareForMovement(Vector3 destination)
    {
        IsPreparingToMove = true;
        StopAgent();
        animator.SetBool(MoveHash, false);

        yield return StandBeforeMovementIfNeeded();
        yield return PutDownHeldToyBeforeMovementIfNeeded();
        yield return WaitForLocomotionToSettle();

        float remainingLock = movementLockedUntil - Time.time;
        if (remainingLock > 0f)
        {
            yield return new WaitForSeconds(remainingLock);
        }

        yield return TurnTowardDestination(destination);

        if (preMovePauseDuration > 0f)
        {
            yield return new WaitForSeconds(preMovePauseDuration);
        }

        IsPreparingToMove = false;
    }

    private IEnumerator TurnTowardDestination(Vector3 destination)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            yield break;
        }

        Quaternion initialLookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float initialSignedAngle = Vector3.SignedAngle(transform.forward, direction.normalized, Vector3.up);
        if (Mathf.Abs(initialSignedAngle) <= preMoveTurnAngle)
        {
            yield break;
        }

        BeginPreMoveTurnAnimation(GetTurnBlendDirection(initialSignedAngle));

        float elapsed = 0f;
        while (elapsed < preMoveMaxTurnTime)
        {
            direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                break;
            }

            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float angle = Quaternion.Angle(transform.rotation, lookRotation);
            if (angle <= preMoveTurnAngle)
            {
                break;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, preMoveTurnSpeed * Time.deltaTime);
            UpdatePreMoveTurnAnimation();
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (Quaternion.Angle(transform.rotation, initialLookRotation) <= preMoveTurnAngle)
        {
            transform.rotation = initialLookRotation;
        }

        FinishPreMoveTurnAnimation();
    }

    private IEnumerator FaceWorldPoint(Vector3 worldPosition, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, preMoveTurnSpeed * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void StopAgent()
    {
        StopReleaseAgentRoutine();
        if (isPlayingPreMoveTurnAnimation)
        {
            FinishPreMoveTurnAnimation();
        }

        pathPrimedForWalk = false;
        if (!IsPreparingToMove)
        {
            ReleaseReservedDestination();
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }

        if (!IsPreparingToMove && animator != null)
        {
            SetNeutralIdle();
        }

        // IsMoving is cleared by UpdateAnimator after the locomotion blend has eased down.
    }

    private bool TryEnsureOnNavMesh(bool warnIfMissing)
    {
        if (agent.isOnNavMesh && IsInsideRoomBounds(transform.position))
        {
            return true;
        }

        NavMeshHit hit;
        if (TrySampleRoomPosition(transform.position, sampleRadius, out hit)
            || TrySampleRoomPosition(homePosition, sampleRadius, out hit)
            || TrySampleRoomPosition(roomBoundsCenter, Mathf.Max(sampleRadius, 2f), out hit)
            || TryFindFallbackRoomNavMeshPoint(out hit))
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            agent.Warp(hit.position);
            homePosition = ClampToRoomBounds(hit.position);
            return agent.isOnNavMesh && IsInsideRoomBounds(transform.position);
        }

        if (warnIfMissing)
        {
            WarnMissingNavMesh();
        }

        return false;
    }

    private void ConfigureAgent()
    {
        ApplyPace(DogMovementPace.Walk);
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.radius = Mathf.Max(agent.radius, Mathf.Max(0.05f, personalSpaceRadius * agentRadiusFromPersonalSpace));
        agent.avoidancePriority = Mathf.Clamp(35 + Mathf.Abs(GetInstanceID()) % 35, 0, 99);
    }

    private bool PrimeDestination(Vector3 destination, DogMovementPace pace)
    {
        StopReleaseAgentRoutine();
        ApplyPace(pace);
        pathPrimedForWalk = true;
        animatorDirectionValue = 0f;
        animatorDirectionVelocity = 0f;
        animatorSpeedValue = GetAnimatorSpeed(pace) * animatorSpeedScale;
        animatorSpeedVelocity = 0f;
        animator.SetInteger(IdleIndexHash, MovementIdleIndex);
        animator.SetBool(MoveHash, true);
        animator.SetFloat(SpeedHash, animatorSpeedValue);
        animator.SetFloat(DirectionHash, 0f);

        if (pace == DogMovementPace.Run && runStateHash != 0)
        {
            animator.CrossFadeInFixedTime(runStateHash, locomotionCrossFadeDuration, BaseLayerIndex, 0f);
        }
        else if (locomotionStateHash != 0)
        {
            animator.CrossFadeInFixedTime(locomotionStateHash, locomotionCrossFadeDuration, BaseLayerIndex, 0f);
        }
        else if (!warnedMissingLocomotionState)
        {
            warnedMissingLocomotionState = true;
            Debug.LogWarning(name + " could not find Animator state '" + locomotionStateName + "'. Movement will still use Move/Speed/Direction parameters.");
        }

        agent.isStopped = true;
        bool acceptedDestination = agent.SetDestination(destination);
        if (!acceptedDestination)
        {
            pathPrimedForWalk = false;
            agent.ResetPath();
            SetNeutralIdle();
            animator.SetBool(MoveHash, false);
            return false;
        }

        return true;
    }

    private void UpdateAnimator()
    {
        if (agent == null || animator == null)
        {
            return;
        }

        if (isPlayingPreMoveTurnAnimation)
        {
            UpdatePreMoveTurnAnimation();
            return;
        }

        Vector3 velocity = agent.enabled ? agent.velocity : Vector3.zero;
        velocity.y = 0f;

        bool hasMoveIntent = HasMoveIntent();
        float targetSpeed = hasMoveIntent ? GetAnimatorSpeed(CurrentPace) * animatorSpeedScale : 0f;
        if (hasMoveIntent && agent.hasPath && !agent.pathPending && agent.remainingDistance <= arrivalSlowdownDistance)
        {
            float slowFactor = Mathf.InverseLerp(destinationReachedDistance, arrivalSlowdownDistance, agent.remainingDistance);
            targetSpeed *= Mathf.Lerp(0.35f, 1f, slowFactor);
        }

        float targetDirection = GetMovementTurnBlendDirection(velocity, hasMoveIntent);

        animatorSpeedValue = Mathf.SmoothDamp(
            animatorSpeedValue,
            targetSpeed,
            ref animatorSpeedVelocity,
            Mathf.Max(0.01f, animatorSpeedDampTime));
        animatorDirectionValue = Mathf.SmoothDamp(
            animatorDirectionValue,
            targetDirection,
            ref animatorDirectionVelocity,
            Mathf.Max(0.01f, animatorDirectionDampTime));

        bool velocityMoving = velocity.sqrMagnitude > movingVelocityThreshold * movingVelocityThreshold;
        bool shouldMove = hasMoveIntent || velocityMoving || animatorSpeedValue > 0.08f;

        IsMoving = shouldMove;
        animator.SetBool(MoveHash, shouldMove);
        animator.SetFloat(SpeedHash, animatorSpeedValue);
        animator.SetFloat(DirectionHash, animatorDirectionValue);
    }

    private void UpdateWalkingAudio()
    {
        EnsureWalkingAudioSource();
        if (walkingAudioSource == null)
        {
            return;
        }

        bool shouldPlayWalking = IsMoving
            && !isPlayingOneShotAnimation
            && !isResting
            && !isSleeping
            && agent != null
            && agent.enabled
            && agent.velocity.sqrMagnitude > movingVelocityThreshold * movingVelocityThreshold;

        if (shouldPlayWalking)
        {
            walkingAudioSource.volume = PawPalAudioSettings.ApplySoundEffectsVolume(walkingVolume);
            if (!walkingAudioSource.isPlaying)
            {
                walkingAudioSource.Play();
            }

            return;
        }

        if (walkingAudioSource.isPlaying)
        {
            walkingAudioSource.Stop();
        }
    }

    private void EnsureWalkingAudioSource()
    {
        if (walkingClip == null)
        {
            return;
        }

        if (walkingAudioSource == null)
        {
            walkingAudioSource = gameObject.AddComponent<AudioSource>();
            walkingAudioSource.playOnAwake = false;
            walkingAudioSource.loop = true;
            walkingAudioSource.spatialBlend = 1f;
            walkingAudioSource.rolloffMode = AudioRolloffMode.Linear;
            walkingAudioSource.minDistance = 0.3f;
            walkingAudioSource.maxDistance = 5f;
        }

        walkingAudioSource.clip = walkingClip;
        walkingAudioSource.volume = PawPalAudioSettings.ApplySoundEffectsVolume(walkingVolume);
    }

    private void StopWalkingAudio()
    {
        if (walkingAudioSource != null && walkingAudioSource.isPlaying)
        {
            walkingAudioSource.Stop();
        }
    }

    private void PlayToyPickupAudio(Vector3 position)
    {
        PlaySpatialOneShot(toyPickupClip, position, toyPickupVolume);
    }

    private void PlayBallBounceAudio(Vector3 position)
    {
        if (Time.time < nextAllowedBallBounceAudioTime)
        {
            return;
        }

        nextAllowedBallBounceAudioTime = Time.time + Mathf.Max(0f, minSecondsBetweenBallBounceSounds);
        PlaySpatialOneShot(ballBounceClip, position, ballBounceVolume);
    }

    private void PlaySpatialOneShot(AudioClip clip, Vector3 position, float volume)
    {
        float effectiveVolume = PawPalAudioSettings.ApplySoundEffectsVolume(volume);
        if (clip == null || effectiveVolume <= 0f)
        {
            return;
        }

        GameObject audioObject = new GameObject(name + "_ActionAudio");
        audioObject.hideFlags = HideFlags.DontSave;
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.25f;
        source.maxDistance = 6f;
        source.PlayOneShot(clip, effectiveVolume);
        Destroy(audioObject, Mathf.Max(0.1f, clip.length + 0.15f));
    }

    private void BeginPreMoveTurnAnimation(float turnBlendDirection)
    {
        if (animator == null)
        {
            return;
        }

        preMoveTurnBlendDirection = turnBlendDirection;
        isPlayingPreMoveTurnAnimation = Mathf.Abs(preMoveTurnBlendDirection) > 0.001f;
        animatorSpeedValue = 0f;
        animatorSpeedVelocity = 0f;
        animatorDirectionValue = preMoveTurnBlendDirection;
        animatorDirectionVelocity = 0f;
        UpdatePreMoveTurnAnimation();

        if (locomotionStateHash != 0)
        {
            animator.CrossFadeInFixedTime(locomotionStateHash, locomotionCrossFadeDuration, BaseLayerIndex, 0f);
        }
    }

    private void UpdatePreMoveTurnAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetInteger(IdleIndexHash, MovementIdleIndex);
        animator.SetBool(MoveHash, true);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetFloat(DirectionHash, preMoveTurnBlendDirection);
    }

    private void FinishPreMoveTurnAnimation()
    {
        isPlayingPreMoveTurnAnimation = false;
        preMoveTurnBlendDirection = 0f;
        animatorDirectionValue = 0f;
        animatorDirectionVelocity = 0f;

        if (animator == null)
        {
            return;
        }

        animator.SetFloat(DirectionHash, 0f);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetBool(MoveHash, false);
        SetNeutralIdle();
    }

    private float GetMovementTurnBlendDirection(Vector3 planarVelocity, bool hasMoveIntent)
    {
        if (!hasMoveIntent || agent == null || !agent.enabled)
        {
            return 0f;
        }

        Vector3 desiredDirection = planarVelocity;
        if (desiredDirection.sqrMagnitude <= movingVelocityThreshold * movingVelocityThreshold)
        {
            desiredDirection = agent.desiredVelocity;
            desiredDirection.y = 0f;
        }

        if (desiredDirection.sqrMagnitude <= 0.001f && agent.hasPath)
        {
            desiredDirection = agent.steeringTarget - transform.position;
            desiredDirection.y = 0f;
        }

        if (desiredDirection.sqrMagnitude <= 0.001f)
        {
            return 0f;
        }

        float signedAngle = Vector3.SignedAngle(transform.forward, desiredDirection.normalized, Vector3.up);
        if (Mathf.Abs(signedAngle) <= preMoveTurnAngle)
        {
            return 0f;
        }

        return GetTurnBlendDirection(signedAngle);
    }

    private float GetTurnBlendDirection(float signedAngle)
    {
        return Mathf.Clamp(signedAngle / 90f, -1f, 1f);
    }

    private bool HasMoveIntent()
    {
        if (pathPrimedForWalk)
        {
            return true;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
        {
            return false;
        }

        if (agent.pathPending)
        {
            return true;
        }

        if (!agent.hasPath)
        {
            return false;
        }

        return agent.remainingDistance > Mathf.Max(agent.stoppingDistance, destinationReachedDistance);
    }

    private Vector3 GetMoveDirection(Vector3 velocity, bool hasMoveIntent)
    {
        if (velocity.sqrMagnitude > 0.001f)
        {
            return velocity;
        }

        if (!hasMoveIntent)
        {
            return Vector3.zero;
        }

        Vector3 desiredVelocity = agent.desiredVelocity;
        desiredVelocity.y = 0f;
        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            return desiredVelocity;
        }

        if (agent.hasPath)
        {
            Vector3 steeringDirection = agent.steeringTarget - transform.position;
            steeringDirection.y = 0f;
            return steeringDirection;
        }

        return Vector3.zero;
    }

    private void ApplyPace(DogMovementPace pace)
    {
        CurrentPace = pace;

        if (agent == null)
        {
            return;
        }

        agent.speed = GetAgentSpeed(pace);
        agent.acceleration = acceleration;
    }

    private float GetAgentSpeed(DogMovementPace pace)
    {
        switch (pace)
        {
            case DogMovementPace.Run:
                return Mathf.Max(calmWalkSpeed, runSpeed);
            case DogMovementPace.Trot:
                return Mathf.Max(calmWalkSpeed, trotSpeed);
            default:
                return calmWalkSpeed;
        }
    }

    private float GetAnimatorSpeed(DogMovementPace pace)
    {
        switch (pace)
        {
            case DogMovementPace.Run:
                return runAnimatorSpeed;
            case DogMovementPace.Trot:
                return trotAnimatorSpeed;
            default:
                return walkAnimatorSpeed;
        }
    }

    private bool ShouldTryToyPickup()
    {
        if (!allowToyPickup
            || socialPaused
            || isToyRoutineActive
            || isPlayingOneShotAnimation
            || HasHeldToy
            || Time.time < nextAllowedToyPickupTime)
        {
            return false;
        }

        float chance = HasNearbyPawHitRollToy()
            ? Mathf.Max(toyPickupChanceAfterRoam, bigBallInterestChanceAfterRoam)
            : toyPickupChanceAfterRoam;

        return allowToyPickup
            && Random.value <= chance;
    }

    private bool HasNearbyPawHitRollToy()
    {
        PawPalToyRuntimeMetadata.AutoRegisterSceneLargeToys();
        Transform[] candidates = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = ResolvePickupToyRoot(candidates[i]);
            if (candidate == null || !IsPawHitRollToy(candidate) || ClaimedToyTransforms.Contains(candidate))
            {
                continue;
            }

            if (Vector3.Distance(transform.position, GetToyInterestPosition(candidate)) <= toySearchRadius)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindPickupToy(out Transform toy)
    {
        toy = null;
        PawPalToyRuntimeMetadata.AutoRegisterSceneLargeToys();
        Transform[] candidates = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = ResolvePickupToyRoot(candidates[i]);
            if (!IsPickupToyCandidate(candidate))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, GetToyInterestPosition(candidate));
            if (distance > toySearchRadius || distance >= bestDistance)
            {
                continue;
            }

            if (!CanReachPickupToyCandidate(candidate))
            {
                continue;
            }

            bestDistance = distance;
            toy = candidate;
        }

        return TryClaimToy(toy);
    }

    private bool CanReachPickupToyCandidate(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (IsPawHitRollToy(candidate))
        {
            Vector3 approachPoint;
            return TryGetBigBallApproachPoint(candidate, Random.value < 0.5f, out approachPoint);
        }

        NavMeshHit hit;
        return TrySampleRoomPosition(candidate.position, sampleRadius, out hit)
            && CanReachInsideRoom(hit.position);
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

        Transform current = candidate;
        Transform bestNamedToyRoot = PawPalToyRuntimeMetadata.IsPawHitRollToyName(current.name) ? current : null;
        while (current.parent != null && current.parent.GetComponentInParent<DogRoomAgent>() == null)
        {
            current = current.parent;
            if (PawPalToyRuntimeMetadata.IsPawHitRollToyName(current.name))
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

        if (candidate.GetComponentInParent<DogRoomAgent>() != null)
        {
            return false;
        }

        if (candidate.GetComponentInParent<PawPalPlayerHeldToyMarker>() != null)
        {
            return false;
        }

        if (ClaimedToyTransforms.Contains(candidate))
        {
            return false;
        }

        bool isPawHitRollToy = IsPawHitRollToy(candidate);
        if (candidate.GetComponentInChildren<Renderer>(true) == null
            || (!isPawHitRollToy && candidate.GetComponentInChildren<Collider>(true) == null))
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        if (lowerName.Contains("ballhole") || lowerName.Contains("toyterrier"))
        {
            return false;
        }

        if (isPawHitRollToy || candidate.gameObject.tag == "Toy")
        {
            return true;
        }

        for (int i = 0; i < toyNameKeywords.Length; i++)
        {
            string keyword = toyNameKeywords[i];
            if (!string.IsNullOrEmpty(keyword) && lowerName.Contains(keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
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

    private IEnumerator ToyPickupRoutine(Transform toy)
    {
        IsBusy = true;
        isToyRoutineActive = true;
        EndActiveToyCameraFocuses();

        try
        {
            if (toy == null)
            {
                yield break;
            }

            activeToyCameraFocusId = BeginDogCameraFocus(toy, DogCameraFocusPriority.AmbientToy);

            if (IsPawHitRollToy(toy))
            {
                yield return PawHitRollToyRoutine(toy);
                yield break;
            }

            Vector3 approachPoint;
            if (!TryGetToyApproachPoint(toy, out approachPoint))
            {
                yield break;
            }

            yield return TravelTo(approachPoint, toyApproachTimeout, DogMovementPace.Walk, true);
            if (socialPaused || toy == null)
            {
                yield break;
            }

            StopAgent();
            yield return FaceWorldPoint(toy.position, toyFaceDuration);
            if (!IsToyWithinPickupDistance(toy))
            {
                yield break;
            }

            yield return PlayPickupToy(toy.gameObject);

            if (HasHeldToy)
            {
                yield return PlayToyBurstIfAllowed();
            }

            if (HasHeldToy && carryToyToNewSpot)
            {
                Vector3 carryDestination;
                if (TryGetRoamPoint(out carryDestination))
                {
                    yield return TravelTo(carryDestination, 0f, DogMovementPace.Walk, true);
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
            EndActiveToyCameraFocuses();
            FinishToyRoutine();
        }
    }

    private IEnumerator FetchToyRoutine(GameObject toy, Transform returnTarget, System.Action onCompleted)
    {
        IsBusy = true;
        socialPaused = true;
        isToyRoutineActive = true;
        ClearRestState();

        if (roamRoutine != null)
        {
            StopCoroutine(roamRoutine);
            roamRoutine = null;
        }

        StopAgent();

        if (toy == null)
        {
            FinishFetchToyRoutine(false, onCompleted);
            yield break;
        }

        Transform toyTransform = toy.transform;
        EndActiveFetchCameraFocus();
        activeFetchCameraFocusId = BeginDogCameraFocus(toyTransform, DogCameraFocusPriority.Fetch);

        yield return ChaseFetchToyUntilClose(toyTransform, toyApproachTimeout);

        if (toy == null)
        {
            FinishFetchToyRoutine(false, onCompleted);
            yield break;
        }

        StopAgent();
        yield return FaceWorldPoint(toyTransform.position, toyFaceDuration);

        if (!IsToyWithinPickupDistance(toyTransform))
        {
            yield return MoveNearPrecise(
                toyTransform.position,
                Mathf.Max(1.2f, toyApproachTimeout * 0.35f),
                DogMovementPace.Walk,
                Mathf.Max(0.04f, maxToyPickupPlanarDistance * 0.75f));
            StopAgent();
            yield return FaceWorldPoint(toyTransform.position, Mathf.Max(0.1f, toyFaceDuration * 0.5f));
        }

        if (!IsToyWithinPickupDistance(toyTransform))
        {
            FinishFetchToyRoutine(false, onCompleted);
            yield break;
        }

        yield return PlayPickupToy(toy);
        if (!HasHeldToy)
        {
            FinishFetchToyRoutine(false, onCompleted);
            yield break;
        }

        if (returnTarget != null)
        {
            Vector3 returnPoint = returnTarget.position;
            Vector3 safeReturnPoint;
            if (TryGetReachableRoomPoint(returnPoint, sampleRadius, out safeReturnPoint))
            {
                returnPoint = safeReturnPoint;
            }

            yield return TravelToIgnoringCrowding(
                returnPoint,
                Mathf.Max(2.5f, toyApproachTimeout),
                DogMovementPace.Trot,
                false);

            if (returnTarget != null)
            {
                yield return FaceWorldPoint(returnTarget.position, Mathf.Max(0.15f, toyFaceDuration * 0.6f));
            }
        }

        if (HasHeldToy)
        {
            yield return PlayPutDownToy();
        }

        FinishFetchToyRoutine(true, onCompleted);
    }

    private IEnumerator ChaseFetchToyUntilClose(Transform toy, float timeout)
    {
        if (toy == null || !TryEnsureOnNavMesh(true))
        {
            yield break;
        }

        Vector3 destination;
        if (!TryGetToyApproachPoint(toy, out destination))
        {
            yield break;
        }

        float originalStoppingDistance = agent.stoppingDistance;
        agent.stoppingDistance = Mathf.Min(
            originalStoppingDistance,
            Mathf.Max(0.02f, maxToyPickupPlanarDistance * Mathf.Clamp(fetchToyPickupReachMultiplier, 0.25f, 1f)));

        try
        {
            yield return PrepareForMovement(destination);

            if (!PrimeDestination(destination, DogMovementPace.Run))
            {
                yield break;
            }

            yield return WaitForWalkAnimationLeadIn();
            ReleaseAgentForPrimedPath();

            float elapsed = 0f;
            float repathElapsed = 0f;
            Vector3 lastDestination = destination;

            while (toy != null
                && (timeout <= 0f || elapsed < timeout)
                && !IsToyWithinPickupDistance(toy))
            {
                repathElapsed += Time.deltaTime;
                if (repathElapsed >= Mathf.Max(0.03f, fetchToyRepathInterval)
                    || !agent.hasPath
                    || (!agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, destinationReachedDistance)))
                {
                    repathElapsed = 0f;
                    Vector3 refreshedDestination;
                    bool needsFreshPath = !agent.hasPath
                        || (!agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, destinationReachedDistance));
                    if (TryGetToyApproachPoint(toy, out refreshedDestination)
                        && (needsFreshPath
                            || GetPlanarDistance(refreshedDestination, lastDestination) >= Mathf.Max(0.02f, fetchToyRepathDistance)))
                    {
                        ApplyPace(DogMovementPace.Run);
                        if (agent.SetDestination(refreshedDestination))
                        {
                            agent.isStopped = false;
                            lastDestination = refreshedDestination;
                        }
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            StopAgent();
            agent.stoppingDistance = originalStoppingDistance;
        }
    }

    private void FinishFetchToyRoutine(bool completed, System.Action onCompleted)
    {
        EndActiveFetchCameraFocus();
        DropHeldToyImmediately();
        ReleaseClaimedToy();
        nextAllowedToyPickupTime = Time.time + minimumSecondsBetweenToyPickups;
        isToyRoutineActive = false;
        isPlayingOneShotAnimation = false;
        IsBusy = false;
        socialPaused = false;
        fetchRoutine = null;

        if (completed && onCompleted != null)
        {
            onCompleted();
        }

        if (isActiveAndEnabled)
        {
            StartRoaming();
        }
    }

    private bool TryGetToyApproachPoint(Transform toy, out Vector3 approachPoint)
    {
        Vector3 awayFromToy = transform.position - toy.position;
        awayFromToy.y = 0f;
        if (awayFromToy.sqrMagnitude < 0.001f)
        {
            awayFromToy = -transform.forward;
        }

        float effectiveApproachDistance = Mathf.Min(
            toyApproachDistance,
            Mathf.Max(0.01f, maxToyPickupApproachDistance));
        Vector3 candidate = toy.position + awayFromToy.normalized * effectiveApproachDistance;
        Vector3 roomPoint;
        if (TryGetReachableRoomPoint(candidate, sampleRadius, out roomPoint))
        {
            approachPoint = roomPoint;
            return true;
        }

        if (TryGetReachableRoomPoint(toy.position, sampleRadius, out roomPoint))
        {
            approachPoint = roomPoint;
            return true;
        }

        approachPoint = transform.position;
        return false;
    }

    private IEnumerator PlayToyBurstIfAllowed()
    {
        if (!HasHeldToy || Random.value > toyPlayBurstChance)
        {
            yield break;
        }

        DogRoomAgent chasePartner = null;
        bool hasChasePartner = Random.value <= toyChasePartnerChance && TryFindToyChasePartner(out chasePartner);
        if (hasChasePartner)
        {
            chasePartner.PauseForSocial();
            activeToyPairCameraFocusId = BeginDogCameraFocus(chasePartner.transform, DogCameraFocusPriority.AmbientToy);
        }

        try
        {
            int runCount = Random.Range(Mathf.Max(1, minToyPlayRuns), Mathf.Max(minToyPlayRuns, maxToyPlayRuns) + 1);
            for (int i = 0; i < runCount; i++)
            {
                Vector3 runPoint;
                if (!TryGetToyPlayRunPoint(out runPoint))
                {
                    continue;
                }

                Coroutine partnerMove = null;
                if (hasChasePartner && chasePartner != null)
                {
                    Vector3 partnerPoint;
                    if (TryGetChasePartnerPoint(chasePartner, runPoint, out partnerPoint))
                    {
                        partnerMove = StartCoroutine(chasePartner.MoveNear(partnerPoint, toyPlayRunTimeout, DogMovementPace.Run));
                    }
                }

                yield return TravelToIgnoringCrowding(runPoint, toyPlayRunTimeout, DogMovementPace.Run, true);

                if (partnerMove != null)
                {
                    yield return partnerMove;
                }
            }

            if (hasChasePartner && chasePartner != null)
            {
                yield return FaceTarget(chasePartner.transform, toyFaceDuration);
                yield return chasePartner.FaceTarget(transform, toyFaceDuration);
                Coroutine wagPartner = StartCoroutine(chasePartner.PlayTailWag());
                Coroutine wagHolder = StartCoroutine(PlayTailWag());
                yield return wagPartner;
                yield return wagHolder;
            }
        }
        finally
        {
            EndDogCameraFocus(activeToyPairCameraFocusId);
            activeToyPairCameraFocusId = 0;
            if (hasChasePartner && chasePartner != null && chasePartner.isActiveAndEnabled)
            {
                chasePartner.StartRoaming();
            }
        }
    }

    private bool TryFindToyChasePartner(out DogRoomAgent partner)
    {
        partner = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < ActiveAgents.Count; i++)
        {
            DogRoomAgent candidate = ActiveAgents[i];
            if (candidate == null || candidate == this || !candidate.CanJoinSocialInteraction)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                partner = candidate;
            }
        }

        return partner != null;
    }

    private bool TryGetToyPlayRunPoint(out Vector3 point)
    {
        float minimumRunDistance = Mathf.Clamp(minToyPlayRunDistance, 0.35f, Mathf.Max(0.35f, toyPlayRunDistance));
        for (int i = 0; i < 16; i++)
        {
            Vector2 randomDirection = Random.insideUnitCircle;
            if (randomDirection.sqrMagnitude < 0.001f)
            {
                continue;
            }

            Vector3 candidate = transform.position + new Vector3(randomDirection.normalized.x, 0f, randomDirection.normalized.y) * toyPlayRunDistance;
            candidate = ClampToRoomBounds(candidate);
            if (TryGetReachableRoomPoint(candidate, sampleRadius, out point)
                && GetPlanarDistance(transform.position, point) >= minimumRunDistance
                && IsPointComfortable(point, personalSpaceRadius, true))
            {
                return true;
            }
        }

        point = transform.position;
        return false;
    }

    private bool TryGetChasePartnerPoint(DogRoomAgent partner, Vector3 holderDestination, out Vector3 partnerPoint)
    {
        Vector3 fromHolder = transform.position - holderDestination;
        fromHolder.y = 0f;
        if (fromHolder.sqrMagnitude < 0.001f)
        {
            fromHolder = -transform.forward;
        }

        Vector3 candidate = holderDestination + fromHolder.normalized * toyChaseFollowDistance;
        return partner.TryGetRoomSafePoint(candidate, sampleRadius, out partnerPoint);
    }

    private static float GetPlanarDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private bool IsPawHitRollToy(Transform toy)
    {
        if (toy == null)
        {
            return false;
        }

        PawPalToyRuntimeMetadata metadata = toy.GetComponentInParent<PawPalToyRuntimeMetadata>();
        if (metadata != null)
        {
            return metadata.InteractionMode == PawPalToyInteractionMode.PawHitRoll;
        }

        return PawPalToyRuntimeMetadata.IsPawHitRollToyName(toy.name);
    }

    private IEnumerator PawHitRollToyRoutine(Transform toy)
    {
        int hitCount = Random.Range(Mathf.Max(1, minBigBallHitRepeats), Mathf.Max(minBigBallHitRepeats, maxBigBallHitRepeats) + 1);
        for (int i = 0; i < hitCount; i++)
        {
            if (socialPaused || toy == null)
            {
                yield break;
            }

            bool useLeftPaw = Random.value < 0.5f;
            Vector3 approachPoint;
            if (!TryGetBigBallApproachPoint(toy, useLeftPaw, out approachPoint))
            {
                yield break;
            }

            yield return TravelTo(approachPoint, toyApproachTimeout, DogMovementPace.Walk, true, Mathf.Max(0.04f, destinationReachedDistance * 0.65f), true);
            if (socialPaused || toy == null)
            {
                yield break;
            }

            StopAgent();
            yield return FaceWorldPoint(GetBigBallWorldCenter(toy), toyFaceDuration);
            if (!IsCloseEnoughForBigBallHit(toy))
            {
                yield break;
            }

            yield return PlayBigBallPawHit(toy, useLeftPaw);
            if (socialPaused || toy == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, bigBallPostHitWait));
            if (i >= hitCount - 1 || Random.value > bigBallChaseChance)
            {
                continue;
            }

            Vector3 chasePoint;
            if (TryGetBigBallChasePoint(toy, out chasePoint))
            {
                yield return TravelToIgnoringCrowding(chasePoint, bigBallChaseTimeout, DogMovementPace.Run, true);
            }
        }
    }

    private bool TryGetBigBallApproachPoint(Transform toy, bool useLeftPaw, out Vector3 approachPoint)
    {
        approachPoint = transform.position;
        if (toy == null)
        {
            return false;
        }

        Vector3 toyPosition = GetBigBallWorldCenter(toy);
        Vector3 fromToyToDog = transform.position - toyPosition;
        fromToyToDog.y = 0f;
        if (fromToyToDog.sqrMagnitude < 0.001f)
        {
            fromToyToDog = -transform.forward;
        }

        fromToyToDog.Normalize();
        Vector3 sideDirection = Vector3.Cross(Vector3.up, fromToyToDog).normalized;
        float sideSign = useLeftPaw ? -1f : 1f;
        float approachDistance = GetBigBallApproachDistance(toy);

        Vector3[] candidates =
        {
            toyPosition + fromToyToDog * approachDistance + sideDirection * (sideSign * bigBallPawSideOffset),
            toyPosition + fromToyToDog * approachDistance,
            toyPosition + Quaternion.Euler(0f, sideSign * 25f, 0f) * fromToyToDog * approachDistance,
            toyPosition + Quaternion.Euler(0f, -sideSign * 25f, 0f) * fromToyToDog * approachDistance
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 roomPoint;
            if (TryGetReachableRoomPoint(candidates[i], sampleRadius, out roomPoint)
                && IsPointComfortable(roomPoint, personalSpaceRadius * 0.75f, true))
            {
                approachPoint = roomPoint;
                return true;
            }
        }

        return TryGetReachableRoomPoint(toyPosition + fromToyToDog * approachDistance, sampleRadius, out approachPoint);
    }

    private bool IsCloseEnoughForBigBallHit(Transform toy)
    {
        if (toy == null)
        {
            return false;
        }

        float allowedDistance = GetBigBallPlanarRadius(toy)
            + Mathf.Max(0.08f, bigBallApproachPadding)
            + Mathf.Max(0.05f, bigBallHitPlanarTolerance);
        float blockerRadius;
        if (PawPalToyRuntimeMetadata.TryGetDogNavigationBlockRadius(toy, out blockerRadius))
        {
            allowedDistance = Mathf.Max(allowedDistance, blockerRadius + Mathf.Max(0.08f, bigBallHitPlanarTolerance));
        }

        return GetPlanarDistance(transform.position, GetBigBallWorldCenter(toy)) <= allowedDistance;
    }

    private float GetBigBallApproachDistance(Transform toy)
    {
        float radiusBasedDistance = GetBigBallPlanarRadius(toy) + Mathf.Max(0.05f, bigBallApproachPadding);
        float blockerRadius;
        if (PawPalToyRuntimeMetadata.TryGetDogNavigationBlockRadius(toy, out blockerRadius))
        {
            return Mathf.Max(radiusBasedDistance, blockerRadius + 0.08f);
        }

        return radiusBasedDistance;
    }

    private IEnumerator PlayBigBallPawHit(Transform toy, bool useLeftPaw)
    {
        if (toy == null)
        {
            yield break;
        }

        StopAgent();
        animator.SetBool(MoveHash, false);
        SetNeutralIdle();
        isPlayingOneShotAnimation = true;

        int stateHash = useLeftPaw ? attackLeftStateHash : attackRightStateHash;
        string stateName = useLeftPaw ? attackLeftStateName : attackRightStateName;
        string importedClipSuffix = useLeftPaw ? "Attack_L" : "Attack_R";
        float hitDelay = Mathf.Max(0f, bigBallAttackHitDelay);
        bool hitApplied = false;

        if (stateHash != 0)
        {
            CrossFadeState(stateHash, oneShotCrossFadeDuration);
            yield return new WaitForSeconds(hitDelay);
            ApplyBigBallHitImpulse(toy, useLeftPaw);
            hitApplied = true;
            yield return WaitForCurrentOneShotStateToFinish(stateHash, stateName, Mathf.Max(0.05f, bigBallAttackFallbackDuration - hitDelay));
        }
        else
        {
            AnimationClip clip;
            if (TryGetImportedAttackClip(importedClipSuffix, out clip))
            {
                yield return PlayDirectAttackClipAndHitBall(clip, toy, useLeftPaw, hitDelay);
                hitApplied = true;
            }
        }

        if (!hitApplied)
        {
            yield return new WaitForSeconds(hitDelay);
            ApplyBigBallHitImpulse(toy, useLeftPaw);
            yield return new WaitForSeconds(Mathf.Max(0.05f, bigBallAttackFallbackDuration - hitDelay));
        }

        SetNeutralIdle();
        isPlayingOneShotAnimation = false;
    }

    private IEnumerator PlayDirectAttackClipAndHitBall(AnimationClip clip, Transform toy, bool useLeftPaw, float hitDelay)
    {
        if (clip == null)
        {
            yield break;
        }

        StopDirectClipGraph();

        directClipGraph = PlayableGraph.Create(name + "_DirectDogAttackClip");
        directClipGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(directClipGraph, "DogAttackClip", animator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(directClipGraph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        output.SetSourcePlayable(clipPlayable);

        directClipGraph.Play();

        bool hitApplied = false;
        float elapsed = 0f;
        float targetDuration = Mathf.Max(Mathf.Max(0.05f, bigBallAttackFallbackDuration), clip.length * 0.95f);
        while (elapsed < targetDuration)
        {
            if (!hitApplied && elapsed >= hitDelay)
            {
                ApplyBigBallHitImpulse(toy, useLeftPaw);
                hitApplied = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!hitApplied)
        {
            ApplyBigBallHitImpulse(toy, useLeftPaw);
        }

        StopDirectClipGraph();
    }

    private void ApplyBigBallHitImpulse(Transform toy, bool useLeftPaw)
    {
        if (toy == null)
        {
            return;
        }

        PawPalToyRuntimeMetadata metadata = toy.GetComponentInParent<PawPalToyRuntimeMetadata>();
        Rigidbody body = toy.GetComponentInParent<Rigidbody>();
        bool needsLargeToySetup = body == null || metadata == null;
        if (metadata == null && PawPalToyRuntimeMetadata.IsPawHitRollToyName(toy.name))
        {
            metadata = toy.gameObject.AddComponent<PawPalToyRuntimeMetadata>();
        }

        if (needsLargeToySetup && metadata != null)
        {
            metadata.Initialize(string.Empty, PawPalToyInteractionMode.PawHitRoll);
            metadata.ConfigureLargeToyPhysicsAndNavigation();
        }

        Transform toyRoot = metadata != null ? metadata.transform : toy;
        body = toyRoot.GetComponentInParent<Rigidbody>();
        if (body == null)
        {
            body = toyRoot.GetComponentInChildren<Rigidbody>();
        }

        if (body == null)
        {
            body = toyRoot.gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.mass = Mathf.Max(0.1f, body.mass);
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.WakeUp();

        Vector3 awayFromDog = GetBigBallWorldCenter(toy) - transform.position;
        awayFromDog.y = 0f;
        if (awayFromDog.sqrMagnitude < 0.001f)
        {
            awayFromDog = transform.forward;
        }

        awayFromDog.Normalize();
        Vector3 sideDirection = transform.right * (useLeftPaw ? -1f : 1f);
        Vector3 impulseDirection = (awayFromDog + sideDirection * Mathf.Max(0f, bigBallSideImpulseBias)).normalized;
        body.AddForce(impulseDirection * Mathf.Max(0f, bigBallHitImpulse * BigBallHitImpulseScale), ForceMode.Impulse);
        PlayBallBounceAudio(GetBigBallWorldCenter(toy));

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, impulseDirection);
        if (torqueAxis.sqrMagnitude > 0.001f)
        {
            body.AddTorque(torqueAxis.normalized * Mathf.Max(0f, bigBallHitTorque * BigBallHitTorqueScale), ForceMode.Impulse);
        }
    }

    private bool TryGetBigBallChasePoint(Transform toy, out Vector3 chasePoint)
    {
        chasePoint = transform.position;
        if (toy == null)
        {
            return false;
        }

        Vector3 ballCenter = GetBigBallWorldCenter(toy);
        Vector3 fromBallToDog = transform.position - ballCenter;
        fromBallToDog.y = 0f;
        if (fromBallToDog.sqrMagnitude < 0.001f)
        {
            fromBallToDog = -transform.forward;
        }

        float followDistance = GetBigBallPlanarRadius(toy) + Mathf.Max(0.05f, bigBallChaseFollowDistance);
        Vector3 candidate = ballCenter + fromBallToDog.normalized * followDistance;
        return TryGetReachableRoomPoint(candidate, sampleRadius, out chasePoint);
    }

    private Vector3 GetToyInterestPosition(Transform toy)
    {
        if (toy == null)
        {
            return transform.position;
        }

        return IsPawHitRollToy(toy) ? GetBigBallWorldCenter(toy) : toy.position;
    }

    private Vector3 GetBigBallWorldCenter(Transform toy)
    {
        if (toy == null)
        {
            return transform.position;
        }

        Vector3 blockerCenter;
        if (PawPalToyRuntimeMetadata.TryGetDogNavigationBlockCenter(toy, out blockerCenter))
        {
            return blockerCenter;
        }

        Bounds bounds;
        if (TryGetToyVisualWorldBounds(toy, out bounds))
        {
            return bounds.center;
        }

        return toy.position;
    }

    private float GetBigBallPlanarRadius(Transform toy)
    {
        Bounds bounds;
        if (TryGetToyWorldBounds(toy, out bounds))
        {
            return Mathf.Max(0.08f, Mathf.Max(bounds.extents.x, bounds.extents.z));
        }

        return 0.22f;
    }

    private bool TryGetToyWorldBounds(Transform toy, out Bounds bounds)
    {
        bounds = toy != null ? new Bounds(toy.position, Vector3.zero) : new Bounds();
        if (toy == null)
        {
            return false;
        }

        if (TryGetToyVisualWorldBounds(toy, out bounds))
        {
            return true;
        }

        bool hasBounds = false;
        Collider[] colliders = toy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
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

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = toy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
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

        return hasBounds;
    }

    private bool TryGetToyVisualWorldBounds(Transform toy, out Bounds bounds)
    {
        bounds = toy != null ? new Bounds(toy.position, Vector3.zero) : new Bounds();
        if (toy == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = toy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
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

        return hasBounds;
    }

    private IEnumerator PlayPickupToy(GameObject toy)
    {
        if (HasHeldToy || toy == null)
        {
            yield break;
        }

        animator.SetBool(MoveHash, false);
        SetNeutralIdle();
        isPlayingOneShotAnimation = true;

        if (pickupStateHash != 0)
        {
            CrossFadeState(pickupStateHash, oneShotCrossFadeDuration);
        }
        else
        {
            animator.SetTrigger(PickupHash);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, pickupAttachDelay));

        if (!AttachToy(toy))
        {
            isPlayingOneShotAnimation = false;
            yield break;
        }

        float remainingPickupDuration = Mathf.Max(0.05f, pickupAnimationDuration - Mathf.Max(0f, pickupAttachDelay));
        yield return WaitForCurrentOneShotStateToFinish(pickupStateHash, pickupStateName, remainingPickupDuration);
        isPlayingOneShotAnimation = false;
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

        if (toyAttach == null)
        {
            if (!warnedMissingToyAttach)
            {
                warnedMissingToyAttach = true;
                Debug.LogWarning(name + " cannot pick up toys because no ToyAttach component is available.");
            }

            return false;
        }

        if (!toyAttach.TryAttachToy(toy))
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

        StopAgent();
        animator.SetBool(MoveHash, false);
        SetNeutralIdle();
        isPlayingOneShotAnimation = true;

        if (putDownStateHash != 0)
        {
            CrossFadeState(putDownStateHash, oneShotCrossFadeDuration);
        }
        else
        {
            animator.SetTrigger(PutDownHash);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, putDownReleaseDelay));
        DropHeldToyImmediately();

        yield return WaitForOneShotCompletion(putDownStateHash, putDownStateName, putDownAnimationDuration, oneShotAnimationMaxDuration);
        isPlayingOneShotAnimation = false;
    }

    private IEnumerator PutDownHeldToyBeforeMovementIfNeeded()
    {
        if (!HasHeldToy)
        {
            yield break;
        }

        yield return PlayPutDownToy();
    }

    private void DropHeldToyImmediately()
    {
        if (heldToy == null && (toyAttach == null || !toyAttach.HasToy))
        {
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
            heldToy.transform.SetParent(null, worldPositionStays: true);
            heldToy.transform.position = dropPosition;
            heldToy.transform.rotation = dropRotation;
        }

        heldToy = null;
        ReleaseClaimedToy();
    }

    private bool IsToyWithinPickupDistance(Transform toy)
    {
        if (toy == null)
        {
            return false;
        }

        Vector3 interactionOrigin = GetToyInteractionOrigin();
        interactionOrigin.y = 0f;

        Vector3 toyPosition = toy.position;
        toyPosition.y = 0f;

        return Vector3.Distance(interactionOrigin, toyPosition) <= Mathf.Max(0.01f, maxToyPickupPlanarDistance);
    }

    private Vector3 GetToyInteractionOrigin()
    {
        if (toyAttach != null)
        {
            return toyAttach.GetAnchorWorldPosition();
        }

        return transform.position + transform.forward * Mathf.Max(0.1f, stoppingDistance);
    }

    private Vector3 GetToyDropPosition()
    {
        Vector3 desiredDropOrigin = GetHeldToyWorldPosition();
        desiredDropOrigin = ClampToRoomBounds(desiredDropOrigin);

        Vector3 groundProbeOrigin = desiredDropOrigin + Vector3.up * Mathf.Max(0.5f, sampleRadius);
        Vector3 groundPoint;
        if (TryGetToyDropSurfacePoint(groundProbeOrigin, sampleRadius + 2f, out groundPoint))
        {
            return groundPoint;
        }

        NavMeshHit navHit;
        if (TrySampleRoomPosition(desiredDropOrigin, Mathf.Min(sampleRadius, 0.35f), out navHit))
        {
            return navHit.position;
        }

        return desiredDropOrigin;
    }

    private bool TryGetToyDropSurfacePoint(Vector3 rayOrigin, float rayDistance, out Vector3 groundPoint)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            rayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        groundPoint = default;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidToyDropSurface(hit.collider) || hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            groundPoint = hit.point;
        }

        return bestDistance < float.PositiveInfinity;
    }

    private bool IsValidToyDropSurface(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;
        if (candidateTransform == null)
        {
            return false;
        }

        if (candidateTransform.IsChildOf(transform))
        {
            return false;
        }

        if (toyAttach != null && toyAttach.CurrentToy != null && candidateTransform.IsChildOf(toyAttach.CurrentToy.transform))
        {
            return false;
        }

        if (heldToy != null && candidateTransform.IsChildOf(heldToy.transform))
        {
            return false;
        }

        return true;
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
            + transform.right * toyDropOffset.x;
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

    private void FinishToyRoutine()
    {
        DropHeldToyImmediately();
        ReleaseClaimedToy();
        nextAllowedToyPickupTime = Time.time + minimumSecondsBetweenToyPickups;
        isToyRoutineActive = false;
        isPlayingOneShotAnimation = false;
        IsBusy = false;
    }

    private int BeginDogCameraFocus(Transform secondaryTarget, DogCameraFocusPriority priority)
    {
        if (!focusCameraDuringToyInteractions)
        {
            return 0;
        }

        DogCycleCamera dogCamera = ResolveDogCamera();
        if (dogCamera == null)
        {
            return 0;
        }

        if (secondaryTarget != null && secondaryTarget != transform)
        {
            return dogCamera.BeginPairFocus(transform, secondaryTarget, priority, toyCameraFocusOffset);
        }

        return dogCamera.BeginFocus(transform, priority, toyCameraFocusOffset);
    }

    private void EndDogCameraFocus(int focusId)
    {
        if (focusId == 0)
        {
            return;
        }

        DogCycleCamera dogCamera = ResolveDogCamera();
        if (dogCamera != null)
        {
            dogCamera.EndFocus(focusId);
        }
    }

    private void EndActiveFetchCameraFocus()
    {
        EndDogCameraFocus(activeFetchCameraFocusId);
        activeFetchCameraFocusId = 0;
    }

    private void EndActiveToyCameraFocuses()
    {
        EndDogCameraFocus(activeToyPairCameraFocusId);
        activeToyPairCameraFocusId = 0;
        EndDogCameraFocus(activeToyCameraFocusId);
        activeToyCameraFocusId = 0;
    }

    private void EndActiveDogCameraFocuses()
    {
        EndActiveFetchCameraFocus();
        EndActiveToyCameraFocuses();
    }

    private DogCycleCamera ResolveDogCamera()
    {
        if (resolvedDogCamera != null)
        {
            return resolvedDogCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            resolvedDogCamera = mainCamera.GetComponent<DogCycleCamera>();
            if (resolvedDogCamera != null)
            {
                return resolvedDogCamera;
            }
        }

        resolvedDogCamera = FindFirstObjectByType<DogCycleCamera>();
        return resolvedDogCamera;
    }

    private void ReleaseClaimedToy()
    {
        if (claimedToy == null)
        {
            return;
        }

        ClaimedToyTransforms.Remove(claimedToy);
        claimedToy = null;
    }

    private void ClearRestState()
    {
        isResting = false;
        isSleeping = false;
    }

    private IEnumerator StandBeforeMovementIfNeeded()
    {
        if (!ShouldStandBeforeMovement())
        {
            yield break;
        }

        bool wasSleeping = isSleeping || needsSleepWakeBeforeMovement;
        isResting = false;
        isSleeping = false;
        isPlayingOneShotAnimation = true;
        animator.SetBool(MoveHash, false);
        animator.SetFloat(SpeedHash, 0f);
        animator.SetFloat(DirectionHash, 0f);
        SetNeutralIdle();
        StopDirectClipGraph();

        if (wasSleeping)
        {
            yield return PlaySleepWakeAnimation();
        }

        yield return PlayLieStandAnimation();

        SetNeutralIdle();
        needsStandBeforeMovement = false;
        needsSleepWakeBeforeMovement = false;
        isPlayingOneShotAnimation = false;
    }

    private bool ShouldStandBeforeMovement()
    {
        return needsStandBeforeMovement || IsAnimatorInLieRestState();
    }

    private bool IsAnimatorInLieRestState()
    {
        return IsAnimatorInState(lieStartStateHash, lieStartStateName)
            || IsAnimatorInState(lieLoopStateHash, lieLoopStateName)
            || IsAnimatorInState(lieSleepStartStateHash, lieSleepStartStateName)
            || IsAnimatorInState(lieSleepLoopStateHash, lieSleepLoopStateName)
            || IsAnimatorInState(lieSleepEndStateHash, lieSleepEndStateName);
    }

    private IEnumerator PlaySleepWakeAnimation()
    {
        if (lieSleepEndStateHash != 0)
        {
            yield return PlayStateForDuration(lieSleepEndStateHash, lieSleepEndStateName, wakePauseDuration, restCrossFadeDuration, false);
            yield break;
        }

        AnimationClip sleepEndClip;
        if (TryGetImportedRestClip("Lie_belly_sleep_end", out sleepEndClip)
            || TryGetImportedRestClip("Lie_Sleep_end", out sleepEndClip))
        {
            yield return PlayDirectClip(sleepEndClip, Mathf.Max(0.25f, sleepEndClip.length * 0.95f), false);
        }
    }

    private IEnumerator PlayLieStandAnimation()
    {
        if (lieEndStateHash != 0)
        {
            yield return PlayStateForDuration(lieEndStateHash, lieEndStateName, sitEndRecoveryDuration, restCrossFadeDuration, false);
            yield break;
        }

        AnimationClip lieEndClip;
        if (TryGetImportedRestClip("Lie_belly_end", out lieEndClip))
        {
            yield return PlayDirectClip(lieEndClip, Mathf.Max(0.25f, lieEndClip.length * 0.95f), false);
            yield break;
        }

        if (sitEndStateHash != 0)
        {
            CrossFadeState(sitEndStateHash, restCrossFadeDuration);
        }
        else
        {
            animator.SetTrigger(SitEndTriggerHash);
        }

        yield return new WaitForSeconds(sitEndRecoveryDuration);
    }

    private IEnumerator PlayImportedLieLoop(DogRestFlavor restFlavor, float remainingLieTime, AnimationClip lieLoopClip)
    {
        if (restFlavor != DogRestFlavor.Sleep)
        {
            yield return PlayDirectClip(lieLoopClip, remainingLieTime, true);
            yield break;
        }

        float sleepLoopTime = Mathf.Max(0f, remainingLieTime - wakePauseDuration);
        AnimationClip sleepStartClip;
        if (TryGetImportedRestClip("Lie_belly_sleep_start", out sleepStartClip)
            || TryGetImportedRestClip("Lie_Sleep_start", out sleepStartClip))
        {
            yield return PlayDirectClip(sleepStartClip, Mathf.Max(0.35f, sleepStartClip.length * 0.95f), false);
        }

        AnimationClip sleepLoopClip;
        if (TryGetImportedRestClip("Lie_belly_sleep", out sleepLoopClip)
            || TryGetImportedRestClip("Lie_Sleep_loop", out sleepLoopClip))
        {
            yield return PlayDirectClip(sleepLoopClip, sleepLoopTime, true);
        }
        else
        {
            yield return PlayDirectClip(lieLoopClip, sleepLoopTime, true);
        }

        AnimationClip sleepEndClip;
        if (TryGetImportedRestClip("Lie_belly_sleep_end", out sleepEndClip)
            || TryGetImportedRestClip("Lie_Sleep_end", out sleepEndClip))
        {
            yield return PlayDirectClip(sleepEndClip, Mathf.Max(0.25f, sleepEndClip.length * 0.95f), false);
        }
    }

    private IEnumerator PlayDirectClip(AnimationClip clip, float duration, bool loop)
    {
        if (clip == null)
        {
            yield break;
        }

        StopDirectClipGraph();

        directClipGraph = PlayableGraph.Create(name + "_DirectDogClip");
        directClipGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(directClipGraph, "DogClip", animator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(directClipGraph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        output.SetSourcePlayable(clipPlayable);

        directClipGraph.Play();

        float elapsed = 0f;
        float targetDuration = Mathf.Max(0.05f, duration);
        while (elapsed < targetDuration)
        {
            if (loop && clip.length > 0.001f && clipPlayable.GetTime() >= clip.length)
            {
                clipPlayable.SetTime(0d);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopDirectClipGraph();
    }

    private void StopDirectClipGraph()
    {
        if (!directClipGraph.IsValid())
        {
            return;
        }

        directClipGraph.Stop();
        directClipGraph.Destroy();
    }

    private bool TryGetImportedRestClip(string suffix, out AnimationClip clip)
    {
        clip = null;
        if (!useImportedRestClipsInEditor || string.IsNullOrEmpty(suffix))
        {
            return false;
        }

        return TryGetImportedClip(suffix, out clip);
    }

    private bool TryGetImportedStandingIdleClip(string suffix, out AnimationClip clip)
    {
        clip = null;
        if (!useImportedStandingIdleClipsInEditor || string.IsNullOrEmpty(suffix))
        {
            return false;
        }

        return TryGetImportedClip(suffix, out clip);
    }

    private bool TryGetImportedAttackClip(string suffix, out AnimationClip clip)
    {
        clip = null;
        if (!useImportedAttackClipsInEditor || string.IsNullOrEmpty(suffix))
        {
            return false;
        }

        return TryGetImportedClip(suffix, out clip);
    }

    private bool TryGetImportedClip(string suffix, out AnimationClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(suffix))
        {
            return false;
        }

#if UNITY_EDITOR
        Dictionary<string, AnimationClip> clips = GetEditorImportedClips();
        if (clips == null || clips.Count == 0)
        {
            return false;
        }

        string prefix = GetImportedClipPrefix();
        if (!string.IsNullOrEmpty(prefix) && clips.TryGetValue(prefix + "|" + suffix, out clip))
        {
            return clip != null;
        }

        foreach (KeyValuePair<string, AnimationClip> candidate in clips)
        {
            if (candidate.Key.EndsWith("|" + suffix, System.StringComparison.Ordinal)
                || candidate.Key.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                clip = candidate.Value;
                return clip != null;
            }
        }
#endif

        return false;
    }

    private void AutoAssignEditorAudioClips()
    {
#if UNITY_EDITOR
        if (walkingClip == null)
        {
            walkingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorWalkAudioAssetPath);
        }

        if (ballBounceClip == null)
        {
            ballBounceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorBallBounceAudioAssetPath);
        }

        if (toyPickupClip == null)
        {
            toyPickupClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorToyPickupAudioAssetPath);
        }

        if (sniffingClip == null)
        {
            sniffingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorSniffingAudioAssetPath);
        }

        if (scratchAndShakingClip == null)
        {
            scratchAndShakingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorScratchAndShakingAudioAssetPath);
        }

        if (shakingX3Clip == null)
        {
            shakingX3Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EditorShakingX3AudioAssetPath);
        }
#endif
    }

#if UNITY_EDITOR
    private Dictionary<string, AnimationClip> GetEditorImportedClips()
    {
        if (editorImportedClips != null)
        {
            return editorImportedClips;
        }

        editorImportedClips = new Dictionary<string, AnimationClip>();
        string assetPath = GetImportedAnimationAssetPath();
        if (string.IsNullOrEmpty(assetPath))
        {
            return editorImportedClips;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip importedClip = assets[i] as AnimationClip;
            if (importedClip == null || string.IsNullOrEmpty(importedClip.name))
            {
                continue;
            }

            editorImportedClips[importedClip.name] = importedClip;
        }

        return editorImportedClips;
    }

    private string GetImportedAnimationAssetPath()
    {
        string key = GetBreedKey();
        if (key == "corgi")
        {
            return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Corgi/Dog/FBX/Anim/Corgi_anim_IP.fbx";
        }

        if (key == "labrador")
        {
            return "Assets/3rd Party Packs/Dogs (Red Deer)/Puppy/Puppy_Labrador/Puppy/FBX/Anim/Puppy_Labrador_anim_RM.fbx";
        }

        if (key == "husky")
        {
            return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Husky/Dog/FBX/Anim/Husky_anim_IP.fbx";
        }

        if (key == "frenchbulldog")
        {
            return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/FrenchBulldog/Dog/FBX/Anim/FrenchBulldog_anim_IP.fbx";
        }

        if (key == "golden" || key == "retriever")
        {
            return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/GoldenRetriever/Dog/FBX/Anim/Retriever_anim_IP.fbx";
        }

        if (key == "shepherd")
        {
            return "Assets/3rd Party Packs/Dogs (Red Deer)/Dogs/Shepherd/Dog/FBX/Anim/Shepherd_anim_IP.fbx";
        }

        return null;
    }
#endif

    private string GetImportedClipPrefix()
    {
        string key = GetBreedKey();
        if (key == "corgi")
        {
            return "Arm_Corgi";
        }

        if (key == "labrador")
        {
            return "Arm_Labrador";
        }

        if (key == "husky")
        {
            return "Arm_Husky";
        }

        if (key == "frenchbulldog")
        {
            return "Arm_FrBulldog";
        }

        if (key == "golden" || key == "retriever")
        {
            return "Arm_Retriever";
        }

        if (key == "shepherd")
        {
            return "Arm_Shepherd";
        }

        return null;
    }

    private static string BuildPrefixedStateName(string prefix, string suffix)
    {
        if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
        {
            return null;
        }

        return prefix + "|" + suffix;
    }

    private string GetBreedKey()
    {
        string source = name;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            source += " " + animator.runtimeAnimatorController.name;
        }

        source = source.ToLowerInvariant();
        if (source.Contains("corgi"))
        {
            return "corgi";
        }

        if (source.Contains("labrador"))
        {
            return "labrador";
        }

        if (source.Contains("husky"))
        {
            return "husky";
        }

        if (source.Contains("french") || source.Contains("bulldog"))
        {
            return "frenchbulldog";
        }

        if (source.Contains("gold") || source.Contains("retriever"))
        {
            return "retriever";
        }

        if (source.Contains("shepherd"))
        {
            return "shepherd";
        }

        return null;
    }

    private IEnumerator PlayStandingIdle(float duration)
    {
        int optionCount = GetStandingIdleOptionCount();
        if (optionCount > 0)
        {
            int startIndex = Random.Range(0, optionCount);
            for (int i = 0; i < optionCount; i++)
            {
                int candidateIndex = (startIndex + i) % optionCount;
                if (!CanPlayStandingIdleOption(candidateIndex))
                {
                    continue;
                }

                yield return PlayStandingIdleOption(candidateIndex, duration);
                yield break;
            }
        }

        int idleIndex = Random.value < 0.5f ? tailWagIdleIndex : scratchIdleIndex;
        yield return PlaySocialIdle(idleIndex, duration);
    }

    private int GetStandingIdleOptionCount()
    {
        int stateCount = standingIdleStateHashes != null ? standingIdleStateHashes.Length : 0;
        int importedSingleCount = useImportedStandingIdleClipsInEditor ? ImportedStandingIdleClipSuffixes.Length : 0;
        return stateCount + importedSingleCount;
    }

    private bool CanPlayStandingIdleOption(int optionIndex)
    {
        int stateCount = standingIdleStateHashes != null ? standingIdleStateHashes.Length : 0;
        if (optionIndex < stateCount)
        {
            return standingIdleStateHashes[optionIndex] != 0;
        }

        int importedIndex = optionIndex - stateCount;
        if (useImportedStandingIdleClipsInEditor && importedIndex < ImportedStandingIdleClipSuffixes.Length)
        {
            AnimationClip clip;
            return TryGetImportedStandingIdleClip(ImportedStandingIdleClipSuffixes[importedIndex], out clip);
        }

        return false;
    }

    private IEnumerator PlayStandingIdleOption(int optionIndex, float duration)
    {
        int stateCount = standingIdleStateHashes != null ? standingIdleStateHashes.Length : 0;
        if (optionIndex < stateCount)
        {
            yield return PlayOneShotAnimation(standingIdleStateHashes[optionIndex], standingIdleStateNames[optionIndex], idleResetIndex, duration);
            yield break;
        }

        int importedIndex = optionIndex - stateCount;
        if (useImportedStandingIdleClipsInEditor && importedIndex < ImportedStandingIdleClipSuffixes.Length)
        {
            string idleSuffix = ImportedStandingIdleClipSuffixes[importedIndex];
            AnimationClip clip;
            if (TryGetImportedStandingIdleClip(idleSuffix, out clip))
            {
                yield return PlayImportedStandingIdleClip(clip, duration, false, GetImportedStandingIdleAudioClip(idleSuffix));
                yield break;
            }
        }
    }

    private IEnumerator PlayImportedStandingIdleClip(AnimationClip clip, float duration, bool loop, AudioClip startAudioClip)
    {
        if (clip == null)
        {
            yield break;
        }

        isPlayingOneShotAnimation = true;
        StopAgent();
        yield return WaitForLocomotionToSettle();
        animator.SetBool(MoveHash, false);
        SetNeutralIdle();
        PlaySpatialOneShot(startAudioClip, transform.position, idleActionVolume);

        float targetDuration = loop
            ? Mathf.Max(0.05f, duration)
            : Mathf.Max(Mathf.Max(0.05f, duration), clip.length * 0.95f);
        yield return PlayDirectClip(clip, targetDuration, loop);

        SetNeutralIdle();
        isPlayingOneShotAnimation = false;
    }

    private AudioClip GetImportedStandingIdleAudioClip(string suffix)
    {
        if (string.Equals(suffix, "Idle_3", System.StringComparison.Ordinal))
        {
            return shakingX3Clip;
        }

        if (string.Equals(suffix, "Idle_4", System.StringComparison.Ordinal)
            || string.Equals(suffix, "Idle_7", System.StringComparison.Ordinal))
        {
            return sniffingClip;
        }

        return null;
    }

    private IEnumerator PlayImportedIdleFiveSequence(float duration)
    {
        AnimationClip startClip;
        AnimationClip loopClip;
        AnimationClip endClip;
        if (!TryGetImportedStandingIdleClip("Idle_5_start", out startClip)
            || !TryGetImportedStandingIdleClip("Idle_5_loop", out loopClip)
            || !TryGetImportedStandingIdleClip("Idle_5_end", out endClip))
        {
            yield break;
        }

        isPlayingOneShotAnimation = true;
        StopAgent();
        yield return WaitForLocomotionToSettle();
        animator.SetBool(MoveHash, false);
        SetNeutralIdle();

        yield return PlayDirectClip(startClip, Mathf.Max(0.05f, startClip.length * 0.95f), false);
        float loopDuration = Mathf.Max(0.05f, Mathf.Max(duration, importedIdleFiveLoopDuration));
        yield return PlayDirectClip(loopClip, loopDuration, true);
        yield return PlayDirectClip(endClip, Mathf.Max(0.05f, endClip.length * 0.95f), false);

        SetNeutralIdle();
        isPlayingOneShotAnimation = false;
    }

    private bool CanPlayImportedIdleFiveSequence()
    {
        AnimationClip startClip;
        AnimationClip loopClip;
        AnimationClip endClip;
        return TryGetImportedStandingIdleClip("Idle_5_start", out startClip)
            && TryGetImportedStandingIdleClip("Idle_5_loop", out loopClip)
            && TryGetImportedStandingIdleClip("Idle_5_end", out endClip);
    }

    private IEnumerator PlayOneShotAnimation(int stateHash, string stateName, int fallbackIdleIndex, float duration)
    {
        yield return PlayOneShotAnimation(stateHash, stateName, fallbackIdleIndex, duration, null);
    }

    private IEnumerator PlayOneShotAnimation(int stateHash, string stateName, int fallbackIdleIndex, float duration, AudioClip startAudioClip)
    {
        isPlayingOneShotAnimation = true;
        StopAgent();
        yield return WaitForLocomotionToSettle();

        animator.SetBool(MoveHash, false);
        SetNeutralIdle();
        PlaySpatialOneShot(startAudioClip, transform.position, idleActionVolume);

        if (stateHash != 0)
        {
            CrossFadeState(stateHash, oneShotCrossFadeDuration);
            yield return WaitForOneShotCompletion(stateHash, stateName, duration, oneShotAnimationMaxDuration);
        }
        else
        {
            animator.SetInteger(IdleIndexHash, fallbackIdleIndex);
            yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
            SetNeutralIdle();
        }

        isPlayingOneShotAnimation = false;
    }

    private IEnumerator PlayStateForDuration(int stateHash, string stateName, float minimumDuration, float crossFadeDuration)
    {
        yield return PlayStateForDuration(stateHash, stateName, minimumDuration, crossFadeDuration, true);
    }

    private IEnumerator PlayStateForDuration(int stateHash, string stateName, float minimumDuration, float crossFadeDuration, bool resetToNeutralAfter)
    {
        if (stateHash == 0)
        {
            yield break;
        }

        CrossFadeState(stateHash, crossFadeDuration);
        yield return WaitForOneShotCompletion(stateHash, stateName, minimumDuration, oneShotAnimationMaxDuration, resetToNeutralAfter);
    }

    private IEnumerator PlayNeedInteractionState(int stateHash, string stateName, string importedClipSuffix, float fallbackDuration)
    {
        if (stateHash != 0)
        {
            yield return PlayStateForDuration(stateHash, stateName, 0.2f, oneShotCrossFadeDuration, false);
            yield break;
        }

        AnimationClip clip;
        if (TryGetImportedRestClip(importedClipSuffix, out clip))
        {
            yield return PlayDirectClip(clip, Mathf.Max(0.2f, clip.length * 0.95f), false);
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0.05f, fallbackDuration));
    }

    private IEnumerator PlayNeedInteractionLoop(int stateHash, string stateName, string importedClipSuffix, float duration)
    {
        float targetDuration = Mathf.Max(0.05f, duration);
        if (stateHash != 0)
        {
            CrossFadeState(stateHash, oneShotCrossFadeDuration);
            yield return new WaitForSeconds(targetDuration);
            yield break;
        }

        AnimationClip clip;
        if (TryGetImportedRestClip(importedClipSuffix, out clip))
        {
            yield return PlayDirectClip(clip, targetDuration, true);
            yield break;
        }

        Debug.LogWarning(name + " could not find Animator state '" + stateName + "' or imported clip '" + importedClipSuffix + "'. Bowl interaction will wait without a loop animation.");
        yield return new WaitForSeconds(targetDuration);
    }

    private void CrossFadeState(int stateHash, float duration)
    {
        if (stateHash == 0)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, duration), BaseLayerIndex, 0f);
    }

    private IEnumerator WaitForOneShotCompletion(int stateHash, string stateName, float requestedDuration, float maxDuration)
    {
        yield return WaitForOneShotCompletion(stateHash, stateName, requestedDuration, maxDuration, true);
    }

    private IEnumerator WaitForOneShotCompletion(int stateHash, string stateName, float requestedDuration, float maxDuration, bool resetToNeutralAfter)
    {
        float minimumDuration = Mathf.Max(0.05f, requestedDuration);
        if (stateHash == 0)
        {
            yield return new WaitForSeconds(minimumDuration);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < animatorStateEntryTimeout && !IsAnimatorInState(stateHash, stateName))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        float clipDuration = GetCurrentClipDuration();
        float targetDuration = Mathf.Max(minimumDuration, clipDuration * 0.95f);
        float clampedMaxDuration = Mathf.Max(targetDuration, maxDuration);
        elapsed = 0f;
        while (elapsed < clampedMaxDuration)
        {
            bool inState = IsAnimatorInState(stateHash, stateName);
            if (elapsed >= targetDuration && (!inState || GetCurrentStateNormalizedTime() >= 0.95f))
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (resetToNeutralAfter)
        {
            SetNeutralIdle();
        }
    }

    private IEnumerator WaitForCurrentOneShotStateToFinish(int stateHash, string stateName, float maxWait)
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0.05f, maxWait);
        while (elapsed < timeout)
        {
            if (stateHash != 0 && !IsAnimatorInState(stateHash, stateName))
            {
                break;
            }

            if (stateHash != 0 && GetCurrentStateNormalizedTime() >= 0.95f)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool IsAnimatorInState(int stateHash, string stateName)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        if (stateInfo.fullPathHash == stateHash || stateInfo.shortNameHash == stateHash)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(stateName))
        {
            int shortHash = Animator.StringToHash(stateName);
            if (stateInfo.shortNameHash == shortHash || stateInfo.fullPathHash == shortHash)
            {
                return true;
            }
        }

        if (!animator.IsInTransition(BaseLayerIndex))
        {
            return false;
        }

        AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(BaseLayerIndex);
        if (nextStateInfo.fullPathHash == stateHash || nextStateInfo.shortNameHash == stateHash)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(stateName))
        {
            int shortHash = Animator.StringToHash(stateName);
            return nextStateInfo.shortNameHash == shortHash || nextStateInfo.fullPathHash == shortHash;
        }

        return false;
    }

    private float GetCurrentClipDuration()
    {
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(BaseLayerIndex);
        if (clips == null || clips.Length == 0 || clips[0].clip == null)
        {
            return 0f;
        }

        return clips[0].clip.length;
    }

    private float GetCurrentStateNormalizedTime()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        return stateInfo.normalizedTime;
    }

    private void SetNeutralIdle()
    {
        animator.SetInteger(IdleIndexHash, NeutralIdleIndex);
    }

    private IEnumerator WaitForWalkAnimationLeadIn()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, locomotionLeadInDuration);
        while (elapsed < duration)
        {
            animator.SetInteger(IdleIndexHash, MovementIdleIndex);
            animator.SetBool(MoveHash, true);
            animator.SetFloat(SpeedHash, GetAnimatorSpeed(CurrentPace) * animatorSpeedScale);
            animator.SetFloat(DirectionHash, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void ReleaseAgentForPrimedPath()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            pathPrimedForWalk = false;
            return;
        }

        if (!agent.hasPath && !agent.pathPending)
        {
            pathPrimedForWalk = false;
            return;
        }

        pathPrimedForWalk = false;
        agent.isStopped = false;
    }

    private void StartReleaseAgentRoutine()
    {
        StopReleaseAgentRoutine();
        releaseAgentRoutine = StartCoroutine(ReleaseAgentAfterLeadIn());
    }

    private IEnumerator ReleaseAgentAfterLeadIn()
    {
        yield return WaitForWalkAnimationLeadIn();
        releaseAgentRoutine = null;
        ReleaseAgentForPrimedPath();
    }

    private void StopReleaseAgentRoutine()
    {
        if (releaseAgentRoutine == null)
        {
            return;
        }

        StopCoroutine(releaseAgentRoutine);
        releaseAgentRoutine = null;
    }

    private int ResolveAnimatorStateHash(params string[] stateNames)
    {
        if (animator == null || stateNames == null)
        {
            return 0;
        }

        string layerName = animator.GetLayerName(BaseLayerIndex);
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrEmpty(stateName))
            {
                continue;
            }

            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(BaseLayerIndex, hash))
            {
                return hash;
            }

            if (stateName.Contains("."))
            {
                continue;
            }

            string[] candidates =
            {
                layerName + "." + stateName,
                "Base Layer." + stateName,
                layerName + ".IdleSM." + stateName,
                "Base Layer.IdleSM." + stateName,
                layerName + ".IdleSM.SitSM." + stateName,
                "Base Layer.IdleSM.SitSM." + stateName,
                layerName + ".PickupSM." + stateName,
                "Base Layer.PickupSM." + stateName
            };

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                hash = Animator.StringToHash(candidates[candidateIndex]);
                if (animator.HasState(BaseLayerIndex, hash))
                {
                    return hash;
                }
            }
        }

        return 0;
    }

    private IEnumerator WaitForLocomotionToSettle()
    {
        float elapsed = 0f;
        while (elapsed < locomotionSettleDuration || animatorSpeedValue > 0.08f)
        {
            elapsed += Time.deltaTime;
            if (elapsed > locomotionSettleDuration + 0.35f)
            {
                break;
            }

            yield return null;
        }
    }

    private void DisableLegacyControllers()
    {
        DisableIfPresent<DogMovementController>();
        DisableIfPresent<DogFreeRoam>();
        DisableIfPresent<WASDNavMeshMovement>();
        DisableIfPresent<MoveToClickPoint>();
        DisableIfPresent<DogIdleController>();
        DisableIfPresent<DogController>();
        DisableIfPresent<DogAnimationTester>();
    }

    private void DisableIfPresent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null)
        {
            component.enabled = false;
        }
    }

    private void WarnMissingNavMesh()
    {
        if (warnedMissingNavMesh)
        {
            return;
        }

        warnedMissingNavMesh = true;
        Debug.LogWarning(name + " cannot roam because it is not on a baked NavMesh. Bake navigation for David_Test's room floor.");
    }

    private void KeepInsideRoomBounds()
    {
        if (!restrictToRoomBounds || agent == null || !agent.enabled || IsInsideRoomBounds(transform.position))
        {
            return;
        }

        if (GetOutsideRoomDistance(transform.position) <= Mathf.Max(0f, outsideRoomRepathDistance))
        {
            return;
        }

        TryEnsureOnNavMesh(false);
    }

    private bool TryGetReachableRoomPoint(Vector3 candidate, float radius, out Vector3 point)
    {
        NavMeshHit hit;
        if (!TrySampleRoomPosition(candidate, radius, out hit))
        {
            point = ClampToRoomBounds(candidate);
            return false;
        }

        if (!CanReachInsideRoom(hit.position))
        {
            point = ClampToRoomBounds(candidate);
            return false;
        }

        point = hit.position;
        return true;
    }

    private bool TryReserveDestination(Vector3 destination)
    {
        if (!IsPointComfortable(destination, destinationReservationRadius, true))
        {
            return false;
        }

        ReservedDestinations[this] = destination;
        hasReservedDestination = true;
        return true;
    }

    private void ReleaseReservedDestination()
    {
        if (!hasReservedDestination)
        {
            return;
        }

        ReservedDestinations.Remove(this);
        hasReservedDestination = false;
    }

    private bool TryFindNearbyComfortablePoint(Vector3 desiredPoint, out Vector3 point)
    {
        for (int i = 0; i < 10; i++)
        {
            float angle = (360f / 10f) * i + Random.Range(-12f, 12f);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * destinationReservationRadius;
            Vector3 candidate = ClampToRoomBounds(desiredPoint + offset);
            if (TryGetReachableRoomPoint(candidate, sampleRadius, out point)
                && IsPointComfortable(point, personalSpaceRadius, true))
            {
                return true;
            }
        }

        point = desiredPoint;
        return false;
    }

    private bool IsPointComfortable(Vector3 point, float radius, bool includeReservations)
    {
        float sqrRadius = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        for (int i = 0; i < ActiveAgents.Count; i++)
        {
            DogRoomAgent other = ActiveAgents[i];
            if (other == null || other == this)
            {
                continue;
            }

            Vector3 delta = other.transform.position - point;
            delta.y = 0f;
            if (delta.sqrMagnitude < sqrRadius)
            {
                return false;
            }
        }

        if (!includeReservations)
        {
            return true;
        }

        foreach (KeyValuePair<DogRoomAgent, Vector3> reservation in ReservedDestinations)
        {
            if (reservation.Key == null || reservation.Key == this)
            {
                continue;
            }

            Vector3 delta = reservation.Value - point;
            delta.y = 0f;
            if (delta.sqrMagnitude < sqrRadius)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsCrowdedByAnotherDog(float radius)
    {
        float sqrRadius = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
        for (int i = 0; i < ActiveAgents.Count; i++)
        {
            DogRoomAgent other = ActiveAgents[i];
            if (other == null || other == this)
            {
                continue;
            }

            Vector3 delta = other.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < sqrRadius)
            {
                return true;
            }
        }

        return false;
    }

    private bool TrySampleRoomPosition(Vector3 candidate, float radius, out NavMeshHit hit)
    {
        Vector3 clampedCandidate = ClampToRoomBounds(candidate);
        float searchRadius = Mathf.Max(0.05f, radius);

        if (NavMesh.SamplePosition(clampedCandidate, out hit, searchRadius, NavMesh.AllAreas)
            && IsInsideRoomBounds(hit.position))
        {
            return true;
        }

        hit = new NavMeshHit();
        return false;
    }

    private bool TryFindFallbackRoomNavMeshPoint(out NavMeshHit hit)
    {
        float searchRadius = Mathf.Max(sampleRadius, navMeshRecoverySearchRadius);
        if (TrySampleRoomPosition(transform.position, searchRadius, out hit)
            || TrySampleRoomPosition(homePosition, searchRadius, out hit)
            || TrySampleRoomPosition(roomBoundsCenter, searchRadius, out hit))
        {
            return true;
        }

        if (restrictToRoomBounds)
        {
            Vector2 halfExtents = GetUsableRoomHalfExtents();
            int gridSteps = 4;
            for (int x = -gridSteps; x <= gridSteps; x++)
            {
                for (int z = -gridSteps; z <= gridSteps; z++)
                {
                    Vector3 candidate = roomBoundsCenter + new Vector3(
                        halfExtents.x * x / gridSteps,
                        0f,
                        halfExtents.y * z / gridSteps);

                    if (TrySampleRoomPosition(candidate, Mathf.Max(sampleRadius, 0.45f), out hit))
                    {
                        return true;
                    }
                }
            }
        }

        hit = new NavMeshHit();
        return false;
    }

    private bool CanReachInsideRoom(Vector3 destination)
    {
        PawPalToyRuntimeMetadata.AutoRegisterSceneLargeToys();
        if (!restrictToRoomBounds)
        {
            return true;
        }

        if (!IsInsideRoomBounds(destination))
        {
            return false;
        }

        if (PawPalToyRuntimeMetadata.IsDogNavigationPointBlocked(destination, transform, 0.03f))
        {
            return false;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return true;
        }

        NavMeshPath path = new NavMeshPath();
        if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        Vector3[] corners = path.corners;
        for (int i = 0; i < corners.Length; i++)
        {
            if (!IsInsideRoomBounds(corners[i]))
            {
                return false;
            }
        }

        if (PawPalToyRuntimeMetadata.IsDogNavigationPathBlocked(corners, transform, 0.03f))
        {
            return false;
        }

        return true;
    }

    private bool IsInsideRoomBounds(Vector3 point)
    {
        if (!restrictToRoomBounds)
        {
            return true;
        }

        Vector2 halfExtents = GetUsableRoomHalfExtents();
        return point.x >= roomBoundsCenter.x - halfExtents.x
            && point.x <= roomBoundsCenter.x + halfExtents.x
            && point.z >= roomBoundsCenter.z - halfExtents.y
            && point.z <= roomBoundsCenter.z + halfExtents.y;
    }

    private Vector3 ClampToRoomBounds(Vector3 point)
    {
        if (!restrictToRoomBounds)
        {
            return point;
        }

        Vector2 halfExtents = GetUsableRoomHalfExtents();
        point.x = Mathf.Clamp(point.x, roomBoundsCenter.x - halfExtents.x, roomBoundsCenter.x + halfExtents.x);
        point.z = Mathf.Clamp(point.z, roomBoundsCenter.z - halfExtents.y, roomBoundsCenter.z + halfExtents.y);
        return point;
    }

    private float GetOutsideRoomDistance(Vector3 point)
    {
        Vector3 clamped = ClampToRoomBounds(point);
        Vector2 delta = new Vector2(point.x - clamped.x, point.z - clamped.z);
        return delta.magnitude;
    }

    private Vector2 GetUsableRoomHalfExtents()
    {
        float halfX = Mathf.Max(0.1f, roomBoundsSize.x * 0.5f - Mathf.Max(0f, roomBoundsPadding));
        float halfZ = Mathf.Max(0.1f, roomBoundsSize.y * 0.5f - Mathf.Max(0f, roomBoundsPadding));
        return new Vector2(halfX, halfZ);
    }

    private void OnDrawGizmosSelected()
    {
        if (!restrictToRoomBounds)
        {
            return;
        }

        Vector2 halfExtents = GetUsableRoomHalfExtents();
        Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.35f);
        Gizmos.DrawWireCube(roomBoundsCenter, new Vector3(halfExtents.x * 2f, 0.05f, halfExtents.y * 2f));
    }
}
