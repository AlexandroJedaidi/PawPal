using System;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class AdaptiveLayoutRoot : MonoBehaviour
{
    [SerializeField] private UiLayoutBucket currentBucket = UiLayoutBucket.NarrowPortrait;

    public event Action<UiLayoutBucket> LayoutChanged;

    public UiLayoutBucket CurrentBucket
    {
        get { return currentBucket; }
    }

    private void OnEnable()
    {
        Evaluate(true);
    }

    private void Update()
    {
        Evaluate(false);
    }

    private void Evaluate(bool force)
    {
        UiLayoutBucket nextBucket = ResolveBucket(Screen.width, Screen.height);
        if (!force && nextBucket == currentBucket)
        {
            return;
        }

        currentBucket = nextBucket;
        if (LayoutChanged != null)
        {
            LayoutChanged(currentBucket);
        }
    }

    public static UiLayoutBucket ResolveBucket(float width, float height)
    {
        if (width <= 0f || height <= 0f)
        {
            return UiLayoutBucket.NarrowPortrait;
        }

        float aspect = width / height;
        bool landscape = aspect > 1.05f;
        if (!landscape)
        {
            return width >= 540f ? UiLayoutBucket.WidePortrait : UiLayoutBucket.NarrowPortrait;
        }

        return width >= 1280f || aspect >= 1.7f
            ? UiLayoutBucket.WideLandscape
            : UiLayoutBucket.LandscapeHandheld;
    }
}
