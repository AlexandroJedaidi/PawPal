using UnityEngine;

[DisallowMultipleComponent]
public sealed class PetSelectionCameraController : MonoBehaviour
{
    internal const float HomeSceneCameraHeight = 0.834f;

    [SerializeField] private float positionSmooth = 4.2f;
    [SerializeField] private float rotationSmooth = 6.5f;
    [SerializeField] private Vector3 fallbackOffset = new Vector3(0f, 1.35f, -3.2f);

    private Camera controlledCamera;
    private IntroPetAgent targetAgent;
    private bool snapNextFrame = true;

    public Camera ControlledCamera
    {
        get { return controlledCamera; }
    }

    public void Initialize(Camera camera)
    {
        controlledCamera = camera != null ? camera : Camera.main;
        if (controlledCamera == null)
        {
            controlledCamera = gameObject.AddComponent<Camera>();
            controlledCamera.tag = "MainCamera";
        }
    }

    public void Focus(IntroPetAgent agent, bool snap)
    {
        targetAgent = agent;
        snapNextFrame = snap;
        ApplyFocus();
    }

    private void LateUpdate()
    {
        ApplyFocus();
    }

    private void ApplyFocus()
    {
        if (controlledCamera == null || targetAgent == null)
        {
            return;
        }

        IntroPetDefinition definition = targetAgent.Definition;
        Vector3 offset = definition != null ? definition.IntroCameraOffset : fallbackOffset;
        Transform target = targetAgent.FocusTransform;
        Vector3 focusPoint = target.position + Vector3.up * 0.55f;
        Vector3 desiredPosition = target.position + offset;
        desiredPosition.y = HomeSceneCameraHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - desiredPosition, Vector3.up);

        if (snapNextFrame)
        {
            controlledCamera.transform.position = desiredPosition;
            controlledCamera.transform.rotation = desiredRotation;
            snapNextFrame = false;
            return;
        }

        controlledCamera.transform.position = Vector3.Lerp(controlledCamera.transform.position, desiredPosition, Time.deltaTime * positionSmooth);
        controlledCamera.transform.rotation = Quaternion.Slerp(controlledCamera.transform.rotation, desiredRotation, Time.deltaTime * rotationSmooth);
    }
}
