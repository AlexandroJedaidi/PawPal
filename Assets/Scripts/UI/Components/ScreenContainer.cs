using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ScreenContainer : MonoBehaviour
{
    [SerializeField] private float portraitMaxWidth = 330f;
    [SerializeField] private float landscapeMaxWidth = 436f;
    [SerializeField] private float portraitTopPadding = 28f;
    [SerializeField] private float landscapeTopPadding = 20f;
    [SerializeField] private float portraitSidePadding = 24f;
    [SerializeField] private float landscapeSidePadding = 40f;
    [SerializeField] private float portraitBottomPadding = 20f;
    [SerializeField] private float landscapeBottomPadding = 16f;

    private RectTransform rootRect;
    private RectTransform contentColumn;
    private VerticalLayoutGroup layoutGroup;

    public RectTransform ContentColumn
    {
        get { return contentColumn; }
    }

    public VerticalLayoutGroup LayoutGroup
    {
        get { return layoutGroup; }
    }

    public void Initialize()
    {
        rootRect = GetComponent<RectTransform>();
        UiFactory.Stretch(rootRect, 0f, 0f, 0f, 0f);

        if (contentColumn != null)
        {
            return;
        }

        contentColumn = UiFactory.CreateRect("ContentColumn", transform);
        contentColumn.anchorMin = new Vector2(0.5f, 1f);
        contentColumn.anchorMax = new Vector2(0.5f, 1f);
        contentColumn.pivot = new Vector2(0.5f, 1f);

        layoutGroup = UiFactory.AddVerticalLayout(contentColumn.gameObject, 16f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;
        ContentSizeFitter fitter = contentColumn.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public void SetSpacing(float spacing)
    {
        if (layoutGroup != null)
        {
            layoutGroup.spacing = spacing;
        }
    }

    public void ApplyLayout(UiLayoutBucket bucket, float bottomNavHeight)
    {
        if (rootRect == null || contentColumn == null)
        {
            return;
        }

        bool landscape = bucket == UiLayoutBucket.LandscapeHandheld || bucket == UiLayoutBucket.WideLandscape;
        float sidePadding = landscape ? landscapeSidePadding : portraitSidePadding;
        float maxWidth = landscape ? landscapeMaxWidth : portraitMaxWidth;
        float topPadding = landscape ? landscapeTopPadding : portraitTopPadding;
        float bottomPadding = landscape ? landscapeBottomPadding : portraitBottomPadding;

        float parentWidth = rootRect.rect.width > 0f ? rootRect.rect.width : Screen.width;
        float availableWidth = Mathf.Max(0f, parentWidth - sidePadding * 2f);
        float clampedWidth = Mathf.Min(maxWidth, availableWidth);

        rootRect.offsetMin = new Vector2(0f, bottomNavHeight + bottomPadding);
        rootRect.offsetMax = Vector2.zero;

        contentColumn.sizeDelta = new Vector2(clampedWidth, 0f);
        contentColumn.anchoredPosition = new Vector2(0f, -topPadding);
    }
}
