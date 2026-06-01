using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class DogCameraAttention : MonoBehaviour
{
    private enum AttentionTargetType
    {
        None,
        Camera,
        NearbyDog
    }

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float minTimeBetweenLooks = 2f;
    [SerializeField] private float maxTimeBetweenLooks = 7f;
    [SerializeField] private float firstLookDelay = 0.75f;
    [SerializeField] private float minLookDuration = 1f;
    [SerializeField] private float maxLookDuration = 2.5f;
    [SerializeField] private float maxNeckYaw = 55f;
    [SerializeField] private float maxNeckPitch = 30f;
    [SerializeField] private float safeHorizontalLookAngle = 30f;
    [SerializeField] private bool yawOnlyLook = true;
    [SerializeField] private float blendSpeed = 4f;
    [SerializeField] private float lookPointSmoothTime = 0.08f;
    [SerializeField] private float bodyTurnSpeed = 120f;
    [SerializeField] private float bodyAssistVelocityLimit = 0.15f;
    [SerializeField] private bool disableProximityLock = true;
    [SerializeField] private bool useBoneFallback = true;
    [SerializeField] private bool allowBoneFallbackWithoutRig = false;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform neckTransform;
    [SerializeField, Range(0f, 1f)] private float fallbackHeadWeight = 0.65f;
    [SerializeField, Range(0f, 1f)] private float fallbackNeckWeight = 0.25f;
    [SerializeField] private float reactionHeadTiltAngle = 14f;
    [SerializeField] private float reactionHeadTiltDuration = 0.6f;
    [SerializeField, Range(0f, 1f)] private float reactionNeckTiltWeight = 0.4f;

    [Header("Nearby Dog Glances")]
    [SerializeField] private bool lookAtNearbyDogs = true;
    [SerializeField] private float nearbyDogLookRadius = 1.75f;
    [SerializeField, Range(0f, 1f)] private float nearbyDogLookChance = 0.55f;
    [SerializeField] private float minNearbyDogLookDuration = 0.75f;
    [SerializeField] private float maxNearbyDogLookDuration = 1.6f;
    [SerializeField] private float maxNearbyDogNeckYaw = 40f;
    [SerializeField] private float maxNearbyDogNeckPitch = 22f;
    [SerializeField] private bool holdNearbyDogLookWhileClose = true;
    [SerializeField] private bool allowNearbyDogPitch = true;
    [SerializeField] private float nearbyDogScanInterval = 0.15f;
    [SerializeField] private Vector3 nearbyDogHeadLookOffset = Vector3.zero;
    [SerializeField] private Vector3 nearbyDogLookOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Social Dog Look")]
    [SerializeField] private bool holdSocialDogHeadLook = true;
    [SerializeField] private float socialDogLookRadius = 2.5f;
    [SerializeField] private float socialDogMaxNeckYaw = 70f;
    [SerializeField] private float socialDogMaxNeckPitch = 50f;
    [SerializeField, Range(0f, 1f)] private float socialDogLookWeight = 1f;
    [SerializeField] private float socialDogLookBlendSpeed = 2.2f;
    [SerializeField] private float socialDogLookPointSmoothTime = 0.22f;
    [SerializeField] private bool allowSocialBoneFallbackWithoutRig = true;
    [SerializeField] private bool forceSocialBoneFallback = true;
    [SerializeField] private bool assistSocialBodyTurn = true;
    [SerializeField] private bool allowSocialLookDuringOneShotAnimations = false;
    [SerializeField] private float socialBodyTurnYawThreshold = 12f;
    [SerializeField] private float socialBodyTurnSpeed = 220f;
    [SerializeField] private float socialBodyTurnVelocityLimit = 0.12f;

    private MultiAimConstraint aimConstraint;
    private RigBuilder rigBuilder;
    private NavMeshAgent agent;
    private DogRoomAgent roomAgent;
    private Transform constrainedHead;
    private Transform lookProxy;
    private Coroutine attentionRoutine;
    private float targetWeight;
    private float currentWeight;
    private AttentionTargetType activeTargetType;
    private Transform activeLookTarget;
    private bool proximityLookOverrideActive;
    private Transform proximityLookTarget;
    private bool socialLookOverrideActive;
    private Transform socialLookTarget;
    private Transform socialHeadLookTarget;
    private bool cameraLookOverrideActive;
    private float cameraLookOverrideUntil;
    private float nextNearbyDogScanTime;
    private Vector3 smoothedLookPoint;
    private Vector3 smoothedLookPointVelocity;
    private Transform smoothedLookTarget;
    private AttentionTargetType smoothedLookTargetType;
    private bool smoothedLookWasSocial;
    private bool hasSmoothedLookPoint;
    private float headTiltStartedAt;
    private float headTiltUntil;
    private float requestedHeadTiltAngle;
    private float requestedHeadTiltDuration;
    private Quaternion appliedHeadTilt = Quaternion.identity;
    private Quaternion appliedNeckTilt = Quaternion.identity;
    private readonly Dictionary<Transform, Transform> cachedHeadTargetsByDog = new Dictionary<Transform, Transform>();

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        aimConstraint = GetComponentInChildren<MultiAimConstraint>();
        rigBuilder = GetComponent<RigBuilder>();
        agent = GetComponent<NavMeshAgent>();
        roomAgent = GetComponent<DogRoomAgent>();

        if (disableProximityLock)
        {
            ProximityLock proximityLock = GetComponent<ProximityLock>();
            if (proximityLock != null)
            {
                proximityLock.enabled = false;
            }
        }

        if (aimConstraint != null)
        {
            constrainedHead = aimConstraint.data.constrainedObject;
            lookProxy = new GameObject(name + "_CameraLookProxy").transform;
            lookProxy.SetParent(transform, false);
            lookProxy.localPosition = Vector3.forward;
            AssignLookProxy();
            aimConstraint.weight = 0f;
        }

        if (headTransform == null)
        {
            headTransform = FindBone("head");
        }

        if (neckTransform == null)
        {
            neckTransform = FindBone("neck");
        }
    }

    private void OnEnable()
    {
        attentionRoutine = StartCoroutine(AttentionRoutine());
    }

    private void OnDisable()
    {
        if (attentionRoutine != null)
        {
            StopCoroutine(attentionRoutine);
            attentionRoutine = null;
        }

        if (aimConstraint != null)
        {
            aimConstraint.weight = 0f;
        }

        targetWeight = 0f;
        currentWeight = 0f;
        proximityLookOverrideActive = false;
        proximityLookTarget = null;
        socialLookOverrideActive = false;
        socialLookTarget = null;
        socialHeadLookTarget = null;
        cameraLookOverrideActive = false;
        cameraLookOverrideUntil = 0f;
        activeTargetType = AttentionTargetType.None;
        activeLookTarget = null;
        headTiltStartedAt = 0f;
        headTiltUntil = 0f;
        requestedHeadTiltAngle = 0f;
        requestedHeadTiltDuration = 0f;
        appliedHeadTilt = Quaternion.identity;
        appliedNeckTilt = Quaternion.identity;
        ResetSmoothedLookPoint();
    }

    public void BeginSocialDogLook(Transform otherDog)
    {
        BeginSocialDogLook(otherDog, null);
    }

    public void BeginSocialDogLook(Transform otherDog, Transform otherDogHead)
    {
        if (!holdSocialDogHeadLook || otherDog == null || otherDog == transform)
        {
            return;
        }

        socialLookTarget = otherDog;
        socialHeadLookTarget = otherDogHead != null ? otherDogHead : GetDogHeadLookTarget(otherDog);
        socialLookOverrideActive = true;
        proximityLookOverrideActive = false;
        proximityLookTarget = null;
        activeTargetType = AttentionTargetType.NearbyDog;
        activeLookTarget = socialLookTarget;
        targetWeight = socialDogLookWeight;
    }

    public void EndSocialDogLook(Transform otherDog)
    {
        if (otherDog != null && socialLookTarget != otherDog)
        {
            return;
        }

        ClearSocialDogLookOverride();
    }

    public void RequestCameraAttention(float duration)
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform == null)
        {
            return;
        }

        if (attentionRoutine != null)
        {
            StopCoroutine(attentionRoutine);
            attentionRoutine = null;
        }

        cameraLookOverrideActive = true;
        cameraLookOverrideUntil = Time.time + Mathf.Max(0.1f, duration);
        proximityLookOverrideActive = false;
        proximityLookTarget = null;
        activeTargetType = AttentionTargetType.Camera;
        activeLookTarget = cameraTransform;
        targetWeight = 1f;
        attentionRoutine = StartCoroutine(AttentionRoutine());
    }

    public void RequestHeadTilt(float angleDegrees, float duration)
    {
        float clampedDuration = Mathf.Max(0.1f, duration > 0f ? duration : reactionHeadTiltDuration);
        float clampedAngle = Mathf.Abs(angleDegrees) > 0.01f ? angleDegrees : reactionHeadTiltAngle;
        headTiltStartedAt = Time.time;
        headTiltUntil = headTiltStartedAt + clampedDuration;
        requestedHeadTiltAngle = clampedAngle;
        requestedHeadTiltDuration = clampedDuration;
    }

    public Transform GetOwnHeadLookTarget()
    {
        if (headTransform != null)
        {
            return headTransform;
        }

        if (constrainedHead != null)
        {
            return constrainedHead;
        }

        return GetDogHeadLookTarget(transform);
    }

    private void LateUpdate()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (!UpdateCameraLookOverride() && !UpdateSocialDogLookOverride())
        {
            UpdateProximityDogLookOverride();
        }

        float desiredWeight = CanLookAtActiveTarget() ? targetWeight : 0f;
        currentWeight = Mathf.MoveTowards(currentWeight, desiredWeight, Time.deltaTime * GetActiveBlendSpeed());

        UpdateLookTarget();

        if (aimConstraint != null)
        {
            aimConstraint.weight = currentWeight;
        }

        ApplyReactionHeadTilt();
    }

    private IEnumerator AttentionRoutine()
    {
        yield return new WaitForSeconds(firstLookDelay);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minTimeBetweenLooks, maxTimeBetweenLooks));

            while (proximityLookOverrideActive || socialLookOverrideActive || cameraLookOverrideActive)
            {
                yield return null;
            }

            float lookDuration;
            while (!TryChooseLookTarget(out lookDuration))
            {
                yield return null;
            }

            targetWeight = 1f;
            float elapsed = 0f;
            while (elapsed < lookDuration)
            {
                if (CanLookAtActiveTarget())
                {
                    elapsed += Time.deltaTime;
                }
                else if (activeTargetType == AttentionTargetType.NearbyDog)
                {
                    break;
                }

                yield return null;
            }

            targetWeight = 0f;
            while (currentWeight > 0.01f)
            {
                yield return null;
            }

            activeTargetType = AttentionTargetType.None;
            activeLookTarget = null;
        }
    }

    private void AssignLookProxy()
    {
        WeightedTransformArray sources = aimConstraint.data.sourceObjects;
        sources.Clear();
        sources.Add(new WeightedTransform(lookProxy, 1f));
        aimConstraint.data.sourceObjects = sources;

        if (rigBuilder != null)
        {
            rigBuilder.Build();
        }
    }

    private void UpdateLookTarget()
    {
        Transform reference = constrainedHead != null ? constrainedHead : transform;
        Vector3 lookPoint;
        if (!TryGetActiveLookPoint(out lookPoint))
        {
            if (currentWeight <= 0.01f)
            {
                ResetSmoothedLookPoint();
            }

            return;
        }

        Vector3 toTarget = lookPoint - reference.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 clampedLookPoint = GetClampedLookPoint(reference.position, toTarget, GetActiveMaxYaw(), GetActiveMaxPitch());
        Vector3 smoothedTargetPoint = GetSmoothedLookPoint(reference, clampedLookPoint);
        Vector3 smoothedToTarget = smoothedTargetPoint - reference.position;
        if (smoothedToTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        if (activeTargetType == AttentionTargetType.Camera)
        {
            AssistBodyTurn(smoothedToTarget);
        }
        else if (IsSocialLookActiveForCurrentTarget())
        {
            AssistSocialBodyTurn(smoothedTargetPoint);
        }

        if (lookProxy != null)
        {
            lookProxy.position = smoothedTargetPoint;
        }

        if (ShouldApplyBoneFallbackLook())
        {
            ApplyBoneFallbackLook(currentWeight, smoothedTargetPoint);
        }
    }

    private void ApplyReactionHeadTilt()
    {
        if (headTransform == null)
        {
            return;
        }

        if (appliedHeadTilt != Quaternion.identity)
        {
            headTransform.localRotation *= Quaternion.Inverse(appliedHeadTilt);
            appliedHeadTilt = Quaternion.identity;
        }

        if (neckTransform != null && neckTransform != headTransform && appliedNeckTilt != Quaternion.identity)
        {
            neckTransform.localRotation *= Quaternion.Inverse(appliedNeckTilt);
            appliedNeckTilt = Quaternion.identity;
        }

        if (requestedHeadTiltDuration <= 0f || Time.time >= headTiltUntil)
        {
            return;
        }

        float normalized = Mathf.Clamp01((Time.time - headTiltStartedAt) / requestedHeadTiltDuration);
        float tiltAmount = Mathf.Sin(normalized * Mathf.PI) * requestedHeadTiltAngle;
        if (Mathf.Abs(tiltAmount) <= 0.01f)
        {
            return;
        }

        appliedHeadTilt = Quaternion.AngleAxis(tiltAmount, Vector3.forward);
        headTransform.localRotation *= appliedHeadTilt;
        if (neckTransform != null && neckTransform != headTransform)
        {
            appliedNeckTilt = Quaternion.AngleAxis(
                tiltAmount * Mathf.Clamp01(reactionNeckTiltWeight),
                Vector3.forward);
            neckTransform.localRotation *= appliedNeckTilt;
        }
    }

    private Vector3 GetClampedLookPoint(Vector3 origin, Vector3 worldDirection, float maxYaw, float maxPitch)
    {
        float distance = Mathf.Max(worldDirection.magnitude, 0.5f);
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);

        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float flatDistance = new Vector2(localDirection.x, localDirection.z).magnitude;
        float pitch = Mathf.Atan2(localDirection.y, flatDistance) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = IsPitchEnabledForActiveTarget() ? Mathf.Clamp(pitch, -maxPitch, maxPitch) : 0f;

        float yawRadians = yaw * Mathf.Deg2Rad;
        float pitchRadians = pitch * Mathf.Deg2Rad;
        float pitchCos = Mathf.Cos(pitchRadians);

        Vector3 safeLocalDirection = new Vector3(
            Mathf.Sin(yawRadians) * pitchCos,
            Mathf.Sin(pitchRadians),
            Mathf.Cos(yawRadians) * pitchCos);

        return origin + transform.TransformDirection(safeLocalDirection.normalized) * distance;
    }

    private Vector3 GetSmoothedLookPoint(Transform reference, Vector3 targetPoint)
    {
        bool socialLookActive = IsSocialLookActiveForCurrentTarget();
        bool targetChanged = activeLookTarget != smoothedLookTarget
            || activeTargetType != smoothedLookTargetType
            || socialLookActive != smoothedLookWasSocial;

        if (!hasSmoothedLookPoint || (targetChanged && currentWeight <= 0.01f))
        {
            smoothedLookPoint = GetNeutralLookPoint(reference, targetPoint);
            smoothedLookPointVelocity = Vector3.zero;
            hasSmoothedLookPoint = true;
        }
        else if (targetChanged && lookProxy != null)
        {
            Vector3 proxyDirection = lookProxy.position - reference.position;
            if (proxyDirection.sqrMagnitude > 0.001f)
            {
                smoothedLookPoint = lookProxy.position;
                smoothedLookPointVelocity = Vector3.zero;
            }
        }

        smoothedLookTarget = activeLookTarget;
        smoothedLookTargetType = activeTargetType;
        smoothedLookWasSocial = socialLookActive;

        float smoothTime = GetActiveLookPointSmoothTime();
        if (smoothTime <= 0f)
        {
            smoothedLookPoint = targetPoint;
            smoothedLookPointVelocity = Vector3.zero;
            return targetPoint;
        }

        smoothedLookPoint = Vector3.SmoothDamp(
            smoothedLookPoint,
            targetPoint,
            ref smoothedLookPointVelocity,
            smoothTime);

        Vector3 smoothedDirection = smoothedLookPoint - reference.position;
        if (smoothedDirection.sqrMagnitude < 0.001f)
        {
            return targetPoint;
        }

        smoothedLookPoint = GetClampedLookPoint(reference.position, smoothedDirection, GetActiveMaxYaw(), GetActiveMaxPitch());
        return smoothedLookPoint;
    }

    private Vector3 GetNeutralLookPoint(Transform reference, Vector3 targetPoint)
    {
        Vector3 direction = GetCurrentLookDirection(reference);
        float distance = Mathf.Max(Vector3.Distance(reference.position, targetPoint), 0.5f);
        return reference.position + direction * distance;
    }

    private Vector3 GetCurrentLookDirection(Transform reference)
    {
        if (lookProxy != null && currentWeight > 0.01f)
        {
            Vector3 proxyDirection = lookProxy.position - reference.position;
            if (proxyDirection.sqrMagnitude > 0.001f)
            {
                return proxyDirection.normalized;
            }
        }

        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private float GetActiveBlendSpeed()
    {
        float activeBlendSpeed = IsSocialLookActiveForCurrentTarget() ? socialDogLookBlendSpeed : blendSpeed;
        return Mathf.Max(0.01f, activeBlendSpeed);
    }

    private float GetActiveLookPointSmoothTime()
    {
        return Mathf.Max(0f, IsSocialLookActiveForCurrentTarget() ? socialDogLookPointSmoothTime : lookPointSmoothTime);
    }

    private void ResetSmoothedLookPoint()
    {
        hasSmoothedLookPoint = false;
        smoothedLookPoint = Vector3.zero;
        smoothedLookPointVelocity = Vector3.zero;
        smoothedLookTarget = null;
        smoothedLookTargetType = AttentionTargetType.None;
        smoothedLookWasSocial = false;
    }

    private void AssistBodyTurn(Vector3 toCamera)
    {
        if (currentWeight <= 0f || !CanBodyAssist())
        {
            return;
        }

        if (yawOnlyLook)
        {
            return;
        }

        Vector3 flatDirection = toCamera;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        float yaw = Vector3.SignedAngle(transform.forward, flatDirection.normalized, Vector3.up);
        if (Mathf.Abs(yaw) <= maxNeckYaw)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, bodyTurnSpeed * Time.deltaTime);
    }

    private bool CanBodyAssist()
    {
        if (roomAgent != null && (roomAgent.IsMoving || roomAgent.IsPreparingToMove || roomAgent.IsSocialBusy))
        {
            return false;
        }

        if (agent == null || !agent.enabled)
        {
            return true;
        }

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;
        return velocity.magnitude <= bodyAssistVelocityLimit;
    }

    private bool CanLookAtCamera()
    {
        return roomAgent == null
            || (!roomAgent.IsMoving
                && !roomAgent.IsPreparingToMove
                && !roomAgent.IsSocialBusy
                && !roomAgent.IsResting
                && !roomAgent.IsSleeping
                && !roomAgent.IsPlayingOneShotAnimation);
    }

    private bool CanLookAtActiveTarget()
    {
        if (activeTargetType == AttentionTargetType.Camera)
        {
            return activeLookTarget != null
                && CanLookAtCamera()
                && (cameraLookOverrideActive || IsTargetWithinSafeYaw(activeLookTarget.position));
        }

        if (activeTargetType == AttentionTargetType.NearbyDog)
        {
            return activeLookTarget != null && CanLookAtNearbyDog(activeLookTarget, socialLookOverrideActive && activeLookTarget == socialLookTarget);
        }

        return false;
    }

    private bool TryChooseLookTarget(out float lookDuration)
    {
        if (lookAtNearbyDogs && Random.value <= nearbyDogLookChance && TryFindNearbyDog(out activeLookTarget))
        {
            activeTargetType = AttentionTargetType.NearbyDog;
            lookDuration = Random.Range(minNearbyDogLookDuration, maxNearbyDogLookDuration);
            return true;
        }

        if (cameraTransform != null && CanLookAtCamera() && IsTargetWithinSafeYaw(cameraTransform.position))
        {
            activeTargetType = AttentionTargetType.Camera;
            activeLookTarget = cameraTransform;
            lookDuration = Random.Range(minLookDuration, maxLookDuration);
            return true;
        }

        if (lookAtNearbyDogs && TryFindNearbyDog(out activeLookTarget))
        {
            activeTargetType = AttentionTargetType.NearbyDog;
            lookDuration = Random.Range(minNearbyDogLookDuration, maxNearbyDogLookDuration);
            return true;
        }

        activeTargetType = AttentionTargetType.None;
        activeLookTarget = null;
        lookDuration = 0f;
        return false;
    }

    private bool TryFindNearbyDog(out Transform target)
    {
        target = null;
        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.None);
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < agents.Length; i++)
        {
            DogRoomAgent candidate = agents[i];
            if (candidate == null || candidate == roomAgent || candidate.transform == transform)
            {
                continue;
            }

            if (!CanLookAtNearbyDog(candidate.transform, false))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                target = candidate.transform;
            }
        }

        return target != null;
    }

    private bool CanLookAtNearbyDog(Transform otherDog)
    {
        return CanLookAtNearbyDog(otherDog, false);
    }

    private bool CanLookAtNearbyDog(Transform otherDog, bool socialOverride)
    {
        if (otherDog == null || (!lookAtNearbyDogs && !socialOverride))
        {
            return false;
        }

        if (roomAgent != null
            && (!socialOverride
                && (roomAgent.IsMoving
                    || roomAgent.IsPreparingToMove)
                || roomAgent.IsResting
                || roomAgent.IsSleeping
                || (roomAgent.IsPlayingOneShotAnimation && (!socialOverride || !allowSocialLookDuringOneShotAnimations))))
        {
            return false;
        }

        if (!socialOverride && agent != null && agent.enabled)
        {
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            if (velocity.magnitude > bodyAssistVelocityLimit)
            {
                return false;
            }
        }

        Transform reference = constrainedHead != null ? constrainedHead : headTransform != null ? headTransform : transform;
        Vector3 toDog = GetNearbyDogLookPoint(otherDog, socialOverride) - reference.position;
        if (toDog.sqrMagnitude < 0.001f)
        {
            return false;
        }

        Vector3 flatToDog = toDog;
        flatToDog.y = 0f;
        if (flatToDog.magnitude > GetNearbyDogLookRadius(socialOverride))
        {
            return false;
        }

        if (socialOverride)
        {
            return true;
        }

        Vector3 localDirection = transform.InverseTransformDirection(toDog.normalized);
        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float flatDistance = new Vector2(localDirection.x, localDirection.z).magnitude;
        float pitch = Mathf.Atan2(localDirection.y, flatDistance) * Mathf.Rad2Deg;
        return Mathf.Abs(yaw) <= GetNearbyDogMaxYaw(false)
            && (!CanUsePitchForNearbyDog() || Mathf.Abs(pitch) <= GetNearbyDogMaxPitch(false));
    }

    private bool TryGetActiveLookPoint(out Vector3 lookPoint)
    {
        if (activeTargetType == AttentionTargetType.Camera && activeLookTarget != null)
        {
            lookPoint = activeLookTarget.position;
            return true;
        }

        if (activeTargetType == AttentionTargetType.NearbyDog && activeLookTarget != null)
        {
            lookPoint = GetNearbyDogLookPoint(activeLookTarget, IsSocialLookActiveForCurrentTarget());
            return true;
        }

        lookPoint = transform.position + transform.forward;
        return false;
    }

    private Vector3 GetNearbyDogLookPoint(Transform otherDog)
    {
        return GetNearbyDogLookPoint(otherDog, false);
    }

    private Vector3 GetNearbyDogLookPoint(Transform otherDog, bool socialOverride)
    {
        if (socialOverride && socialHeadLookTarget != null)
        {
            return socialHeadLookTarget.position + nearbyDogHeadLookOffset;
        }

        Transform headTarget = GetDogHeadLookTarget(otherDog);
        if (headTarget != null)
        {
            return headTarget.position + nearbyDogHeadLookOffset;
        }

        return otherDog.position + nearbyDogLookOffset;
    }

    private float GetActiveMaxYaw()
    {
        if (activeTargetType == AttentionTargetType.Camera && cameraLookOverrideActive)
        {
            return maxNeckYaw;
        }

        if (activeTargetType == AttentionTargetType.NearbyDog)
        {
            return GetNearbyDogMaxYaw(IsSocialLookActiveForCurrentTarget());
        }

        return Mathf.Min(maxNeckYaw, safeHorizontalLookAngle);
    }

    private float GetActiveMaxPitch()
    {
        if (!IsPitchEnabledForActiveTarget())
        {
            return 0f;
        }

        if (activeTargetType == AttentionTargetType.NearbyDog)
        {
            return GetNearbyDogMaxPitch(IsSocialLookActiveForCurrentTarget());
        }

        return maxNeckPitch;
    }

    private void UpdateProximityDogLookOverride()
    {
        if (!lookAtNearbyDogs || !holdNearbyDogLookWhileClose)
        {
            ClearProximityDogLookOverride();
            return;
        }

        if (Time.time >= nextNearbyDogScanTime)
        {
            nextNearbyDogScanTime = Time.time + Mathf.Max(0.02f, nearbyDogScanInterval);
            Transform nearbyDog;
            proximityLookTarget = TryFindNearbyDog(out nearbyDog) ? nearbyDog : null;
        }

        if (proximityLookTarget != null && CanLookAtNearbyDog(proximityLookTarget, false))
        {
            proximityLookOverrideActive = true;
            activeTargetType = AttentionTargetType.NearbyDog;
            activeLookTarget = proximityLookTarget;
            targetWeight = 1f;
            return;
        }

        ClearProximityDogLookOverride();
    }

    private bool UpdateCameraLookOverride()
    {
        if (!cameraLookOverrideActive)
        {
            return false;
        }

        if (Time.time <= cameraLookOverrideUntil && cameraTransform != null)
        {
            activeTargetType = AttentionTargetType.Camera;
            activeLookTarget = cameraTransform;
            targetWeight = 1f;
            return true;
        }

        cameraLookOverrideActive = false;
        cameraLookOverrideUntil = 0f;
        targetWeight = 0f;
        activeTargetType = AttentionTargetType.None;
        activeLookTarget = null;
        return false;
    }

    private bool UpdateSocialDogLookOverride()
    {
        if (!holdSocialDogHeadLook || socialLookTarget == null)
        {
            ClearSocialDogLookOverride();
            return false;
        }

        if (CanLookAtNearbyDog(socialLookTarget, true))
        {
            socialLookOverrideActive = true;
            activeTargetType = AttentionTargetType.NearbyDog;
            activeLookTarget = socialLookTarget;
            targetWeight = socialDogLookWeight;
            return true;
        }

        targetWeight = 0f;
        return true;
    }

    private void ClearSocialDogLookOverride()
    {
        if (!socialLookOverrideActive && socialLookTarget == null)
        {
            return;
        }

        socialLookOverrideActive = false;
        socialLookTarget = null;
        socialHeadLookTarget = null;
        targetWeight = 0f;
        activeTargetType = AttentionTargetType.None;
        activeLookTarget = null;
    }

    private void ClearProximityDogLookOverride()
    {
        if (!proximityLookOverrideActive)
        {
            return;
        }

        proximityLookOverrideActive = false;
        proximityLookTarget = null;
        targetWeight = 0f;
        activeTargetType = AttentionTargetType.None;
        activeLookTarget = null;
    }

    private bool IsPitchEnabledForActiveTarget()
    {
        if (activeTargetType == AttentionTargetType.NearbyDog)
        {
            return CanUsePitchForNearbyDog();
        }

        return !yawOnlyLook;
    }

    private bool CanUsePitchForNearbyDog()
    {
        return allowNearbyDogPitch;
    }

    private float GetNearbyDogMaxYaw()
    {
        return GetNearbyDogMaxYaw(false);
    }

    private float GetNearbyDogMaxYaw(bool socialOverride)
    {
        if (socialOverride)
        {
            return Mathf.Max(maxNearbyDogNeckYaw, socialDogMaxNeckYaw);
        }

        return Mathf.Min(Mathf.Min(maxNeckYaw, maxNearbyDogNeckYaw), safeHorizontalLookAngle);
    }

    private float GetNearbyDogMaxPitch()
    {
        return GetNearbyDogMaxPitch(false);
    }

    private float GetNearbyDogMaxPitch(bool socialOverride)
    {
        if (socialOverride)
        {
            return Mathf.Max(maxNearbyDogNeckPitch, socialDogMaxNeckPitch);
        }

        return Mathf.Min(maxNeckPitch, maxNearbyDogNeckPitch);
    }

    private float GetNearbyDogLookRadius(bool socialOverride)
    {
        return socialOverride ? Mathf.Max(nearbyDogLookRadius, socialDogLookRadius) : nearbyDogLookRadius;
    }

    private bool IsSocialLookActiveForCurrentTarget()
    {
        return socialLookOverrideActive && activeLookTarget != null && activeLookTarget == socialLookTarget;
    }

    private bool CanApplyBoneFallbackLook()
    {
        return allowBoneFallbackWithoutRig || (allowSocialBoneFallbackWithoutRig && IsSocialLookActiveForCurrentTarget());
    }

    private bool ShouldApplyBoneFallbackLook()
    {
        if (!CanApplyBoneFallbackLook())
        {
            return false;
        }

        return aimConstraint == null || (forceSocialBoneFallback && IsSocialLookActiveForCurrentTarget());
    }

    private void ApplyBoneFallbackLook(float weight, Vector3 targetPoint)
    {
        if (!useBoneFallback || !CanApplyBoneFallbackLook() || weight <= 0f || headTransform == null)
        {
            return;
        }

        Transform reference = headTransform != null ? headTransform : transform;
        Vector3 toTarget = targetPoint - reference.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 clampedPoint = GetClampedLookPoint(reference.position, toTarget, GetActiveMaxYaw(), GetActiveMaxPitch());
        Vector3 safeDirection = clampedPoint - reference.position;
        if (safeDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        safeDirection.Normalize();
        ApplyBoneLookRotation(neckTransform, safeDirection, weight * fallbackNeckWeight);
        ApplyBoneLookRotation(headTransform, safeDirection, weight * fallbackHeadWeight);
    }

    private void AssistSocialBodyTurn(Vector3 targetPoint)
    {
        if (!assistSocialBodyTurn || !CanAssistSocialBodyTurn())
        {
            return;
        }

        Vector3 flatDirection = targetPoint - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        float yaw = Vector3.SignedAngle(transform.forward, flatDirection.normalized, Vector3.up);
        if (Mathf.Abs(yaw) <= Mathf.Max(0f, socialBodyTurnYawThreshold))
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, socialBodyTurnSpeed * Time.deltaTime);
    }

    private bool CanAssistSocialBodyTurn()
    {
        if (roomAgent != null && (roomAgent.IsMoving || roomAgent.IsPreparingToMove))
        {
            return false;
        }

        if (agent == null || !agent.enabled)
        {
            return true;
        }

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;
        return velocity.magnitude <= socialBodyTurnVelocityLimit;
    }

    private bool IsTargetWithinSafeYaw(Vector3 targetPoint)
    {
        Vector3 toTarget = targetPoint - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return false;
        }

        float yaw = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);
        return Mathf.Abs(yaw) <= GetActiveMaxYaw();
    }

    private void ApplyBoneLookRotation(Transform bone, Vector3 safeDirection, float weight)
    {
        if (bone == null || weight <= 0f || safeDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 currentAimAxis = GetBestBoneAimAxis(bone);
        Quaternion delta = Quaternion.FromToRotation(currentAimAxis, safeDirection);
        Quaternion targetRotation = delta * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    private Vector3 GetBestBoneAimAxis(Transform bone)
    {
        Vector3 dogForward = transform.forward;
        dogForward.y = 0f;
        if (dogForward.sqrMagnitude < 0.001f)
        {
            dogForward = Vector3.forward;
        }

        dogForward.Normalize();

        Vector3 bestAxis = bone.forward;
        float bestDot = Vector3.Dot(FlattenAxis(bestAxis), dogForward);
        TestBoneAimAxis(bone.forward, dogForward, ref bestAxis, ref bestDot);
        TestBoneAimAxis(-bone.forward, dogForward, ref bestAxis, ref bestDot);
        TestBoneAimAxis(bone.up, dogForward, ref bestAxis, ref bestDot);
        TestBoneAimAxis(-bone.up, dogForward, ref bestAxis, ref bestDot);
        TestBoneAimAxis(bone.right, dogForward, ref bestAxis, ref bestDot);
        TestBoneAimAxis(-bone.right, dogForward, ref bestAxis, ref bestDot);
        return bestAxis.normalized;
    }

    private static void TestBoneAimAxis(Vector3 candidateAxis, Vector3 dogForward, ref Vector3 bestAxis, ref float bestDot)
    {
        Vector3 flatCandidate = FlattenAxis(candidateAxis);
        float dot = Vector3.Dot(flatCandidate, dogForward);
        if (dot > bestDot)
        {
            bestAxis = candidateAxis;
            bestDot = dot;
        }
    }

    private static Vector3 FlattenAxis(Vector3 axis)
    {
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        return axis.normalized;
    }

    private Transform FindBone(string namePart)
    {
        return FindBone(transform, namePart);
    }

    private Transform FindBone(Transform root, string namePart)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (IsBoneNameMatch(children[i].name, namePart, true))
            {
                return children[i];
            }
        }

        for (int i = 0; i < children.Length; i++)
        {
            if (IsBoneNameMatch(children[i].name, namePart, false))
            {
                return children[i];
            }
        }

        return null;
    }

    private Transform GetDogHeadLookTarget(Transform dogRoot)
    {
        if (dogRoot == null)
        {
            return null;
        }

        Transform cachedHead;
        if (cachedHeadTargetsByDog.TryGetValue(dogRoot, out cachedHead) && cachedHead != null)
        {
            return cachedHead;
        }

        cachedHead = FindBone(dogRoot, "head");
        cachedHeadTargetsByDog[dogRoot] = cachedHead;
        return cachedHead;
    }

    private bool IsBoneNameMatch(string candidateName, string namePart, bool exact)
    {
        string lowerName = candidateName.ToLowerInvariant();
        string lowerPart = namePart.ToLowerInvariant();

        if (lowerName.Contains("aim") || lowerName.Contains("proxy") || lowerName.Contains("target") || lowerName.Contains("helper"))
        {
            return false;
        }

        if (exact)
        {
            return lowerName == lowerPart;
        }

        return lowerName.Contains(lowerPart);
    }
}
