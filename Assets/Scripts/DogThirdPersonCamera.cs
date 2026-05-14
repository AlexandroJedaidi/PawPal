using UnityEngine;

[DisallowMultipleComponent]
public class DogThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.6f, -3.8f);
    [SerializeField] private float positionLerpSpeed = 6f;
    [SerializeField] private float rotationLerpSpeed = 8f;
    [SerializeField] private float lookHeight = 0.8f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.TransformPoint(localOffset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * positionLerpSpeed);

        Vector3 lookTarget = target.position + Vector3.up * lookHeight;
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationLerpSpeed);
    }
}
