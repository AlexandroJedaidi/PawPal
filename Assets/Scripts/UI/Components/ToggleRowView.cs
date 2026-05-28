using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ToggleRowView : MonoBehaviour
{
    private Image iconImage;
    private TextMeshProUGUI label;
    private Image toggleBackground;
    private RectTransform toggleKnob;
    private Image divider;
    private bool isOn;

    public void Initialize(UiSpriteLibrary sprites, string iconName, string labelText, bool initialState, bool dividerVisible, UnityAction<bool> onValueChanged)
    {
        isOn = initialState;
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
        UiFactory.EnsureLayoutElement(label.gameObject, -1f, 24f, 1f, 0f);

        RectTransform spacer = UiFactory.CreateRect("Spacer", content);
        UiFactory.EnsureLayoutElement(spacer.gameObject, 0f, 0f, 1f, 0f);

        toggleBackground = UiFactory.CreateImage("Toggle", content, UiTheme.RoundedSprite, UiTheme.Brand);
        toggleBackground.type = Image.Type.Sliced;
        toggleBackground.preserveAspect = false;
        RectTransform toggleRect = toggleBackground.rectTransform;
        toggleRect.sizeDelta = new Vector2(48f, 28f);
        UiFactory.EnsureLayoutElement(toggleRect.gameObject, 48f, 28f, 0f, 0f);

        Image knob = UiFactory.CreateImage("Knob", toggleRect, UiTheme.CircleSprite, UiTheme.White);
        knob.type = Image.Type.Sliced;
        knob.preserveAspect = false;
        toggleKnob = knob.rectTransform;
        toggleKnob.anchorMin = new Vector2(0f, 0.5f);
        toggleKnob.anchorMax = new Vector2(0f, 0.5f);
        toggleKnob.pivot = new Vector2(0.5f, 0.5f);
        toggleKnob.sizeDelta = new Vector2(22f, 22f);

        divider = UiFactory.CreateImage("Divider", transform, UiTheme.WhiteSprite, UiTheme.Divider);
        divider.type = Image.Type.Simple;
        RectTransform dividerRect = divider.rectTransform;
        dividerRect.anchorMin = new Vector2(0f, 0f);
        dividerRect.anchorMax = new Vector2(1f, 0f);
        dividerRect.pivot = new Vector2(0.5f, 0f);
        dividerRect.offsetMin = new Vector2(28f, 0f);
        dividerRect.offsetMax = new Vector2(-18f, 1f);
        divider.enabled = dividerVisible;

        UiFactory.AddButton(gameObject, delegate
        {
            isOn = !isOn;
            RefreshVisuals();
            if (onValueChanged != null)
            {
                onValueChanged(isOn);
            }
        });

        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        toggleBackground.color = isOn ? UiTheme.Brand : new Color32(195, 195, 195, 255);
        toggleKnob.anchoredPosition = new Vector2(isOn ? 34f : 14f, 0f);
    }
}
