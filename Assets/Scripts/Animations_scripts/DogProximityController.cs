using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class DogProximityController : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("The stationary dog the roaming dog should interact with.")]
    [SerializeField] private Transform stationaryDog;
    [Tooltip("If true, proximity starts when the assigned trigger collider is entered. Otherwise, distance checks are used.")]
    [SerializeField] private bool useTriggerDetection = true;
    [Tooltip("Optional trigger collider used for proximity checks. If left empty, a trigger on this GameObject can still call OnTriggerEnter/Exit.")]
    [SerializeField] private Collider proximityTrigger;
    [Tooltip("Distance fallback used when trigger detection is disabled.")]
    [SerializeField] private float interactionDistance = 2.5f;

    [Header("Roaming Dog")]
    [SerializeField] private DogBaseIdleController roamingBaseIdleController;
    [SerializeField] private DogMovementController roamingMovementController;
    [SerializeField] private DogAnimationBridge roamingAnimationBridge;
    [SerializeField] private NavMeshAgent roamingAgent;
    [SerializeField] private DogEyeContactRig roamingEyeContactRig;
    [SerializeField] private Transform roamingEyeContactTarget;

    [Header("Stationary Dog")]
    [SerializeField] private DogAnimationBridge stationaryAnimationBridge;
    [SerializeField] private DogEyeContactRig stationaryEyeContactRig;
    [SerializeField] private Transform stationaryEyeContactTarget;
    [SerializeField] private bool keepStationaryDogSitting = true;

    [Header("Interaction Timing")]
    [SerializeField] private float faceEachOtherDuration = 0.4f;
    [SerializeField] private float barkInterval = 0.15f;
    [SerializeField] private float postBarkRecoveryDuration = 0.3f;
    [SerializeField] private int barkCount = 2;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float neckTurnSpeed = 8f;
    [SerializeField] private float maxNeckTurnAngle = 60f;
    [SerializeField] private float maxNeckPitchAngle = 40f;
    [SerializeField] private bool requireExitBeforeRetrigger = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private bool interactionInProgress;
    [SerializeField] private bool targetInRange;
    [SerializeField] private bool waitingForExit;

    private Coroutine interactionCoroutine;
    private bool hasLoggedResolutionSummary;

    private void Reset()
    {
        roamingBaseIdleController = GetComponent<DogBaseIdleController>();
        roamingMovementController = GetComponent<DogMovementController>();
        roamingAnimationBridge = GetComponent<DogAnimationBridge>();
        roamingAgent = GetComponent<NavMeshAgent>();

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
            proximityTrigger = sphereCollider;
        }
    }

    private void Awake()
    {
        if (roamingBaseIdleController == null)
        {
            roamingBaseIdleController = GetComponent<DogBaseIdleController>();
        }

        if (roamingMovementController == null)
        {
            roamingMovementController = GetComponent<DogMovementController>();
        }

        if (roamingAnimationBridge == null)
        {
            roamingAnimationBridge = GetComponent<DogAnimationBridge>();
        }

        if (roamingAgent == null)
        {
            roamingAgent = GetComponent<NavMeshAgent>();
        }

        if (roamingEyeContactRig == null)
        {
            roamingEyeContactRig = ResolveOrAddEyeContactRig(transform);
        }

        if (stationaryDog != null)
        {
            if (stationaryAnimationBridge == null)
            {
                stationaryAnimationBridge = ResolveComponentInHierarchy<DogAnimationBridge>(stationaryDog);
            }

            if (stationaryEyeContactRig == null)
            {
                stationaryEyeContactRig = ResolveOrAddEyeContactRig(stationaryDog);
            }
        }

        if (roamingEyeContactTarget == null)
        {
            roamingEyeContactTarget = FindLookTarget(transform);
        }

        if (stationaryEyeContactTarget == null && stationaryDog != null)
        {
            stationaryEyeContactTarget = FindLookTarget(stationaryDog);
        }

        LogResolutionSummary();
    }

    private void Start()
    {
        if (keepStationaryDogSitting && stationaryAnimationBridge != null)
        {
            stationaryAnimationBridge.TriggerSit();
        }
    }

    private void Update()
    {
        if (stationaryDog == null || interactionInProgress || useTriggerDetection)
        {
            return;
        }

        float currentDistance = Vector3.Distance(transform.position, stationaryDog.position);
        targetInRange = currentDistance <= interactionDistance;

        if (!targetInRange)
        {
            waitingForExit = false;
            return;
        }

        TryStartInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTriggerDetection || stationaryDog == null)
        {
            return;
        }

        if (!BelongsToStationaryDog(other))
        {
            return;
        }

        targetInRange = true;
        TryStartInteraction();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useTriggerDetection || stationaryDog == null)
        {
            return;
        }

        if (!BelongsToStationaryDog(other))
        {
            return;
        }

        targetInRange = false;
        waitingForExit = false;
    }

    private void TryStartInteraction()
    {
        if (interactionInProgress)
        {
            return;
        }

        if (requireExitBeforeRetrigger && waitingForExit)
        {
            return;
        }

        if (interactionCoroutine != null)
        {
            StopCoroutine(interactionCoroutine);
        }

        if (enableDebugLogging)
        {
            Debug.Log(
                $"DogProximityController on '{name}' starting interaction. " +
                $"stationaryDog='{SafeName(stationaryDog)}', roamingTarget='{SafeName(roamingEyeContactTarget)}', " +
                $"stationaryTarget='{SafeName(stationaryEyeContactTarget)}'",
                this);
        }

        interactionCoroutine = StartCoroutine(InteractionSequence());
    }

    private IEnumerator InteractionSequence()
    {
        interactionInProgress = true;
        waitingForExit = requireExitBeforeRetrigger;

        PauseRoamingMovement();
        KeepStationaryDogIdle();
        BeginEyeContact();

        float faceEndTime = Time.time + faceEachOtherDuration;
        while (Time.time < faceEndTime)
        {
            FaceEachOther();
            yield return null;
        }

        int totalBarks = Mathf.Max(0, barkCount);
        for (int barkIndex = 0; barkIndex < totalBarks; barkIndex++)
        {
            FaceEachOther();
            if (roamingAnimationBridge != null)
            {
                roamingAnimationBridge.TriggerBark();
            }

            yield return WaitForBarkToFinish();

            if (barkIndex < totalBarks - 1)
            {
                float barkGapEndTime = Time.time + barkInterval;
                while (Time.time < barkGapEndTime)
                {
                    FaceEachOther();
                    yield return null;
                }
            }
        }

        float recoveryEndTime = Time.time + postBarkRecoveryDuration;
        while (Time.time < recoveryEndTime)
        {
            FaceEachOther();
            yield return null;
        }

        EndEyeContact();
        ResumeRoamingMovement();
        interactionInProgress = false;
        interactionCoroutine = null;

        if (!useTriggerDetection && stationaryDog != null)
        {
            float currentDistance = Vector3.Distance(transform.position, stationaryDog.position);
            targetInRange = currentDistance <= interactionDistance;
            if (!targetInRange)
            {
                waitingForExit = false;
            }
        }
    }

    private IEnumerator WaitForBarkToFinish()
    {
        if (roamingAnimationBridge == null)
        {
            yield break;
        }

        bool barkStateObserved = false;
        float timeoutAt = Time.time + 5f;

        while (Time.time < timeoutAt)
        {
            FaceEachOther();

            if (roamingAnimationBridge.IsInBarkState())
            {
                barkStateObserved = true;
            }
            else if (barkStateObserved && !roamingAnimationBridge.IsTransitioning())
            {
                yield break;
            }

            yield return null;
        }
    }

    private void PauseRoamingMovement()
    {
        if (roamingBaseIdleController != null)
        {
            roamingBaseIdleController.BeginExternalPause();
        }

        if (roamingMovementController != null)
        {
            roamingMovementController.enabled = false;
        }

        if (roamingAgent != null && roamingAgent.enabled && roamingAgent.isOnNavMesh)
        {
            roamingAgent.isStopped = true;
            roamingAgent.ResetPath();
        }

        if (roamingAnimationBridge != null)
        {
            roamingAnimationBridge.SetIdle();
            roamingAnimationBridge.ClearFacingOverride();
        }
    }

    private void ResumeRoamingMovement()
    {
        if (roamingMovementController != null)
        {
            roamingMovementController.enabled = true;
        }

        if (roamingAgent != null && roamingAgent.enabled && roamingAgent.isOnNavMesh)
        {
            roamingAgent.isStopped = false;
        }

        if (roamingBaseIdleController != null)
        {
            roamingBaseIdleController.EndExternalPause();
        }

        if (roamingAnimationBridge != null)
        {
            roamingAnimationBridge.ClearFacingOverride();
        }
    }

    private void KeepStationaryDogIdle()
    {
        if (stationaryAnimationBridge == null)
        {
            return;
        }

        if (keepStationaryDogSitting)
        {
            stationaryAnimationBridge.TriggerSit();
            return;
        }

        stationaryAnimationBridge.SetIdle();
    }

    private void FaceEachOther()
    {
        if (stationaryDog == null)
        {
            return;
        }

        Vector3 directionToStationary = stationaryDog.position - transform.position;
        Vector3 directionToRoaming = transform.position - stationaryDog.position;
        Vector3 roamingBodyDirection = GetBodyFacingDirection(transform, directionToStationary);
        Vector3 stationaryBodyDirection = GetBodyFacingDirection(stationaryDog, directionToRoaming);

        if (roamingAnimationBridge != null)
        {
            roamingAnimationBridge.FaceDirection(roamingBodyDirection, turnSpeed);
        }
        else
        {
            RotateTransform(transform, roamingBodyDirection);
        }

        if (stationaryAnimationBridge != null)
        {
            stationaryAnimationBridge.FaceDirection(stationaryBodyDirection, turnSpeed);
        }
        else
        {
            RotateTransform(stationaryDog, stationaryBodyDirection);
        }

    }

    private void BeginEyeContact()
    {
        if (roamingEyeContactRig != null && stationaryEyeContactTarget != null)
        {
            roamingEyeContactRig.BeginEyeContact(stationaryEyeContactTarget, maxNeckTurnAngle, maxNeckPitchAngle, neckTurnSpeed);
        }

        if (stationaryEyeContactRig != null && roamingEyeContactTarget != null)
        {
            stationaryEyeContactRig.BeginEyeContact(roamingEyeContactTarget, maxNeckTurnAngle, maxNeckPitchAngle, neckTurnSpeed);
        }
    }

    private void EndEyeContact()
    {
        if (roamingEyeContactRig != null)
        {
            roamingEyeContactRig.EndEyeContact();
        }

        if (stationaryEyeContactRig != null)
        {
            stationaryEyeContactRig.EndEyeContact();
        }
    }

    private void RotateTransform(Transform targetTransform, Vector3 worldDirection)
    {
        Vector3 flatDirection = worldDirection;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        targetTransform.rotation = Quaternion.Slerp(targetTransform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    private Vector3 GetBodyFacingDirection(Transform dogTransform, Vector3 desiredDirection)
    {
        Vector3 flatDesiredDirection = desiredDirection;
        flatDesiredDirection.y = 0f;
        if (flatDesiredDirection.sqrMagnitude <= 0.0001f)
        {
            return dogTransform.forward;
        }

        Vector3 currentForward = dogTransform.forward;
        currentForward.y = 0f;
        currentForward.Normalize();

        Vector3 desiredForward = flatDesiredDirection.normalized;
        float signedAngleToTarget = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up);
        if (Mathf.Abs(signedAngleToTarget) <= maxNeckTurnAngle)
        {
            return currentForward;
        }

        float clampedNeckAngle = Mathf.Clamp(signedAngleToTarget, -maxNeckTurnAngle, maxNeckTurnAngle);
        float bodyTurnAngle = signedAngleToTarget - clampedNeckAngle;
        Quaternion bodyRotation = Quaternion.AngleAxis(bodyTurnAngle, Vector3.up);
        return bodyRotation * currentForward;
    }

    private bool BelongsToStationaryDog(Collider other)
    {
        if (stationaryDog == null || other == null)
        {
            return false;
        }

        Transform otherRoot = other.transform.root;
        Transform stationaryRoot = stationaryDog.root;
        return other.transform == stationaryDog || otherRoot == stationaryRoot;
    }

    private Transform FindLookTarget(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform head = FindDeepChild(root, "head");
        return head != null ? head : root;
    }

    private T ResolveComponentInHierarchy<T>(Transform root) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        T component = root.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        component = root.GetComponentInParent<T>();
        if (component != null)
        {
            return component;
        }

        return root.GetComponentInChildren<T>(true);
    }

    private DogEyeContactRig ResolveOrAddEyeContactRig(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        DogEyeContactRig existingRig = ResolveComponentInHierarchy<DogEyeContactRig>(root);
        if (existingRig != null)
        {
            return existingRig;
        }

        Animator targetAnimator = ResolveComponentInHierarchy<Animator>(root);
        if (targetAnimator != null)
        {
            return targetAnimator.gameObject.AddComponent<DogEyeContactRig>();
        }

        return root.gameObject.AddComponent<DogEyeContactRig>();
    }

    private void LogResolutionSummary()
    {
        if (!enableDebugLogging || hasLoggedResolutionSummary)
        {
            return;
        }

        hasLoggedResolutionSummary = true;
        Debug.Log(
            $"DogProximityController '{name}' resolved: " +
            $"stationaryDog='{SafeName(stationaryDog)}', roamingEyeContactRig='{SafeName(roamingEyeContactRig)}', " +
            $"stationaryEyeContactRig='{SafeName(stationaryEyeContactRig)}', " +
            $"roamingEyeContactTarget='{SafeName(roamingEyeContactTarget)}', " +
            $"stationaryEyeContactTarget='{SafeName(stationaryEyeContactTarget)}'",
            this);
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform match = FindDeepChild(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private string SafeName(Object obj)
    {
        return obj != null ? obj.name : "<null>";
    }
}
