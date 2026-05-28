using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileScreenView : AppScreenViewBase
{
    private struct MilestonePill
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public string Text;
        public Color32 BorderColor;
        public Color32 TextColor;
    }

    private struct MilestoneIcon
    {
        public float X;
        public float Y;
        public float Size;
        public float IconSize;
        public string IconName;
        public float IconInset;
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
    private static readonly Color32 AgilityGold = new Color32(241, 179, 28, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 ProgressGreen = new Color32(0, 197, 6, 255);
    private static readonly Color32 ProgressGreenLight = new Color32(214, 255, 188, 255);
    private static readonly Color32 ProgressGreenBox = new Color32(201, 255, 203, 255);
    private static readonly Color32 ProgressGreenShadow = new Color32(0, 233, 8, 64);
    private static readonly Color32 ActivityCardFill = new Color32(255, 255, 255, 255);

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
    private TextMeshProUGUI progressHeaderLabel;
    private Image trainerProgressFill;
    private TextMeshProUGUI trainerLevelLabel;
    private TextMeshProUGUI trainerXpLabel;
    private RectTransform activitiesSection;
    private TextMeshProUGUI dailyResetLabel;
    private TextMeshProUGUI emptyActivitiesLabel;
    private readonly List<GameObject> activityCardObjects = new List<GameObject>();

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

        RectTransform overview = UiFactory.CreateRect("Overview", exactFrame);
        overview.anchorMin = new Vector2(0f, 1f);
        overview.anchorMax = new Vector2(0f, 1f);
        overview.pivot = new Vector2(0f, 1f);
        overview.sizeDelta = new Vector2(379f, 786f);
        overview.anchoredPosition = new Vector2(7f, 0f);

        Image overviewImage = overview.gameObject.AddComponent<Image>();
        overviewImage.sprite = UiTheme.ProfileOverviewSprite;
        overviewImage.type = Image.Type.Simple;
        overviewImage.preserveAspect = false;
        overviewImage.color = UiTheme.NavBackgroundCream;

        BuildLevelSection(overview);
        BuildActivitiesSection(overview);
        BuildStatisticsSection(overview);
        RefreshRuntimeState();
    }

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            exactFrame.anchorMin = new Vector2(0.5f, 1f);
            exactFrame.anchorMax = new Vector2(0.5f, 1f);
            exactFrame.pivot = new Vector2(0.5f, 1f);
            exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
            exactFrame.anchoredPosition = Vector2.zero;
        }
    }

    private void BuildLevelSection(RectTransform overview)
    {
        RectTransform levelSection = CreateNode("Frame_Level", overview, 10f, 50f, 359f, 183f);
        BuildCurrentLevel(levelSection);
        BuildOfferwall(levelSection);
    }

    private void BuildCurrentLevel(RectTransform parent)
    {
        RectTransform section = CreateNode("Frame_CurrentLevel", parent, 0f, 0f, 359f, 75f);
        CreateSectionHeader(section, "ProgressHeader", "\u201CTrainer name\u201D - Progress", 0f, 0f, 359f, 21f, 14);
        progressHeaderLabel = section.Find("ProgressHeader/Label").GetComponent<TextMeshProUGUI>();

        RectTransform trainerFrame = CreateNode("Frame_TrainerLevel", section, 50f, 31f, 259f, 44f);

        Image emptyBar = UiFactory.CreateImage("BarEmpty", trainerFrame, UiTheme.ProgressPillSprite, Color.white);
        emptyBar.type = Image.Type.Sliced;
        emptyBar.preserveAspect = false;
        emptyBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        emptyBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        emptyBar.rectTransform.pivot = new Vector2(0f, 1f);
        emptyBar.rectTransform.sizeDelta = new Vector2(249f, 11f);
        emptyBar.rectTransform.anchoredPosition = new Vector2(10f, -10f);
        Shadow emptyShadow = emptyBar.gameObject.AddComponent<Shadow>();
        emptyShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        emptyShadow.effectDistance = new Vector2(0f, -2f);
        emptyShadow.useGraphicAlpha = true;

        trainerProgressFill = UiFactory.CreateImage(
            "BarFill",
            trainerFrame,
            BuildGradientSprite("ProfileTrainerProgress", 176, 11, UiTheme.NavBrand, Color.white, false),
            Color.white);
        trainerProgressFill.type = Image.Type.Sliced;
        trainerProgressFill.preserveAspect = false;
        trainerProgressFill.rectTransform.anchorMin = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.pivot = new Vector2(0f, 1f);
        trainerProgressFill.rectTransform.sizeDelta = new Vector2(175.022f, 11f);
        trainerProgressFill.rectTransform.anchoredPosition = new Vector2(10f, -10f);

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

    private void BuildOfferwall(RectTransform parent)
    {
        RectTransform frame = CreateNode("Frame_Offerwall", parent, 0f, 85f, 359f, 98f);
        RectTransform viewport = UiFactory.CreateRect("Viewport", frame);
        UiFactory.Stretch(viewport, 5f, 5f, 5f, 5f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.002f);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UiFactory.CreateRect("Group_Milestones", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(1542f, 88f);
        content.anchoredPosition = Vector2.zero;

        ScrollRect scroll = frame.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.scrollSensitivity = 20f;
        Canvas.ForceUpdateCanvases();
        scroll.horizontalNormalizedPosition = 0f;

        BuildOfferwallTicks(content);
        BuildOfferwallProgress(content);
        BuildOfferwallLevels(content);
        BuildOfferwallRewards(content);
    }

    private void BuildOfferwallTicks(RectTransform parent)
    {
        for (int i = 0; i < 25; i++)
        {
            CreateRectFill(parent, "BottomTick" + i, ComingSoonGrey, 46f + (62f * i), 61f, 1f, 13f);
        }

        CreateRectFill(parent, "TopTick0", ComingSoonGrey, 77f, 30f, 1f, 10f);
        for (int i = 1; i < 24; i++)
        {
            CreateRectFill(parent, "TopTick" + i, ComingSoonGrey, 77f + (62f * i), 27f, 1f, 13f);
        }
    }

    private void BuildOfferwallProgress(RectTransform parent)
    {
        CreateRoundedBar(parent, "ProgressBase", UiTheme.NavShadow, 166f, 52f, 1366f, 3f);
        CreateRoundedBar(parent, "ProgressFill", UiTheme.NavBrand, 136f, 52f, 129f, 3f);

        Image progressBlend = UiFactory.CreateImage(
            "ProgressBlend",
            parent,
            BuildOfferwallBlendSprite("ProfileOfferwallBlend", 24, 3),
            Color.white);
        progressBlend.type = Image.Type.Simple;
        progressBlend.preserveAspect = false;
        progressBlend.rectTransform.anchorMin = new Vector2(0f, 1f);
        progressBlend.rectTransform.anchorMax = new Vector2(0f, 1f);
        progressBlend.rectTransform.pivot = new Vector2(0f, 1f);
        progressBlend.rectTransform.sizeDelta = new Vector2(24f, 3f);
        progressBlend.rectTransform.anchoredPosition = new Vector2(143f, -52f);
    }

    private void BuildOfferwallLevels(RectTransform parent)
    {
        for (int level = 1; level <= 50; level++)
        {
            float x = 5f + (31f * (level - 1));
            bool unlocked = level <= 5;
            BuildOfferwallLevelNode(parent, x, 40f, level.ToString(), unlocked);
        }
    }

    private void BuildOfferwallLevelNode(RectTransform parent, float x, float y, string text, bool unlocked)
    {
        Image circle = UiFactory.CreateImage("Level_" + text, parent, UiTheme.CircleSprite, unlocked ? UiTheme.NavBrand : UiTheme.NavShadow);
        circle.type = Image.Type.Simple;
        circle.preserveAspect = false;
        circle.rectTransform.anchorMin = new Vector2(0f, 1f);
        circle.rectTransform.anchorMax = new Vector2(0f, 1f);
        circle.rectTransform.pivot = new Vector2(0f, 1f);
        circle.rectTransform.sizeDelta = new Vector2(21f, 21f);
        circle.rectTransform.anchoredPosition = new Vector2(x, -y);

        Image outline = UiFactory.CreateImage("Outline", circle.rectTransform, UiTheme.CircleOutlineSprite, SupportCream);
        outline.type = Image.Type.Simple;
        outline.preserveAspect = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", circle.rectTransform, text, 13, unlocked ? UiTheme.White : Color.black, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavBoldFont;
        UiFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void BuildOfferwallRewards(RectTransform parent)
    {
        List<MilestoneIcon> icons = new List<MilestoneIcon>
        {
            new MilestoneIcon { X = 65f, Y = 5f, Size = 25f, IconSize = 23f, IconName = "icon_collar_brand", IconInset = 1f },
            new MilestoneIcon { X = 220f, Y = 68f, Size = 25f, IconSize = 21f, IconName = "icon_toys_brand", IconInset = 2f },
            new MilestoneIcon { X = 375f, Y = 5f, Size = 25f, IconSize = 23f, IconName = "icon_collar_brand", IconInset = 1f },
            new MilestoneIcon { X = 406f, Y = 68f, Size = 25f, IconSize = 17f, IconName = "icon_foodsupply_brand", IconInset = 4f },
            new MilestoneIcon { X = 530f, Y = 68f, Size = 25f, IconSize = 17f, IconName = "icon_clothing_brand", IconInset = 4f },
            new MilestoneIcon { X = 623f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_foodsupply_brand", IconInset = 4f },
            new MilestoneIcon { X = 716f, Y = 68f, Size = 25f, IconSize = 21f, IconName = "icon_toys_brand", IconInset = 2f },
            new MilestoneIcon { X = 809f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_clothing_brand", IconInset = 4f },
            new MilestoneIcon { X = 871f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_foodsupply_brand", IconInset = 4f },
            new MilestoneIcon { X = 964f, Y = 68f, Size = 25f, IconSize = 23f, IconName = "icon_collar_brand", IconInset = 1f },
            new MilestoneIcon { X = 995f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_foodsupply_brand", IconInset = 4f },
            new MilestoneIcon { X = 1057f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_dogbed_brand", IconInset = 4f },
            new MilestoneIcon { X = 1119f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_foodsupply_brand", IconInset = 4f },
            new MilestoneIcon { X = 1181f, Y = 5f, Size = 25f, IconSize = 19f, IconName = "icon_toys_brand", IconInset = 3f },
            new MilestoneIcon { X = 1274f, Y = 68f, Size = 25f, IconSize = 17f, IconName = "icon_clothing_brand", IconInset = 4f },
            new MilestoneIcon { X = 1305f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_foodsupply_brand", IconInset = 4f },
            new MilestoneIcon { X = 1367f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_dog_brand", IconInset = 4f },
            new MilestoneIcon { X = 1491f, Y = 5f, Size = 25f, IconSize = 17f, IconName = "icon_foodsupply_brand", IconInset = 4f },
            new MilestoneIcon { X = 1522f, Y = 68f, Size = 25f, IconSize = 17f, IconName = "icon_dog_brand", IconInset = 4f }
        };

        foreach (MilestoneIcon icon in icons)
        {
            BuildOfferwallRewardIcon(parent, icon);
        }

        BuildBluePawReward(parent);

        List<MilestonePill> pills = new List<MilestonePill>
        {
            new MilestonePill { X = 118f, Y = 7f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 148f, Y = 70f, Width = 47f, Height = 20f, Text = "2. dog", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 26f, Y = 70f, Width = 42f, Height = 20f, Text = "Clubs", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 272f, Y = 70f, Width = 47f, Height = 20f, Text = "3. dog", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 582f, Y = 70f, Width = 47f, Height = 20f, Text = "4. dog", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 737f, Y = 7f, Width = 47f, Height = 20f, Text = "5. dog", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 176f, Y = 7f, Width = 50f, Height = 20f, Text = "Agility", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 303f, Y = 7f, Width = 44f, Height = 20f, Text = "Disc", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 492f, Y = 7f, Width = 40f, Height = 20f, Text = "Style", BorderColor = AgilityGold, TextColor = AgilityGold },
            new MilestonePill { X = 242f, Y = 7f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 335f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 459f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 552f, Y = 7f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 676f, Y = 7f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 769f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 831f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 924f, Y = 7f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 1016f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 1079f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 1141f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 1233f, Y = 7f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 1327f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 1388f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark },
            new MilestonePill { X = 1451f, Y = 70f, Width = 40f, Height = 21f, Text = "\u20B1200", BorderColor = UiTheme.NavBrandDark, TextColor = UiTheme.NavBrandDark }
        };

        foreach (MilestonePill pill in pills)
        {
            BuildMilestonePill(parent, pill);
        }
    }

    private void BuildOfferwallRewardIcon(RectTransform parent, MilestoneIcon reward)
    {
        Image bubble = UiFactory.CreateImage("Reward_" + reward.IconName, parent, UiTheme.CircleSprite, UiTheme.White);
        bubble.type = Image.Type.Simple;
        bubble.preserveAspect = false;
        bubble.rectTransform.anchorMin = new Vector2(0f, 1f);
        bubble.rectTransform.anchorMax = new Vector2(0f, 1f);
        bubble.rectTransform.pivot = new Vector2(0f, 1f);
        bubble.rectTransform.sizeDelta = new Vector2(reward.Size, reward.Size);
        bubble.rectTransform.anchoredPosition = new Vector2(reward.X, -reward.Y);

        Image outline = UiFactory.CreateImage("Outline", bubble.rectTransform, UiTheme.CircleOutlineSprite, UiTheme.NavBrand);
        outline.type = Image.Type.Simple;
        outline.preserveAspect = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        Image icon = UiFactory.CreateImage("Icon", bubble.rectTransform, sprites.GetIcon(reward.IconName), Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0f, 1f);
        icon.rectTransform.pivot = new Vector2(0f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(reward.IconSize, reward.IconSize);
        icon.rectTransform.anchoredPosition = new Vector2(reward.IconInset, -reward.IconInset);
    }

    private void BuildBluePawReward(RectTransform parent)
    {
        RectTransform group = CreateNode("BluePawReward", parent, 86f, 69f, 45f, 22f);

        Image bubble = UiFactory.CreateImage("Bubble", group, UiTheme.CircleSprite, CtaBlue);
        bubble.type = Image.Type.Simple;
        bubble.preserveAspect = false;
        bubble.rectTransform.anchorMin = new Vector2(0f, 1f);
        bubble.rectTransform.anchorMax = new Vector2(0f, 1f);
        bubble.rectTransform.pivot = new Vector2(0f, 1f);
        bubble.rectTransform.sizeDelta = new Vector2(22f, 22f);
        bubble.rectTransform.anchoredPosition = Vector2.zero;

        Image paw = UiFactory.CreateImage("Paw", bubble.rectTransform, sprites.GetIcon("icon_paw_white"), Color.white);
        paw.type = Image.Type.Simple;
        paw.preserveAspect = true;
        paw.rectTransform.anchorMin = new Vector2(0f, 1f);
        paw.rectTransform.anchorMax = new Vector2(0f, 1f);
        paw.rectTransform.pivot = new Vector2(0f, 1f);
        paw.rectTransform.sizeDelta = new Vector2(19f, 19f);
        paw.rectTransform.anchoredPosition = new Vector2(1.5f, -1.5f);

        TextMeshProUGUI value = UiFactory.CreateLabel("Value", group, "50", 13, CtaBlue, FontStyles.Normal, TextAlignmentOptions.Left);
        ConfigureCompactLabel(value, UiTheme.DefaultFont, 13);
        value.rectTransform.anchorMin = new Vector2(0f, 1f);
        value.rectTransform.anchorMax = new Vector2(0f, 1f);
        value.rectTransform.pivot = new Vector2(0f, 1f);
        value.rectTransform.sizeDelta = new Vector2(23f, 21f);
        value.rectTransform.anchoredPosition = new Vector2(24.5f, -0.5f);
    }

    private void BuildMilestonePill(RectTransform parent, MilestonePill pill)
    {
        Image background = UiFactory.CreateImage("Pill_" + pill.Text, parent, UiTheme.RoundedTenSprite, UiTheme.White);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.rectTransform.anchorMin = new Vector2(0f, 1f);
        background.rectTransform.anchorMax = new Vector2(0f, 1f);
        background.rectTransform.pivot = new Vector2(0f, 1f);
        background.rectTransform.sizeDelta = new Vector2(pill.Width, pill.Height);
        background.rectTransform.anchoredPosition = new Vector2(pill.X, -pill.Y);

        Image outline = UiFactory.CreateImage("Outline", background.rectTransform, UiTheme.RoundedTenOutlineSprite, pill.BorderColor);
        outline.type = Image.Type.Sliced;
        outline.preserveAspect = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", background.rectTransform, pill.Text, 13, pill.TextColor, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 13);
        UiFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void BuildActivitiesSection(RectTransform overview)
    {
        activitiesSection = CreateNode("Frame_Activities", overview, 10f, 273f, 359f, 238f);
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

        RebuildActivityCards();
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
        Shadow emptyShadow = barEmpty.gameObject.AddComponent<Shadow>();
        emptyShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        emptyShadow.effectDistance = new Vector2(0f, -1f);
        emptyShadow.useGraphicAlpha = true;

        Image barFill = UiFactory.CreateImage(
            "BarFill",
            row,
            BuildGradientSprite("Activity_" + data.Goal.Replace(" ", string.Empty), 196, 14, data.ProgressLeft, data.ProgressRight, true),
            Color.white);
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

        if (progressHeaderLabel != null)
        {
            progressHeaderLabel.text = "\u201C" + runtime.TrainerState.TrainerName + "\u201D - Progress";
        }

        if (trainerProgressFill != null)
        {
            trainerProgressFill.rectTransform.sizeDelta = new Vector2(249f * runtime.GetTrainerLevelProgress01(), 11f);
        }

        if (trainerLevelLabel != null)
        {
            trainerLevelLabel.text = runtime.TrainerState.Level.ToString();
        }

        if (trainerXpLabel != null)
        {
            trainerXpLabel.text = runtime.GetTrainerExperienceText();
        }

        if (dailyResetLabel != null)
        {
            dailyResetLabel.text = runtime.GetDailyResetCountdownText();
        }

        RebuildActivityCards();
    }

    private void RebuildActivityCards()
    {
        if (activitiesSection == null)
        {
            return;
        }

        for (int i = 0; i < activityCardObjects.Count; i++)
        {
            if (activityCardObjects[i] != null)
            {
                Destroy(activityCardObjects[i]);
            }
        }

        activityCardObjects.Clear();

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            if (emptyActivitiesLabel != null)
            {
                emptyActivitiesLabel.gameObject.SetActive(true);
            }
            return;
        }

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

    private void BuildStatisticsSection(RectTransform overview)
    {
        RectTransform section = CreateNode("Frame_Statistics", overview, 10f, 551f, 359f, 326f);
        CreateSectionHeader(section, "StatisticsHeader", "Statistics", 0f, 0f, 359f, 21f, 14);

        float y = 26f;
        for (int i = 0; i < Statistics.Length; i++)
        {
            BuildStatisticRow(section, Statistics[i], y);
            y += 26f;
            if (i < Statistics.Length - 1)
            {
                CreateRectFill(section, "Divider" + i, SupportCream, 0f, y + 0.0175f, 359f, 1f);
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

    private static void ConfigureCompactLabel(TextMeshProUGUI label, TMP_FontAsset font, float fontSize)
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

    private static void CreateRoundedBar(RectTransform parent, string name, Color color, float x, float y, float width, float height)
    {
        Image bar = UiFactory.CreateImage(name, parent, UiTheme.ProgressPillSprite, color);
        bar.type = Image.Type.Sliced;
        bar.preserveAspect = false;
        bar.rectTransform.anchorMin = new Vector2(0f, 1f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 1f);
        bar.rectTransform.sizeDelta = new Vector2(width, height);
        bar.rectTransform.anchoredPosition = new Vector2(x, -y);
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

    private static Sprite BuildOfferwallBlendSprite(string name, int width, int height)
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

                float t = width <= 1 ? 0f : (float)x / (width - 1f);
                Color pixel;
                if (t <= 0.19f)
                {
                    pixel = UiTheme.NavBrand;
                }
                else if (t >= 0.20f)
                {
                    pixel = UiTheme.NavShadow;
                }
                else
                {
                    float blendT = Mathf.InverseLerp(0.19f, 0.20f, t);
                    pixel = Color.Lerp(UiTheme.NavBrand, UiTheme.NavShadow, blendT);
                }

                texture.SetPixel(x, y, pixel);
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

    private static bool ColorsEqual(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
