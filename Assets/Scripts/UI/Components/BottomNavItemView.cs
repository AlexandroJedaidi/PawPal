using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BottomNavItemView : MonoBehaviour
{
    private const float RegularItemHeight = 52f;
    private const float MapItemSize = 60f;
    private const float IconSize = 30f;
    private const float LabelWidth = 60f;
    private const float LabelHeight = 20f;

    private UiSpriteLibrary sprites;
    private string defaultIconName;
    private string selectedIconName;
    private bool isMapItem;

    private Image selectedBackground;
    private Image selectedTopBorder;
    private Image iconImage;
    private TextMeshProUGUI label;
    private Image mapCircle;
    private LayoutElement layoutElement;
    private bool isSelected;
    private RectTransform rectTransform;

    public AppScreenId ScreenId { get; private set; }

    public void Initialize(AppScreenId screenId, UiSpriteLibrary spriteLibrary, string labelText, string defaultIcon, string selectedIcon, bool mapItem, UnityAction onClick)
    {
        ScreenId = screenId;
        sprites = spriteLibrary;
        defaultIconName = defaultIcon;
        selectedIconName = selectedIcon;
        isMapItem = mapItem;
        rectTransform = GetComponent<RectTransform>();

        layoutElement = UiFactory.EnsureLayoutElement(gameObject, 60f, 60f, 0f, 0f);

        selectedBackground = UiFactory.CreateImage("SelectedBackground", transform, UiTheme.NavTabActiveSprite, UiTheme.NavBrand);
        selectedBackground.type = Image.Type.Simple;
        selectedBackground.preserveAspect = false;

        selectedTopBorder = UiFactory.CreateImage("SelectedTopBorder", transform, UiTheme.WhiteSprite, UiTheme.NavTopBorder);
        selectedTopBorder.type = Image.Type.Simple;
        selectedTopBorder.preserveAspect = false;

        mapCircle = UiFactory.CreateImage("MapCircle", transform, UiTheme.NavMapCircleOutlineSprite, UiTheme.NavBrand);
        mapCircle.type = Image.Type.Simple;
        mapCircle.preserveAspect = false;

        iconImage = UiFactory.CreateImage("Icon", transform, sprites.GetIcon(defaultIconName), UiTheme.White);
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;

        label = UiFactory.CreateLabel("Label", transform, labelText, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavRegularFont;
        label.fontSize = 13;
        label.color = UiTheme.NavBrandDark;

        UiFactory.AddButton(gameObject, onClick);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        selectedBackground.enabled = selected && !isMapItem;
        selectedTopBorder.enabled = selected && !isMapItem;

        if (isMapItem)
        {
            mapCircle.enabled = true;
            mapCircle.sprite = selected ? UiTheme.CircleSprite : UiTheme.NavMapCircleOutlineSprite;
            mapCircle.color = selected ? UiTheme.NavBrand : UiTheme.NavBrand;
        }
        else
        {
            mapCircle.enabled = false;
        }

        string iconName = selected ? selectedIconName : defaultIconName;
        iconImage.sprite = sprites.GetIcon(iconName);
        iconImage.color = UiTheme.White;
        label.color = selected ? UiTheme.White : UiTheme.NavBrandDark;
        label.font = selected ? UiTheme.NavExtraBoldFont : UiTheme.NavRegularFont;
        label.fontStyle = FontStyles.Normal;
    }

    public void ApplyLayout(float scale)
    {
        float itemWidth = 60f * scale;
        float itemHeight = (isMapItem ? MapItemSize : RegularItemHeight) * scale;
        float iconSize = IconSize * scale;
        float labelHeight = LabelHeight * scale;
        float labelWidth = LabelWidth * scale;
        float labelSize = 13f * scale;

        layoutElement.preferredWidth = itemWidth;
        layoutElement.preferredHeight = itemHeight;
        layoutElement.minWidth = itemWidth;
        layoutElement.minHeight = itemHeight;
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(itemWidth, itemHeight);
        }

        if (isMapItem)
        {
            mapCircle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            mapCircle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            mapCircle.rectTransform.pivot = new Vector2(0.5f, 1f);
            mapCircle.rectTransform.sizeDelta = new Vector2(itemWidth, itemHeight);
            mapCircle.rectTransform.anchoredPosition = Vector2.zero;

            iconImage.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            iconImage.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            iconImage.rectTransform.pivot = new Vector2(0.5f, 1f);
            iconImage.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, -5.5f * scale);
            iconImage.color = isSelected ? UiTheme.White : UiTheme.White;

            label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(labelWidth, labelHeight);
            label.rectTransform.anchoredPosition = new Vector2(0f, -37.5f * scale);
            label.fontSize = labelSize;

            selectedBackground.enabled = false;
            selectedTopBorder.enabled = false;
        }
        else
        {
            selectedBackground.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            selectedBackground.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            selectedBackground.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            selectedBackground.rectTransform.sizeDelta = new Vector2(itemWidth, itemHeight);
            selectedBackground.rectTransform.anchoredPosition = Vector2.zero;

            selectedTopBorder.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            selectedTopBorder.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            selectedTopBorder.rectTransform.pivot = new Vector2(0.5f, 1f);
            selectedTopBorder.rectTransform.sizeDelta = new Vector2(itemWidth, 2f * scale);
            selectedTopBorder.rectTransform.anchoredPosition = Vector2.zero;

            iconImage.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            iconImage.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            iconImage.rectTransform.pivot = new Vector2(0.5f, 1f);
            iconImage.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, -4f * scale);
            iconImage.color = UiTheme.White;

            label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(labelWidth, labelHeight);
            label.rectTransform.anchoredPosition = new Vector2(0f, -34f * scale);
            label.fontSize = labelSize;

            if (!isSelected)
            {
                iconImage.color = UiTheme.White;
            }
        }
    }
}
