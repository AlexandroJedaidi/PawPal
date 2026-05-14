using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class DogAnimationBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;

    [Header("Animator Parameters")]
    [SerializeField] private string moveBoolParameter = "Move";
    [SerializeField] private string speedFloatParameter = "Speed";
    [SerializeField] private string directionFloatParameter = "Direction";
    [SerializeField] private string idleIndexIntParameter = "IdleIndex";
    [SerializeField] private string barkTriggerParameter = "ProxBark";
    [SerializeField] private string sitEndTriggerParameter = "SitEndTrigger";

    [Header("Animator States")]
    [SerializeField] private string barkStateName = "Bark";
    [SerializeField] private string[] sitStateNames = { "Sit start", "Sit loop", "Sit end", "Sit" };

    [Header("Idle Mapping")]
    [Tooltip("Existing IdleIndex value used to enter the sit behaviour. Change this if your controller expects another value.")]
    [SerializeField] private int sitIdleIndex = 3;

    [Tooltip("Existing IdleIndex value used to enter the bark behaviour when the controller expects bark through its idle state flow.")]
    [SerializeField] private int barkIdleIndex = 2;

    [Tooltip("IdleIndex used for a neutral idle/reset request.")]
    [SerializeField] private int defaultIdleIndex = 99;

    [Tooltip("Smoothing applied to Speed and Direction values.")]
    [SerializeField] private float locomotionSmoothing = 5f;

    [Header("Root Motion")]
    [SerializeField] private bool lockVerticalPosition;

    [Header("Debug")]
    [SerializeField] private bool locomotionEnabled;
    [SerializeField] private float currentSpeed;
    [SerializeField] private float currentDirection;
    [SerializeField] private bool facingOverrideActive;
    [SerializeField] private float facingOverrideTurnSpeed;

    private readonly HashSet<string> warnedMissingParameters = new HashSet<string>();

    private int moveBoolHash;
    private int speedFloatHash;
    private int directionFloatHash;
    private int idleIndexIntHash;
    private int barkTriggerHash;
    private int sitEndTriggerHash;

    private bool hasMoveBool;
    private bool hasSpeedFloat;
    private bool hasDirectionFloat;
    private bool hasIdleIndexInt;
    private bool hasBarkTrigger;
    private bool hasSitEndTrigger;
    private Quaternion facingOverrideRotation;
    private float facingOverrideUntil;
    private float lockedVerticalPosition;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        CacheAnimatorParameters();

        animator.applyRootMotion = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        lockedVerticalPosition = transform.position.y;
    }

    private void Update()
    {
        if (facingOverrideActive && Time.time > facingOverrideUntil)
        {
            facingOverrideActive = false;
        }

        UpdateLocomotionParameters();
    }

    private void OnAnimatorMove()
    {
        if (animator == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 rootPosition = animator.rootPosition;
        rootPosition.y = lockVerticalPosition ? lockedVerticalPosition : agent.nextPosition.y;
        transform.position = rootPosition;
        if (facingOverrideActive)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                facingOverrideRotation,
                Time.deltaTime * facingOverrideTurnSpeed);
        }
        else
        {
            transform.rotation = animator.rootRotation;
        }

        agent.nextPosition = rootPosition;
    }

    public void SetMoving(bool shouldMove)
    {
        locomotionEnabled = shouldMove;
        if (hasMoveBool)
        {
            animator.SetBool(moveBoolHash, shouldMove);
        }
    }

    public void SetIdle()
    {
        SetMoving(false);
        SetIdleIndex(defaultIdleIndex);
    }

    public void TriggerSit()
    {
        SetMoving(false);
        SetIdleIndex(sitIdleIndex);
    }

    public void EndSit()
    {
        if (hasSitEndTrigger)
        {
            animator.SetTrigger(sitEndTriggerHash);
        }
    }

    public void TriggerBark()
    {
        SetMoving(false);
        SetIdleIndex(barkIdleIndex);
        if (hasBarkTrigger)
        {
            animator.SetTrigger(barkTriggerHash);
        }
    }

    public void FaceDirection(Vector3 worldDirection, float turnSpeed)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 flatDirection = worldDirection;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        facingOverrideActive = true;
        facingOverrideTurnSpeed = turnSpeed;
        facingOverrideUntil = Time.time + 0.2f;
        facingOverrideRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);

        transform.rotation = Quaternion.Slerp(transform.rotation, facingOverrideRotation, Time.deltaTime * turnSpeed);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
        }
    }

    public void ClearFacingOverride()
    {
        facingOverrideActive = false;
    }

    public bool IsInBarkState()
    {
        return IsStateActive(barkStateName);
    }

    public bool IsInSitState()
    {
        if (sitStateNames == null)
        {
            return false;
        }

        for (int index = 0; index < sitStateNames.Length; index++)
        {
            if (IsStateActive(sitStateNames[index]))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsTransitioning()
    {
        return animator != null && animator.IsInTransition(0);
    }

    private void SetIdleIndex(int idleIndex)
    {
        if (hasIdleIndexInt)
        {
            animator.SetInteger(idleIndexIntHash, idleIndex);
        }
    }

    private void UpdateLocomotionParameters()
    {
        if (animator == null || agent == null)
        {
            return;
        }

        Vector3 worldDelta = agent.nextPosition - transform.position;
        worldDelta.y = 0f;
        Vector3 localDelta = transform.InverseTransformDirection(worldDelta);
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        float targetSpeed = localDelta.z / deltaTime;
        float targetDirection = localDelta.x / deltaTime;

        if (!locomotionEnabled)
        {
            targetSpeed = 0f;
            targetDirection = 0f;
        }

        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * locomotionSmoothing);
        currentDirection = Mathf.Lerp(currentDirection, targetDirection, Time.deltaTime * locomotionSmoothing);

        if (hasSpeedFloat)
        {
            animator.SetFloat(speedFloatHash, currentSpeed);
        }

        if (hasDirectionFloat)
        {
            animator.SetFloat(directionFloatHash, currentDirection);
        }

        if (locomotionEnabled && agent.remainingDistance > agent.stoppingDistance)
        {
            Vector3 lookDirection = agent.desiredVelocity;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }

        if (agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
        }
    }

    private bool IsStateActive(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.IsName(stateName))
        {
            return true;
        }

        if (!animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
        return nextState.IsName(stateName);
    }

    private void CacheAnimatorParameters()
    {
        moveBoolHash = Animator.StringToHash(moveBoolParameter);
        speedFloatHash = Animator.StringToHash(speedFloatParameter);
        directionFloatHash = Animator.StringToHash(directionFloatParameter);
        idleIndexIntHash = Animator.StringToHash(idleIndexIntParameter);
        barkTriggerHash = Animator.StringToHash(barkTriggerParameter);
        sitEndTriggerHash = Animator.StringToHash(sitEndTriggerParameter);

        hasMoveBool = HasParameter(moveBoolParameter, AnimatorControllerParameterType.Bool);
        hasSpeedFloat = HasParameter(speedFloatParameter, AnimatorControllerParameterType.Float);
        hasDirectionFloat = HasParameter(directionFloatParameter, AnimatorControllerParameterType.Float);
        hasIdleIndexInt = HasParameter(idleIndexIntParameter, AnimatorControllerParameterType.Int);
        hasBarkTrigger = HasParameter(barkTriggerParameter, AnimatorControllerParameterType.Trigger);
        hasSitEndTrigger = HasParameter(sitEndTriggerParameter, AnimatorControllerParameterType.Trigger);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == expectedType)
            {
                return true;
            }
        }

        WarnMissingParameter(parameterName, expectedType);
        return false;
    }

    private void WarnMissingParameter(string parameterName, AnimatorControllerParameterType expectedType)
    {
        if (!warnedMissingParameters.Add(parameterName))
        {
            return;
        }

        Debug.LogWarning(
            $"DogAnimationBridge on '{name}' could not find Animator parameter '{parameterName}' of type '{expectedType}'. " +
            "Map this field to an existing parameter or add the parameter manually.");
    }
}
