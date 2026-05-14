using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class DogEyeContactRig : MonoBehaviour
{
    [Header("Auto-Find")]
    [SerializeField] private Animator animator;
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private MultiAimConstraint headAimConstraint;
    [SerializeField] private Transform neckBone;
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform lookReferenceBone;

    [Header("Tuning")]
    [SerializeField] private float neckTurnWeight = 0.7f;
    [SerializeField] private float headTurnWeight = 0.9f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private Transform currentTarget;
    [SerializeField] private bool eyeContactActive;

    private Quaternion defaultNeckLocalRotation;
    private Quaternion defaultHeadLocalRotation;
    private Vector3 defaultNeckLookDirectionLocal;
    private Vector3 defaultHeadLookDirectionLocal;
    private bool hasDefaultNeckLocalRotation;
    private bool hasDefaultHeadLocalRotation;
    private bool hasDefaultNeckLookDirection;
    private bool hasDefaultHeadLookDirection;
    private Transform headAimProxy;
    private float maxYaw = 60f;
    private float maxPitch = 40f;
    private float turnSpeed = 8f;
    private bool hasLoggedResolutionSummary;
    private bool hasLoggedMissingReferences;
    private bool hasLoggedBeginEyeContact;
    private bool hasLoggedBoneApplication;

    private void Awake()
    {
        ResolveReferences();
        EnsureHeadAimProxy();
        RestoreDefaultPoseImmediate();
        DisableHeadAim();
        LogResolutionSummary("Awake");
    }

    public void BeginEyeContact(Transform target, float maxYawAngle, float maxPitchAngle, float rotationSpeed)
    {
        ResolveReferences();
        EnsureHeadAimProxy();
        LogResolutionSummary("BeginEyeContact");

        currentTarget = target;
        maxYaw = Mathf.Max(0f, maxYawAngle);
        maxPitch = Mathf.Max(0f, maxPitchAngle);
        turnSpeed = Mathf.Max(0.01f, rotationSpeed);
        eyeContactActive = currentTarget != null;

        if (!eyeContactActive)
        {
            LogMissingEyeContactTarget();
            DisableHeadAim();
            return;
        }

        if (enableDebugLogging && !hasLoggedBeginEyeContact)
        {
            hasLoggedBeginEyeContact = true;
            Debug.Log(
                $"DogEyeContactRig on '{name}' starting eye contact. " +
                $"target='{currentTarget.name}', yaw={maxYaw}, pitch={maxPitch}, turnSpeed={turnSpeed}",
                this);
        }

        UpdateHeadAimProxy();
        EnableHeadAim();
    }

    public void EndEyeContact()
    {
        eyeContactActive = false;
        currentTarget = null;
        DisableHeadAim();
        RestoreDefaultPoseImmediate();
    }

    private void LateUpdate()
    {
        if (!eyeContactActive || currentTarget == null)
        {
            return;
        }

        UpdateHeadAimProxy();
        ApplyBoneLookRotation(
            neckBone,
            hasDefaultNeckLocalRotation,
            hasDefaultNeckLookDirection,
            defaultNeckLocalRotation,
            defaultNeckLookDirectionLocal,
            neckTurnWeight);
        ApplyBoneLookRotation(
            headBone,
            hasDefaultHeadLocalRotation,
            hasDefaultHeadLookDirection,
            defaultHeadLocalRotation,
            defaultHeadLookDirectionLocal,
            headTurnWeight);
    }

    private void ResolveReferences()
    {
        animator = SanitizeSceneComponent(animator);
        rigBuilder = SanitizeSceneComponent(rigBuilder);
        headAimConstraint = SanitizeSceneComponent(headAimConstraint);
        neckBone = SanitizeSceneTransform(neckBone);
        headBone = SanitizeSceneTransform(headBone);
        lookReferenceBone = SanitizeSceneTransform(lookReferenceBone);

        animator = ResolveAnimator();
        Transform animatedRoot = GetAnimatedRoot();

        if (rigBuilder == null)
        {
            rigBuilder = FindComponentInHierarchy<RigBuilder>(animatedRoot);
        }

        if (headAimConstraint == null)
        {
            headAimConstraint = FindHeadAimConstraint(animatedRoot);
        }

        if (neckBone == null)
        {
            neckBone = FindDeepChild(animatedRoot, "neck");
        }

        if (headBone == null)
        {
            headBone = FindDeepChild(animatedRoot, "head");
        }

        if (lookReferenceBone == null)
        {
            lookReferenceBone = FindLookReferenceBone(animatedRoot);
        }

        if (!hasDefaultNeckLocalRotation && neckBone != null)
        {
            defaultNeckLocalRotation = neckBone.localRotation;
            hasDefaultNeckLocalRotation = true;
        }

        if (!hasDefaultHeadLocalRotation && headBone != null)
        {
            defaultHeadLocalRotation = headBone.localRotation;
            hasDefaultHeadLocalRotation = true;
        }

        if (!hasDefaultNeckLookDirection && neckBone != null && lookReferenceBone != null)
        {
            Vector3 localDirection = neckBone.InverseTransformDirection(lookReferenceBone.position - neckBone.position);
            if (localDirection.sqrMagnitude > 0.0001f)
            {
                defaultNeckLookDirectionLocal = localDirection.normalized;
                hasDefaultNeckLookDirection = true;
            }
        }

        if (!hasDefaultHeadLookDirection && headBone != null && lookReferenceBone != null)
        {
            Vector3 localDirection = headBone.InverseTransformDirection(lookReferenceBone.position - headBone.position);
            if (localDirection.sqrMagnitude > 0.0001f)
            {
                defaultHeadLookDirectionLocal = localDirection.normalized;
                hasDefaultHeadLookDirection = true;
            }
        }
    }

    private Animator ResolveAnimator()
    {
        Animator[] parentAnimators = GetComponentsInParent<Animator>(true);
        if (parentAnimators != null && parentAnimators.Length > 0)
        {
            return parentAnimators[parentAnimators.Length - 1];
        }

        Animator localAnimator = GetComponent<Animator>();
        if (localAnimator != null)
        {
            return localAnimator;
        }

        return GetComponentInChildren<Animator>(true);
    }

    private Transform GetAnimatedRoot()
    {
        if (animator != null)
        {
            return animator.transform;
        }

        return transform.root != null ? transform.root : transform;
    }

    private void EnsureHeadAimProxy()
    {
        if (headAimProxy != null)
        {
            return;
        }

        Transform root = GetAnimatedRoot();
        Transform existingProxy = root.Find("EyeContactTargetProxy");
        if (existingProxy != null)
        {
            headAimProxy = existingProxy;
            return;
        }

        GameObject proxyObject = new GameObject("EyeContactTargetProxy");
        headAimProxy = proxyObject.transform;
        headAimProxy.SetParent(root, false);
        headAimProxy.localPosition = Vector3.forward;
        headAimProxy.localRotation = Quaternion.identity;
    }

    private void UpdateHeadAimProxy()
    {
        if (headAimProxy == null || currentTarget == null)
        {
            return;
        }

        headAimProxy.position = currentTarget.position;
    }

    private void ApplyBoneLookRotation(
        Transform bone,
        bool hasDefaultLocalRotation,
        bool hasDefaultLookDirection,
        Quaternion defaultLocalRotation,
        Vector3 defaultLookDirectionLocal,
        float turnWeight)
    {
        if (bone == null || currentTarget == null || !hasDefaultLocalRotation || !hasDefaultLookDirection)
        {
            LogMissingReferencesForBones();
            return;
        }

        Vector3 desiredWorldDirection = currentTarget.position - bone.position;
        if (desiredWorldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        desiredWorldDirection.Normalize();
        Vector3 currentLookWorld = bone.TransformDirection(defaultLookDirectionLocal);
        if (currentLookWorld.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion worldDelta = Quaternion.FromToRotation(currentLookWorld.normalized, desiredWorldDirection);
        float clampAngle = Mathf.Max(maxYaw, maxPitch) * Mathf.Clamp01(turnWeight);
        Quaternion clampedDelta = Quaternion.RotateTowards(Quaternion.identity, worldDelta, clampAngle);
        Quaternion targetWorldRotation = clampedDelta * bone.rotation;

        bone.rotation = Quaternion.Slerp(
            bone.rotation,
            targetWorldRotation,
            Time.deltaTime * turnSpeed);

        if (enableDebugLogging && !hasLoggedBoneApplication)
        {
            hasLoggedBoneApplication = true;
            Debug.Log(
                $"DogEyeContactRig on '{name}' applied direct bone look. " +
                $"bone='{bone.name}', lookRef='{SafeName(lookReferenceBone)}', target='{currentTarget.name}'",
                this);
        }
    }

    private void EnableHeadAim()
    {
        if (headAimConstraint == null || headAimProxy == null)
        {
            if (enableDebugLogging)
            {
                Debug.LogWarning(
                    $"DogEyeContactRig on '{name}' could not enable head aim. " +
                    $"constraint='{SafeName(headAimConstraint)}', proxy='{SafeName(headAimProxy)}'",
                    this);
            }
            return;
        }

        WeightedTransformArray sources = headAimConstraint.data.sourceObjects;
        sources.Clear();
        sources.Add(new WeightedTransform(headAimProxy, 1f));
        headAimConstraint.data.sourceObjects = sources;
        headAimConstraint.weight = 1f;

        if (rigBuilder != null)
        {
            rigBuilder.Build();
        }

        if (enableDebugLogging)
        {
            Debug.Log(
                $"DogEyeContactRig on '{name}' enabled head aim. " +
                $"constraint='{SafeName(headAimConstraint)}', proxy='{SafeName(headAimProxy)}', target='{SafeName(currentTarget)}'",
                this);
        }
    }

    private void DisableHeadAim()
    {
        if (headAimConstraint == null)
        {
            return;
        }

        WeightedTransformArray sources = headAimConstraint.data.sourceObjects;
        sources.Clear();
        headAimConstraint.data.sourceObjects = sources;
        headAimConstraint.weight = 0f;

        if (rigBuilder != null)
        {
            rigBuilder.Build();
        }
    }

    private void RestoreDefaultPoseImmediate()
    {
        if (neckBone != null && hasDefaultNeckLocalRotation)
        {
            neckBone.localRotation = defaultNeckLocalRotation;
        }

        if (headBone != null && hasDefaultHeadLocalRotation)
        {
            headBone.localRotation = defaultHeadLocalRotation;
        }
    }

    private Transform FindLookReferenceBone(Transform root)
    {
        Transform nose = FindDeepChild(root, "nose");
        if (nose != null)
        {
            return nose;
        }

        Transform mouthSocket = FindDeepChild(root, "MouthSocket");
        if (mouthSocket != null)
        {
            return mouthSocket;
        }

        Transform mouth = FindDeepChild(root, "mouth");
        if (mouth != null)
        {
            return mouth;
        }

        return headBone;
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

    private T FindComponentInHierarchy<T>(Transform root) where T : Component
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

        return root.GetComponentInChildren<T>(true);
    }

    private MultiAimConstraint FindHeadAimConstraint(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        MultiAimConstraint[] constraints = root.GetComponentsInChildren<MultiAimConstraint>(true);
        foreach (MultiAimConstraint constraint in constraints)
        {
            if (constraint != null && constraint.name.Contains("HeadAimRig"))
            {
                return constraint;
            }
        }

        return constraints.Length > 0 ? constraints[0] : null;
    }

    private T SanitizeSceneComponent<T>(T component) where T : Component
    {
        if (component == null)
        {
            return null;
        }

        return component.gameObject.scene.IsValid() ? component : null;
    }

    private Transform SanitizeSceneTransform(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return null;
        }

        return targetTransform.gameObject.scene.IsValid() ? targetTransform : null;
    }

    private void LogResolutionSummary(string phase)
    {
        if (!enableDebugLogging || hasLoggedResolutionSummary)
        {
            return;
        }

        hasLoggedResolutionSummary = true;
        Debug.Log(
            $"DogEyeContactRig '{name}' resolved during {phase}: " +
            $"animator='{SafeName(animator)}', rigBuilder='{SafeName(rigBuilder)}', " +
            $"headAimConstraint='{SafeName(headAimConstraint)}', neck='{SafeName(neckBone)}', " +
            $"head='{SafeName(headBone)}', lookRef='{SafeName(lookReferenceBone)}', " +
            $"proxy='{SafeName(headAimProxy)}', scene='{gameObject.scene.name}'",
            this);
    }

    private void LogMissingReferencesForBones()
    {
        if (!enableDebugLogging || hasLoggedMissingReferences)
        {
            return;
        }

        hasLoggedMissingReferences = true;
        Debug.LogWarning(
            $"DogEyeContactRig '{name}' cannot apply bone look. " +
            $"neck='{SafeName(neckBone)}', head='{SafeName(headBone)}', lookRef='{SafeName(lookReferenceBone)}', " +
            $"target='{SafeName(currentTarget)}', hasNeckDefault={hasDefaultNeckLocalRotation}, " +
            $"hasHeadDefault={hasDefaultHeadLocalRotation}, hasNeckLookDir={hasDefaultNeckLookDirection}, " +
            $"hasHeadLookDir={hasDefaultHeadLookDirection}",
            this);
    }

    private void LogMissingEyeContactTarget()
    {
        if (!enableDebugLogging)
        {
            return;
        }

        Debug.LogWarning(
            $"DogEyeContactRig '{name}' was asked to start eye contact without a valid target.",
            this);
    }

    private string SafeName(Object obj)
    {
        return obj != null ? obj.name : "<null>";
    }
}
