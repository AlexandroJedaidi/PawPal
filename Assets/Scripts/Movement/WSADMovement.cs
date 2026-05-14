using UnityEngine;
using UnityEngine.AI;

public class WASDNavMeshMovement : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float turnSpeed = 120f;

    Animator animator;
    NavMeshAgent agent;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        agent.updateRotation = false; // RM handles rotation
        agent.updatePosition = false; // RM handles translation
    }

    void Update()
    {
        float vertical = Input.GetAxisRaw("Vertical");   // W/S
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D

        animator.SetFloat("Speed", vertical);
        animator.SetFloat("Direction", horizontal);

        // Convert speed to movement in world space
        Vector3 move = transform.forward * vertical * moveSpeed * Time.deltaTime;

        // Move agent by root-motion-friendly manual positioning
        agent.Move(move);

        // Character turning (if NOT using RM for rotation)
        //transform.Rotate(Vector3.up, horizontal * turnSpeed * Time.deltaTime);
    }
}
