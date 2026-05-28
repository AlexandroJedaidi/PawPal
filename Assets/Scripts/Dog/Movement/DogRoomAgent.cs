using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum DogMovementPace
{
    Walk,
    Trot,
    Run
}

internal enum DogAmbientActionType
{
    ShortIdle,
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
    private const int BaseLayerIndex = 0;
    private const int MovementIdleIndex = -1;
    private static readonly HashSet<Transform> ClaimedToyTransforms = new HashSet<Transform>();

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

    [Header("Movement")]
    [SerializeField] private float calmWalkSpeed = 0.38f;
    [SerializeField] private float acceleration = 4.5f;
    [SerializeField] private float angularSpeed = 420f;
    [SerializeField] private float stoppingDistance = 0.15f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float animatorSpeedScale = 1f;
    [SerializeField] private float walkAnimatorSpeed = 0.5f;
    [SerializeField] private float arrivalSlowdownDistance = 0.65f;
    [SerializeField] private float animatorSpeedDampTime = 0.16f;
    [SerializeField] private float animatorDirectionDampTime = 0.12f;
    [SerializeField] private float movingVelocityThreshold = 0.05f;
    [SerializeField] private float preMoveTurnSpeed = 180f;
    [SerializeField] private float preMoveMaxTurnTime = 1.1f;
    [SerializeField] private float preMoveTurnAngle = 8f;
    [SerializeField] private float preMovePauseDuration = 0.12f;
    [SerializeField] private string locomotionStateName = "Locomotion";
    [SerializeField] private float locomotionCrossFadeDuration = 0.12f;
    [SerializeField] private float locomotionLeadInDuration = 0.2f;

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
    [SerializeField, Range(0f, 1f)] private float ambientIdleChanceAfterRoam = 0.7f;
    [SerializeField] private float minAmbientIdleDuration = 1.4f;
    [SerializeField] private float maxAmbientIdleDuration = 2.4f;
    [SerializeField, Range(0f, 1f)] private float ambientChillChance = 0.45f;
    [SerializeField, Range(0f, 1f)] private float ambientSleepChance = 0.18f;
    [SerializeField] private float minAmbientChillDuration = 4.5f;
    [SerializeField] private float maxAmbientChillDuration = 7.5f;
    [SerializeField] private float minAmbientSleepDuration = 8f;
    [SerializeField] private float maxAmbientSleepDuration = 13f;
    [SerializeField] private float minimumSecondsBetweenAmbientSleeps = 18f;
    [SerializeField] private float sitToLieDelay = 0.85f;
    [SerializeField] private float restCrossFadeDuration = 0.18f;
    [SerializeField] private float wakePauseDuration = 0.45f;
    [SerializeField] private string lieLoopStateName = "CatSimple_Lie_side_loop_1";
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
    [SerializeField] private float toyFaceDuration = 0.45f;
    [SerializeField] private float pickupAttachDelay = 0.55f;
    [SerializeField] private float pickupAnimationDuration = 1.15f;
    [SerializeField] private float minToyCarryDuration = 2f;
    [SerializeField] private float maxToyCarryDuration = 4f;
    [SerializeField] private bool carryToyToNewSpot = true;
    [SerializeField] private float putDownReleaseDelay = 0.45f;
    [SerializeField] private float putDownAnimationDuration = 0.9f;
    [SerializeField] private Vector3 toyDropOffset = new Vector3(0f, 0.04f, 0.45f);

    private Animator animator;
    private NavMeshAgent agent;
    private ToyAttach toyAttach;
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
    private bool pathPrimedForWalk;
    private bool warnedMissingLocomotionState;
    private Coroutine releaseAgentRoutine;
    private Transform claimedToy;
    private GameObject heldToy;
    private float nextAllowedToyPickupTime;
    private float nextAllowedAmbientSleepTime;
    private int lieLoopStateHash;
    private int sitEndStateHash;
    private bool isResting;
    private bool isSleeping;

    public bool IsBusy { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsPreparingToMove { get; private set; }
    public DogMovementPace CurrentPace { get; private set; } = DogMovementPace.Walk;
    public bool IsSocialBusy => IsBusy;
    public bool IsResting => isResting;
    public bool IsSleeping => isSleeping;
    public bool CanJoinSocialInteraction => !IsBusy
        && !socialPaused
        && !isResting
        && !isSleeping
        && heldToy == null
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
        lieLoopStateHash = ResolveAnimatorStateHash(lieLoopStateName);
        sitEndStateHash = ResolveAnimatorStateHash(sitEndStateName);
        animator.applyRootMotion = false;
        homePosition = ClampToRoomBounds(transform.position);
    }

    private void OnEnable()
    {
        DogSocialDirector.Register(this);
    }

    private void Start()
    {
        StartRoaming();
    }

    private void OnDisable()
    {
        ClearRestState();
        DropHeldToyImmediately();
        ReleaseClaimedToy();
        DogSocialDirector.Unregister(this);
    }

    private void Update()
    {
        KeepInsideRoomBounds();
        UpdateAnimator();
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
        ClearRestState();
        animator.SetInteger(IdleIndexHash, idleResetIndex);
        TryEnsureOnNavMesh(false);
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        roamRoutine = StartCoroutine(RoamRoutine());
    }

    public void PauseForSocial()
    {
        socialPaused = true;
        IsBusy = true;
        ClearRestState();
        DropHeldToyImmediately();
        ReleaseClaimedToy();

        if (roamRoutine != null)
        {
            StopCoroutine(roamRoutine);
            roamRoutine = null;
        }

        IsPreparingToMove = false;
        StopAgent();
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

        PrimeDestination(roomPoint, pace);
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

    public IEnumerator PlayTailWag()
    {
        yield return PlaySocialIdle(tailWagIdleIndex, socialIdleDuration);
    }

    public IEnumerator PlayBark(float duration)
    {
        float barkDuration = duration > 0f ? duration : barkIdleDuration;
        yield return PlaySocialIdle(barkIdleIndex, barkDuration);
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
        StopAgent();
        yield return WaitForLocomotionToSettle();

        animator.SetBool(MoveHash, false);
        animator.SetInteger(IdleIndexHash, idleIndex);
        yield return new WaitForSeconds(duration);
        animator.SetInteger(IdleIndexHash, idleResetIndex);
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
        if (socialPaused || Random.value > ambientIdleChanceAfterRoam)
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

        int idleIndex = Random.value < 0.5f ? tailWagIdleIndex : scratchIdleIndex;
        float idleDuration = Random.Range(minAmbientIdleDuration, maxAmbientIdleDuration);
        yield return PlaySocialIdle(idleIndex, idleDuration);
    }

    private DogAmbientActionType SelectAmbientAction()
    {
        float shortIdleWeight = Mathf.Max(0f, 1f - ambientChillChance - ambientSleepChance);
        float chillWeight = Mathf.Max(0f, ambientChillChance);
        float sleepWeight = Time.time >= nextAllowedAmbientSleepTime
            ? Mathf.Max(0f, ambientSleepChance)
            : 0f;

        float totalWeight = shortIdleWeight + chillWeight + sleepWeight;
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
        if (roll < chillWeight)
        {
            return DogAmbientActionType.ChillLie;
        }

        return DogAmbientActionType.SleepLie;
    }

    private IEnumerator PlayRestIdle(float duration, DogRestFlavor restFlavor, bool allowLieLoop)
    {
        isResting = true;
        isSleeping = restFlavor == DogRestFlavor.Sleep;
        StopAgent();
        yield return WaitForLocomotionToSettle();

        animator.SetBool(MoveHash, false);
        animator.SetInteger(IdleIndexHash, sitIdleIndex);

        float clampedDuration = Mathf.Max(duration, sitToLieDelay);
        bool usedLieLoop = false;

        if (allowLieLoop && lieLoopStateHash != 0)
        {
            yield return new WaitForSeconds(sitToLieDelay);
            animator.CrossFadeInFixedTime(lieLoopStateHash, restCrossFadeDuration, BaseLayerIndex, 0f);
            usedLieLoop = true;

            float remainingLieTime = Mathf.Max(0f, clampedDuration - sitToLieDelay);
            if (remainingLieTime > 0f)
            {
                yield return new WaitForSeconds(remainingLieTime);
            }
        }
        else
        {
            yield return new WaitForSeconds(clampedDuration);
        }

        if (usedLieLoop && sitEndStateHash != 0)
        {
            animator.CrossFadeInFixedTime(sitEndStateHash, restCrossFadeDuration, BaseLayerIndex, 0f);
        }
        else
        {
            animator.SetTrigger(SitEndTriggerHash);
        }

        movementLockedUntil = Time.time + sitEndRecoveryDuration;
        yield return new WaitForSeconds(sitEndRecoveryDuration);
        if (restFlavor == DogRestFlavor.Sleep)
        {
            nextAllowedAmbientSleepTime = Time.time + minimumSecondsBetweenAmbientSleeps;
            if (wakePauseDuration > 0f)
            {
                movementLockedUntil = Mathf.Max(movementLockedUntil, Time.time + wakePauseDuration);
                yield return new WaitForSeconds(wakePauseDuration);
            }
        }

        animator.SetInteger(IdleIndexHash, idleResetIndex);
        ClearRestState();
    }

    private bool TryGetRoamPoint(out Vector3 point)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = ClampToRoomBounds(homePosition + new Vector3(randomCircle.x, 0f, randomCircle.y));

            Vector3 roomPoint;
            if (TryGetReachableRoomPoint(candidate, sampleRadius, out roomPoint))
            {
                point = roomPoint;
                return true;
            }
        }

        point = transform.position;
        return false;
    }

    private bool HasReachedDestination()
    {
        if (agent.pathPending)
        {
            return false;
        }

        return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, destinationReachedDistance);
    }

    private IEnumerator TravelTo(Vector3 worldPosition, float timeout, DogMovementPace pace, bool stopWhenSocialPaused)
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

        yield return PrepareForMovement(roomPoint);

        if (stopWhenSocialPaused && socialPaused)
        {
            yield break;
        }

        PrimeDestination(roomPoint, pace);
        yield return WaitForWalkAnimationLeadIn();
        ReleaseAgentForPrimedPath();

        float elapsed = 0f;
        while ((timeout <= 0f || elapsed < timeout) && !HasReachedDestination())
        {
            if (stopWhenSocialPaused && socialPaused)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopAgent();
    }

    private IEnumerator PrepareForMovement(Vector3 destination)
    {
        IsPreparingToMove = true;
        StopAgent();
        animator.SetBool(MoveHash, false);

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

        float elapsed = 0f;
        while (elapsed < preMoveMaxTurnTime)
        {
            direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                yield break;
            }

            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float angle = Quaternion.Angle(transform.rotation, lookRotation);
            if (angle <= preMoveTurnAngle)
            {
                yield break;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, preMoveTurnSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
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
        pathPrimedForWalk = false;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }

        if (!IsPreparingToMove && animator != null)
        {
            animator.SetInteger(IdleIndexHash, idleResetIndex);
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
            || TrySampleRoomPosition(roomBoundsCenter, Mathf.Max(sampleRadius, 2f), out hit))
        {
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            agent.Warp(hit.position);
            homePosition = ClampToRoomBounds(homePosition);
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
    }

    private void PrimeDestination(Vector3 destination, DogMovementPace pace)
    {
        StopReleaseAgentRoutine();
        ApplyPace(pace);
        pathPrimedForWalk = true;
        animatorDirectionValue = 0f;
        animatorDirectionVelocity = 0f;
        animatorSpeedValue = walkAnimatorSpeed * animatorSpeedScale;
        animatorSpeedVelocity = 0f;
        animator.SetInteger(IdleIndexHash, MovementIdleIndex);
        animator.SetBool(MoveHash, true);
        animator.SetFloat(SpeedHash, animatorSpeedValue);
        animator.SetFloat(DirectionHash, 0f);

        if (locomotionStateHash != 0)
        {
            animator.CrossFadeInFixedTime(locomotionStateHash, locomotionCrossFadeDuration, BaseLayerIndex, 0f);
        }
        else if (!warnedMissingLocomotionState)
        {
            warnedMissingLocomotionState = true;
            Debug.LogWarning(name + " could not find Animator state '" + locomotionStateName + "'. Movement will still use Move/Speed/Direction parameters.");
        }

        agent.isStopped = true;
        agent.SetDestination(destination);
    }

    private void UpdateAnimator()
    {
        if (agent == null || animator == null)
        {
            return;
        }

        Vector3 velocity = agent.enabled ? agent.velocity : Vector3.zero;
        velocity.y = 0f;

        bool hasMoveIntent = HasMoveIntent();
        float targetSpeed = hasMoveIntent ? walkAnimatorSpeed * animatorSpeedScale : 0f;
        if (hasMoveIntent && agent.hasPath && !agent.pathPending && agent.remainingDistance <= arrivalSlowdownDistance)
        {
            float slowFactor = Mathf.InverseLerp(destinationReachedDistance, arrivalSlowdownDistance, agent.remainingDistance);
            targetSpeed *= Mathf.Lerp(0.35f, 1f, slowFactor);
        }

        float targetDirection = 0f;

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
        return calmWalkSpeed;
    }

    private bool ShouldTryToyPickup()
    {
        return allowToyPickup
            && !socialPaused
            && heldToy == null
            && Time.time >= nextAllowedToyPickupTime
            && Random.value <= toyPickupChanceAfterRoam;
    }

    private bool TryFindPickupToy(out Transform toy)
    {
        toy = null;
        Transform[] candidates = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];
            if (!IsPickupToyCandidate(candidate))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, candidate.position);
            if (distance > toySearchRadius || distance >= bestDistance)
            {
                continue;
            }

            NavMeshHit hit;
            if (!TrySampleRoomPosition(candidate.position, sampleRadius, out hit)
                || !CanReachInsideRoom(hit.position))
            {
                continue;
            }

            bestDistance = distance;
            toy = candidate;
        }

        return TryClaimToy(toy);
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

        if (ClaimedToyTransforms.Contains(candidate))
        {
            return false;
        }

        if (candidate.GetComponent<Renderer>() == null || candidate.GetComponent<Collider>() == null)
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        if (lowerName.Contains("ballhole") || lowerName.Contains("toyterrier"))
        {
            return false;
        }

        if (candidate.gameObject.tag == "Toy")
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

        if (toy == null)
        {
            FinishToyRoutine();
            yield break;
        }

        Vector3 approachPoint;
        if (!TryGetToyApproachPoint(toy, out approachPoint))
        {
            FinishToyRoutine();
            yield break;
        }

        yield return TravelTo(approachPoint, toyApproachTimeout, DogMovementPace.Walk, true);
        if (socialPaused || toy == null)
        {
            FinishToyRoutine();
            yield break;
        }

        StopAgent();
        yield return FaceWorldPoint(toy.position, toyFaceDuration);
        if (!IsToyWithinPickupDistance(toy))
        {
            FinishToyRoutine();
            yield break;
        }

        yield return PlayPickupToy(toy.gameObject);

        if (heldToy != null && carryToyToNewSpot)
        {
            Vector3 carryDestination;
            if (TryGetRoamPoint(out carryDestination))
            {
                yield return TravelTo(carryDestination, 0f, DogMovementPace.Walk, true);
            }
        }

        if (heldToy != null)
        {
            yield return new WaitForSeconds(Random.Range(minToyCarryDuration, maxToyCarryDuration));
            yield return PlayPutDownToy();
        }

        FinishToyRoutine();
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

    private IEnumerator PlayPickupToy(GameObject toy)
    {
        animator.SetBool(MoveHash, false);
        animator.SetInteger(IdleIndexHash, MovementIdleIndex);
        animator.SetTrigger(PickupHash);

        yield return new WaitForSeconds(Mathf.Max(0f, pickupAttachDelay));

        if (!AttachToy(toy))
        {
            FinishToyRoutine();
            yield break;
        }

        float remaining = Mathf.Max(0f, pickupAnimationDuration - pickupAttachDelay);
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }
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
        return true;
    }

    private IEnumerator PlayPutDownToy()
    {
        StopAgent();
        animator.SetBool(MoveHash, false);
        animator.SetInteger(IdleIndexHash, MovementIdleIndex);
        animator.SetTrigger(PutDownHash);

        yield return new WaitForSeconds(Mathf.Max(0f, putDownReleaseDelay));
        DropHeldToyImmediately();

        float remaining = Mathf.Max(0f, putDownAnimationDuration - putDownReleaseDelay);
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }
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
        IsBusy = false;
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

    private IEnumerator WaitForWalkAnimationLeadIn()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, locomotionLeadInDuration);
        while (elapsed < duration)
        {
            animator.SetInteger(IdleIndexHash, MovementIdleIndex);
            animator.SetBool(MoveHash, true);
            animator.SetFloat(SpeedHash, walkAnimatorSpeed * animatorSpeedScale);
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
                "Base Layer.IdleSM.SitSM." + stateName
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

    private bool CanReachInsideRoom(Vector3 destination)
    {
        if (!restrictToRoomBounds)
        {
            return true;
        }

        if (!IsInsideRoomBounds(destination))
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
