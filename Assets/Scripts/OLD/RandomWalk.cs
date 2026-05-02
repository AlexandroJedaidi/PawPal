using UnityEngine;

public class RandomWalk : MonoBehaviour
{
    public Vector3 roomMinBounds; // Minimum room bounds (x, y, z)
    public Vector3 roomMaxBounds; // Maximum room bounds (x, y, z)
    public float moveSpeed = 3f;  // Speed of movement
    public float changeDirectionInterval = 2f; // Time interval to pick a new direction

    private Vector3 direction;    // Current movement direction
    private float timer;          // Timer to track direction change

    void Start()
    {
        // Initialize direction randomly
        direction = GetRandomDirection();
        timer = changeDirectionInterval;
    }

    void Update()
    {
        // Move the sphere in the current direction
        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction);

        // Check for collisions with room bounds
        CheckBounds();

        // Update the timer and change direction if necessary
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            direction = GetRandomDirection();
            timer = changeDirectionInterval;
        }
    }

    private void CheckBounds()
    {
        Vector3 pos = transform.position;

        // Constrain position to within the room bounds
        if (pos.x < roomMinBounds.x || pos.x > roomMaxBounds.x)
            direction.x = -direction.x;

        if (pos.z < roomMinBounds.z || pos.z > roomMaxBounds.z)
            direction.z = -direction.z;

        // Update position to remain within bounds
        pos.x = Mathf.Clamp(pos.x, roomMinBounds.x, roomMaxBounds.x);
        pos.z = Mathf.Clamp(pos.z, roomMinBounds.z, roomMaxBounds.z);

        transform.position = pos;
    }

    private Vector3 GetRandomDirection()
    {
        // Generate a random normalized direction
        return new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
    }
}
