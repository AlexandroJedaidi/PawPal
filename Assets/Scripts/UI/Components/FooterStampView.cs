using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class FooterStampView : MonoBehaviour
{
    public void Initialize(UiSpriteLibrary sprites, string text)
    {
        UiFactory.EnsureLayoutElement(gameObject, -1f, 86f, 1f, 0f);

        RectTransform row = UiFactory.CreateRect("Row", transform);
        UiFactory.Stretch(row, 0f, 0f, 0f, 0f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Text", row, text, 12, UiTheme.SubtleText, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 0f);
        labelRect.pivot = new Vector2(0f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 10f);
        labelRect.sizeDelta = new Vector2(160f, 48f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;

        Image paw = UiFactory.CreateImage("Paw", row, sprites.GetIcon("icon_pawprint_other"), UiTheme.White);
        paw.preserveAspect = true;
        RectTransform pawRect = paw.rectTransform;
        pawRect.anchorMin = new Vector2(1f, 0f);
        pawRect.anchorMax = new Vector2(1f, 0f);
        pawRect.pivot = new Vector2(1f, 0f);
        pawRect.anchoredPosition = new Vector2(0f, 8f);
        pawRect.sizeDelta = new Vector2(72f, 72f);
    }
}
