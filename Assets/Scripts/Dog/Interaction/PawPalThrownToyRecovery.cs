using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class PawPalThrownToyRecovery : MonoBehaviour
{
    [SerializeField] private float monitorDuration = 8f;
    [SerializeField] private float minAgeBeforeRecovery = 0.75f;
    [SerializeField] private float wallStuckSecondsRequired = 0.45f;
    [SerializeField] private float stillStuckSecondsRequired = 0.75f;
    [SerializeField] private float maxAirborneSeconds = 2.4f;
    [SerializeField] private float lowSpeedThreshold = 0.35f;
    [SerializeField] private float stillPositionDistance = 0.055f;
    [SerializeField] private float maxComfortableHeightAboveFloor = 0.35f;
    [SerializeField] private float floorRayHeight = 1.5f;
    [SerializeField] private float floorRayDistance = 4f;
    [SerializeField] private float floorPadding = 0.04f;
    [SerializeField] private float navMeshSampleRadius = 1.5f;

    private Rigidbody body;
    private Collider[] ownColliders;
    private float startedAt;
    private float wallContactSeconds;
    private float airborneSeconds;
    private float stillSeconds;
    private Vector3 lastPosition;
    private bool hadWallContactThisFrame;

    public static PawPalThrownToyRecovery Arm(GameObject toy)
    {
        if (toy == null)
        {
            return null;
        }

        PawPalThrownToyRecovery recovery = toy.GetComponent<PawPalThrownToyRecovery>();
        if (recovery == null)
        {
            recovery = toy.AddComponent<PawPalThrownToyRecovery>();
        }

        recovery.ResetMonitor();
        return recovery;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        ownColliders = GetComponentsInChildren<Collider>(true);
        ResetMonitor();
    }

    private void OnEnable()
    {
        ResetMonitor();
    }

    private void Update()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        float age = Time.time - startedAt;
        if (age > Mathf.Max(0.1f, monitorDuration))
        {
            enabled = false;
            return;
        }

        UpdateStillTimer();
        UpdateAirborneTimer();

        if (hadWallContactThisFrame)
        {
            wallContactSeconds += Time.deltaTime;
        }
        else
        {
            wallContactSeconds = Mathf.Max(0f, wallContactSeconds - Time.deltaTime * 2f);
        }

        hadWallContactThisFrame = false;

        if (age < Mathf.Max(0f, minAgeBeforeRecovery))
        {
            return;
        }

        float speed = body != null ? body.linearVelocity.magnitude : 0f;
        bool wallStuck = wallContactSeconds >= Mathf.Max(0.05f, wallStuckSecondsRequired)
            && (speed <= Mathf.Max(0.01f, lowSpeedThreshold) || stillSeconds >= Mathf.Max(0.05f, stillStuckSecondsRequired));
        bool suspendedTooLong = airborneSeconds >= Mathf.Max(0.15f, maxAirborneSeconds)
            && (speed <= Mathf.Max(0.01f, lowSpeedThreshold) || stillSeconds >= Mathf.Max(0.05f, stillStuckSecondsRequired));

        if (wallStuck || suspendedTooLong)
        {
            RecoverToFloor();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y < 0.35f)
            {
                hadWallContactThisFrame = true;
                return;
            }
        }
    }

    private void UpdateStillTimer()
    {
        Vector3 delta = transform.position - lastPosition;
        if (delta.sqrMagnitude <= stillPositionDistance * stillPositionDistance)
        {
            stillSeconds += Time.deltaTime;
        }
        else
        {
            stillSeconds = 0f;
            lastPosition = transform.position;
        }
    }

    private void UpdateAirborneTimer()
    {
        float floorDistance;
        Vector3 floorPoint;
        if (TryFindFloorBelow(transform.position, out floorPoint, out floorDistance)
            && floorDistance <= Mathf.Max(0.02f, maxComfortableHeightAboveFloor))
        {
            airborneSeconds = 0f;
            return;
        }

        airborneSeconds += Time.deltaTime;
    }

    private void RecoverToFloor()
    {
        Vector3 floorPoint;
        if (!TryFindRecoveryPoint(out floorPoint))
        {
            enabled = false;
            return;
        }

        Quaternion rotation = transform.rotation;
        transform.position = GetPositionWithBottomAt(floorPoint.y + Mathf.Max(0f, floorPadding), floorPoint);
        transform.rotation = rotation;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        enabled = false;
    }

    private bool TryFindRecoveryPoint(out Vector3 point)
    {
        float floorDistance;
        if (TryFindFloorBelow(transform.position, out point, out floorDistance))
        {
            return true;
        }

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(transform.position, out navHit, Mathf.Max(0.05f, navMeshSampleRadius), NavMesh.AllAreas))
        {
            point = navHit.position;
            return true;
        }

        DogRoomAgent[] dogs = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < dogs.Length; i++)
        {
            DogRoomAgent dog = dogs[i];
            if (dog == null)
            {
                continue;
            }

            Vector3 safePoint;
            if (dog.TryGetRoomSafePoint(transform.position, navMeshSampleRadius, out safePoint))
            {
                point = safePoint;
                return true;
            }
        }

        point = transform.position;
        return false;
    }

    private bool TryFindFloorBelow(Vector3 position, out Vector3 floorPoint, out float floorDistance)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            position + Vector3.up * Mathf.Max(0.05f, floorRayHeight),
            Vector3.down,
            Mathf.Max(0.1f, floorRayDistance),
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        floorPoint = Vector3.zero;
        floorDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null
                || hit.normal.y < 0.35f
                || IsOwnCollider(hit.collider)
                || hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            floorPoint = hit.point;
            floorDistance = Mathf.Max(0f, position.y - hit.point.y);
        }

        return bestDistance < float.PositiveInfinity;
    }

    private bool IsOwnCollider(Collider candidate)
    {
        if (candidate == null || ownColliders == null)
        {
            return false;
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (candidate == ownColliders[i])
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetPositionWithBottomAt(float bottomY, Vector3 desiredPoint)
    {
        Bounds bounds;
        if (!TryGetToyBounds(out bounds))
        {
            return new Vector3(desiredPoint.x, bottomY, desiredPoint.z);
        }

        float yAdjustment = bottomY - bounds.min.y;
        return transform.position + new Vector3(desiredPoint.x - bounds.center.x, yAdjustment, desiredPoint.z - bounds.center.z);
    }

    private bool TryGetToyBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        if (ownColliders == null)
        {
            ownColliders = GetComponentsInChildren<Collider>(true);
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider collider = ownColliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    public void ResetMonitor()
    {
        body = GetComponent<Rigidbody>();
        ownColliders = GetComponentsInChildren<Collider>(true);
        startedAt = Time.time;
        wallContactSeconds = 0f;
        airborneSeconds = 0f;
        stillSeconds = 0f;
        lastPosition = transform.position;
        hadWallContactThisFrame = false;
        enabled = true;
    }
}
