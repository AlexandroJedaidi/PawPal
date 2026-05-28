using System.Collections;
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
    [SerializeField] private float blendSpeed = 4f;
    [SerializeField] private float bodyTurnSpeed = 120f;
    [SerializeField] private float bodyAssistVelocityLimit = 0.15f;
    [SerializeField] private bool disableProximityLock = true;
    [SerializeField] private bool useBoneFallback = true;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform neckTransform;
    [SerializeField, Range(0f, 1f)] private float fallbackHeadWeight = 0.65f;
    [SerializeField, Range(0f, 1f)] private float fallbackNeckWeight = 0.25f;

    [Header("Nearby Dog Glances")]
    [SerializeField] private bool lookAtNearbyDogs = true;
    [SerializeField] private float nearbyDogLookRadius = 1.75f;
    [SerializeField, Range(0f, 1f)] private float nearbyDogLookChance = 0.55f;
    [SerializeField] private float minNearbyDogLookDuration = 0.75f;
    [SerializeField] private float maxNearbyDogLookDuration = 1.6f;
    [SerializeField] private float maxNearbyDogNeckYaw = 40f;
    [SerializeField] private float maxNearbyDogNeckPitch = 22f;
    [SerializeField] private Vector3 nearbyDogLookOffset = new Vector3(0f, 0.35f, 0f);

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
    }

    private void LateUpdate()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        float desiredWeight = CanLookAtActiveTarget() ? targetWeight : 0f;
        currentWeight = Mathf.MoveTowards(currentWeight, desiredWeight, Time.deltaTime * blendSpeed);

        UpdateLookTarget();

        if (aimConstraint != null)
        {
            aimConstraint.weight = currentWeight;
        }
    }

    private IEnumerator AttentionRoutine()
    {
        yield return new WaitForSeconds(firstLookDelay);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minTimeBetweenLooks, maxTimeBetweenLooks));

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
            return;
        }

        Vector3 toTarget = lookPoint - reference.position;
        if (toTarget.sqrMagnitude < 0.001f)
        {
            return;
        }

        if (activeTargetType == AttentionTargetType.Camera)
        {
            AssistBodyTurn(toTarget);
        }

        if (lookProxy != null)
        {
            lookProxy.position = GetClampedLookPoint(reference.position, toTarget, GetActiveMaxYaw(), GetActiveMaxPitch());
        }

        if (aimConstraint == null)
        {
            ApplyBoneFallbackLook(currentWeight, lookPoint);
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
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        float yawRadians = yaw * Mathf.Deg2Rad;
        float pitchRadians = pitch * Mathf.Deg2Rad;
        float pitchCos = Mathf.Cos(pitchRadians);

        Vector3 safeLocalDirection = new Vector3(
            Mathf.Sin(yawRadians) * pitchCos,
            Mathf.Sin(pitchRadians),
            Mathf.Cos(yawRadians) * pitchCos);

        return origin + transform.TransformDirection(safeLocalDirection.normalized) * distance;
    }

    private void AssistBodyTurn(Vector3 toCamera)
    {
        if (currentWeight <= 0f || !CanBodyAssist())
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
        return roomAgent == null || (!roomAgent.IsMoving && !roomAgent.IsPreparingToMove && !roomAgent.IsSocialBusy);
    }

    private bool CanLookAtActiveTarget()
    {
        if (activeTargetType == AttentionTargetType.Camera)
        {
            return activeLookTarget != null && CanLookAtCamera();
        }

        if (activeTargetType == AttentionTargetType.NearbyDog)
        {
            return activeLookTarget != null && CanLookAtNearbyDog(activeLookTarget);
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

        if (cameraTransform != null && CanLookAtCamera())
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

            if (!CanLookAtNearbyDog(candidate.transform))
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
        if (!lookAtNearbyDogs || otherDog == null)
        {
            return false;
        }

        if (roomAgent != null && (roomAgent.IsMoving || roomAgent.IsPreparingToMove))
        {
            return false;
        }

        if (agent != null && agent.enabled)
        {
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            if (velocity.magnitude > bodyAssistVelocityLimit)
            {
                return false;
            }
        }

        Transform reference = constrainedHead != null ? constrainedHead : headTransform != null ? headTransform : transform;
        Vector3 toDog = GetNearbyDogLookPoint(otherDog) - reference.position;
        if (toDog.sqrMagnitude < 0.001f)
        {
            return false;
        }

        Vector3 flatToDog = toDog;
        flatToDog.y = 0f;
        if (flatToDog.magnitude > nearbyDogLookRadius)
        {
            return false;
        }

        Vector3 localDirection = transform.InverseTransformDirection(toDog.normalized);
        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float flatDistance = new Vector2(localDirection.x, localDirection.z).magnitude;
        float pitch = Mathf.Atan2(localDirection.y, flatDistance) * Mathf.Rad2Deg;

        return Mathf.Abs(yaw) <= maxNearbyDogNeckYaw && Mathf.Abs(pitch) <= maxNearbyDogNeckPitch;
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
            lookPoint = GetNearbyDogLookPoint(activeLookTarget);
            return true;
        }

        lookPoint = transform.position + transform.forward;
        return false;
    }

    private Vector3 GetNearbyDogLookPoint(Transform otherDog)
    {
        return otherDog.position + nearbyDogLookOffset;
    }

    private float GetActiveMaxYaw()
    {
        if (activeTargetType == AttentionTargetType.NearbyDog)
        {
            return Mathf.Min(maxNeckYaw, maxNearbyDogNeckYaw);
        }

        return maxNeckYaw;
    }

    private float GetActiveMaxPitch()
    {
        if (activeTargetType == AttentionTargetType.NearbyDog)
        {
            return Mathf.Min(maxNeckPitch, maxNearbyDogNeckPitch);
        }

        return maxNeckPitch;
    }

    private void ApplyBoneFallbackLook(float weight, Vector3 targetPoint)
    {
        if (!useBoneFallback || weight <= 0f || headTransform == null)
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

    private void ApplyBoneLookRotation(Transform bone, Vector3 safeDirection, float weight)
    {
        if (bone == null || weight <= 0f || safeDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion delta = Quaternion.FromToRotation(bone.forward, safeDirection);
        Quaternion targetRotation = delta * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, Mathf.Clamp01(weight));
    }

    private Transform FindBone(string namePart)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
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
