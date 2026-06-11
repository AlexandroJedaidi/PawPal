using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileScreenView : AppScreenViewBase
{
    private enum ProfileTab
    {
        Progress,
        Activities,
        Statistics
    }

    private sealed class TabButtonView
    {
        public ProfileTab Tab;
        public Image Fill;
        public Image Border;
        public TextMeshProUGUI Label;
    }

    private sealed class ProgressRowView
    {
        public int Level;
        public Image Dot;
        public TextMeshProUGUI LevelLabel;
        public TextMeshProUGUI HeaderLabel;
        public TextMeshProUGUI DescriptionLabel;
        public TextMeshProUGUI UnlockLabel;
        public TextMeshProUGUI RewardLabel;
    }

    private struct ActivityCardData
    {
        public string Goal;
        public string ProgressText;
        public float ProgressWidth;
        public string XpText;
        public string CurrencyText;
        public Color32 BorderColor;
        public Color32 ShadowColor;
        public Color32 XpFill;
        public Color32 XpTextColor;
        public Color32 CurrencyFill;
        public Color32 CurrencyTextColor;
        public Color32 ProgressLeft;
        public Color32 ProgressRight;
    }

    private struct StatisticRowData
    {
        public string Label;
        public string Value;
    }

    private static readonly Color32 SupportCream = new Color32(236, 223, 200, 255);
    private static readonly Color32 ComingSoonGrey = new Color32(220, 220, 220, 255);
    private static readonly Color32 MutedTextGrey = new Color32(163, 163, 163, 255);
    private static readonly Color32 AgilityGold = new Color32(241, 179, 28, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 ProgressGreen = new Color32(0, 197, 6, 255);
    private static readonly Color32 ProgressGreenLight = new Color32(214, 255, 188, 255);
    private static readonly Color32 ProgressGreenBox = new Color32(201, 255, 203, 255);
    private static readonly Color32 ProgressGreenShadow = new Color32(0, 233, 8, 64);
    private static readonly Color32 ActivityCardFill = new Color32(255, 255, 255, 255);
    private static readonly Color32 TabInactiveFill = new Color32(248, 239, 224, 255);
    private static readonly Color32 TabInactiveBorder = new Color32(212, 191, 164, 255);
    private static readonly Color32 ProgressTrack = new Color32(214, 202, 181, 255);

    private static readonly StatisticRowData[] Statistics =
    {
        new StatisticRowData { Label = "Competitions participated", Value = "10" },
        new StatisticRowData { Label = "Competitions won 1st place", Value = "3" },
        new StatisticRowData { Label = "Competitions won 2nd place", Value = "2" },
        new StatisticRowData { Label = "Competitions won 3rd place", Value = "1" },
        new StatisticRowData { Label = "Walks completed", Value = "12" },
        new StatisticRowData { Label = "Time spent on walks", Value = "1h 12m" },
        new StatisticRowData { Label = "Daily activities completed", Value = "23" },
        new StatisticRowData { Label = "Money earned", Value = "\u20B19,880" },
        new StatisticRowData { Label = "Highest dog club ranking", Value = "#137" }
    };

    private RectTransform exactFrame;
    private RectTransform tabsRoot;
    private RectTransform headerRoot;
    private RectTransform progressPanel;
    private RectTransform activitiesPanel;
    private RectTransform statisticsPanel;
    private RectTransform progressScrollContent;
    private RectTransform progressRowsRoot;
    private RectTransform activitiesSection;
    private RectTransform statisticsSection;
    private ScrollRect progressScrollRect;
    private RectTransform progressViewport;
    private readonly List<TabButtonView> tabButtons = new List<TabButtonView>();
    private readonly List<ProgressRowView> progressRowViews = new List<ProgressRowView>();
    private readonly List<GameObject> activityCardObjects = new List<GameObject>();
    private readonly Dictionary<ProfileTab, GameObject> tabPanels = new Dictionary<ProfileTab, GameObject>();
    private ProfileTab selectedTab = ProfileTab.Activities;
    private TextMeshProUGUI progressHeaderLabel;
    private Image trainerProgressFill;
    private TextMeshProUGUI trainerLevelLabel;
    private TextMeshProUGUI trainerXpLabel;
    private TextMeshProUGUI dailyResetLabel;
    private TextMeshProUGUI emptyActivitiesLabel;
    private string lastActivitySignature = string.Empty;
    private ResponsiveFigmaFrameLayout frameLayout;

    protected override bool UseScreenContainer
    {
        get { return false; }
    }

    private void OnEnable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged += HandleRuntimeStateChanged;
        }

        RefreshRuntimeState();
    }

    private void OnDisable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged -= HandleRuntimeStateChanged;
        }
    }

    protected override void BuildContent()
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        Image fullBackground = root.gameObject.AddComponent<Image>();
        fullBackground.sprite = UiTheme.WhiteSprite;
        fullBackground.type = Image.Type.Simple;
        fullBackground.preserveAspect = false;
        fullBackground.color = UiTheme.NavBackgroundCream;
        fullBackground.raycastTarget = true;

        exactFrame = UiFactory.CreateRect("ProfileMainFrame", root);
        exactFrame.anchorMin = new Vector2(0.5f, 1f);
        exactFrame.anchorMax = new Vector2(0.5f, 1f);
        exactFrame.pivot = new Vector2(0.5f, 1f);
        exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        exactFrame.anchoredPosition = Vector2.zero;

        Image rootBackground = exactFrame.gameObject.AddComponent<Image>();
        rootBackground.sprite = UiTheme.WhiteSprite;
        rootBackground.type = Image.Type.Simple;
        rootBackground.preserveAspect = false;
        rootBackground.color = UiTheme.NavBackgroundCream;

        BuildHeaderSection(exactFrame);
        BuildTabBar(exactFrame);
        BuildPanels(exactFrame);

        ApplyTabSelection(selectedTab);
        RefreshRuntimeState();
    }

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            frameLayout = ResponsiveFigmaFrame.Apply(root, exactFrame, 887f);
        }

        ApplyPanelLayout();
    }

    private void BuildHeaderSection(RectTransform parent)
    {
        headerRoot = UiFactory.CreateRect("Header", parent);
        headerRoot.anchorMin = new Vector2(0f, 1f);
        headerRoot.anchorMax = new Vector2(0f, 1f);
        headerRoot.pivot = new Vector2(0f, 1f);
        headerRoot.sizeDelta = new Vector2(359f, 54f);
        headerRoot.anchoredPosition = new Vector2(10f, -10f);

        Image headerBackground = headerRoot.gameObject.AddComponent<Image>();
        headerBackground.sprite = UiTheme.WhiteSprite;
        headerBackground.type = Image.Type.Simple;
        headerBackground.preserveAspect = false;
        headerBackground.color = UiTheme.NavBackgroundCream;

        TextMeshProUGUI title = UiFactory.CreateLabel("Title", headerRoot, "Profile", 16, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        title.font = UiTheme.NavBoldFont;
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(359f, 22f);
        title.rectTransform.anchoredPosition = Vector2.zero;

        RectTransform trainerFrame = UiFactory.CreateRect("TrainerFrame", headerRoot);
        trainerFrame.anchorMin = new Vector2(0f, 1f);
        trainerFrame.anchorMax = new Vector2(0f, 1f);
        trainerFrame.pivot = new Vector2(0f, 1f);
        trainerFrame.sizeDelta = new Vector2(259f, 24f);
        trainerFrame.anchoredPosition = new Vector2(50f, -24f);

        Image emptyBar = UiFactory.CreateImage("BarEmpty", trainerFrame, UiTheme.ProgressPillSprite, Color.white);
        emptyBar.type = Image.Type.Sliced;
        emptyBar.preserveAspect = false;
        emptyBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        emptyBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        emptyBar.rectTransform.pivot = new Vector2(0f, 1f);
        emptyBar.rectTransform.sizeDelta = new Vector2(249f, 11f);
        emptyBar.rectTransform.anchoredPosition = new Vector2(10f, -5f);

        trainerProgressFill = UiFactory.CreateImage("BarFill", trainerFrame, BuildGradientSprite("ProfileTrainerProgress", 176, 11, UiTheme.NavBrand, Color.white, false), Color.white);
        trainerProgressFill.type = Image.Type.Sliced;
        trainerProgressFill.preserveAspect = false;
        trainerProgressFill.rectTransform.anchorMin = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.pivot = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.sizeDelta = new Vector2(175f, 11f);
        trainerProgressFill.rectTransform.anchoredPosition = new Vector2(10f, -5f);

        Image star = UiFactory.CreateImage("Star", trainerFrame, sprites.GetResourceSprite("UI/Figma/HomeMain/trainer_star"), Color.white);
        star.type = Image.Type.Simple;
        star.preserveAspect = true;
        star.rectTransform.anchorMin = new Vector2(0f, 1f);
        star.rectTransform.anchorMax = new Vector2(0f, 1f);
        star.rectTransform.pivot = new Vector2(0f, 1f);
        star.rectTransform.sizeDelta = new Vector2(31f, 29f);
        star.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        trainerLevelLabel = UiFactory.CreateLabel("LevelLabel", trainerFrame, "5", 13, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        trainerLevelLabel.font = UiTheme.NavBoldFont;
        trainerLevelLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        trainerLevelLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        trainerLevelLabel.rectTransform.pivot = new Vector2(0f, 1f);
        trainerLevelLabel.rectTransform.sizeDelta = new Vector2(11.03f, 22.848f);
        trainerLevelLabel.rectTransform.anchoredPosition = new Vector2(10f, -3f);

        trainerXpLabel = UiFactory.CreateLabel("XpLabel", trainerFrame, "700 / 1000 XP", 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        trainerXpLabel.font = UiTheme.NavBoldFont;
        trainerXpLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        trainerXpLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        trainerXpLabel.rectTransform.pivot = new Vector2(0f, 1f);
        trainerXpLabel.rectTransform.sizeDelta = new Vector2(171f, 18f);
        trainerXpLabel.rectTransform.anchoredPosition = new Vector2(47f, -26f);
    }

    private void BuildTabBar(RectTransform parent)
    {
        tabsRoot = UiFactory.CreateRect("ProfileTabs", parent);
        tabsRoot.anchorMin = new Vector2(0f, 1f);
        tabsRoot.anchorMax = new Vector2(0f, 1f);
        tabsRoot.pivot = new Vector2(0f, 1f);
        tabsRoot.sizeDelta = new Vector2(359f, 36f);
        tabsRoot.anchoredPosition = new Vector2(10f, -80f);

        AddTabButton(tabsRoot, ProfileTab.Progress, "Progress", 0f);
        AddTabButton(tabsRoot, ProfileTab.Activities, "Activities", 123f);
        AddTabButton(tabsRoot, ProfileTab.Statistics, "Statistics", 246f);
    }

    private void AddTabButton(RectTransform parent, ProfileTab tab, string labelText, float x)
    {
        RectTransform button = UiFactory.CreateRect(tab + "Tab", parent);
        button.anchorMin = new Vector2(0f, 1f);
        button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(0f, 1f);
        button.sizeDelta = new Vector2(110f, 30f);
        button.anchoredPosition = new Vector2(x, 0f);

        Image fill = UiFactory.CreateImage("Fill", button, UiTheme.RoundedFiveSprite, TabInactiveFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        UiFactory.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);

        Image border = UiFactory.CreateImage("Border", button, UiTheme.RoundedFiveOutlineSprite, TabInactiveBorder);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", button, labelText, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavBoldFont;
        UiFactory.Stretch(label.rectTransform, 4f, 0f, 4f, 0f);

        UiFactory.AddButton(button.gameObject, delegate
        {
            ApplyTabSelection(tab);
        });

        tabButtons.Add(new TabButtonView
        {
            Tab = tab,
            Fill = fill,
            Border = border,
            Label = label
        });
    }

    private void BuildPanels(RectTransform parent)
    {
        progressPanel = CreatePanel("ProgressPanel", parent);
        BuildProgressPanel(progressPanel);
        tabPanels[ProfileTab.Progress] = progressPanel.gameObject;

        activitiesPanel = CreatePanel("ActivitiesPanel", parent);
        BuildActivitiesSection(activitiesPanel);
        tabPanels[ProfileTab.Activities] = activitiesPanel.gameObject;

        statisticsPanel = CreatePanel("StatisticsPanel", parent);
        BuildStatisticsSection(statisticsPanel);
        tabPanels[ProfileTab.Statistics] = statisticsPanel.gameObject;
    }

    private RectTransform CreatePanel(string name, RectTransform parent)
    {
        RectTransform panel = UiFactory.CreateRect(name, parent);
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(359f, 700f);
        panel.anchoredPosition = new Vector2(10f, -118f);

        Image background = panel.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.WhiteSprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = UiTheme.NavBackgroundCream;

        return panel;
    }

    private void BuildProgressPanel(RectTransform parent)
    {
        progressScrollRect = parent.gameObject.AddComponent<ScrollRect>();
        progressViewport = UiFactory.CreateRect("Viewport", parent);
        UiFactory.Stretch(progressViewport, 0f, 0f, 0f, 0f);
        Image viewportImage = progressViewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.002f);
        viewportImage.raycastTarget = true;
        progressViewport.gameObject.AddComponent<RectMask2D>();

        progressScrollContent = UiFactory.CreateRect("Content", progressViewport);
        progressScrollContent.anchorMin = new Vector2(0f, 1f);
        progressScrollContent.anchorMax = new Vector2(0f, 1f);
        progressScrollContent.pivot = new Vector2(0f, 1f);
        progressScrollContent.sizeDelta = new Vector2(359f, 960f);
        progressScrollContent.anchoredPosition = Vector2.zero;

        progressScrollRect.viewport = progressViewport;
        progressScrollRect.content = progressScrollContent;
        progressScrollRect.horizontal = false;
        progressScrollRect.vertical = true;
        progressScrollRect.movementType = ScrollRect.MovementType.Clamped;
        progressScrollRect.scrollSensitivity = 22f;

        RectTransform levelHeader = CreateNode("Frame_CurrentLevel", progressScrollContent, 0f, 0f, 359f, 75f);
        CreateSectionHeader(levelHeader, "ProgressHeader", "\u201CTrainer name\u201D - Progress", 0f, 0f, 359f, 21f, 14);
        progressHeaderLabel = levelHeader.Find("ProgressHeader/Label").GetComponent<TextMeshProUGUI>();

        RectTransform trainerFrame = CreateNode("Frame_TrainerLevel", levelHeader, 50f, 31f, 259f, 44f);
        Image emptyBar = UiFactory.CreateImage("BarEmpty", trainerFrame, UiTheme.ProgressPillSprite, Color.white);
        emptyBar.type = Image.Type.Sliced;
        emptyBar.preserveAspect = false;
        emptyBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        emptyBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        emptyBar.rectTransform.pivot = new Vector2(0f, 1f);
        emptyBar.rectTransform.sizeDelta = new Vector2(249f, 11f);
        emptyBar.rectTransform.anchoredPosition = new Vector2(10f, -10f);

        trainerProgressFill = UiFactory.CreateImage("BarFill", trainerFrame, BuildGradientSprite("ProfileTrainerProgressFill", 176, 11, UiTheme.NavBrand, Color.white, false), Color.white);
        trainerProgressFill.type = Image.Type.Sliced;
        trainerProgressFill.preserveAspect = false;
        trainerProgressFill.rectTransform.anchorMin = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.pivot = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.sizeDelta = new Vector2(175f, 11f);
        trainerProgressFill.rectTransform.anchoredPosition = new Vector2(10f, -10f);

        Image star = UiFactory.CreateImage("Star", trainerFrame, sprites.GetResourceSprite("UI/Figma/HomeMain/trainer_star"), Color.white);
        star.type = Image.Type.Simple;
        star.preserveAspect = true;
        star.rectTransform.anchorMin = new Vector2(0f, 1f);
        star.rectTransform.anchorMax = new Vector2(0f, 1f);
        star.rectTransform.pivot = new Vector2(0f, 1f);
        star.rectTransform.sizeDelta = new Vector2(31f, 29f);
        star.rectTransform.anchoredPosition = Vector2.zero;

        trainerLevelLabel = UiFactory.CreateLabel("LevelLabel", trainerFrame, "5", 13, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        trainerLevelLabel.font = UiTheme.NavBoldFont;
        trainerLevelLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        trainerLevelLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        trainerLevelLabel.rectTransform.pivot = new Vector2(0f, 1f);
        trainerLevelLabel.rectTransform.sizeDelta = new Vector2(11.03f, 22.848f);
        trainerLevelLabel.rectTransform.anchoredPosition = new Vector2(10f, -3f);

        trainerXpLabel = UiFactory.CreateLabel("XpLabel", trainerFrame, "700 / 1000 XP", 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        trainerXpLabel.font = UiTheme.NavBoldFont;
        trainerXpLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        trainerXpLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        trainerXpLabel.rectTransform.pivot = new Vector2(0f, 1f);
        trainerXpLabel.rectTransform.sizeDelta = new Vector2(171f, 18f);
        trainerXpLabel.rectTransform.anchoredPosition = new Vector2(47f, -26f);

        progressRowsRoot = UiFactory.CreateRect("ProgressRows", progressScrollContent);
        progressRowsRoot.anchorMin = new Vector2(0f, 1f);
        progressRowsRoot.anchorMax = new Vector2(0f, 1f);
        progressRowsRoot.pivot = new Vector2(0f, 1f);
        progressRowsRoot.sizeDelta = new Vector2(359f, 860f);
        progressRowsRoot.anchoredPosition = new Vector2(0f, -86f);

        BuildProgressRows(progressRowsRoot);
    }

    private void BuildProgressRows(RectTransform parent)
    {
        progressRowViews.Clear();
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        IReadOnlyList<TrainerMilestoneDefinition> milestones = runtime != null ? runtime.GetTrainerMilestones() : null;
        if (milestones == null)
        {
            return;
        }

        float y = 0f;
        for (int i = 0; i < milestones.Count; i++)
        {
            TrainerMilestoneDefinition milestone = milestones[i];
            if (milestone == null || milestone.Level <= 1)
            {
                continue;
            }

            progressRowViews.Add(BuildProgressRow(parent, milestone, y));
            y += 102f;
        }

        parent.sizeDelta = new Vector2(parent.sizeDelta.x, Mathf.Max(420f, y));
    }

    private ProgressRowView BuildProgressRow(RectTransform parent, TrainerMilestoneDefinition milestone, float y)
    {
        RectTransform row = CreateNode("Row_" + milestone.Level, parent, 0f, y, 359f, 96f);

        RectTransform timeline = CreateNode("Timeline", row, 18f, 0f, 44f, 96f);
        Image line = UiFactory.CreateImage("Line", timeline, UiTheme.WhiteSprite, ProgressTrack);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        line.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        line.rectTransform.sizeDelta = new Vector2(4f, 96f);
        line.rectTransform.anchoredPosition = Vector2.zero;

        Image dot = UiFactory.CreateImage("Dot", timeline, UiTheme.CircleSprite, UiTheme.NavBrand);
        dot.type = Image.Type.Simple;
        dot.preserveAspect = false;
        dot.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        dot.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        dot.rectTransform.pivot = new Vector2(0.5f, 1f);
        dot.rectTransform.sizeDelta = new Vector2(18f, 18f);
        dot.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        TextMeshProUGUI levelLabel = UiFactory.CreateLabel("Level", dot.rectTransform, milestone.Level.ToString(), 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        levelLabel.font = UiTheme.NavBoldFont;
        UiFactory.Stretch(levelLabel.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform textRoot = CreateNode("TextRoot", row, 64f, 2f, 277f, 42f);
        TextMeshProUGUI header = UiFactory.CreateLabel("Header", textRoot, BuildMilestoneHeader(milestone), 14, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        header.font = UiTheme.NavBoldFont;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.sizeDelta = new Vector2(277f, 18f);
        header.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI description = UiFactory.CreateLabel("Description", textRoot, BuildMilestoneDescription(milestone), 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        description.font = UiTheme.NavRegularFont;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.rectTransform.anchorMin = new Vector2(0f, 1f);
        description.rectTransform.anchorMax = new Vector2(1f, 1f);
        description.rectTransform.pivot = new Vector2(0f, 1f);
        description.rectTransform.sizeDelta = new Vector2(277f, 20f);
        description.rectTransform.anchoredPosition = new Vector2(0f, -18f);

        RectTransform unlockRoot = CreateNode("UnlockRoot", row, 64f, 48f, 277f, 34f);
        TextMeshProUGUI unlockLabel = UiFactory.CreateLabel("UnlockLabel", unlockRoot, "Unlocks", 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        unlockLabel.font = UiTheme.NavBoldFont;
        unlockLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        unlockLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        unlockLabel.rectTransform.pivot = new Vector2(0f, 1f);
        unlockLabel.rectTransform.sizeDelta = new Vector2(277f, 14f);
        unlockLabel.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI reward = UiFactory.CreateLabel("Reward", unlockRoot, BuildMilestoneRewardText(milestone), 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        reward.font = UiTheme.NavRegularFont;
        reward.textWrappingMode = TextWrappingModes.Normal;
        reward.rectTransform.anchorMin = new Vector2(0f, 1f);
        reward.rectTransform.anchorMax = new Vector2(1f, 1f);
        reward.rectTransform.pivot = new Vector2(0f, 1f);
        reward.rectTransform.sizeDelta = new Vector2(277f, 18f);
        reward.rectTransform.anchoredPosition = new Vector2(0f, -14f);

        return new ProgressRowView
        {
            Level = milestone.Level,
            Dot = dot,
            LevelLabel = levelLabel,
            HeaderLabel = header,
            DescriptionLabel = description,
            UnlockLabel = unlockLabel,
            RewardLabel = reward
        };
    }

    private void BuildActivitiesSection(RectTransform parent)
    {
        activitiesSection = CreateNode("Frame_Activities", parent, 0f, 0f, 359f, 238f);
        CreateSectionHeader(activitiesSection, "ActivitiesHeader", "Daily activities", 0f, 0f, 359f, 21f, 14);

        Image timePill = UiFactory.CreateImage("TimePill", activitiesSection, UiTheme.RoundedFiveSprite, UiTheme.NavBrand);
        timePill.type = Image.Type.Sliced;
        timePill.preserveAspect = false;
        timePill.rectTransform.anchorMin = new Vector2(0f, 1f);
        timePill.rectTransform.anchorMax = new Vector2(0f, 1f);
        timePill.rectTransform.pivot = new Vector2(0f, 1f);
        timePill.rectTransform.sizeDelta = new Vector2(90f, 25f);
        timePill.rectTransform.anchoredPosition = new Vector2(269f, -26f);

        Image timeOutline = UiFactory.CreateImage("Outline", timePill.rectTransform, UiTheme.RoundedFiveOutlineSprite, UiTheme.NavBackgroundCream);
        timeOutline.type = Image.Type.Sliced;
        timeOutline.preserveAspect = false;
        UiFactory.Stretch(timeOutline.rectTransform, 0f, 0f, 0f, 0f);

        Image clock = UiFactory.CreateImage("Clock", timePill.rectTransform, sprites.GetIcon("icon_clock"), Color.white);
        clock.type = Image.Type.Simple;
        clock.preserveAspect = true;
        clock.rectTransform.anchorMin = new Vector2(0f, 1f);
        clock.rectTransform.anchorMax = new Vector2(0f, 1f);
        clock.rectTransform.pivot = new Vector2(0f, 1f);
        clock.rectTransform.sizeDelta = new Vector2(18f, 18f);
        clock.rectTransform.anchoredPosition = new Vector2(5f, -3f);

        dailyResetLabel = UiFactory.CreateLabel("TimeText", timePill.rectTransform, "22h 48m", 13, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        dailyResetLabel.font = UiTheme.NavBoldFont;
        dailyResetLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        dailyResetLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        dailyResetLabel.rectTransform.pivot = new Vector2(0f, 1f);
        dailyResetLabel.rectTransform.sizeDelta = new Vector2(57f, 19f);
        dailyResetLabel.rectTransform.anchoredPosition = new Vector2(28f, -3f);

        emptyActivitiesLabel = UiFactory.CreateLabel("EmptyActivities", activitiesSection, "Daily activities will appear here.", 14, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(emptyActivitiesLabel, UiTheme.DefaultFont, 14);
        emptyActivitiesLabel.textWrappingMode = TextWrappingModes.Normal;
        emptyActivitiesLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        emptyActivitiesLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        emptyActivitiesLabel.rectTransform.pivot = new Vector2(0f, 1f);
        emptyActivitiesLabel.rectTransform.sizeDelta = new Vector2(260f, 40f);
        emptyActivitiesLabel.rectTransform.anchoredPosition = new Vector2(49f, -128f);
        emptyActivitiesLabel.gameObject.SetActive(false);

        RebuildActivityCards(PawPalGameRuntime.Instance);
    }

    private void BuildActivityCard(RectTransform parent, ActivityCardData data, float y)
    {
        RectTransform card = CreateNode("Activity_" + data.Goal.Replace(" ", string.Empty), parent, 0f, y, 359f, 54f);
        activityCardObjects.Add(card.gameObject);

        Image fill = UiFactory.CreateImage("Fill", card, UiTheme.RoundedFiveSprite, ActivityCardFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        UiFactory.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);

        Image border = UiFactory.CreateImage("Border", card, UiTheme.RoundedFiveOutlineSprite, data.BorderColor);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = data.ShadowColor;
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        RectTransform row = CreateNode("Row", card, 10f, 10f, 339f, 34f);

        TextMeshProUGUI goal = UiFactory.CreateLabel("Goal", row, data.Goal, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        ConfigureCompactLabel(goal, UiTheme.DefaultFont, 13);
        goal.rectTransform.anchorMin = new Vector2(0f, 1f);
        goal.rectTransform.anchorMax = new Vector2(0f, 1f);
        goal.rectTransform.pivot = new Vector2(0f, 1f);
        goal.rectTransform.sizeDelta = new Vector2(196f, 18f);
        goal.rectTransform.anchoredPosition = Vector2.zero;

        Image barEmpty = UiFactory.CreateImage("BarEmpty", row, UiTheme.ProgressPillSprite, Color.white);
        barEmpty.type = Image.Type.Sliced;
        barEmpty.preserveAspect = false;
        barEmpty.rectTransform.anchorMin = new Vector2(0f, 1f);
        barEmpty.rectTransform.anchorMax = new Vector2(0f, 1f);
        barEmpty.rectTransform.pivot = new Vector2(0f, 1f);
        barEmpty.rectTransform.sizeDelta = new Vector2(196f, 14f);
        barEmpty.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        Image barFill = UiFactory.CreateImage("BarFill", row, BuildGradientSprite("Activity_" + data.Goal.Replace(" ", string.Empty), 196, 14, data.ProgressLeft, data.ProgressRight, true), Color.white);
        barFill.type = Image.Type.Sliced;
        barFill.preserveAspect = false;
        barFill.rectTransform.anchorMin = new Vector2(0f, 1f);
        barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        barFill.rectTransform.pivot = new Vector2(0f, 1f);
        barFill.rectTransform.sizeDelta = new Vector2(data.ProgressWidth, 14f);
        barFill.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        TextMeshProUGUI progress = UiFactory.CreateLabel("Progress", row, data.ProgressText, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(progress, UiTheme.DefaultFont, 13);
        progress.rectTransform.anchorMin = new Vector2(0f, 1f);
        progress.rectTransform.anchorMax = new Vector2(0f, 1f);
        progress.rectTransform.pivot = new Vector2(0f, 1f);
        progress.rectTransform.sizeDelta = new Vector2(196f, 14f);
        progress.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        CreateRewardBadge(row, "XpBadge", data.XpText, 220.5f, 3f, 53f, 28f, data.XpFill, data.XpTextColor, ColorsEqual(data.XpFill, UiTheme.NavBrand) ? UiTheme.NavBrandDark : data.XpFill);
        CreateRewardBadge(row, "CurrencyBadge", data.CurrencyText, 298f, 3f, 41f, 28f, data.CurrencyFill, data.CurrencyTextColor, ColorsEqual(data.CurrencyFill, UiTheme.NavBrand) ? UiTheme.NavBrandDark : data.CurrencyFill);
    }

    private void BuildStatisticsSection(RectTransform parent)
    {
        statisticsSection = CreateNode("Frame_Statistics", parent, 0f, 0f, 359f, 326f);
        CreateSectionHeader(statisticsSection, "StatisticsHeader", "Statistics", 0f, 0f, 359f, 21f, 14);

        float y = 26f;
        for (int i = 0; i < Statistics.Length; i++)
        {
            BuildStatisticRow(statisticsSection, Statistics[i], y);
            y += 26f;
            if (i < Statistics.Length - 1)
            {
                CreateRectFill(statisticsSection, "Divider" + i, SupportCream, 0f, y + 0.0175f, 359f, 1f);
                y += 5f;
            }
        }
    }

    private void BuildStatisticRow(RectTransform parent, StatisticRowData row, float y)
    {
        RectTransform item = CreateNode("Row_" + row.Label.Replace(" ", string.Empty), parent, 0f, y, 359f, 21f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", item, row.Label, 14, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 14);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(260f, 21f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI value = UiFactory.CreateLabel("Value", item, row.Value, 14, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Right);
        ConfigureCompactLabel(value, UiTheme.DefaultFont, 14);
        value.rectTransform.anchorMin = new Vector2(1f, 1f);
        value.rectTransform.anchorMax = new Vector2(1f, 1f);
        value.rectTransform.pivot = new Vector2(1f, 1f);
        value.rectTransform.sizeDelta = new Vector2(90f, 21f);
        value.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void HandleRuntimeStateChanged()
    {
        RefreshRuntimeState();
    }

    private void RefreshRuntimeState()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        TrainerProgressionSnapshot snapshot = runtime.GetTrainerProgressionSnapshot();
        if (progressHeaderLabel != null)
        {
            progressHeaderLabel.text = "\u201C" + runtime.TrainerState.TrainerName + "\u201D - Progress";
        }

        if (trainerProgressFill != null)
        {
            trainerProgressFill.rectTransform.sizeDelta = new Vector2(249f * snapshot.LevelProgress01, 11f);
        }

        if (trainerLevelLabel != null)
        {
            trainerLevelLabel.text = snapshot.Level.ToString();
        }

        if (trainerXpLabel != null)
        {
            trainerXpLabel.text = runtime.GetTrainerExperienceText();
        }

        if (dailyResetLabel != null)
        {
            dailyResetLabel.text = runtime.GetDailyResetCountdownText();
        }

        RefreshProgressRows(runtime, snapshot);
        RebuildActivityCards(runtime);
        ApplyTabSelection(selectedTab);
    }

    private void RefreshProgressRows(PawPalGameRuntime runtime, TrainerProgressionSnapshot snapshot)
    {
        if (runtime == null)
        {
            return;
        }

        IReadOnlyList<TrainerMilestoneDefinition> milestones = runtime.GetTrainerMilestones();
        for (int i = 0; i < progressRowViews.Count; i++)
        {
            ProgressRowView row = progressRowViews[i];
            if (row == null || row.Level <= 0 || row.Level > milestones.Count)
            {
                continue;
            }

            TrainerMilestoneDefinition milestone = milestones[row.Level - 1];
            bool reached = row.Level <= snapshot.Level;

            if (row.Dot != null)
            {
                row.Dot.color = reached ? UiTheme.NavBrand : ProgressTrack;
            }

            if (row.LevelLabel != null)
            {
                row.LevelLabel.color = reached ? Color.white : UiTheme.NavBrandDark;
            }

            if (row.HeaderLabel != null)
            {
                row.HeaderLabel.text = BuildMilestoneHeader(milestone);
            }

            if (row.DescriptionLabel != null)
            {
                row.DescriptionLabel.text = BuildMilestoneDescription(milestone);
            }

            if (row.UnlockLabel != null)
            {
                row.UnlockLabel.text = reached ? "Unlocked" : "Unlocks";
            }

            if (row.RewardLabel != null)
            {
                row.RewardLabel.text = BuildMilestoneRewardText(milestone);
                row.RewardLabel.color = reached ? UiTheme.NavBrandDark : MutedTextGrey;
            }
        }
    }

    private void RebuildActivityCards(PawPalGameRuntime runtime)
    {
        if (activitiesSection == null)
        {
            return;
        }

        if (runtime == null)
        {
            if (emptyActivitiesLabel != null)
            {
                emptyActivitiesLabel.gameObject.SetActive(true);
            }

            return;
        }

        string activitySignature = BuildActivitySignature(runtime);
        if (activitySignature == lastActivitySignature)
        {
            return;
        }

        lastActivitySignature = activitySignature;

        for (int i = 0; i < activityCardObjects.Count; i++)
        {
            if (activityCardObjects[i] != null)
            {
                Destroy(activityCardObjects[i]);
            }
        }

        activityCardObjects.Clear();

        if (emptyActivitiesLabel != null)
        {
            emptyActivitiesLabel.gameObject.SetActive(runtime.DailyTasks.Count == 0);
        }

        float[] yPositions = { 56f, 120f, 184f };
        for (int i = 0; i < runtime.DailyTasks.Count && i < yPositions.Length; i++)
        {
            BuildActivityCard(activitiesSection, BuildActivityCardData(runtime.DailyTasks[i]), yPositions[i]);
        }
    }

    private static string BuildActivitySignature(PawPalGameRuntime runtime)
    {
        StringBuilder builder = new StringBuilder();
        IList<PawPalDailyTaskState> tasks = runtime.DailyTasks;
        builder.Append(tasks.Count);
        for (int i = 0; i < tasks.Count; i++)
        {
            PawPalDailyTaskState task = tasks[i];
            builder.Append('|');
            builder.Append(task.Id);
            builder.Append(':');
            builder.Append(task.Progress);
            builder.Append('/');
            builder.Append(task.RequiredCount);
            builder.Append(':');
            builder.Append(task.RewardGranted ? '1' : '0');
        }

        return builder.ToString();
    }

    private ActivityCardData BuildActivityCardData(PawPalDailyTaskState task)
    {
        bool completed = task.RewardGranted;
        float progress01 = task.RequiredCount <= 0 ? 0f : Mathf.Clamp01((float)task.Progress / task.RequiredCount);
        return new ActivityCardData
        {
            Goal = task.Title + " x" + task.RequiredCount,
            ProgressText = task.Progress + "/" + task.RequiredCount,
            ProgressWidth = 196f * progress01,
            XpText = task.ExperienceReward + " XP",
            CurrencyText = "$" + task.BasicCurrencyReward,
            BorderColor = completed ? ProgressGreen : UiTheme.NavBrand,
            ShadowColor = completed ? ProgressGreenShadow : SupportCream,
            XpFill = completed ? ProgressGreenBox : ComingSoonGrey,
            XpTextColor = UiTheme.NavBrandDark,
            CurrencyFill = completed ? ProgressGreenBox : ComingSoonGrey,
            CurrencyTextColor = UiTheme.NavBrandDark,
            ProgressLeft = completed ? ProgressGreenLight : SupportCream,
            ProgressRight = completed ? ProgressGreen : UiTheme.NavBrand
        };
    }

    private void CreateRewardBadge(RectTransform parent, string name, string text, float x, float y, float width, float height, Color32 fill, Color32 textColor, Color32 shadowColor)
    {
        Image badge = UiFactory.CreateImage(name, parent, UiTheme.RoundedFiveSprite, fill);
        badge.type = Image.Type.Sliced;
        badge.preserveAspect = false;
        badge.rectTransform.anchorMin = new Vector2(0f, 1f);
        badge.rectTransform.anchorMax = new Vector2(0f, 1f);
        badge.rectTransform.pivot = new Vector2(0f, 1f);
        badge.rectTransform.sizeDelta = new Vector2(width, height);
        badge.rectTransform.anchoredPosition = new Vector2(x, -y);

        if (ColorsEqual(fill, UiTheme.NavBrand))
        {
            Shadow shadow = badge.gameObject.AddComponent<Shadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = new Vector2(0f, -1f);
            shadow.useGraphicAlpha = true;
        }

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", badge.rectTransform, text, 13, textColor, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 13);
        UiFactory.Stretch(label.rectTransform, 4f, 5f, 4f, 5f);
    }

    private void ApplyTabSelection(ProfileTab tab)
    {
        selectedTab = tab;

        foreach (KeyValuePair<ProfileTab, GameObject> panel in tabPanels)
        {
            if (panel.Value != null)
            {
                panel.Value.SetActive(panel.Key == tab);
            }
        }

        for (int i = 0; i < tabButtons.Count; i++)
        {
            TabButtonView button = tabButtons[i];
            if (button == null)
            {
                continue;
            }

            bool selected = button.Tab == tab;
            if (button.Fill != null)
            {
                button.Fill.color = selected ? UiTheme.NavBrand : TabInactiveFill;
            }

            if (button.Border != null)
            {
                button.Border.color = selected ? UiTheme.NavBrandDark : TabInactiveBorder;
            }

            if (button.Label != null)
            {
                button.Label.color = selected ? Color.white : UiTheme.NavBrandDark;
                button.Label.font = selected ? UiTheme.NavExtraBoldFont : UiTheme.NavBoldFont;
            }
        }
    }

    private void ApplyPanelLayout()
    {
        float visibleHeight = frameLayout.VisibleLogicalHeight > 0f ? frameLayout.VisibleLogicalHeight : 887f;
        if (tabsRoot != null)
        {
            tabsRoot.anchoredPosition = new Vector2(10f, -80f);
        }

        if (progressPanel != null)
        {
            progressPanel.sizeDelta = new Vector2(359f, Mathf.Max(visibleHeight - 126f, 520f));
        }

        if (activitiesPanel != null)
        {
            activitiesPanel.sizeDelta = new Vector2(359f, Mathf.Max(visibleHeight - 126f, 520f));
        }

        if (statisticsPanel != null)
        {
            statisticsPanel.sizeDelta = new Vector2(359f, Mathf.Max(visibleHeight - 126f, 520f));
        }

        if (progressScrollRect != null && progressScrollContent != null)
        {
            progressScrollRect.viewport = progressViewport;
            progressScrollRect.content = progressScrollContent;
        }

        if (progressRowsRoot != null)
        {
            progressRowsRoot.sizeDelta = new Vector2(359f, Mathf.Max(progressRowsRoot.sizeDelta.y, 860f));
        }
    }

    private void CreateSectionHeader(RectTransform parent, string name, string text, float x, float y, float width, float height, int fontSize)
    {
        Image header = UiFactory.CreateImage(name, parent, UiTheme.RoundedFiveSprite, UiTheme.NavBrand);
        header.type = Image.Type.Sliced;
        header.preserveAspect = false;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(0f, 1f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.sizeDelta = new Vector2(width, height);
        header.rectTransform.anchoredPosition = new Vector2(x, -y);

        Shadow shadow = header.gameObject.AddComponent<Shadow>();
        shadow.effectColor = UiTheme.NavBrandDark;
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", header.rectTransform, text, fontSize, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, fontSize);
        UiFactory.Stretch(label.rectTransform, 5f, 0f, 5f, 0f);
    }

    private void ConfigureCompactLabel(TextMeshProUGUI label, TMP_FontAsset font, float fontSize)
    {
        if (label == null)
        {
            return;
        }

        label.font = font;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.isTextObjectScaleStatic = true;
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

    private static void CreateRectFill(RectTransform parent, string name, Color color, float x, float y, float width, float height)
    {
        Image image = UiFactory.CreateImage(name, parent, UiTheme.WhiteSprite, color);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.rectTransform.anchorMin = new Vector2(0f, 1f);
        image.rectTransform.anchorMax = new Vector2(0f, 1f);
        image.rectTransform.pivot = new Vector2(0f, 1f);
        image.rectTransform.sizeDelta = new Vector2(width, height);
        image.rectTransform.anchoredPosition = new Vector2(x, -y);
    }

    private static Sprite BuildGradientSprite(string name, int width, int height, Color32 startColor, Color32 endColor, bool vertical)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float radius = height * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsInsideRoundedRect(x, y, width, height, radius))
                {
                    texture.SetPixel(x, y, new Color32(255, 255, 255, 0));
                    continue;
                }

                float t = vertical
                    ? (height <= 1 ? 0f : (float)y / (height - 1f))
                    : (width <= 1 ? 0f : (float)x / (width - 1f));
                texture.SetPixel(x, y, Color.Lerp(startColor, endColor, t));
            }
        }

        texture.Apply();
        Vector4 borders = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, borders);
    }

    private static bool IsInsideRoundedRect(int x, int y, int width, int height, float radius)
    {
        float clampedX = Mathf.Clamp(x, radius, width - radius - 1f);
        float clampedY = Mathf.Clamp(y, radius, height - radius - 1f);
        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(clampedX, clampedY));
        return distance <= radius;
    }

    private static string BuildMilestoneHeader(TrainerMilestoneDefinition milestone)
    {
        if (milestone == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(milestone.RewardLabel) ? "Level " + milestone.Level : milestone.RewardLabel;
    }

    private static string BuildMilestoneDescription(TrainerMilestoneDefinition milestone)
    {
        if (milestone == null)
        {
            return string.Empty;
        }

        switch (milestone.RewardKind)
        {
            case TrainerMilestoneRewardKind.CurrencyBasic:
                return "Earn basic currency as you progress.";
            case TrainerMilestoneRewardKind.CurrencyPremium:
                return "Unlock premium currency for special purchases.";
            case TrainerMilestoneRewardKind.PremiumFood:
                return "Gain access to premium food support.";
            case TrainerMilestoneRewardKind.DogUnlock:
                return "Unlock a new dog for your home roster.";
            case TrainerMilestoneRewardKind.Feature:
                return "Unlock a new profile feature.";
            case TrainerMilestoneRewardKind.CatalogItemReward:
                return "Unlock a catalog reward tied to this level.";
            default:
                return "Continue training to unlock the next reward.";
        }
    }

    private static string BuildMilestoneRewardText(TrainerMilestoneDefinition milestone)
    {
        return milestone != null ? TrainerProgressionDatabase.GetMilestoneShortLabel(milestone) : string.Empty;
    }

    private static Color GetMilestoneAccentColor(TrainerMilestoneDefinition milestone)
    {
        if (milestone == null)
        {
            return UiTheme.NavBrandDark;
        }

        switch (milestone.RewardKind)
        {
            case TrainerMilestoneRewardKind.Feature:
            case TrainerMilestoneRewardKind.DogUnlock:
                return AgilityGold;
            default:
                return UiTheme.NavBrandDark;
        }
    }

    private static bool ColorsEqual(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
