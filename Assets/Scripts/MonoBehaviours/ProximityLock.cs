using UnityEngine;
using System.Collections.Generic; 
using UnityEngine.Animations.Rigging;

public class ProximityLock : MonoBehaviour
{
    public MultiAimConstraint aimConstraint;
    public float detectionRadius = 1f;
    public string dogLayerName = "DogFace";    // Start is called once before the first execution of Update after the MonoBehaviour is created
    RigBuilder rigs;
    Collider[] hitList;
    Transform nearestDog;
    Transform oldTarget;
    bool hasNewTarget;


    void Start()
    {
        aimConstraint = GetComponentsInChildren<MultiAimConstraint>()[0];
        rigs = GetComponent<RigBuilder>();
        oldTarget = null;
        nearestDog = null;
        hasNewTarget = false;
    }


    void Update()
    {
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
            rigs.Build();
        }

        if (hasNewTarget)
        {
            SetAimTarget(nearestDog);
            rigs.Build();
            oldTarget = nearestDog;
            hasNewTarget = false;
        } 
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
        // Read the current sources
        WeightedTransformArray sources = aimConstraint.data.sourceObjects;

        if (dog == null)
        {
            sources.Clear();
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
    }
}
