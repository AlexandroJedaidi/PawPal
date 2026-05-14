using UnityEngine;

[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(Animator))]
public class DogNeckLookDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string neckBoneName = "neck";
    [SerializeField] private string headBoneName = "head";
    [SerializeField] private float neckYawWeight = 1f;
    [SerializeField] private float neckPitchWeight = 1f;
    [SerializeField] private float headYawWeight = 0.65f;
    [SerializeField] private float headPitchWeight = 0.65f;
    [SerializeField] private bool useAnimatorBoneOverride = true;
    [SerializeField] private bool hasHumanAvatar;

    private Transform target;
    private Transform neckBone;
    private Transform headBone;
    private Quaternion defaultNeckLocalRotation;
    private Quaternion defaultHeadLocalRotation;
    private bool hasNeckDefaultRotation;
    private bool hasHeadDefaultRotation;
    private float maxYaw = 60f;
    private float maxPitch = 40f;
    private float turnSpeed = 12f;

    private void Awake()
    {
        ResolveBones();
        enabled = false;
    }

    public void Configure(float maxYawAngle, float maxPitchAngle, float rotationSpeed)
    {
        maxYaw = Mathf.Max(0f, maxYawAngle);
        maxPitch = Mathf.Max(0f, maxPitchAngle);
        turnSpeed = Mathf.Max(0.01f, rotationSpeed);
    }

    public void SetLookTarget(Transform lookTarget)
    {
        if (neckBone == null && headBone == null)
        {
            ResolveBones();
        }

        target = lookTarget;
        enabled = target != null;
    }

    public void ClearLookTarget()
    {
        target = null;
    }

    private void LateUpdate()
    {
        if (useAnimatorBoneOverride && hasHumanAvatar)
        {
            return;
        }

        if (neckBone == null && headBone == null)
        {
            ResolveBones();
        }

        if (target == null)
        {
            RestoreDefaultPose();
            enabled = false;
            return;
        }

        ApplyLook(neckBone, defaultNeckLocalRotation, hasNeckDefaultRotation, neckYawWeight, neckPitchWeight);
        ApplyLook(headBone, defaultHeadLocalRotation, hasHeadDefaultRotation, headYawWeight, headPitchWeight);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useAnimatorBoneOverride)
        {
            return;
        }

        if (animator == null || !hasHumanAvatar)
        {
            return;
        }

        if (target == null)
        {
            RestoreAnimatorBonePose();
            enabled = false;
            return;
        }

        ApplyAnimatorBoneLook(HumanBodyBones.Neck, defaultNeckLocalRotation, hasNeckDefaultRotation, neckYawWeight, neckPitchWeight);
        ApplyAnimatorBoneLook(HumanBodyBones.Head, defaultHeadLocalRotation, hasHeadDefaultRotation, headYawWeight, headPitchWeight);
    }

    private void ResolveBones()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }

        hasHumanAvatar = animator != null && animator.avatar != null && animator.avatar.isHuman;
        if (hasHumanAvatar)
        {
            neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        if (neckBone == null)
        {
            neckBone = FindDeepChildByNameContains(transform, neckBoneName);
        }

        if (headBone == null)
        {
            headBone = FindDeepChildByNameContains(transform, headBoneName);
        }

        if (neckBone != null)
        {
            defaultNeckLocalRotation = neckBone.localRotation;
            hasNeckDefaultRotation = true;
        }

        if (headBone != null)
        {
            defaultHeadLocalRotation = headBone.localRotation;
            hasHeadDefaultRotation = true;
        }
    }

    private void ApplyLook(Transform bone, Quaternion defaultRotation, bool hasDefaultRotation, float yawWeight, float pitchWeight)
    {
        if (bone == null || target == null || !hasDefaultRotation)
        {
            return;
        }

        Transform referenceSpace = bone.parent != null ? bone.parent : bone;
        Vector3 localDirection = referenceSpace.InverseTransformDirection(target.position - bone.position);
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw) * yawWeight;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch) * pitchWeight;

        Quaternion targetLocalRotation = defaultRotation * Quaternion.Euler(pitch, yaw, 0f);
        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            targetLocalRotation,
            Time.deltaTime * turnSpeed);
    }

    private void ApplyAnimatorBoneLook(HumanBodyBones boneId, Quaternion defaultRotation, bool hasDefaultRotation, float yawWeight, float pitchWeight)
    {
        if (!hasDefaultRotation || animator == null || target == null)
        {
            return;
        }

        Transform bone = animator.GetBoneTransform(boneId);
        if (bone == null)
        {
            return;
        }

        Transform referenceSpace = bone.parent != null ? bone.parent : bone;
        Vector3 localDirection = referenceSpace.InverseTransformDirection(target.position - bone.position);
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw) * yawWeight;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch) * pitchWeight;

        Quaternion targetLocalRotation = defaultRotation * Quaternion.Euler(pitch, yaw, 0f);
        animator.SetBoneLocalRotation(boneId, Quaternion.Slerp(bone.localRotation, targetLocalRotation, Time.deltaTime * turnSpeed));
    }

    private void RestoreDefaultPose()
    {
        if (neckBone != null && hasNeckDefaultRotation)
        {
            neckBone.localRotation = Quaternion.Slerp(
                neckBone.localRotation,
                defaultNeckLocalRotation,
                Time.deltaTime * turnSpeed);
        }

        if (headBone != null && hasHeadDefaultRotation)
        {
            headBone.localRotation = Quaternion.Slerp(
                headBone.localRotation,
                defaultHeadLocalRotation,
                Time.deltaTime * turnSpeed);
        }
    }

    private void RestoreAnimatorBonePose()
    {
        if (animator == null)
        {
            return;
        }

        if (hasNeckDefaultRotation)
        {
            animator.SetBoneLocalRotation(HumanBodyBones.Neck, defaultNeckLocalRotation);
        }

        if (hasHeadDefaultRotation)
        {
            animator.SetBoneLocalRotation(HumanBodyBones.Head, defaultHeadLocalRotation);
        }
    }

    private Transform FindDeepChildByNameContains(Transform parent, string childNameFragment)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childNameFragment))
        {
            return null;
        }

        string fragment = childNameFragment.ToLowerInvariant();
        if (parent.name.ToLowerInvariant().Contains(fragment))
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform match = FindDeepChildByNameContains(child, childNameFragment);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
