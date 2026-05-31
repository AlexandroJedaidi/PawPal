using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class HomeVoicePanelView : MonoBehaviour
{
    private const float PanelWidth = 315f;
    private const float PanelHeight = 144f;
    private const int ProgressDotCount = 3;
    private const string NameIconName = "icon_paw_brand";
    private const string SitIconName = "icon_dog_brand";
    private const string ListenIconPath = "UI/Figma/HomeMain/icon_mic";

    private static readonly Color32 CueFill = new Color32(255, 255, 255, 245);
    private static readonly Color32 DockFill = new Color32(252, 248, 232, 242);
    private static readonly Color32 EnabledFill = new Color32(255, 255, 255, 255);
    private static readonly Color32 DisabledFill = new Color32(226, 220, 207, 220);
    private static readonly Color32 DisabledText = new Color32(158, 141, 130, 255);
    private static readonly Color32 EmptyProgress = new Color32(228, 214, 203, 255);
    private static readonly Color32 ListenAccent = new Color32(72, 139, 166, 255);

    private PawPalVoiceInputController controller;
    private UiSpriteLibrary spriteLibrary;
    private Action closeRequested;
    private TextMeshProUGUI cueLabel;
    private TextMeshProUGUI confidenceLabel;
    private TrainingButtonView nameButtonView;
    private TrainingButtonView sitButtonView;
    private TrainingButtonView listenButtonView;
    private Image closeBackground;
    private Image cueBubble;
    private TrainingButtonView activePulseView;

    public void Initialize(PawPalVoiceInputController voiceController, Action onCloseRequested)
    {
        controller = voiceController;
        closeRequested = onCloseRequested;
        spriteLibrary = GetComponentInParent<UiSpriteLibrary>();
        if (spriteLibrary == null)
        {
            spriteLibrary = FindFirstObjectByType<UiSpriteLibrary>();
        }

        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        BuildCueBubble(root);
        BuildDock(root);

        if (controller != null)
        {
            controller.StateChanged += Refresh;
        }

        Refresh();
    }

    private void Update()
    {
        if (activePulseView == null || activePulseView.Border == null)
        {
            return;
        }

        float pulse = Mathf.PingPong(Time.unscaledTime * 2.8f, 1f);
        Color color = activePulseView.Accent;
        color.a = Mathf.Lerp(0.45f, 1f, pulse);
        activePulseView.Border.color = color;
        float scale = Mathf.Lerp(1f, 1.035f, pulse);
        activePulseView.Background.rectTransform.localScale = new Vector3(scale, scale, 1f);
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
        if (controller == null || cueLabel == null)
        {
            return;
        }

        PawPalVoiceInputSnapshot snapshot = controller.Snapshot;
        string dogName = string.IsNullOrEmpty(snapshot.ActiveDogName) ? "Dog" : snapshot.ActiveDogName;
        cueLabel.text = BuildCueText(snapshot, dogName);
        confidenceLabel.text = snapshot.LastConfidence > 0f
            ? "Match " + Mathf.RoundToInt(snapshot.LastConfidence * 100f) + "%"
            : BuildProgressSummary(snapshot);

        if (cueBubble != null)
        {
            cueBubble.color = snapshot.Mode == PawPalVoiceInputMode.Unavailable
                ? new Color32(255, 240, 232, 250)
                : CueFill;
        }

        bool busy = snapshot.IsBusy;
        bool nameEnabled = !busy;
        bool sitEnabled = !busy && snapshot.NameLearned;
        bool listenEnabled = !busy && snapshot.NameLearned;

        TrainingButtonView activeView = ResolveActiveButton(snapshot.Mode, busy);
        SetButtonState(nameButtonView, nameEnabled, activeView == nameButtonView, snapshot.NameLearned, snapshot.NameSampleCount, snapshot.RequiredSamples);
        SetButtonState(sitButtonView, sitEnabled, activeView == sitButtonView, snapshot.SitLearned, snapshot.SitSampleCount, snapshot.RequiredSamples);
        SetButtonState(listenButtonView, listenEnabled, activeView == listenButtonView, false, 0, 0);

        SetCloseButtonState(true);
        activePulseView = activeView;
    }

    private void BuildCueBubble(RectTransform root)
    {
        cueBubble = UiFactory.CreateImage("CueBubble", root, UiTheme.RoundedTenSprite, CueFill);
        cueBubble.type = Image.Type.Sliced;
        cueBubble.preserveAspect = false;
        cueBubble.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        cueBubble.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        cueBubble.rectTransform.pivot = new Vector2(0.5f, 1f);
        cueBubble.rectTransform.sizeDelta = new Vector2(264f, 45f);
        cueBubble.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        cueBubble.raycastTarget = false;

        Shadow cueShadow = cueBubble.gameObject.AddComponent<Shadow>();
        cueShadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
        cueShadow.effectDistance = new Vector2(0f, -2f);
        cueShadow.useGraphicAlpha = true;

        cueLabel = UiFactory.CreateLabel("Cue", cueBubble.rectTransform, "Ready to train", 15, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        cueLabel.font = UiTheme.NavExtraBoldFont;
        cueLabel.textWrappingMode = TextWrappingModes.NoWrap;
        cueLabel.overflowMode = TextOverflowModes.Ellipsis;
        cueLabel.enableAutoSizing = true;
        cueLabel.fontSizeMin = 11f;
        cueLabel.fontSizeMax = 15f;
        cueLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        cueLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        cueLabel.rectTransform.offsetMin = new Vector2(16f, 16f);
        cueLabel.rectTransform.offsetMax = new Vector2(-16f, -4f);

        confidenceLabel = UiFactory.CreateLabel("Detail", cueBubble.rectTransform, string.Empty, 10, UiTheme.SubtleText, FontStyles.Normal, TextAlignmentOptions.Center);
        confidenceLabel.font = UiTheme.NavMediumFont;
        confidenceLabel.textWrappingMode = TextWrappingModes.NoWrap;
        confidenceLabel.overflowMode = TextOverflowModes.Ellipsis;
        confidenceLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        confidenceLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
        confidenceLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        confidenceLabel.rectTransform.offsetMin = new Vector2(14f, 4f);
        confidenceLabel.rectTransform.offsetMax = new Vector2(-14f, 17f);

        closeBackground = UiFactory.CreateImage("CloseButton", root, UiTheme.CircleSprite, UiTheme.NavBackgroundCream);
        closeBackground.type = Image.Type.Simple;
        closeBackground.preserveAspect = false;
        closeBackground.rectTransform.anchorMin = new Vector2(1f, 1f);
        closeBackground.rectTransform.anchorMax = new Vector2(1f, 1f);
        closeBackground.rectTransform.pivot = new Vector2(1f, 1f);
        closeBackground.rectTransform.sizeDelta = new Vector2(25f, 25f);
        closeBackground.rectTransform.anchoredPosition = new Vector2(-3f, -5f);

        TextMeshProUGUI closeLabel = UiFactory.CreateLabel("Label", closeBackground.rectTransform, "X", 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        closeLabel.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(closeLabel.rectTransform, 0f, 0f, 0f, 0f);

        Button closeButton = UiFactory.AddButton(closeBackground.gameObject, delegate
        {
            if (controller != null)
            {
                controller.CancelActiveOperation();
            }

            if (closeRequested != null)
            {
                closeRequested();
            }
        });
        closeButton.transition = Selectable.Transition.None;
    }

    private void BuildDock(RectTransform root)
    {
        Image dock = UiFactory.CreateImage("TrainingDock", root, UiTheme.RoundedTenSprite, DockFill);
        dock.type = Image.Type.Sliced;
        dock.preserveAspect = false;
        dock.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        dock.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        dock.rectTransform.pivot = new Vector2(0.5f, 0f);
        dock.rectTransform.sizeDelta = new Vector2(315f, 92f);
        dock.rectTransform.anchoredPosition = Vector2.zero;
        dock.raycastTarget = false;

        Shadow shadow = dock.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        nameButtonView = CreateTrainingButton(
            dock.rectTransform,
            "TeachNameButton",
            "Name",
            GetIcon(NameIconName, null, false),
            GetIcon(NameIconName, null, true),
            new Vector2(12f, -16f),
            UiTheme.NavBrand,
            true,
            delegate
            {
                if (controller != null)
                {
                    controller.BeginTeachName();
                }
            });

        sitButtonView = CreateTrainingButton(
            dock.rectTransform,
            "TeachSitButton",
            "Sit",
            GetIcon(SitIconName, null, false),
            GetIcon(SitIconName, null, true),
            new Vector2(114f, -16f),
            UiTheme.NavBrandDark,
            true,
            delegate
            {
                if (controller != null)
                {
                    controller.BeginTeachSit();
                }
            });

        listenButtonView = CreateTrainingButton(
            dock.rectTransform,
            "ListenButton",
            "Listen",
            GetIcon(null, ListenIconPath, false),
            GetIcon(null, ListenIconPath, true),
            new Vector2(216f, -16f),
            ListenAccent,
            false,
            delegate
            {
                if (controller != null)
                {
                    controller.BeginListen();
                }
            });
    }

    private TrainingButtonView CreateTrainingButton(
        RectTransform parent,
        string name,
        string label,
        Sprite defaultIcon,
        Sprite activeIcon,
        Vector2 anchoredPosition,
        Color accent,
        bool hasProgress,
        UnityEngine.Events.UnityAction onClick)
    {
        Image background = UiFactory.CreateImage(name, parent, UiTheme.RoundedTenSprite, EnabledFill);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.rectTransform.anchorMin = new Vector2(0f, 1f);
        background.rectTransform.anchorMax = new Vector2(0f, 1f);
        background.rectTransform.pivot = new Vector2(0f, 1f);
        background.rectTransform.sizeDelta = new Vector2(87f, 60f);
        background.rectTransform.anchoredPosition = anchoredPosition;

        Image border = UiFactory.CreateImage("PulseBorder", background.rectTransform, UiTheme.RoundedTenOutlineSprite, accent);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

        Image icon = UiFactory.CreateImage("Icon", background.rectTransform, defaultIcon, accent);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        icon.rectTransform.pivot = new Vector2(0.5f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(22f, 22f);
        icon.rectTransform.anchoredPosition = new Vector2(0f, -7f);

        TextMeshProUGUI labelText = UiFactory.CreateLabel("Label", background.rectTransform, label, 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        labelText.font = UiTheme.NavExtraBoldFont;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.rectTransform.anchorMin = new Vector2(0f, 1f);
        labelText.rectTransform.anchorMax = new Vector2(1f, 1f);
        labelText.rectTransform.pivot = new Vector2(0.5f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(5f, -42f);
        labelText.rectTransform.offsetMax = new Vector2(-5f, -25f);

        Image[] progressDots = hasProgress ? CreateProgressDots(background.rectTransform) : null;
        Button button = UiFactory.AddButton(background.gameObject, onClick);
        button.transition = Selectable.Transition.None;

        return new TrainingButtonView(button, background, border, icon, labelText, progressDots, defaultIcon, activeIcon, accent);
    }

    private static Image[] CreateProgressDots(RectTransform parent)
    {
        Image[] dots = new Image[ProgressDotCount];
        float startX = -12f;
        for (int i = 0; i < dots.Length; i++)
        {
            Image dot = UiFactory.CreateImage("ProgressDot" + (i + 1), parent, UiTheme.CircleSprite, EmptyProgress);
            dot.type = Image.Type.Simple;
            dot.preserveAspect = false;
            dot.raycastTarget = false;
            dot.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            dot.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            dot.rectTransform.pivot = new Vector2(0.5f, 0f);
            dot.rectTransform.sizeDelta = new Vector2(6f, 6f);
            dot.rectTransform.anchoredPosition = new Vector2(startX + i * 12f, 7f);
            dots[i] = dot;
        }

        return dots;
    }

    private void SetButtonState(TrainingButtonView view, bool interactable, bool active, bool complete, int sampleCount, int requiredSamples)
    {
        if (view == null)
        {
            return;
        }

        view.Button.interactable = interactable;
        view.Background.rectTransform.localScale = Vector3.one;

        Color fillColor = active ? view.Accent : (interactable ? EnabledFill : DisabledFill);
        Color textColor = active ? UiTheme.White : (interactable ? UiTheme.NavBrandDark : DisabledText);
        view.Background.color = fillColor;
        view.Label.color = textColor;
        view.Icon.sprite = active ? view.ActiveIcon : view.DefaultIcon;
        view.Icon.color = active ? UiTheme.White : (interactable ? view.Accent : DisabledText);
        view.Border.gameObject.SetActive(active || complete);
        view.Border.color = complete && !active ? UiTheme.NavBrand : view.Accent;

        if (view.ProgressDots != null)
        {
            int required = Mathf.Max(1, requiredSamples);
            int filledCount = complete ? ProgressDotCount : Mathf.Clamp(sampleCount, 0, required);
            for (int i = 0; i < view.ProgressDots.Length; i++)
            {
                Image dot = view.ProgressDots[i];
                bool filled = i < filledCount;
                dot.color = filled ? (active ? UiTheme.White : view.Accent) : EmptyProgress;
                dot.rectTransform.sizeDelta = complete && filled ? new Vector2(7f, 7f) : new Vector2(6f, 6f);
            }
        }
    }

    private void SetCloseButtonState(bool enabled)
    {
        if (closeBackground == null)
        {
            return;
        }

        Button closeButton = closeBackground.GetComponent<Button>();
        if (closeButton != null)
        {
            closeButton.interactable = enabled;
        }

        closeBackground.color = enabled ? UiTheme.NavBackgroundCream : DisabledFill;
    }

    private TrainingButtonView ResolveActiveButton(PawPalVoiceInputMode mode, bool busy)
    {
        if (!busy)
        {
            return null;
        }

        switch (mode)
        {
            case PawPalVoiceInputMode.TeachingName:
                return nameButtonView;
            case PawPalVoiceInputMode.TeachingTrick:
                return sitButtonView;
            case PawPalVoiceInputMode.ListeningName:
            case PawPalVoiceInputMode.ListeningTrick:
                return listenButtonView;
            default:
                return null;
        }
    }

    private string BuildCueText(PawPalVoiceInputSnapshot snapshot, string dogName)
    {
        if (snapshot == null)
        {
            return "Ready to train";
        }

        switch (snapshot.Mode)
        {
            case PawPalVoiceInputMode.TeachingName:
                return "Say " + dogName;
            case PawPalVoiceInputMode.TeachingTrick:
                return "Say sit";
            case PawPalVoiceInputMode.ListeningName:
                if (!string.IsNullOrEmpty(snapshot.Message) && !snapshot.Message.StartsWith("Say ", StringComparison.Ordinal))
                {
                    return SimplifyMessage(snapshot.Message);
                }

                return "Call " + dogName;
            case PawPalVoiceInputMode.ListeningTrick:
                return "Say sit";
        }

        return SimplifyMessage(snapshot.Message);
    }

    private static string BuildProgressSummary(PawPalVoiceInputSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return string.Empty;
        }

        int requiredSamples = Mathf.Max(1, snapshot.RequiredSamples);
        string nameProgress = snapshot.NameLearned ? "Name ready" : "Name " + Mathf.Clamp(snapshot.NameSampleCount, 0, requiredSamples) + "/" + requiredSamples;
        string sitProgress = snapshot.SitLearned ? "Sit ready" : "Sit " + Mathf.Clamp(snapshot.SitSampleCount, 0, requiredSamples) + "/" + requiredSamples;
        return nameProgress + "   " + sitProgress;
    }

    private static string SimplifyMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.Equals(message, "Voice is ready.", StringComparison.Ordinal))
        {
            return "Ready to train";
        }

        string simplified = message.Trim();
        simplified = simplified.Replace("Good. ", "Good, ");
        simplified = simplified.Replace(" name samples.", " name");
        simplified = simplified.Replace(" sit samples.", " sit");
        simplified = simplified.Replace(" knows its name.", " knows the name.");
        if (simplified.EndsWith(".", StringComparison.Ordinal))
        {
            simplified = simplified.Substring(0, simplified.Length - 1);
        }

        return simplified;
    }

    private Sprite GetIcon(string iconName, string resourcePath, bool white)
    {
        if (spriteLibrary != null)
        {
            if (!string.IsNullOrEmpty(resourcePath))
            {
                return white ? spriteLibrary.GetWhiteResourceSprite(resourcePath) : spriteLibrary.GetResourceSprite(resourcePath);
            }

            return white ? spriteLibrary.GetWhiteIcon(iconName) : spriteLibrary.GetIcon(iconName);
        }

        Sprite sprite = null;
        if (!string.IsNullOrEmpty(resourcePath))
        {
            sprite = Resources.Load<Sprite>(resourcePath);
        }
        else if (!string.IsNullOrEmpty(iconName))
        {
            sprite = Resources.Load<Sprite>("UI/Icons/" + iconName);
        }

        return sprite != null ? sprite : UiTheme.WhiteSprite;
    }

    private sealed class TrainingButtonView
    {
        public TrainingButtonView(
            Button button,
            Image background,
            Image border,
            Image icon,
            TextMeshProUGUI label,
            Image[] progressDots,
            Sprite defaultIcon,
            Sprite activeIcon,
            Color accent)
        {
            Button = button;
            Background = background;
            Border = border;
            Icon = icon;
            Label = label;
            ProgressDots = progressDots;
            DefaultIcon = defaultIcon;
            ActiveIcon = activeIcon;
            Accent = accent;
        }

        public readonly Button Button;
        public readonly Image Background;
        public readonly Image Border;
        public readonly Image Icon;
        public readonly TextMeshProUGUI Label;
        public readonly Image[] ProgressDots;
        public readonly Sprite DefaultIcon;
        public readonly Sprite ActiveIcon;
        public readonly Color Accent;
    }
}
