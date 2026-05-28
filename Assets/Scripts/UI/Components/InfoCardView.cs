using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class InfoCardView : MonoBehaviour
{
    private RectTransform contentRoot;
    private VerticalLayoutGroup layoutGroup;

    public RectTransform ContentRoot
    {
        get { return contentRoot; }
    }

    public VerticalLayoutGroup LayoutGroup
    {
        get { return layoutGroup; }
    }

    public void Initialize(float minHeight)
    {
        Image background = gameObject.GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }

        background.sprite = UiTheme.RoundedSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.CardWhite;
        background.preserveAspect = false;

        UiFactory.EnsureLayoutElement(gameObject, -1f, minHeight, 1f, 0f);

        Outline outline = gameObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }

        outline.effectColor = UiTheme.Divider;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        contentRoot = UiFactory.CreateRect("Content", transform);
        UiFactory.Stretch(contentRoot, 0f, 0f, 0f, 0f);
        layoutGroup = UiFactory.AddVerticalLayout(contentRoot.gameObject, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
    }
}
