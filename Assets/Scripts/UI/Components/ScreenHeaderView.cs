using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScreenHeaderView : MonoBehaviour
{
    private Image iconImage;
    private TextMeshProUGUI titleLabel;
    private Image lineImage;

    public void Initialize(UiSpriteLibrary sprites, string iconName, string title)
    {
        RectTransform root = GetComponent<RectTransform>();
        if (root == null)
        {
            root = gameObject.AddComponent<RectTransform>();
        }

        UiFactory.EnsureLayoutElement(gameObject, -1f, 40f, 1f, 0f);

        RectTransform row = UiFactory.CreateRect("Row", transform);
        UiFactory.Stretch(row, 0f, 0f, 0f, 0f);
        HorizontalLayoutGroup layout = UiFactory.AddHorizontalLayout(row.gameObject, 10f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;

        iconImage = UiFactory.CreateImage("Icon", row, sprites.GetIcon(iconName), UiTheme.Brand);
        RectTransform iconRect = iconImage.rectTransform;
        iconRect.sizeDelta = new Vector2(26f, 26f);
        UiFactory.EnsureLayoutElement(iconRect.gameObject, 26f, 26f, 0f, 0f);

        titleLabel = UiFactory.CreateLabel("Title", row, title, 24, UiTheme.BrandDark, FontStyles.Bold, TextAlignmentOptions.Left);
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        UiFactory.EnsureLayoutElement(titleLabel.gameObject, -1f, 32f, 0f, 0f);

        lineImage = UiFactory.CreateImage("Rule", row, UiTheme.WhiteSprite, UiTheme.Brand);
        lineImage.type = Image.Type.Simple;
        RectTransform lineRect = lineImage.rectTransform;
        lineRect.sizeDelta = new Vector2(120f, 1.5f);
        LayoutElement lineElement = UiFactory.EnsureLayoutElement(lineRect.gameObject, 80f, 2f, 1f, 0f);
        lineElement.minWidth = 24f;
    }
}
