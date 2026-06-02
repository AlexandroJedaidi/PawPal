using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalPetFollowCamera : MonoBehaviour
{
    [SerializeField] private float positionSmooth = 4f;
    [SerializeField] private float rotationSmooth = 6f;
    [SerializeField] private Vector3 fallbackOffset = new Vector3(0f, 0.8f, -2.45f);

    private Transform target;
    private Vector3 offset;
    private bool snapNextFrame;

    public void Focus(Transform targetTransform, Vector3 followOffset, bool snap)
    {
        target = targetTransform;
        offset = followOffset == Vector3.zero ? fallbackOffset : followOffset;
        snapNextFrame = snap;
        ApplyFollow();
    }

    private void LateUpdate()
    {
        ApplyFollow();
    }

    private void ApplyFollow()
    {
        if (target == null)
        {
            return;
        }

        Vector3 focusPoint = target.position + Vector3.up * 0.5f;
        Vector3 desiredPosition = target.position + offset;
        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - desiredPosition, Vector3.up);

        if (snapNextFrame)
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            snapNextFrame = false;
            return;
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * positionSmooth);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSmooth);
    }

    public static bool TryFocusRuntimeActivePet()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return false;
        }

        PawPalPetFollowCamera followCamera = mainCamera.GetComponent<PawPalPetFollowCamera>();
        if (followCamera == null)
        {
            return false;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null || runtime.ActivePetSpecies != IntroPetSpecies.Cat)
        {
            return false;
        }

        PawPalCatRoomAgent[] cats = Object.FindObjectsByType<PawPalCatRoomAgent>(FindObjectsSortMode.InstanceID);
        PawPalCatRoomAgent cat = null;
        string activePetId = runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty;
        for (int i = 0; i < cats.Length; i++)
        {
            if (cats[i] != null && string.Equals(cats[i].RuntimePetId, activePetId, System.StringComparison.OrdinalIgnoreCase))
            {
                cat = cats[i];
                break;
            }
        }

        if (cat == null && cats.Length > 0)
        {
            cat = cats[0];
        }

        if (cat == null)
        {
            return false;
        }

        followCamera.enabled = true;
        followCamera.Focus(cat.FocusTransform, cat.HomeCameraOffset, false);
        return true;
    }
}
