using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class DogWanderNavigator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Debug")]
    [SerializeField] private bool hasUsableNavMesh;
    [SerializeField] private bool hasDestination;
    [SerializeField] private Vector3 currentDestination;

    public NavMeshAgent Agent => agent;
    public bool HasUsableNavMesh => hasUsableNavMesh;
    public bool HasDestination => hasDestination;
    public Vector3 CurrentDestination => currentDestination;
    public bool IsMoving => hasDestination && !agent.pathPending && !HasReachedDestination;
    public bool HasReachedDestination =>
        hasDestination &&
        agent.isOnNavMesh &&
        !agent.pathPending &&
        agent.remainingDistance <= agent.stoppingDistance &&
        (!agent.hasPath || agent.velocity.sqrMagnitude <= 0.01f);

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void Update()
    {
        hasUsableNavMesh = agent != null && agent.enabled && agent.isOnNavMesh;
    }

    public void ApplySettings(DogBaseIdleSettings settings)
    {
        if (settings == null || agent == null)
        {
            return;
        }

        agent.speed = settings.MovementSpeed;
        agent.stoppingDistance = settings.StoppingDistance;
    }

    public bool TryPickRandomDestination(
        Vector3 roamCenter,
        float wanderRadius,
        float minDistance,
        float sampleRadius,
        int maxAttempts,
        float maxDistanceFromCenter,
        out Vector3 destination)
    {
        destination = transform.position;

        if (!HasUsableNavMesh)
        {
            return false;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomOffset2D = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = roamCenter + new Vector3(randomOffset2D.x, 0f, randomOffset2D.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 sampledPosition = hit.position;
            sampledPosition.y = transform.position.y;

            if (Vector3.Distance(transform.position, sampledPosition) < minDistance)
            {
                continue;
            }

            if (maxDistanceFromCenter > 0f)
            {
                Vector3 flatOffset = sampledPosition - roamCenter;
                flatOffset.y = 0f;
                if (flatOffset.magnitude > maxDistanceFromCenter)
                {
                    continue;
                }
            }

            NavMeshPath path = new NavMeshPath();
            bool hasPathToPoint = agent.CalculatePath(hit.position, path);
            if (!hasPathToPoint || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            destination = hit.position;
            return true;
        }

        return false;
    }

    public bool MoveTo(Vector3 destination)
    {
        if (!HasUsableNavMesh)
        {
            return false;
        }

        bool accepted = agent.SetDestination(destination);
        if (!accepted)
        {
            return false;
        }

        currentDestination = destination;
        hasDestination = true;
        return true;
    }

    public void Stop()
    {
        if (agent == null)
        {
            return;
        }

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        hasDestination = false;
        currentDestination = transform.position;
    }

    public void Resume()
    {
        if (agent == null)
        {
            return;
        }

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }
}
