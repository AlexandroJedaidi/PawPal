using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsScreenView : AppScreenViewBase
{
    private enum SettingsPage
    {
        Main,
        Notifications,
        Languages
    }

    private enum SettingsModal
    {
        None,
        Feedback,
        FeedbackSuccess
    }

    private sealed class ToggleVisual
    {
        public Image Track;
        public Image Shadow;
        public RectTransform Knob;
    }

    private sealed class VolumeControlVisual
    {
        public TextMeshProUGUI PercentLabel;
        public Button MinusButton;
        public Button PlusButton;
        public Image MinusFill;
        public Image PlusFill;
        public TextMeshProUGUI MinusLabel;
        public TextMeshProUGUI PlusLabel;
    }

    private struct NotificationToggleData
    {
        public string Key;
        public string Label;
        public float Y;
        public bool InitialValue;
    }

    private static readonly Color32 SettingsBackground = new Color32(252, 248, 232, 255);
    private static readonly Color32 CardFill = new Color32(255, 255, 255, 255);
    private static readonly Color32 DividerColor = new Color32(236, 223, 200, 255);
    private static readonly Color32 TitleColor = new Color32(223, 120, 97, 255);
    private static readonly Color32 BodyBlack = new Color32(0, 0, 0, 255);
    private static readonly Color32 FooterGrey = new Color32(122, 122, 122, 255);
    private static readonly Color32 ToggleOffGrey = new Color32(188, 188, 188, 255);
    private static readonly Color32 ToggleShadow = new Color32(0, 0, 0, 64);
    private static readonly Color32 FeedbackFill = new Color32(241, 236, 226, 255);
    private static readonly Color32 FeedbackBorder = new Color32(236, 223, 200, 255);
    private static readonly Color32 DisabledFill = new Color32(220, 220, 220, 255);
    private static readonly Color32 DisabledBorder = new Color32(163, 163, 163, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 CtaBlueDark = new Color32(0, 118, 177, 255);

    private static readonly NotificationToggleData[] NotificationRows =
    {
        new NotificationToggleData { Key = "needs", Label = "Needs", Y = 15.5f, InitialValue = false },
        new NotificationToggleData { Key = "friend_requests", Label = "Friend requests", Y = 73.5f, InitialValue = false },
        new NotificationToggleData { Key = "club_requests", Label = "Club requests", Y = 131.5f, InitialValue = false },
        new NotificationToggleData { Key = "special_offers", Label = "Special offers", Y = 189.5f, InitialValue = true },
        new NotificationToggleData { Key = "weekly_activities", Label = "Weekly activities", Y = 247.5f, InitialValue = false },
        new NotificationToggleData { Key = "competitions", Label = "Competitions", Y = 305.5f, InitialValue = false }
    };

    private static readonly string[] Languages =
    {
        "Arabic",
        "Bengali",
        "Chinese",
        "Dansk (Danish)",
        "Nederlands (Dutch)",
        "English UK (English)",
        "English US (English)",
        "Suomi (Finnish)",
        "Fran\u00E7ais (French)",
        "Deutsch (German)",
        "Greek",
        "Italiano (Italian)",
        "Japanese",
        "Korean",
        "Norsk (Norwegian)",
        "Polski (Polish)",
        "Portugu\u00EAs (Portuguese)",
        "Russian",
        "Espa\u00F1ol (Spanish)",
        "Svenska (Swedish)",
        "T\u00FCrk\u00E7e (Turkish)"
    };

    private static Sprite settingsToggleShadowSprite;

    private readonly Dictionary<string, bool> notificationStates = new Dictionary<string, bool>();
    private readonly Dictionary<string, ToggleVisual> notificationToggleVisuals = new Dictionary<string, ToggleVisual>();

    private RectTransform exactFrame;
    private RectTransform mainPage;
    private RectTransform notificationsPage;
    private RectTransform languagesPage;
    private RectTransform modalLayer;
    private Image modalBlocker;
    private RectTransform feedbackModal;
    private RectTransform feedbackSuccessModal;
    private TMP_InputField feedbackInput;
    private ScrollRect languagesScrollRect;
    private ScrollRect screenScrollRect;
    private VolumeControlVisual musicVolumeControl;
    private VolumeControlVisual soundEffectsVolumeControl;

    private SettingsPage currentPage = SettingsPage.Main;
    private SettingsModal currentModal = SettingsModal.None;
    private ResponsiveFigmaFrameLayout frameLayout;

    protected override bool UseScreenContainer
    {
        get { return false; }
    }

    private void OnEnable()
    {
        PawPalAudioSettings.MusicVolumeChanged += RefreshAudioVolumeControls;
        PawPalAudioSettings.SoundEffectsVolumeChanged += RefreshAudioVolumeControls;
        RefreshAudioVolumeControls();
    }

    private void OnDisable()
    {
        PawPalAudioSettings.MusicVolumeChanged -= RefreshAudioVolumeControls;
        PawPalAudioSettings.SoundEffectsVolumeChanged -= RefreshAudioVolumeControls;
    }

    protected override void BuildContent()
    {
        InitializeNotificationDefaults();

        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        Image fullBackground = root.gameObject.AddComponent<Image>();
        fullBackground.sprite = UiTheme.WhiteSprite;
        fullBackground.type = Image.Type.Simple;
        fullBackground.preserveAspect = false;
        fullBackground.color = SettingsBackground;
        fullBackground.raycastTarget = true;

        exactFrame = UiFactory.CreateRect("SettingsExactFrame", root);
        exactFrame.anchorMin = new Vector2(0.5f, 1f);
        exactFrame.anchorMax = new Vector2(0.5f, 1f);
        exactFrame.pivot = new Vector2(0.5f, 1f);
        exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        exactFrame.anchoredPosition = Vector2.zero;

        Image rootBackground = exactFrame.gameObject.AddComponent<Image>();
        rootBackground.sprite = UiTheme.WhiteSprite;
        rootBackground.type = Image.Type.Simple;
        rootBackground.preserveAspect = false;
        rootBackground.color = SettingsBackground;

        mainPage = CreatePage("MainPage");
        BuildMainPage(mainPage);

        notificationsPage = CreatePage("NotificationsPage");
        BuildNotificationsPage(notificationsPage);

        languagesPage = CreatePage("LanguagesPage");
        BuildLanguagesPage(languagesPage);

        modalLayer = UiFactory.CreateRect("ModalLayer", exactFrame);
        UiFactory.Stretch(modalLayer, 0f, 0f, 0f, 0f);

        modalBlocker = UiFactory.CreateImage("ModalBlocker", modalLayer, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        modalBlocker.type = Image.Type.Simple;
        modalBlocker.preserveAspect = false;
        UiFactory.Stretch(modalBlocker.rectTransform, 0f, 0f, 0f, 0f);
        modalBlocker.raycastTarget = true;

        feedbackModal = BuildFeedbackModal(modalLayer);
        feedbackSuccessModal = BuildFeedbackSuccessModal(modalLayer);

        screenScrollRect = root.gameObject.AddComponent<ScrollRect>();
        screenScrollRect.viewport = root;
        screenScrollRect.content = exactFrame;
        screenScrollRect.horizontal = false;
        screenScrollRect.vertical = true;
        screenScrollRect.movementType = ScrollRect.MovementType.Clamped;
        screenScrollRect.scrollSensitivity = 22f;

        RefreshViewState();
    }

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            frameLayout = ResponsiveFigmaFrame.Apply(root, exactFrame);
        }

        ApplyFooterLayout(mainPage);
        ApplyFooterLayout(notificationsPage);
        ApplyFooterLayout(languagesPage);

        if (screenScrollRect != null)
        {
            bool contentOverflows = frameLayout.LogicalHeight > frameLayout.VisibleLogicalHeight + 0.5f;
            screenScrollRect.vertical = contentOverflows;
            if (!contentOverflows)
            {
                screenScrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }

    private void InitializeNotificationDefaults()
    {
        if (notificationStates.Count > 0)
        {
            return;
        }

        for (int i = 0; i < NotificationRows.Length; i++)
        {
            notificationStates[NotificationRows[i].Key] = NotificationRows[i].InitialValue;
        }
    }

    private RectTransform CreatePage(string name)
    {
        RectTransform page = UiFactory.CreateRect(name, exactFrame);
        UiFactory.Stretch(page, 0f, 0f, 0f, 0f);
        return page;
    }

    private void BuildMainPage(RectTransform parent)
    {
        BuildHeader(parent, "icon_settings_notfilled", "Settings");
        BuildFooter(parent);

        RectTransform settingsCardTop = BuildCard(parent, "Frame_Settings1", 57f, 126f, 280f, 224f);
        musicVolumeControl = BuildVolumeRow(
            settingsCardTop,
            "Frame_Music",
            13.5f,
            "icon_audio",
            "Music",
            delegate
            {
                PawPalAudioSettings.AdjustMusicVolume(-PawPalAudioSettings.VolumeStep);
                RefreshViewState();
            },
            delegate
            {
                PawPalAudioSettings.AdjustMusicVolume(PawPalAudioSettings.VolumeStep);
                RefreshViewState();
            });
        BuildDivider(settingsCardTop, 58.5f);
        soundEffectsVolumeControl = BuildVolumeRow(
            settingsCardTop,
            "Frame_Audio",
            72.5f,
            "icon_audio",
            "Sound effects",
            delegate
            {
                PawPalAudioSettings.AdjustSoundEffectsVolume(-PawPalAudioSettings.VolumeStep);
                RefreshViewState();
            },
            delegate
            {
                PawPalAudioSettings.AdjustSoundEffectsVolume(PawPalAudioSettings.VolumeStep);
                RefreshViewState();
            });
        BuildDivider(settingsCardTop, 117.5f);
        BuildActionRow(
            settingsCardTop,
            "Frame_Notifications",
            133.5f,
            "icon_notification",
            "Notifications",
            delegate
            {
                currentPage = SettingsPage.Notifications;
                currentModal = SettingsModal.None;
                RefreshViewState();
            });
        BuildDivider(settingsCardTop, 173.5f);
        BuildActionRow(
            settingsCardTop,
            "Frame_Language",
            189.5f,
            "icon_language",
            "Language",
            delegate
            {
                currentPage = SettingsPage.Languages;
                currentModal = SettingsModal.None;
                RefreshViewState();
            });

        RectTransform settingsCardBottom = BuildCard(parent, "Frame_Settings2", 57f, 399f, 280f, 163f);
        BuildActionRow(
            settingsCardBottom,
            "Frame_Feedback",
            13.5f,
            "icon_feedback",
            "Send us feedback",
            delegate
            {
                currentModal = SettingsModal.Feedback;
                RefreshViewState();
                if (feedbackInput != null)
                {
                    feedbackInput.ActivateInputField();
                }
            });
        BuildDivider(settingsCardBottom, 53.5f);
        BuildActionRow(
            settingsCardBottom,
            "Frame_Rate",
            69.5f,
            "icon_rating",
            "Rate our app",
            delegate
            {
                Debug.Log("Settings rate action is not defined by the current Figma variants.");
            });
        BuildDivider(settingsCardBottom, 109.5f);
        BuildActionRow(
            settingsCardBottom,
            "Frame_Share",
            125.5f,
            "icon_share",
            "Share our app",
            delegate
            {
                Debug.Log("Settings share action is not defined by the current Figma variants.");
            });
    }

    private void BuildNotificationsPage(RectTransform parent)
    {
        BuildBackButton(parent);
        BuildHeader(parent, "icon_notification", "Notifications");
        BuildFooter(parent);

        RectTransform card = BuildCard(parent, "NotificationsCard", 57f, 126f, 280f, 347f);
        for (int i = 0; i < NotificationRows.Length; i++)
        {
            NotificationToggleData row = NotificationRows[i];
            ToggleVisual toggle = BuildTextOnlyToggleRow(
                card,
                row.Key,
                row.Label,
                row.Y,
                delegate
                {
                    notificationStates[row.Key] = !notificationStates[row.Key];
                    RefreshViewState();
                });
            notificationToggleVisuals[row.Key] = toggle;

            if (i < NotificationRows.Length - 1)
            {
                BuildDivider(card, row.Y + 42f);
            }
        }
    }

    private void BuildLanguagesPage(RectTransform parent)
    {
        BuildBackButton(parent);
        BuildHeader(parent, "icon_language", "Languages");
        BuildFooter(parent);

        RectTransform panel = BuildCard(parent, "LanguagesPanel", 57f, 152f, 280f, 445f);
        RectTransform viewport = UiFactory.CreateRect("Viewport", panel);
        UiFactory.Stretch(viewport, 0f, 0f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UiFactory.CreateRect("LanguagesContent", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(280f, 953f);
        content.anchoredPosition = Vector2.zero;

        languagesScrollRect = panel.gameObject.AddComponent<ScrollRect>();
        languagesScrollRect.viewport = viewport;
        languagesScrollRect.content = content;
        languagesScrollRect.horizontal = false;
        languagesScrollRect.vertical = true;
        languagesScrollRect.movementType = ScrollRect.MovementType.Clamped;
        languagesScrollRect.scrollSensitivity = 20f;

        for (int i = 0; i < Languages.Length; i++)
        {
            float rowY = 15f + (46f * i);
            TextMeshProUGUI label = CreateText(content, "Language_" + i, Languages[i], 16, BodyBlack, UiTheme.NavMediumFont, TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0f, 1f);
            label.rectTransform.anchorMax = new Vector2(0f, 1f);
            label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.sizeDelta = new Vector2(266f, 18f);
            label.rectTransform.anchoredPosition = new Vector2(7f, -rowY);

            if (i < Languages.Length - 1)
            {
                CreateLine(content, "Divider_" + i, 0f, rowY + 32f, 280f);
            }
        }

        Canvas.ForceUpdateCanvases();
        languagesScrollRect.verticalNormalizedPosition = 1f;
    }

    private RectTransform BuildFeedbackModal(RectTransform parent)
    {
        RectTransform modal = BuildOutlinedPanel(parent, "FeedbackModal", 49.5f, 298f, 294f, 256f);

        TextMeshProUGUI title = CreateText(modal, "FeedbackTitle", "Send us your feedback!", 16, TitleColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(274f, 24f);
        title.rectTransform.anchoredPosition = new Vector2(10f, -10f);

        RectTransform textFrame = CreateNode("Frame_Text", modal, 10f, 44f, 274f, 163f);
        Image textFill = textFrame.gameObject.AddComponent<Image>();
        textFill.sprite = UiTheme.RoundedTenSprite;
        textFill.type = Image.Type.Sliced;
        textFill.preserveAspect = false;
        textFill.color = FeedbackFill;

        Image textOutline = UiFactory.CreateImage("Outline", textFrame, UiTheme.RoundedTenOutlineSprite, FeedbackBorder);
        textOutline.type = Image.Type.Sliced;
        textOutline.preserveAspect = false;
        UiFactory.Stretch(textOutline.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform textViewport = UiFactory.CreateRect("TextViewport", textFrame);
        UiFactory.Stretch(textViewport, 10f, 10f, 10f, 10f);
        textViewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI inputText = CreateText(textViewport, "Text", string.Empty, 15, BodyBlack, UiTheme.NavRegularFont, TextAlignmentOptions.TopLeft);
        UiFactory.Stretch(inputText.rectTransform, 0f, 0f, 0f, 0f);
        inputText.enableAutoSizing = false;
        inputText.textWrappingMode = TextWrappingModes.Normal;
        inputText.overflowMode = TextOverflowModes.Overflow;

        TextMeshProUGUI placeholder = CreateText(textViewport, "Placeholder", "Enter feedback here ...", 15, BodyBlack, UiTheme.NavRegularFont, TextAlignmentOptions.TopLeft);
        UiFactory.Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);
        placeholder.enableAutoSizing = false;
        placeholder.textWrappingMode = TextWrappingModes.Normal;
        placeholder.overflowMode = TextOverflowModes.Overflow;

        feedbackInput = textFrame.gameObject.AddComponent<TMP_InputField>();
        feedbackInput.textViewport = textViewport;
        feedbackInput.textComponent = inputText;
        feedbackInput.placeholder = placeholder;
        feedbackInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        feedbackInput.pointSize = 15f;
        feedbackInput.text = string.Empty;

        RectTransform options = CreateNode("Frame_Options", modal, 71.5f, 217f, 151f, 24f);
        CreateActionButton(options, "CancelButton", "Cancel", 0f, 0f, 69f, false, delegate
        {
            currentModal = SettingsModal.None;
            RefreshViewState();
        });
        CreateActionButton(options, "SendButton", "Send", 96f, 0f, 55f, true, delegate
        {
            currentModal = SettingsModal.FeedbackSuccess;
            PawPalUiAudio.PlaySuccessPopup();
            RefreshViewState();
        });

        return modal;
    }

    private RectTransform BuildFeedbackSuccessModal(RectTransform parent)
    {
        RectTransform modal = BuildOutlinedPanel(parent, "FeedbackSuccessModal", 49.5f, 372.5f, 294f, 107f);

        TextMeshProUGUI title = CreateText(modal, "SuccessTitle", "Your feedback has been sent\n- Thanks!", 16, TitleColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(274f, 48f);
        title.rectTransform.anchoredPosition = new Vector2(10f, -10f);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.overflowMode = TextOverflowModes.Overflow;

        RectTransform options = CreateNode("Frame_Options", modal, 103f, 68f, 88f, 24f);
        CreateActionButton(options, "ContinueButton", "Continue", 0f, 0f, 88f, true, delegate
        {
            currentModal = SettingsModal.None;
            if (feedbackInput != null)
            {
                feedbackInput.text = string.Empty;
            }

            RefreshViewState();
        });

        return modal;
    }

    private RectTransform BuildOutlinedPanel(RectTransform parent, string name, float x, float y, float width, float height)
    {
        RectTransform panel = CreateNode(name, parent, x, y, width, height);

        Image fill = panel.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = SettingsBackground;

        Image outline = UiFactory.CreateImage("Outline", panel, UiTheme.RoundedTenOutlineSprite, UiTheme.NavBrand);
        outline.type = Image.Type.Sliced;
        outline.preserveAspect = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        return panel;
    }

    private void BuildHeader(RectTransform parent, string iconName, string title)
    {
        RectTransform header = CreateNode("Frame_Header_" + title, parent, 22f, 67f, 315.0018f, 31f);

        Image icon = UiFactory.CreateImage("Icon", header, sprites.GetIcon(iconName), TitleColor);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0f, 1f);
        icon.rectTransform.pivot = new Vector2(0f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(31f, 31f);
        icon.rectTransform.anchoredPosition = Vector2.zero;

        RectTransform textFrame = CreateNode("TextFrame", header, 35f, 2f, 280.0018f, 27f);
        TextMeshProUGUI label = CreateText(textFrame, "Title", title, 24, TitleColor, UiTheme.NavBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(280.0018f, 18f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        CreateLine(textFrame, "HeaderLine", 0f, 27f, 280.0018f);
    }

    private void BuildBackButton(RectTransform parent)
    {
        RectTransform back = CreateNode("Frame_Back", parent, 20f, 26f, 130f, 24f);
        UiFactory.AddButton(back.gameObject, delegate
        {
            currentPage = SettingsPage.Main;
            currentModal = SettingsModal.None;
            RefreshViewState();
        });

        Image arrow = UiFactory.CreateImage("Arrow", back, sprites.GetIcon("icon_nextarrow"), TitleColor);
        arrow.type = Image.Type.Simple;
        arrow.preserveAspect = true;
        arrow.rectTransform.anchorMin = new Vector2(0f, 1f);
        arrow.rectTransform.anchorMax = new Vector2(0f, 1f);
        arrow.rectTransform.pivot = new Vector2(0f, 1f);
        arrow.rectTransform.sizeDelta = new Vector2(12f, 24f);
        arrow.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        arrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        TextMeshProUGUI label = CreateText(back, "Label", "Settings", 15, TitleColor, UiTheme.NavBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(115f, 18f);
        label.rectTransform.anchoredPosition = new Vector2(15f, -3f);
    }

    private void BuildFooter(RectTransform parent)
    {
        RectTransform footer = CreateNode("Frame_PWVersion", parent, 57f, 620.0186f, 280f, 105.9635f);

        TextMeshProUGUI label = CreateText(footer, "FooterLabel", "Paw World \u00A9 2025.\nVersion 0.1", 16, FooterGrey, UiTheme.NavBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(155f, 49f);
        label.rectTransform.anchoredPosition = new Vector2(2.0182f, -28.4818f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;

        Image paw = UiFactory.CreateImage("Paw", footer, sprites.GetIcon("icon_pawprint_other"), UiTheme.White);
        paw.type = Image.Type.Simple;
        paw.preserveAspect = true;
        paw.rectTransform.anchorMin = new Vector2(0f, 1f);
        paw.rectTransform.anchorMax = new Vector2(0f, 1f);
        paw.rectTransform.pivot = new Vector2(0f, 1f);
        paw.rectTransform.sizeDelta = new Vector2(105.9635f, 105.9635f);
        paw.rectTransform.anchoredPosition = new Vector2(172.0182f, 0f);
    }

    private void ApplyFooterLayout(RectTransform page)
    {
        if (page == null)
        {
            return;
        }

        Transform footerTransform = page.Find("Frame_PWVersion");
        RectTransform footer = footerTransform as RectTransform;
        if (footer == null)
        {
            return;
        }

        float frameHeight = frameLayout.VisibleLogicalHeight > 0f ? frameLayout.VisibleLogicalHeight : UiTheme.ReferenceContentHeight;
        float footerY = Mathf.Max(0f, frameHeight - 105.9635f - 60f);
        footer.anchoredPosition = new Vector2(57f, -footerY);
    }

    private RectTransform BuildCard(RectTransform parent, string name, float x, float y, float width, float height)
    {
        RectTransform card = CreateNode(name, parent, x, y, width, height);
        Image fill = card.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = CardFill;
        return card;
    }

    private VolumeControlVisual BuildVolumeRow(RectTransform parent, string name, float y, string iconName, string labelText, UnityEngine.Events.UnityAction onDecrease, UnityEngine.Events.UnityAction onIncrease)
    {
        RectTransform row = CreateNode(name, parent, 0f, y, 280f, 34f);

        Image icon = UiFactory.CreateImage("Icon", row, sprites.GetIcon(iconName), TitleColor);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0f, 1f);
        icon.rectTransform.pivot = new Vector2(0f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
        icon.rectTransform.anchoredPosition = new Vector2(5f, -5f);

        TextMeshProUGUI label = CreateText(row, "Label", labelText, 16, BodyBlack, UiTheme.NavMediumFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(112f, 18f);
        label.rectTransform.anchoredPosition = new Vector2(39f, -8f);

        VolumeControlVisual visual = new VolumeControlVisual();
        visual.MinusButton = CreateVolumeStepButton(row, "Decrease", "-", 154f, 3f, onDecrease, out visual.MinusFill, out visual.MinusLabel);

        TextMeshProUGUI percent = CreateText(row, "Percent", "100%", 15, BodyBlack, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        percent.rectTransform.anchorMin = new Vector2(0f, 1f);
        percent.rectTransform.anchorMax = new Vector2(0f, 1f);
        percent.rectTransform.pivot = new Vector2(0f, 1f);
        percent.rectTransform.sizeDelta = new Vector2(49f, 18f);
        percent.rectTransform.anchoredPosition = new Vector2(185f, -8f);
        visual.PercentLabel = percent;

        visual.PlusButton = CreateVolumeStepButton(row, "Increase", "+", 240f, 3f, onIncrease, out visual.PlusFill, out visual.PlusLabel);
        return visual;
    }

    private Button CreateVolumeStepButton(RectTransform parent, string name, string labelText, float x, float y, UnityEngine.Events.UnityAction onClick, out Image fill, out TextMeshProUGUI label)
    {
        RectTransform buttonRect = CreateNode(name, parent, x, y, 28f, 28f);

        fill = buttonRect.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = TitleColor;

        Button button = UiFactory.AddButton(buttonRect.gameObject, onClick);

        label = CreateText(buttonRect, "Label", labelText, 18, UiTheme.White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 1f);
        label.raycastTarget = false;
        return button;
    }

    private ToggleVisual BuildToggleRow(RectTransform parent, string name, float y, string iconName, string labelText, float labelWidth, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform row = CreateNode(name, parent, 0f, y, 280f, 26f);
        UiFactory.AddButton(row.gameObject, onClick);

        Image icon = UiFactory.CreateImage("Icon", row, sprites.GetIcon(iconName), TitleColor);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0f, 1f);
        icon.rectTransform.pivot = new Vector2(0f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
        icon.rectTransform.anchoredPosition = new Vector2(5f, -1f);

        TextMeshProUGUI label = CreateText(row, "Label", labelText, 16, BodyBlack, UiTheme.NavMediumFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(labelWidth, 18f);
        label.rectTransform.anchoredPosition = new Vector2(39f, -4f);

        return BuildToggle(row, "Toggle", 221f, 0f);
    }

    private void BuildActionRow(RectTransform parent, string name, float y, string iconName, string labelText, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform row = CreateNode(name, parent, 0f, y, 280f, 24f);
        UiFactory.AddButton(row.gameObject, onClick);

        Image icon = UiFactory.CreateImage("Icon", row, sprites.GetIcon(iconName), TitleColor);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0f, 1f);
        icon.rectTransform.pivot = new Vector2(0f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
        icon.rectTransform.anchoredPosition = new Vector2(5f, 0f);

        TextMeshProUGUI label = CreateText(row, "Label", labelText, 16, BodyBlack, UiTheme.NavMediumFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(206f, 18f);
        label.rectTransform.anchoredPosition = new Vector2(39f, -3f);

        Image chevron = UiFactory.CreateImage("Chevron", row, sprites.GetIcon("icon_nextarrow"), TitleColor);
        chevron.type = Image.Type.Simple;
        chevron.preserveAspect = true;
        chevron.rectTransform.anchorMin = new Vector2(0f, 1f);
        chevron.rectTransform.anchorMax = new Vector2(0f, 1f);
        chevron.rectTransform.pivot = new Vector2(0f, 1f);
        chevron.rectTransform.sizeDelta = new Vector2(12f, 24f);
        chevron.rectTransform.anchoredPosition = new Vector2(255f, 0f);
    }

    private ToggleVisual BuildTextOnlyToggleRow(RectTransform parent, string key, string labelText, float y, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform row = CreateNode("Row_" + key, parent, 0f, y, 280f, 26f);
        UiFactory.AddButton(row.gameObject, onClick);

        TextMeshProUGUI label = CreateText(row, "Label", labelText, 16, BodyBlack, UiTheme.NavMediumFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(207f, 18f);
        label.rectTransform.anchoredPosition = new Vector2(5f, -4f);

        return BuildToggle(row, "Toggle", 222f, 0f);
    }

    private ToggleVisual BuildToggle(RectTransform parent, string name, float x, float y)
    {
        RectTransform toggleRoot = CreateNode(name, parent, x, y, 50f, 26f);

        Image track = toggleRoot.gameObject.AddComponent<Image>();
        track.sprite = UiTheme.RoundedTenSprite;
        track.type = Image.Type.Sliced;
        track.preserveAspect = false;
        track.color = ToggleOffGrey;

        Image shadow = UiFactory.CreateImage("InsetShadow", toggleRoot, GetSettingsToggleShadowSprite(), Color.white);
        shadow.type = Image.Type.Sliced;
        shadow.preserveAspect = false;
        UiFactory.Stretch(shadow.rectTransform, 0f, 0f, 0f, 0f);
        shadow.raycastTarget = false;

        Image knob = UiFactory.CreateImage("Knob", toggleRoot, UiTheme.CircleSprite, UiTheme.White);
        knob.type = Image.Type.Simple;
        knob.preserveAspect = false;
        knob.rectTransform.anchorMin = new Vector2(0f, 1f);
        knob.rectTransform.anchorMax = new Vector2(0f, 1f);
        knob.rectTransform.pivot = new Vector2(0f, 1f);
        knob.rectTransform.sizeDelta = new Vector2(24f, 24f);
        knob.rectTransform.anchoredPosition = new Vector2(1f, -1f);

        Shadow knobShadow = knob.gameObject.AddComponent<Shadow>();
        knobShadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
        knobShadow.effectDistance = new Vector2(0f, -1f);
        knobShadow.useGraphicAlpha = true;

        return new ToggleVisual
        {
            Track = track,
            Shadow = shadow,
            Knob = knob.rectTransform
        };
    }

    private void BuildDivider(RectTransform parent, float y)
    {
        CreateLine(parent, "Divider_" + y, 0f, y, 280f);
    }

    private void CreateLine(RectTransform parent, string name, float x, float y, float width)
    {
        Image line = UiFactory.CreateImage(name, parent, UiTheme.WhiteSprite, DividerColor);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = new Vector2(0f, 1f);
        line.rectTransform.pivot = new Vector2(0f, 1f);
        line.rectTransform.sizeDelta = new Vector2(width, 1f);
        line.rectTransform.anchoredPosition = new Vector2(x, -y);
    }

    private void CreateActionButton(RectTransform parent, string name, string labelText, float x, float y, float width, bool primary, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform buttonRect = CreateNode(name, parent, x, y, width, 24f);

        Color32 fillColor = primary ? CtaBlue : DisabledFill;
        Color32 underlineColor = primary ? CtaBlueDark : DisabledBorder;
        Color32 textColor = primary ? UiTheme.White : DisabledBorder;

        Image fill = buttonRect.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = fillColor;

        UiFactory.AddButton(buttonRect.gameObject, onClick);

        Image underline = UiFactory.CreateImage("Underline", buttonRect, UiTheme.WhiteSprite, underlineColor);
        underline.type = Image.Type.Simple;
        underline.preserveAspect = false;
        underline.rectTransform.anchorMin = new Vector2(0f, 0f);
        underline.rectTransform.anchorMax = new Vector2(1f, 0f);
        underline.rectTransform.pivot = new Vector2(0.5f, 0f);
        underline.rectTransform.offsetMin = new Vector2(0f, 0f);
        underline.rectTransform.offsetMax = new Vector2(0f, 2f);

        TextMeshProUGUI label = CreateText(buttonRect, "Label", labelText, 16, textColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 8f, 0f, 8f, 0f);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Normal, alignment);
        label.font = font;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private void RefreshViewState()
    {
        if (mainPage != null)
        {
            mainPage.gameObject.SetActive(currentPage == SettingsPage.Main);
        }

        if (notificationsPage != null)
        {
            notificationsPage.gameObject.SetActive(currentPage == SettingsPage.Notifications);
        }

        if (languagesPage != null)
        {
            languagesPage.gameObject.SetActive(currentPage == SettingsPage.Languages);
        }

        RefreshAudioVolumeControls();

        foreach (KeyValuePair<string, ToggleVisual> pair in notificationToggleVisuals)
        {
            bool isOn;
            if (notificationStates.TryGetValue(pair.Key, out isOn))
            {
                ApplyToggleState(pair.Value, isOn);
            }
        }

        bool showFeedback = currentModal == SettingsModal.Feedback;
        bool showSuccess = currentModal == SettingsModal.FeedbackSuccess;
        bool showModalBlocker = showFeedback || showSuccess;

        if (modalBlocker != null)
        {
            modalBlocker.gameObject.SetActive(showModalBlocker);
        }

        if (feedbackModal != null)
        {
            feedbackModal.gameObject.SetActive(showFeedback);
        }

        if (feedbackSuccessModal != null)
        {
            feedbackSuccessModal.gameObject.SetActive(showSuccess);
        }

        if (languagesScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            languagesScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void RefreshAudioVolumeControls()
    {
        ApplyVolumeControlState(musicVolumeControl, PawPalAudioSettings.MusicVolume);
        ApplyVolumeControlState(soundEffectsVolumeControl, PawPalAudioSettings.SoundEffectsVolume);
    }

    private void ApplyVolumeControlState(VolumeControlVisual control, float volume)
    {
        if (control == null)
        {
            return;
        }

        float clampedVolume = Mathf.Clamp01(volume);
        int percent = PawPalAudioSettings.ToPercent(clampedVolume);
        if (control.PercentLabel != null)
        {
            control.PercentLabel.text = percent + "%";
        }

        bool canDecrease = percent > 0;
        bool canIncrease = percent < 100;
        ApplyVolumeStepButtonState(control.MinusButton, control.MinusFill, control.MinusLabel, canDecrease);
        ApplyVolumeStepButtonState(control.PlusButton, control.PlusFill, control.PlusLabel, canIncrease);
    }

    private void ApplyVolumeStepButtonState(Button button, Image fill, TextMeshProUGUI label, bool enabled)
    {
        if (button != null)
        {
            button.interactable = enabled;
        }

        if (fill != null)
        {
            fill.color = enabled ? TitleColor : DisabledFill;
        }

        if (label != null)
        {
            label.color = enabled ? UiTheme.White : DisabledBorder;
        }
    }

    private void ApplyToggleState(ToggleVisual toggle, bool isOn)
    {
        toggle.Track.color = isOn ? UiTheme.NavBrand : ToggleOffGrey;
        toggle.Knob.anchoredPosition = new Vector2(isOn ? 25f : 1f, -1f);
    }

    private RectTransform CreateNode(string name, RectTransform parent, float x, float y, float width, float height)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
        return rect;
    }

    private static Sprite GetSettingsToggleShadowSprite()
    {
        if (settingsToggleShadowSprite != null)
        {
            return settingsToggleShadowSprite;
        }

        const int width = 50;
        const int height = 26;
        const float radius = 13f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = "SettingsToggleShadow";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsInsideRoundedRect(x, y, width, height, radius))
                {
                    texture.SetPixel(x, y, new Color32(255, 255, 255, 0));
                    continue;
                }

                float left = x;
                float right = width - 1f - x;
                float top = y;
                float bottom = height - 1f - y;
                float minEdge = Mathf.Min(left, right, top, bottom);
                float alpha = 0f;
                if (minEdge < 4f)
                {
                    alpha = Mathf.Lerp(0.25f, 0f, minEdge / 4f);
                }

                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        texture.Apply();
        settingsToggleShadowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return settingsToggleShadowSprite;
    }

    private static bool IsInsideRoundedRect(int x, int y, int width, int height, float radius)
    {
        float clampedX = Mathf.Clamp(x, radius, width - radius - 1f);
        float clampedY = Mathf.Clamp(y, radius, height - radius - 1f);
        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(clampedX, clampedY));
        return distance <= radius;
    }
}
