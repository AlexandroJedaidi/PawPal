using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class TrainerLevelWidgetView : MonoBehaviour
{
    private const float BaseWidth = 175f;
    private const float BaseHeight = 76f;
    private const float BarGroupWidth = 148f;
    private const float BarGroupHeight = 29f;
    private const float TrackWidth = 138f;
    private const float TrackHeight = 11f;
    private const float FillWidth = 97f;
    private const float StarWidth = 31f;
    private const float StarHeight = 29f;

    private LayoutElement layoutElement;
    private Image background;
    private TextMeshProUGUI titleLabel;
    private RectTransform barGroup;
    private Image barTrack;
    private Image barFill;
    private Image starBadge;
    private TextMeshProUGUI levelLabel;

    public void Initialize(int level, float progress01, UiSpriteLibrary sprites)
    {
        layoutElement = UiFactory.EnsureLayoutElement(gameObject, -1f, BaseHeight, 1f, 0f);
        layoutElement.minHeight = BaseHeight;
        layoutElement.flexibleWidth = 1f;

        RectTransform frame = UiFactory.CreateRect("Frame", transform);
        frame.anchorMin = new Vector2(0.5f, 1f);
        frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.sizeDelta = new Vector2(BaseWidth, BaseHeight);
        frame.anchoredPosition = Vector2.zero;

        background = frame.gameObject.GetComponent<Image>();
        if (background == null)
        {
            background = frame.gameObject.AddComponent<Image>();
        }

        background.sprite = UiTheme.TrainerLevelCardSprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = UiTheme.NavBackgroundCream;

        Shadow shadow = frame.gameObject.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = frame.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;

        titleLabel = UiFactory.CreateLabel("Title", frame, "Trainer level", 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        titleLabel.font = UiTheme.NavExtraBoldFont;
        titleLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        titleLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        titleLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        titleLabel.rectTransform.sizeDelta = new Vector2(171f, 18f);
        titleLabel.rectTransform.anchoredPosition = new Vector2(0f, 21f);

        barGroup = UiFactory.CreateRect("BarGroup", frame);
        barGroup.anchorMin = new Vector2(0.5f, 0f);
        barGroup.anchorMax = new Vector2(0.5f, 0f);
        barGroup.pivot = new Vector2(0.5f, 0f);
        barGroup.sizeDelta = new Vector2(BarGroupWidth, BarGroupHeight);
        barGroup.anchoredPosition = Vector2.zero;

        barTrack = UiFactory.CreateImage("Track", barGroup, UiTheme.RoundedSprite, UiTheme.White);
        barTrack.type = Image.Type.Sliced;
        barTrack.preserveAspect = false;
        barTrack.rectTransform.anchorMin = new Vector2(0f, 0f);
        barTrack.rectTransform.anchorMax = new Vector2(0f, 0f);
        barTrack.rectTransform.pivot = new Vector2(0f, 0f);
        barTrack.rectTransform.sizeDelta = new Vector2(TrackWidth, TrackHeight);
        barTrack.rectTransform.anchoredPosition = new Vector2(10f, 10f);

        Shadow trackShadow = barTrack.gameObject.GetComponent<Shadow>();
        if (trackShadow == null)
        {
            trackShadow = barTrack.gameObject.AddComponent<Shadow>();
        }

        trackShadow.effectColor = new Color(0f, 0f, 0f, 0.14f);
        trackShadow.effectDistance = new Vector2(0f, -1f);
        trackShadow.useGraphicAlpha = true;

        barFill = UiFactory.CreateImage("Fill", barGroup, UiTheme.TrainerLevelProgressSprite, Color.white);
        barFill.type = Image.Type.Simple;
        barFill.preserveAspect = false;
        barFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        barFill.rectTransform.anchorMax = new Vector2(0f, 0f);
        barFill.rectTransform.pivot = new Vector2(0f, 0f);
        barFill.rectTransform.sizeDelta = new Vector2(FillWidth * Mathf.Clamp01(progress01), TrackHeight);
        barFill.rectTransform.anchoredPosition = new Vector2(10f, 10f);

        Sprite exactStar = sprites != null ? sprites.GetResourceSprite("UI/Figma/HomeMain/trainer_star") : UiTheme.TrainerLevelStarSprite;
        starBadge = UiFactory.CreateImage("StarBadge", barGroup, exactStar, Color.white);
        starBadge.type = Image.Type.Simple;
        starBadge.preserveAspect = false;
        starBadge.rectTransform.anchorMin = new Vector2(0f, 0f);
        starBadge.rectTransform.anchorMax = new Vector2(0f, 0f);
        starBadge.rectTransform.pivot = new Vector2(0f, 0f);
        starBadge.rectTransform.sizeDelta = new Vector2(StarWidth, StarHeight);
        starBadge.rectTransform.anchoredPosition = Vector2.zero;

        Shadow badgeShadow = starBadge.gameObject.GetComponent<Shadow>();
        if (badgeShadow == null)
        {
            badgeShadow = starBadge.gameObject.AddComponent<Shadow>();
        }

        badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        badgeShadow.effectDistance = new Vector2(0f, -2f);
        badgeShadow.useGraphicAlpha = true;

        levelLabel = UiFactory.CreateLabel("Level", barGroup, level.ToString(), 13, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        levelLabel.font = UiTheme.NavExtraBoldFont;
        levelLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        levelLabel.rectTransform.anchorMax = new Vector2(0f, 0f);
        levelLabel.rectTransform.pivot = new Vector2(0f, 0f);
        levelLabel.rectTransform.sizeDelta = new Vector2(11.5f, 23f);
        levelLabel.rectTransform.anchoredPosition = new Vector2(10f, 3f);
    }

    public void SetState(int level, float progress01)
    {
        if (levelLabel != null)
        {
            levelLabel.text = level.ToString();
        }

        if (barFill != null)
        {
            barFill.rectTransform.sizeDelta = new Vector2(FillWidth * Mathf.Clamp01(progress01), TrackHeight);
        }
    }
}
