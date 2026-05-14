using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class DogMovementController : MonoBehaviour
{
    public Transform target;
    public float directionSmoothing = 5f;
    public float stopDistance = 0.3f;

    private Animator animator;
    private NavMeshAgent agent;

    // For calculating local direction
    private Vector3 lastPosition;
    private float speed;
    private float direction;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        // Root motion handles movement
        agent.updatePosition = false;
        agent.updateRotation = false;

        lastPosition = transform.position;
    }

    void Update()
    {
        //if (target == null) return;

        //agent.SetDestination(target.position);
        float remaining = agent.remainingDistance;
        if (remaining < stopDistance + 0.5f)
        {
            agent.velocity = Vector3.Lerp(agent.velocity, Vector3.zero, Time.deltaTime * 3f);
        }
        else
        {
            agent.velocity = agent.desiredVelocity;
        }
        Vector3 worldDelta = agent.nextPosition - transform.position;
        Vector3 localDelta = transform.InverseTransformDirection(worldDelta);

        float dt = Time.deltaTime;
        if (dt > 1e-5f)
        {
            speed = Mathf.Clamp(localDelta.z / dt, -2.5f, 2.5f);  // forward/backward speed
            direction = Mathf.Clamp(localDelta.x / dt, -1f, 1f);  // left/right direction
        }

        // Smooth parameters
        speed = Mathf.Lerp(animator.GetFloat("Speed"), speed, Time.deltaTime * directionSmoothing);
        direction = Mathf.Lerp(animator.GetFloat("Direction"), direction, Time.deltaTime * directionSmoothing);

        // Apply to animator
        animator.SetFloat("Speed", speed); 
        animator.SetFloat("Direction", direction);

        // Rotation blending — steer towards NavMeshAgent direction gradually
        if (agent.remainingDistance > stopDistance)
        {
            Vector3 lookDir = agent.desiredVelocity.normalized;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }
        } 

        // Sync NavMeshAgent with root motion
        agent.nextPosition = transform.position;
    }

    void OnAnimatorMove()
    {
        if (!enabled || animator == null)
        {
            return;
        }

        // Use root motion for movement
        transform.position = animator.rootPosition;
        transform.rotation = animator.rootRotation;
    }
}
