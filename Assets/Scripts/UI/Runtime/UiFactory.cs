using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class UiFactory
{
    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        if (parent != null)
        {
            rectTransform.SetParent(parent, false);
        }

        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    public static RectTransform Stretch(RectTransform rectTransform, float left, float bottom, float right, float top)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
        return rectTransform;
    }

    public static RectTransform AnchorTopStretch(RectTransform rectTransform, float left, float top, float right, float height)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.offsetMin = new Vector2(left, -top - height);
        rectTransform.offsetMax = new Vector2(-right, -top);
        return rectTransform;
    }

    public static RectTransform AnchorBottomStretch(RectTransform rectTransform, float left, float bottom, float right, float height)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, bottom + height);
        return rectTransform;
    }

    public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rectTransform = CreateRect(name, parent);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Sliced;
        image.preserveAspect = true;
        return image;
    }

    public static Button AddButton(GameObject target, UnityAction onClick)
    {
        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.AddComponent<Button>();
        }

        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic == null)
        {
            Image hitGraphic = target.AddComponent<Image>();
            hitGraphic.color = new Color(1f, 1f, 1f, 0.002f);
            hitGraphic.raycastTarget = true;
            graphic = hitGraphic;
        }

        button.transition = Selectable.Transition.None;
        button.targetGraphic = graphic;
        button.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        return button;
    }

    public static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, int fontSize, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        RectTransform rectTransform = CreateRect(name, parent);
        TextMeshProUGUI label = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = UiTheme.DefaultFont;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = style;
        label.alignment = alignment;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    public static VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing, RectOffset padding, TextAnchor alignment)
    {
        VerticalLayoutGroup layout = target.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = target.AddComponent<VerticalLayoutGroup>();
        }

        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = alignment;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        return layout;
    }

    public static HorizontalLayoutGroup AddHorizontalLayout(GameObject target, float spacing, RectOffset padding, TextAnchor alignment)
    {
        HorizontalLayoutGroup layout = target.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = target.AddComponent<HorizontalLayoutGroup>();
        }

        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = alignment;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        return layout;
    }

    public static LayoutElement EnsureLayoutElement(GameObject target, float preferredWidth, float preferredHeight, float flexibleWidth, float flexibleHeight)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = target.AddComponent<LayoutElement>();
        }

        element.preferredWidth = preferredWidth;
        element.preferredHeight = preferredHeight;
        element.flexibleWidth = flexibleWidth;
        element.flexibleHeight = flexibleHeight;
        return element;
    }
}
