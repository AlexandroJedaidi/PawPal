using UnityEngine;

public class DogAnimationTester : MonoBehaviour
{
    Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Mit Taste P starten wir die Pickup-Animation
        if (Input.GetKeyDown(KeyCode.P))
        {
            animator.SetTrigger("Pickup");
        }
    }
}
