using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IntroBackdropScreenHandle : MonoBehaviour
{
    private IntroBackdropRingController owner;
    private int screenIndex = -1;
    private Vector3 lastLocalPosition;
    private Vector3 lastLocalEulerAngles;
    private Vector3 lastLocalScale;

    public void Initialize(IntroBackdropRingController nextOwner, int nextScreenIndex)
    {
        owner = nextOwner;
        screenIndex = nextScreenIndex;
        CacheCurrentTransform();
    }

    private void Update()
    {
        if (Application.isPlaying || owner == null || owner.IsApplyingLayout || screenIndex < 0)
        {
            return;
        }

        Transform cachedTransform = transform;
        Vector3 currentPosition = cachedTransform.localPosition;
        Vector3 currentEulerAngles = cachedTransform.localEulerAngles;
        Vector3 currentScale = cachedTransform.localScale;
        if (currentPosition == lastLocalPosition &&
            currentEulerAngles == lastLocalEulerAngles &&
            currentScale == lastLocalScale)
        {
            return;
        }

        owner.SaveScreenLayoutFromTransform(screenIndex, currentPosition, currentEulerAngles, new Vector2(currentScale.x, currentScale.y));
        CacheCurrentTransform();
    }

    private void CacheCurrentTransform()
    {
        lastLocalPosition = transform.localPosition;
        lastLocalEulerAngles = transform.localEulerAngles;
        lastLocalScale = transform.localScale;
    }
}
