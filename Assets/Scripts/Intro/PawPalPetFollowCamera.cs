using UnityEngine;
using UnityEngine.SceneManagement;

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

        if (ShouldUseHomeSceneFixedCamera(mainCamera))
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

        PawPalRoomPetHandle pet = PawPalRoomPetRuntime.ResolveActivePet();
        if (pet == null || !pet.IsValid || pet.RootTransform == null || pet.FocusTransform == null)
        {
            return false;
        }

        followCamera.enabled = true;
        followCamera.Focus(pet.FocusTransform, pet.HomeCameraOffset, false);
        return true;
    }

    private static bool ShouldUseHomeSceneFixedCamera(Camera mainCamera)
    {
        if (mainCamera == null || mainCamera.GetComponent<DogCycleCamera>() == null)
        {
            return false;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null && runtime.ActivePetSpecies == IntroPetSpecies.Cat)
        {
            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        return PawPalIntroSceneFlow.IsHomeScene(activeScene);
    }
}
