using UnityEngine;

public class MovingObjectController : MonoBehaviour
{
    public float speed = 5f;       
    public float radius = 3f;     
    private float angle = 0f;     
    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.position;
        Debug.Log("Initial Position: " + initialPosition);
    }

    void Update()
    {
        angle += speed * Time.deltaTime; 
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;
        transform.position = new Vector3(initialPosition.x + x, initialPosition.y, initialPosition.z + z); 
    }
}
