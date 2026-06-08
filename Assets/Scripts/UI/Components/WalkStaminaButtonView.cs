using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class WalkStaminaButtonView : MonoBehaviour
{
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 StaminaGreen = new Color32(37, 199, 63, 255);
    private static readonly Color32 RingTrack = new Color32(224, 242, 207, 255);
    private static readonly Color32 Coral = new Color32(223, 120, 97, 255);

    private const float ButtonSize = 73f;
    private const float InnerSize = 55f;
    private const float IconSize = 38f;

    private RectTransform rootRect;
    private Image outerFill;
    private Image ringTrack;
    private Image ringFill;
    private Image innerFill;
    private Image icon;

    public void Initialize(UiSpriteLibrary sprites, UnityAction onClick)
    {
        rootRect = GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

        UiFactory.AddButton(gameObject, onClick);

        outerFill = UiFactory.CreateImage("OuterFill", transform, UiTheme.CircleSprite, White);
        outerFill.type = Image.Type.Simple;
        outerFill.preserveAspect = false;
        outerFill.raycastTarget = false;

        ringTrack = UiFactory.CreateImage("RingTrack", transform, UiTheme.CircleSprite, RingTrack);
        ringTrack.type = Image.Type.Simple;
        ringTrack.preserveAspect = false;
        ringTrack.raycastTarget = false;

        ringFill = UiFactory.CreateImage("StaminaRing", transform, UiTheme.CircleSprite, StaminaGreen);
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        ringFill.preserveAspect = false;
        ringFill.raycastTarget = false;

        Shadow shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        innerFill = UiFactory.CreateImage("InnerFill", transform, UiTheme.CircleSprite, White);
        innerFill.type = Image.Type.Simple;
        innerFill.preserveAspect = false;
        innerFill.raycastTarget = false;

        icon = UiFactory.CreateImage("Icon", transform, sprites != null ? sprites.GetIcon("icon_activity_brand") : UiTheme.WhiteSprite, Coral);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        ApplyLayout(1f);
        SetStamina(0f, 0f, 0f);
    }

    public void ApplyLayout(float scale)
    {
        float safeScale = Mathf.Max(0.01f, scale);
        SetCentered(outerFill != null ? outerFill.rectTransform : null, ButtonSize * safeScale, ButtonSize * safeScale, Vector2.zero);
        SetCentered(ringTrack != null ? ringTrack.rectTransform : null, ButtonSize * safeScale, ButtonSize * safeScale, Vector2.zero);
        SetCentered(ringFill != null ? ringFill.rectTransform : null, ButtonSize * safeScale, ButtonSize * safeScale, Vector2.zero);
        SetCentered(innerFill != null ? innerFill.rectTransform : null, InnerSize * safeScale, InnerSize * safeScale, Vector2.zero);
        SetCentered(icon != null ? icon.rectTransform : null, IconSize * safeScale, IconSize * safeScale, Vector2.zero);
    }

    public void SetStamina(float current, float max, float previewCost)
    {
        float fill01 = CalculateFill01(current, max, previewCost);

        if (ringFill != null)
        {
            ringFill.fillAmount = fill01;
        }
    }

    public static float CalculateFill01(float current, float max, float previewCost)
    {
        if (max <= 0.001f)
        {
            return 0f;
        }

        return Mathf.Clamp01((current - Mathf.Max(0f, previewCost)) / max);
    }

    private static void SetCentered(RectTransform rect, float width, float height, Vector2 anchoredPosition)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = anchoredPosition;
    }
}
