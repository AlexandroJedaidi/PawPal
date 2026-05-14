using UnityEngine;
using UnityEngine.AI;

public class DogBehavior : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;

    // Animation control variables
    private float speed;
    private float direction;

    // Timing for random behaviors
    private float idleTimer;
    private float roamTimer;

    private void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        idleTimer = Random.Range(2f, 5f);
        roamTimer = Random.Range(5f, 10f);
    }

    private void Update()
    {
        // Choose a random behavior every few seconds
        idleTimer -= Time.deltaTime;
        roamTimer -= Time.deltaTime;

        if (idleTimer <= 0)
        {
            PerformRandomAction();
            idleTimer = Random.Range(2f, 5f);
        }

        else if (roamTimer <= 0)
        {
            animator.SetBool("isSitting", false);
            animator.SetBool("isBarking", false);
            StartRoaming();
            roamTimer = Random.Range(10f, 15f);
        }

        UpdateAnimations();
    }

    private void PerformRandomAction()
    {
        int action = Random.Range(0, 3);
        if (action == 0)
        {
            Sit();
        }
        else if (action == 1)
        {
            Bark();
        }
    }

    private void Sit()
    {
        agent.isStopped = true;
        animator.SetFloat("Speed", 0);
        animator.SetTrigger("Sit");
        animator.SetBool("isSitting", true);
        animator.SetBool("isBarking", false);
    }

    private void Bark()
    {
        agent.isStopped = true;
        animator.SetFloat("Speed", 0);
        animator.SetTrigger("Bark");
        animator.SetBool("isSitting", false);
        animator.SetBool("isBarking", true);
    }

    private void StartRoaming()
    {
        agent.isStopped = false;
        Vector3 randomDestination = GetRandomDestination();
        agent.SetDestination(randomDestination);
    }

    private Vector3 GetRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * 5f; // Adjust radius
        randomDirection += transform.position;
        NavMeshHit hit;
        NavMesh.SamplePosition(randomDirection, out hit, 5f, NavMesh.AllAreas);
        return hit.position;
    }

    private void UpdateAnimations()
    {
        if (agent.velocity.magnitude > 0.1f)
        {
            speed = agent.velocity.magnitude;
            direction = Vector3.Dot(agent.velocity.normalized, transform.right);

            animator.SetFloat("Speed", speed);
            animator.SetFloat("Direction", direction);
        }
        else
        {
            animator.SetFloat("Speed", 0);
        }
    }
}

