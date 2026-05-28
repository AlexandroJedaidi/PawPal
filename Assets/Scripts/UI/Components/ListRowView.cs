using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ListRowView : MonoBehaviour
{
    private Image iconImage;
    private TextMeshProUGUI label;
    private Image chevronImage;
    private Image divider;

    public void Initialize(UiSpriteLibrary sprites, string iconName, string labelText, bool showChevron, bool dividerVisible, UnityAction onClick)
    {
        UiFactory.EnsureLayoutElement(gameObject, -1f, 54f, 1f, 0f);

        RectTransform content = UiFactory.CreateRect("Content", transform);
        UiFactory.Stretch(content, 0f, 0f, 0f, 0f);

        HorizontalLayoutGroup layout = UiFactory.AddHorizontalLayout(content.gameObject, 12f, new RectOffset(16, 16, 0, 0), TextAnchor.MiddleLeft);
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;

        iconImage = UiFactory.CreateImage("Icon", content, sprites.GetIcon(iconName), UiTheme.Brand);
        iconImage.rectTransform.sizeDelta = new Vector2(20f, 20f);
        UiFactory.EnsureLayoutElement(iconImage.gameObject, 20f, 20f, 0f, 0f);

        label = UiFactory.CreateLabel("Label", content, labelText, 14, UiTheme.BodyText, FontStyles.Normal, TextAlignmentOptions.Left);
        label.enableAutoSizing = false;
        LayoutElement labelElement = UiFactory.EnsureLayoutElement(label.gameObject, -1f, 24f, 1f, 0f);
        labelElement.minWidth = 60f;

        RectTransform spacer = UiFactory.CreateRect("Spacer", content);
        UiFactory.EnsureLayoutElement(spacer.gameObject, 0f, 0f, 1f, 0f);

        chevronImage = UiFactory.CreateImage("Chevron", content, sprites.GetIcon("icon_nextarrow"), UiTheme.Brand);
        chevronImage.rectTransform.sizeDelta = new Vector2(12f, 12f);
        chevronImage.enabled = showChevron;
        UiFactory.EnsureLayoutElement(chevronImage.gameObject, showChevron ? 12f : 0f, 12f, 0f, 0f);

        divider = UiFactory.CreateImage("Divider", transform, UiTheme.WhiteSprite, UiTheme.Divider);
        divider.type = Image.Type.Simple;
        RectTransform dividerRect = divider.rectTransform;
        dividerRect.anchorMin = new Vector2(0f, 0f);
        dividerRect.anchorMax = new Vector2(1f, 0f);
        dividerRect.pivot = new Vector2(0.5f, 0f);
        dividerRect.offsetMin = new Vector2(28f, 0f);
        dividerRect.offsetMax = new Vector2(-18f, 1f);
        divider.enabled = dividerVisible;

        if (onClick != null)
        {
            UiFactory.AddButton(gameObject, onClick);
        }
    }
}
