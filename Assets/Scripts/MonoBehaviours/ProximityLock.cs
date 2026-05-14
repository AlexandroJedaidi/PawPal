using UnityEngine;
using System.Collections.Generic; 
using UnityEngine.Animations.Rigging;

public class ProximityLock : MonoBehaviour
{
    public MultiAimConstraint aimConstraint;
    public float detectionRadius = 1f;
    public string dogLayerName = "DogFace";    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private Transform forcedTarget;
    [SerializeField] private string neckBoneName = "neck";
    [SerializeField] private string headBoneName = "head";
    [SerializeField] private float maxHorizontalNeckAngle = 60f;
    [SerializeField] private float maxVerticalNeckAngle = 40f;
    [SerializeField] private float neckTurnSpeed = 8f;
    RigBuilder rigs;
    Animator animator;
    bool hasTarget;
    Collider[] hitList;
    Transform nearestDog;
    Transform oldTarget;
    bool hasNewTarget;
    Transform neckBone;
    Transform headBone;
    Quaternion defaultNeckLocalRotation;
    bool hasDefaultNeckRotation;

    void Awake()
    {
        animator = GetComponentInParent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (aimConstraint == null)
        {
            MultiAimConstraint[] constraints = GetComponentsInChildren<MultiAimConstraint>(true);
            if (constraints.Length > 0)
            {
                aimConstraint = constraints[0];
            }
        }

        rigs = GetComponent<RigBuilder>();
        if (rigs == null)
        {
            rigs = GetComponentInParent<RigBuilder>();
        }
        if (rigs == null)
        {
            rigs = GetComponentInChildren<RigBuilder>(true);
        }

        neckBone = ResolveBone(neckBoneName, HumanBodyBones.Neck, HumanBodyBones.Head);
        headBone = ResolveBone(headBoneName, HumanBodyBones.Head, HumanBodyBones.Neck);
        if (neckBone != null)
        {
            defaultNeckLocalRotation = neckBone.localRotation;
            hasDefaultNeckRotation = true;
        }

        hasTarget = false; 
        oldTarget = null;
        nearestDog = null;
        hasNewTarget = false;
    }

    void OnDisable()
    {
        if (aimConstraint != null)
        {
            aimConstraint.weight = 0f;
        }
    }

    void OnEnable()
    {
        if (aimConstraint != null && forcedTarget != null)
        {
            aimConstraint.weight = 1f;
        }
    }

    void Update()
    {
        if (forcedTarget != null)
        {
            if (oldTarget != forcedTarget)
            {
                SetAimTarget(forcedTarget);
                RebuildRigs();
                oldTarget = forcedTarget;
            }

            return;
        }

        var res = GetNearestDog();
        nearestDog = res.dog;
        hitList = res.hits;
        if (hitList.Length != 0 && oldTarget != nearestDog)
        {
            hasNewTarget = true;
        }
        if (hitList.Length == 0)
        {
            SetAimTarget(null);
            RebuildRigs();
        }

        if (hasNewTarget)
        {
            SetAimTarget(nearestDog);
            RebuildRigs();
            oldTarget = nearestDog;
            hasNewTarget = false;
        } 
    }

    void LateUpdate()
    {
        if (neckBone == null || !hasDefaultNeckRotation)
        {
            return;
        }

        if (forcedTarget == null)
        {
            neckBone.localRotation = Quaternion.Slerp(
                neckBone.localRotation,
                defaultNeckLocalRotation,
                Time.deltaTime * neckTurnSpeed);
            return;
        }

        Transform referenceSpace = neckBone.parent != null ? neckBone.parent : transform;
        Vector3 localDirection = referenceSpace.InverseTransformDirection(forcedTarget.position - neckBone.position);
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg;
        yaw = Mathf.Clamp(yaw, -maxHorizontalNeckAngle, maxHorizontalNeckAngle);
        pitch = Mathf.Clamp(pitch, -maxVerticalNeckAngle, maxVerticalNeckAngle);

        Quaternion targetLocalRotation = defaultNeckLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
        neckBone.localRotation = Quaternion.Slerp(
            neckBone.localRotation,
            targetLocalRotation,
            Time.deltaTime * neckTurnSpeed);
    }

    public void SetForcedTarget(Transform target)
    {
        forcedTarget = target;
        SetAimTarget(forcedTarget);
        RebuildRigs();
        oldTarget = forcedTarget;
    }

    public void ClearForcedTarget()
    {
        forcedTarget = null;
        oldTarget = null;
        SetAimTarget(null);
        RebuildRigs();
    }

    (Transform dog, Collider[] hits) GetNearestDog()
    {
        int dogLayerMask = LayerMask.GetMask(dogLayerName);
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, dogLayerMask);

        if (hits.Length == 0)
            return (null, hits);

        Transform nearest = null;
        float smallestDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < smallestDist)
            {
                smallestDist = dist;
                nearest = hit.transform;
            }
        }

        return (nearest, hits);
    }   

    void SetAimTarget(Transform dog)
    {
        if (aimConstraint == null)
        {
            return;
        }

        // Read the current sources
        WeightedTransformArray sources = aimConstraint.data.sourceObjects;

        if (dog == null)
        {
            sources.Clear();
            aimConstraint.data.sourceObjects = sources;
            aimConstraint.weight = 0f;
            return;
        }

        // Clear any old targets
        sources.Clear();

        // Add the new target
        sources.Add(new WeightedTransform(dog, 1f));

        // Write back to constraint
        aimConstraint.data.sourceObjects = sources;

        // Update constraint weight (optional but safe)
        aimConstraint.weight = 1f;
        hasTarget = true;
    }

    private void RebuildRigs()
    {
        if (rigs != null)
        {
            rigs.Build();
        }
    }

    private Transform ResolveBone(string preferredName, HumanBodyBones primaryBone, HumanBodyBones fallbackBone)
    {
        Transform resolvedBone = null;

        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
        {
            resolvedBone = animator.GetBoneTransform(primaryBone);
            if (resolvedBone == null)
            {
                resolvedBone = animator.GetBoneTransform(fallbackBone);
            }
        }

        if (resolvedBone == null)
        {
            resolvedBone = FindDeepChildByNameContains(transform.root, preferredName);
        }

        if (resolvedBone == null)
        {
            resolvedBone = FindDeepChild(transform.root, preferredName);
        }

        if (resolvedBone == null)
        {
            resolvedBone = FindDeepChildByNameContains(transform, preferredName);
        }

        if (resolvedBone == null)
        {
            resolvedBone = FindDeepChild(transform, preferredName);
        }

        return resolvedBone;
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
