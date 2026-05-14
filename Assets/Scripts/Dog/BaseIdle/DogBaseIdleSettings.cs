using UnityEngine;

[CreateAssetMenu(fileName = "DogBaseIdleSettings", menuName = "PawPal/Dog/Base Idle Settings")]
public class DogBaseIdleSettings : ScriptableObject
{
    [Header("Movement")]
    [Tooltip("Optional override applied to the NavMeshAgent speed while base idle is active.")]
    [Min(0f)]
    [SerializeField] private float movementSpeed = 3.5f;

    [Tooltip("Stopping distance used for wander destinations.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float stoppingDistance = 0.4f;

    [Tooltip("Maximum wander radius around the roam center.")]
    [Range(0.5f, 25f)]
    [SerializeField] private float wanderRadius = 6f;

    [Tooltip("Minimum distance a new destination must be from the dog.")]
    [Range(0.1f, 10f)]
    [SerializeField] private float minWanderDistance = 1.5f;

    [Tooltip("Maximum time allowed to reach a chosen destination before a new one is picked.")]
    [Range(1f, 60f)]
    [SerializeField] private float arrivalTimeout = 15f;

    [Header("NavMesh Sampling")]
    [Tooltip("Radius used when sampling random points onto the NavMesh.")]
    [Range(0.1f, 10f)]
    [SerializeField] private float destinationSampleRadius = 2f;

    [Tooltip("How many random destination attempts are made before giving up for this cycle.")]
    [Range(1, 100)]
    [SerializeField] private int maxDestinationAttempts = 20;

    [Tooltip("Optional clamp that keeps sampled points within this radius from the roam center. Zero disables the clamp.")]
    [Min(0f)]
    [SerializeField] private float maxDistanceFromRoamCenter = 0f;

    [Header("Pause Behaviour")]
    [Tooltip("Minimum pause duration after reaching a destination.")]
    [Range(0f, 30f)]
    [SerializeField] private float minPauseDuration = 2f;

    [Tooltip("Maximum pause duration after reaching a destination.")]
    [Range(0f, 30f)]
    [SerializeField] private float maxPauseDuration = 6f;

    [Tooltip("Chance to sit after a pause finishes.")]
    [Range(0f, 1f)]
    [SerializeField] private float sitChanceDuringPause = 0.25f;

    [Tooltip("Chance to bark after a pause finishes.")]
    [Range(0f, 1f)]
    [SerializeField] private float barkChanceDuringPause = 0.15f;

    [Tooltip("How long to hold the sit behaviour before ending it.")]
    [Range(0.1f, 20f)]
    [SerializeField] private float sitDuration = 3f;

    [Tooltip("How long to reserve the bark state before resuming the loop.")]
    [Range(0.1f, 10f)]
    [SerializeField] private float barkDuration = 1.5f;

    [Header("Cooldowns")]
    [Tooltip("Minimum time between sit actions.")]
    [Range(0f, 60f)]
    [SerializeField] private float sitCooldown = 12f;

    [Tooltip("Minimum time between bark actions.")]
    [Range(0f, 60f)]
    [SerializeField] private float barkCooldown = 10f;

    public float MovementSpeed => movementSpeed;
    public float StoppingDistance => stoppingDistance;
    public float WanderRadius => wanderRadius;
    public float MinWanderDistance => minWanderDistance;
    public float ArrivalTimeout => arrivalTimeout;
    public float DestinationSampleRadius => destinationSampleRadius;
    public int MaxDestinationAttempts => maxDestinationAttempts;
    public float MaxDistanceFromRoamCenter => maxDistanceFromRoamCenter;
    public float MinPauseDuration => minPauseDuration;
    public float MaxPauseDuration => maxPauseDuration;
    public float SitChanceDuringPause => sitChanceDuringPause;
    public float BarkChanceDuringPause => barkChanceDuringPause;
    public float SitDuration => sitDuration;
    public float BarkDuration => barkDuration;
    public float SitCooldown => sitCooldown;
    public float BarkCooldown => barkCooldown;

    private void OnValidate()
    {
        movementSpeed = Mathf.Max(0f, movementSpeed);
        stoppingDistance = Mathf.Max(0.05f, stoppingDistance);
        wanderRadius = Mathf.Max(minWanderDistance, wanderRadius);
        minWanderDistance = Mathf.Max(0.1f, minWanderDistance);
        arrivalTimeout = Mathf.Max(1f, arrivalTimeout);
        destinationSampleRadius = Mathf.Max(0.1f, destinationSampleRadius);
        maxDestinationAttempts = Mathf.Max(1, maxDestinationAttempts);
        maxDistanceFromRoamCenter = Mathf.Max(0f, maxDistanceFromRoamCenter);
        minPauseDuration = Mathf.Max(0f, minPauseDuration);
        maxPauseDuration = Mathf.Max(minPauseDuration, maxPauseDuration);
        sitDuration = Mathf.Max(0.1f, sitDuration);
        barkDuration = Mathf.Max(0.1f, barkDuration);
        sitCooldown = Mathf.Max(0f, sitCooldown);
        barkCooldown = Mathf.Max(0f, barkCooldown);
    }
}
