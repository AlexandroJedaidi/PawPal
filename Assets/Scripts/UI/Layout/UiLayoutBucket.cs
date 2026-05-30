using UnityEngine;

public enum UiLayoutBucket
{
    NarrowPortrait,
    WidePortrait,
    LandscapeHandheld,
    WideLandscape
}

public struct ResponsiveFigmaFrameLayout
{
    public float Scale;
    public float LogicalWidth;
    public float LogicalHeight;
    public float VisibleLogicalHeight;
    public float SafeWidth;
    public float UsableHeight;
    public float ExtraWidth;
    public float AvailableWidth;
    public float AvailableHeight;
    public bool IsHeightConstrained;
}

public struct ResponsiveLayoutContext
{
    public float SafeWidth;
    public float SafeHeight;
    public float VisibleDesignHeight;
    public float ExtraWidth;
    public bool IsHeightConstrained;

    public static ResponsiveLayoutContext FromHost(RectTransform host)
    {
        float safeWidth = UiTheme.ReferenceWidth;
        float safeHeight = UiTheme.ReferenceContentHeight;

        if (host != null)
        {
            safeWidth = host.rect.width > 0f ? host.rect.width : UiTheme.ReferenceWidth;
            safeHeight = host.rect.height > 0f ? host.rect.height : UiTheme.ReferenceContentHeight;
        }

        return new ResponsiveLayoutContext
        {
            SafeWidth = safeWidth,
            SafeHeight = safeHeight,
            VisibleDesignHeight = safeHeight,
            ExtraWidth = Mathf.Max(0f, safeWidth - UiTheme.ReferenceWidth),
            IsHeightConstrained = safeHeight < UiTheme.ReferenceContentHeight
        };
    }
}

public static class ResponsiveFigmaFrame
{
    public static ResponsiveFigmaFrameLayout Apply(RectTransform host, RectTransform frame)
    {
        return Apply(host, frame, UiTheme.ReferenceContentHeight);
    }

    public static ResponsiveFigmaFrameLayout Apply(RectTransform host, RectTransform frame, float requiredLogicalHeight)
    {
        ResponsiveLayoutContext context = ResponsiveLayoutContext.FromHost(host);
        float requiredHeight = Mathf.Max(UiTheme.ReferenceContentHeight, requiredLogicalHeight);
        ResponsiveFigmaFrameLayout layout = new ResponsiveFigmaFrameLayout
        {
            Scale = 1f,
            LogicalWidth = UiTheme.ReferenceWidth,
            LogicalHeight = Mathf.Max(requiredHeight, context.VisibleDesignHeight),
            VisibleLogicalHeight = context.VisibleDesignHeight,
            SafeWidth = context.SafeWidth,
            UsableHeight = context.SafeHeight,
            ExtraWidth = context.ExtraWidth,
            AvailableWidth = context.SafeWidth,
            AvailableHeight = context.SafeHeight,
            IsHeightConstrained = context.IsHeightConstrained
        };

        if (host == null || frame == null)
        {
            return layout;
        }

        float logicalHeight = layout.LogicalHeight;

        frame.anchorMin = new Vector2(0.5f, 1f);
        frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, logicalHeight);
        frame.anchoredPosition = Vector2.zero;
        frame.localScale = Vector3.one;

        layout.Scale = 1f;
        layout.LogicalWidth = UiTheme.ReferenceWidth;
        return layout;
    }
}
