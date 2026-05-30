using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class HomeVoicePanelView : MonoBehaviour
{
    private const float PanelWidth = 276f;
    private const float PanelHeight = 178f;

    private PawPalVoiceInputController controller;
    private Action closeRequested;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI nameProgressLabel;
    private TextMeshProUGUI sitProgressLabel;
    private TextMeshProUGUI confidenceLabel;
    private Button teachNameButton;
    private Button teachSitButton;
    private Button listenButton;
    private Image teachNameBackground;
    private Image teachSitBackground;
    private Image listenBackground;

    public void Initialize(PawPalVoiceInputController voiceController, Action onCloseRequested)
    {
        controller = voiceController;
        closeRequested = onCloseRequested;

        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image background = UiFactory.CreateImage("Background", root, UiTheme.RoundedTenSprite, UiTheme.NavBackgroundCream);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        Outline outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = UiTheme.NavBrand;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Shadow shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        BuildHeader(root);
        BuildProgress(root);
        BuildButtons(root);

        if (controller != null)
        {
            controller.StateChanged += Refresh;
        }

        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.StateChanged -= Refresh;
        }
    }

    public void Refresh()
    {
        if (controller == null || titleLabel == null)
        {
            return;
        }

        PawPalVoiceInputSnapshot snapshot = controller.Snapshot;
        string dogName = string.IsNullOrEmpty(snapshot.ActiveDogName) ? "Dog" : snapshot.ActiveDogName;
        titleLabel.text = dogName + " Voice";
        statusLabel.text = string.IsNullOrEmpty(snapshot.Message) ? "Voice is ready." : snapshot.Message;
        nameProgressLabel.text = BuildProgressText("Name", snapshot.NameSampleCount, snapshot.RequiredSamples, snapshot.NameLearned);
        sitProgressLabel.text = BuildProgressText("Sit", snapshot.SitSampleCount, snapshot.RequiredSamples, snapshot.SitLearned);
        confidenceLabel.text = snapshot.LastConfidence > 0f ? "Match " + Mathf.RoundToInt(snapshot.LastConfidence * 100f) + "%" : string.Empty;

        bool busy = snapshot.IsBusy;
        SetButtonState(teachNameButton, teachNameBackground, !busy, UiTheme.NavBrand);
        SetButtonState(teachSitButton, teachSitBackground, !busy && snapshot.NameLearned, UiTheme.NavBrandDark);
        SetButtonState(listenButton, listenBackground, !busy && snapshot.NameLearned, UiTheme.Brand);
    }

    private void BuildHeader(RectTransform root)
    {
        titleLabel = UiFactory.CreateLabel("Title", root, "Dog Voice", 16, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        titleLabel.font = UiTheme.NavExtraBoldFont;
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(14f, -36f);
        titleLabel.rectTransform.offsetMax = new Vector2(-44f, -8f);

        Image closeBackground;
        Button closeButton = CreateTextButton(root, "CloseButton", "X", new Vector2(238f, -10f), new Vector2(26f, 24f), UiTheme.NavBackgroundCream, UiTheme.NavBrandDark, delegate
        {
            if (controller != null)
            {
                controller.CancelActiveOperation();
            }

            if (closeRequested != null)
            {
                closeRequested();
            }
        }, out closeBackground);

        if (closeButton != null)
        {
            closeButton.transition = Selectable.Transition.None;
        }

        statusLabel = UiFactory.CreateLabel("Status", root, "Voice is ready.", 13, UiTheme.BodyText, FontStyles.Normal, TextAlignmentOptions.Left);
        statusLabel.font = UiTheme.NavRegularFont;
        statusLabel.textWrappingMode = TextWrappingModes.Normal;
        statusLabel.overflowMode = TextOverflowModes.Ellipsis;
        statusLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        statusLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        statusLabel.rectTransform.pivot = new Vector2(0f, 1f);
        statusLabel.rectTransform.offsetMin = new Vector2(14f, -78f);
        statusLabel.rectTransform.offsetMax = new Vector2(-14f, -40f);
    }

    private void BuildProgress(RectTransform root)
    {
        nameProgressLabel = UiFactory.CreateLabel("NameProgress", root, "Name 0/3", 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        nameProgressLabel.font = UiTheme.NavMediumFont;
        nameProgressLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameProgressLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        nameProgressLabel.rectTransform.pivot = new Vector2(0f, 1f);
        nameProgressLabel.rectTransform.sizeDelta = new Vector2(96f, 22f);
        nameProgressLabel.rectTransform.anchoredPosition = new Vector2(14f, -83f);

        sitProgressLabel = UiFactory.CreateLabel("SitProgress", root, "Sit 0/3", 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        sitProgressLabel.font = UiTheme.NavMediumFont;
        sitProgressLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        sitProgressLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        sitProgressLabel.rectTransform.pivot = new Vector2(0f, 1f);
        sitProgressLabel.rectTransform.sizeDelta = new Vector2(82f, 22f);
        sitProgressLabel.rectTransform.anchoredPosition = new Vector2(114f, -83f);

        confidenceLabel = UiFactory.CreateLabel("Confidence", root, string.Empty, 12, UiTheme.SubtleText, FontStyles.Normal, TextAlignmentOptions.Right);
        confidenceLabel.font = UiTheme.NavMediumFont;
        confidenceLabel.rectTransform.anchorMin = new Vector2(1f, 1f);
        confidenceLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        confidenceLabel.rectTransform.pivot = new Vector2(1f, 1f);
        confidenceLabel.rectTransform.sizeDelta = new Vector2(70f, 22f);
        confidenceLabel.rectTransform.anchoredPosition = new Vector2(-14f, -83f);
    }

    private void BuildButtons(RectTransform root)
    {
        teachNameButton = CreateTextButton(root, "TeachNameButton", "Name", new Vector2(14f, -124f), new Vector2(76f, 36f), UiTheme.NavBrand, UiTheme.White, delegate
        {
            if (controller != null)
            {
                controller.BeginTeachName();
            }
        }, out teachNameBackground);

        teachSitButton = CreateTextButton(root, "TeachSitButton", "Sit", new Vector2(100f, -124f), new Vector2(76f, 36f), UiTheme.NavBrandDark, UiTheme.White, delegate
        {
            if (controller != null)
            {
                controller.BeginTeachSit();
            }
        }, out teachSitBackground);

        listenButton = CreateTextButton(root, "ListenButton", "Listen", new Vector2(186f, -124f), new Vector2(76f, 36f), UiTheme.Brand, UiTheme.White, delegate
        {
            if (controller != null)
            {
                controller.BeginListen();
            }
        }, out listenBackground);
    }

    private static Button CreateTextButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color backgroundColor, Color labelColor, UnityEngine.Events.UnityAction onClick, out Image background)
    {
        background = UiFactory.CreateImage(name, parent, UiTheme.RoundedTenSprite, backgroundColor);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.rectTransform.anchorMin = new Vector2(0f, 1f);
        background.rectTransform.anchorMax = new Vector2(0f, 1f);
        background.rectTransform.pivot = new Vector2(0f, 1f);
        background.rectTransform.sizeDelta = size;
        background.rectTransform.anchoredPosition = anchoredPosition;

        TextMeshProUGUI text = UiFactory.CreateLabel("Label", background.rectTransform, label, 13, labelColor, FontStyles.Normal, TextAlignmentOptions.Center);
        text.font = UiTheme.NavExtraBoldFont;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 13f;
        UiFactory.Stretch(text.rectTransform, 4f, 4f, 4f, 4f);

        return UiFactory.AddButton(background.gameObject, onClick);
    }

    private static string BuildProgressText(string label, int count, int required, bool learned)
    {
        if (learned)
        {
            return label + " ready";
        }

        return label + " " + Mathf.Clamp(count, 0, required) + "/" + required;
    }

    private static void SetButtonState(Button button, Image background, bool interactable, Color enabledColor)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }

        if (background != null)
        {
            background.color = interactable ? enabledColor : new Color(0.72f, 0.68f, 0.64f, 0.75f);
        }
    }
}
