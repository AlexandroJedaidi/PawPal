using UnityEngine;
using System.Collections;

public class DogProximityBark : MonoBehaviour
{
    [Header("Dog Setup")]
    public Transform dogA;               // Dog that moves around
    public Transform dogB;               // Dog that sits
    public Animator dogAAnimator;
    public Animator dogBAnimator;

    [Header("Head Bones")]
    public Transform[] dogAHead;
    public Transform[] dogBHead;

    [Header("Settings")]
    public float barkDistance = 3f;
    public float lookSpeed = 5f;
    public float maxHeadTurnAngle = 50f;
    public float barkCooldown = 3f;

    private bool isBarking = false;

    void Update()
    {
        float distance = Vector3.Distance(dogA.position, dogB.position);

        // If dogs are close and not already barking, trigger bark behavior
        if (distance <= barkDistance && !isBarking)
        {
            //StartCoroutine(BarkSequence());
        }

        // If barking, keep them looking at each other
        if (isBarking)
        {
            //SmoothLookAt(dogAHead[0], dogBHead[0]);
            //SmoothLookAt(dogBHead[0], dogAHead[0]);
        }
    }

    IEnumerator BarkSequence()
    {
        isBarking = true;

        // Make both look at each other right away
        //SmoothLookAt(dogAHead[0], dogBHead[0]);
        //SmoothLookAt(dogBHead[0], dogAHead[0]);

        // Trigger barking animation on both animators
    
        dogAAnimator.SetBool("Move", false);
        dogAAnimator.SetInteger("IdleIndex", 2);
        dogBAnimator.SetBool("Move", false);
        dogBAnimator.SetInteger("IdleIndex", 2);

        // Wait for bark duration
        yield return new WaitForSeconds(barkCooldown);
        dogAAnimator.SetBool("Move", true);
        dogBAnimator.SetBool("Move", true);

        // Reset
        isBarking = false;
    }

    private void SmoothLookAt(Transform head, Transform target)
    {
        if (head == null || target == null) return;

        Vector3 direction = target.position - head.position;

        // Ignore vertical difference if you want more natural behavior
        // direction.y = 0;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float angle = Quaternion.Angle(head.rotation, targetRotation);

        // Only rotate head if within allowed range
        if (angle < maxHeadTurnAngle)
        {
            head.rotation = Quaternion.Slerp(
                head.rotation,
                targetRotation,
                Time.deltaTime * lookSpeed
            );
        }
    }
}
