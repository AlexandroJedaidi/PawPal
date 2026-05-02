using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class DogFreeRoam : MonoBehaviour
{
    NavMeshAgent agent;
    Animator animator;
    DogIdleController idleController;
    float _interval = 1f;
    float _time;
    public float roamRadius = 5f; // wie weit vom Startpunkt darf er laufen?
    public float waitTimeMin = 2f;
    public float waitTimeMax = 6f;
    public float turnThreshold = 30f;
    private Vector2 Velocity;
    private Vector2 SmoothDeltaPosition;

    Vector3 startPos;

    void Start()
    {
        _time = 0f;
        agent = GetComponent<NavMeshAgent>();
        animator = agent.GetComponent<Animator>();
        idleController = GetComponent<DogIdleController>();

        animator.applyRootMotion = true;
        agent.updatePosition = false;
        agent.updateRotation = false;

        startPos = transform.position;

        //StartCoroutine(RoamRoutine());
    }

    private void OnAnimatorMove()
    {
        Vector3 rootPosition = animator.rootPosition;
        rootPosition.y = agent.nextPosition.y;
        transform.position = rootPosition;
        transform.rotation = animator.rootRotation;
        agent.nextPosition = rootPosition;
    }

    void Update()
    {
       _time += Time.deltaTime;
        // Animator Speed updaten
        //float speed = agent.velocity.magnitude;
        //animator.SetFloat("Speed", speed);
        SynchronizeAnimatorAndAgent();
    }

    private void SynchronizeAnimatorAndAgent()
    {
        Vector3 worldDeltaPosition = agent.nextPosition - transform.position;
        worldDeltaPosition.y = 0;

        float dx = Vector3.Dot(transform.right, worldDeltaPosition);
        float dy = Vector3.Dot(transform.forward, worldDeltaPosition);
        Vector2 deltaPosition = new Vector2(dx, dy);

        float smooth = Mathf.Min(1, Time.deltaTime / 0.1f);
        SmoothDeltaPosition = Vector2.Lerp(SmoothDeltaPosition, deltaPosition, smooth);

        Velocity = SmoothDeltaPosition / Time.deltaTime;
        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            Velocity = Vector2.Lerp(
                Vector2.zero,
                Velocity,
                 agent.remainingDistance / agent.stoppingDistance
            );
        }

        bool shouldMove = Velocity.magnitude > 0.5f && agent.remainingDistance > agent.stoppingDistance;
        if (shouldMove)
        {
            animator.SetBool("Move", true);
        }

        Vector3 toTarget = agent.nextPosition - transform.position;
        toTarget.y = 0;
        Vector3 forward = transform.forward;
        forward.y = 0;

        forward.Normalize();
        toTarget.Normalize();

        float turn_angle = Vector3.SignedAngle(forward, toTarget, Vector3.up);
        float direction = Mathf.Clamp(turn_angle / 180f, -1f, 1f);

        float speed = Vector3.Dot(forward, agent.desiredVelocity.normalized); //* (agent.desiredVelocity.magnitude / agent.speed);

        
        if (_time >= _interval)
        {
            _time -= _interval;
            animator.SetFloat("Direction", direction);
        }
        
        if (Mathf.Abs(turn_angle) < turnThreshold)
        {
            animator.SetFloat("Speed", speed);
        }
        else
        {
            animator.SetFloat("Speed", speed);
        }


    }

    IEnumerator RoamRoutine()
    {
        while (true)
        {
            // Neues Ziel berechnen
            Vector3 roamTarget = RandomNavSphere(startPos, roamRadius, -1);
            agent.SetDestination(roamTarget);

            // Warten bis Ziel erreicht
            while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
            {
                yield return null;
            }

            // An Ziel angekommen → Idle Phase
            float waitTime = Random.Range(waitTimeMin, waitTimeMax);
            float timer = 0f;

            while (timer < waitTime)
            {
                // IdleController kümmert sich um Random Idles
                timer += Time.deltaTime;
                yield return null;
            }
        }
    }

    // Utility für random Punkte im NavMesh
    public static Vector3 RandomNavSphere(Vector3 origin, float distance, int layermask)
    {
        Vector3 randomDirection = Random.insideUnitSphere * distance;
        randomDirection += origin;

        NavMeshHit navHit;
        NavMesh.SamplePosition(randomDirection, out navHit, distance, layermask);

        return navHit.position;
    }
}


