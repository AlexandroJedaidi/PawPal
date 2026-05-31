using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class DogDetailsWidgetView : MonoBehaviour
{
    private static readonly Color32 NeedBlueLight = new Color32(59, 177, 232, 255);
    private static readonly Color32 NeedBlue = new Color32(34, 144, 214, 255);
    private static readonly Color32 NeedBluePale = new Color32(207, 234, 248, 255);
    private static readonly Color32 NeedBluePaleLight = new Color32(220, 241, 251, 255);
    private static readonly Color32 HandleWhite = new Color32(248, 248, 248, 255);
    private static readonly Color32 SupportCream = new Color32(236, 223, 200, 255);
    private static readonly Color32 TrickLockedFill = new Color32(244, 238, 226, 255);
    private static readonly Color32 TrickLockedText = new Color32(164, 137, 121, 255);
    private static readonly Color32 TrickRequirementFill = new Color32(255, 252, 241, 255);

    private const float BaseWidth = 246f;
    private const float CollapsedHeight = 117f;
    private const float ExpandedHeight = 251f;
    private const float PagerPageWidth = 207f;
    private const int PagerPageCount = 3;
    private const float PagerHeight = 124f;
    private const float PagerHeaderHeight = 19f;
    private const float StatLevelMax = 5f;
    private const float BondLevelMax = 10f;
    private static readonly Vector2 SelectorArrowSize = new Vector2(16f, 24f);
    private static readonly Vector2 PagerArrowHitSize = new Vector2(28f, 40f);
    private static readonly Vector2 StatRingSize = new Vector2(31.8f, 31.8f);
    private LayoutElement layout;
    private RectTransform frame;
    private Image background;
    private UiSpriteLibrary spriteLibrary;
    private RectTransform statsPager;
    private RectTransform statsPagerContent;
    private RectTransform statsFrame;
    private Button pagerPreviousButton;
    private Button pagerNextButton;
    private Image pagerPreviousIcon;
    private Image pagerNextIcon;
    private TextMeshProUGUI dogNameLabel;
    private Image walkStaminaFill;
    private TextMeshProUGUI enduranceValueLabel;
    private Image enduranceRingFill;
    private TextMeshProUGUI mobilityValueLabel;
    private Image mobilityRingFill;
    private TextMeshProUGUI speedValueLabel;
    private Image speedRingFill;
    private TextMeshProUGUI focusValueLabel;
    private Image focusRingFill;
    private TextMeshProUGUI bondValueLabel;
    private Image bondRingFill;
    private Image genderProfileIcon;
    private TextMeshProUGUI genderValueLabel;
    private TextMeshProUGUI personalityValueLabel;
    private TextMeshProUGUI furValueLabel;
    private TextMeshProUGUI breedValueLabel;
    private readonly Dictionary<PawPalDogNeed, Image> needCircles = new Dictionary<PawPalDogNeed, Image>();
    private readonly Dictionary<PawPalDogNeed, Image> needFills = new Dictionary<PawPalDogNeed, Image>();
    private readonly Dictionary<PawPalDogNeed, Image> needIcons = new Dictionary<PawPalDogNeed, Image>();
    private readonly List<TrickTileView> trickTiles = new List<TrickTileView>();
    private Action toggleRequested;
    private Action previousDogRequested;
    private Action nextDogRequested;
    private Action<PawPalDogNeed> needRequested;
    private Action<PawPalTrickId> trickRequested;
    private bool expanded;
    private int statsPagerPageIndex;

    public void Initialize(UiSpriteLibrary sprites, Action onToggleRequested)
    {
        spriteLibrary = sprites;
        toggleRequested = onToggleRequested;
        layout = UiFactory.EnsureLayoutElement(gameObject, -1f, CollapsedHeight, 1f, 0f);
        layout.minHeight = CollapsedHeight;
        layout.preferredHeight = CollapsedHeight;
        layout.flexibleWidth = 1f;

        frame = UiFactory.CreateRect("Frame", transform);
        frame.anchorMin = new Vector2(0.5f, 1f);
        frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.sizeDelta = new Vector2(BaseWidth, CollapsedHeight);
        frame.anchoredPosition = Vector2.zero;

        background = frame.gameObject.GetComponent<Image>();
        if (background == null)
        {
            background = frame.gameObject.AddComponent<Image>();
        }

        background.sprite = UiTheme.TopRoundedPanelSprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = UiTheme.NavBackgroundCream;

        BuildHandle(frame);
        BuildBreedRow(frame, sprites);
        BuildNeedsHeader(frame);
        BuildNeedsRow(frame, sprites);
        BuildStatsPager(frame, sprites);
        SetExpanded(false);
    }

    private void BuildHandle(RectTransform parent)
    {
        RectTransform handleGroup = UiFactory.CreateRect("HandleGroup", parent);
        handleGroup.anchorMin = new Vector2(0.5f, 1f);
        handleGroup.anchorMax = new Vector2(0.5f, 1f);
        handleGroup.pivot = new Vector2(0.5f, 1f);
        handleGroup.sizeDelta = new Vector2(68f, 12f);
        handleGroup.anchoredPosition = new Vector2(0f, 0f);

        Image handle = UiFactory.CreateImage("Handle", handleGroup, UiTheme.WhiteSprite, HandleWhite);
        handle.type = Image.Type.Simple;
        handle.preserveAspect = false;
        handle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        handle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        handle.rectTransform.pivot = new Vector2(0.5f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(38f, 4f);
        handle.rectTransform.anchoredPosition = new Vector2(0f, -3f);

        Shadow handleShadow = handle.gameObject.AddComponent<Shadow>();
        handleShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        handleShadow.effectDistance = new Vector2(0f, -1f);
        handleShadow.useGraphicAlpha = true;

        Image hitArea = UiFactory.CreateImage("HandleHitArea", handleGroup, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        UiFactory.Stretch(hitArea.rectTransform, 0f, 0f, 0f, 0f);
        UiFactory.AddButton(hitArea.gameObject, delegate
        {
            if (toggleRequested != null)
            {
                toggleRequested();
            }
        });
    }

    private void BuildBreedRow(RectTransform parent, UiSpriteLibrary sprites)
    {
        RectTransform row = UiFactory.CreateRect("BreedRow", parent);
        row.anchorMin = new Vector2(0.5f, 1f);
        row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(222f, 24f);
        row.anchoredPosition = new Vector2(0f, -15f);

        CreateArrow(row, sprites, "BackArrow", "UI/Figma/HomeMain/button_back", new Vector2(-105f, -12f), true);
        CreateNameField(row);
        CreateArrow(row, sprites, "ForwardArrow", "UI/Figma/HomeMain/button_forward", new Vector2(105f, -12f), false);
    }

    private void CreateArrow(RectTransform parent, UiSpriteLibrary sprites, string name, string resourcePath, Vector2 anchoredPosition, bool previousDog)
    {
        Image hitArea = UiFactory.CreateImage(name + "HitArea", parent, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        hitArea.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        hitArea.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        hitArea.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hitArea.rectTransform.sizeDelta = new Vector2(28f, 28f);
        hitArea.rectTransform.anchoredPosition = anchoredPosition;

        Image arrow = UiFactory.CreateImage(name, hitArea.rectTransform, sprites.GetResourceSprite(resourcePath), Color.white);
        arrow.type = Image.Type.Simple;
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        arrow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.sizeDelta = SelectorArrowSize;
        arrow.rectTransform.anchoredPosition = Vector2.zero;
        UiFactory.AddButton(hitArea.gameObject, delegate
        {
            if (previousDog)
            {
                if (previousDogRequested != null)
                {
                    previousDogRequested();
                }
            }
            else if (nextDogRequested != null)
            {
                nextDogRequested();
            }
        });
    }

    private void CreateNameField(RectTransform parent)
    {
        Image field = UiFactory.CreateImage("NameField", parent, UiTheme.DogDetailsFieldFillSprite, UiTheme.White);
        field.type = Image.Type.Sliced;
        field.preserveAspect = false;
        field.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        field.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        field.rectTransform.pivot = new Vector2(0.5f, 1f);
        field.rectTransform.sizeDelta = new Vector2(182f, 24f);
        field.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        Image outline = UiFactory.CreateImage("Outline", field.rectTransform, UiTheme.DogDetailsFieldOutlineSprite, UiTheme.NavBrand);
        outline.type = Image.Type.Sliced;
        outline.preserveAspect = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        dogNameLabel = UiFactory.CreateLabel("DogName", field.rectTransform, "Pepper", 15, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        dogNameLabel.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(dogNameLabel.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void BuildNeedsHeader(RectTransform parent)
    {
        RectTransform header = UiFactory.CreateRect("NeedsHeader", parent);
        header.anchorMin = new Vector2(0.5f, 1f);
        header.anchorMax = new Vector2(0.5f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(220f, 19f);
        header.anchoredPosition = new Vector2(0f, -42f);

        Image line = UiFactory.CreateImage("Line", header, UiTheme.WhiteSprite, UiTheme.NavBrand);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        line.rectTransform.sizeDelta = new Vector2(220f, 1f);
        line.rectTransform.anchoredPosition = new Vector2(0f, -0.5f);

        Image labelMask = UiFactory.CreateImage("LabelMask", header, UiTheme.WhiteSprite, UiTheme.NavBackgroundCream);
        labelMask.type = Image.Type.Simple;
        labelMask.preserveAspect = false;
        labelMask.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.sizeDelta = new Vector2(49f, 19f);
        labelMask.rectTransform.anchoredPosition = new Vector2(0.05f, 0f);

        TextMeshProUGUI label = UiFactory.CreateLabel("NeedsLabel", header, "Needs", 13, UiTheme.NavBrand, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 12f);
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(52f, 22f);
        label.rectTransform.anchoredPosition = new Vector2(1.05f, 0f);
    }

    private void BuildNeedsRow(RectTransform parent, UiSpriteLibrary sprites)
    {
        RectTransform row = UiFactory.CreateRect("NeedsRow", parent);
        row.anchorMin = new Vector2(0.5f, 1f);
        row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(246f, 50f);
        row.anchoredPosition = new Vector2(0f, -64f);

        CreateNeed(row, sprites, PawPalDogNeed.Food, "Food", "icon_food_brand", NeedBlueLight, new Vector2(-90f, -0.028f), new Vector2(22.64f, 22.64f), 3.802f);
        CreateNeed(row, sprites, PawPalDogNeed.Water, "Water", "icon_water_brand", NeedBlue, new Vector2(-30f, 0f), new Vector2(20.998f, 21.838f), 4.2f);
        CreateNeed(row, sprites, PawPalDogNeed.Hygiene, "Hygiene", "icon_clean_brand", NeedBlue, new Vector2(30f, -0.028f), new Vector2(24f, 24f), 3.723f);
        CreateNeed(row, sprites, PawPalDogNeed.Activity, "Activity", "icon_activity_brand", NeedBlue, new Vector2(90f, -0.028f), new Vector2(26.952f, 26.952f), 2.184f);
    }

    private void BuildStatsPager(RectTransform parent, UiSpriteLibrary sprites)
    {
        statsPager = UiFactory.CreateRect("StatsTricksPager", parent);
        statsPager.anchorMin = new Vector2(0f, 1f);
        statsPager.anchorMax = new Vector2(0f, 1f);
        statsPager.pivot = new Vector2(0f, 1f);
        statsPager.sizeDelta = new Vector2(PagerPageWidth, PagerHeight);
        statsPager.anchoredPosition = new Vector2(20f, -117f);

        RectTransform viewport = UiFactory.CreateRect("Viewport", statsPager);
        UiFactory.Stretch(viewport, 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.002f);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = UiFactory.CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(PagerPageWidth * PagerPageCount, PagerHeight);
        content.anchoredPosition = Vector2.zero;
        statsPagerContent = content;

        ScrollRect scrollRect = statsPager.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
        scrollRect.scrollSensitivity = 0f;

        RectTransform statsPage = CreatePagerPage(content, "StatsPage", 0f);
        BuildPagerHeader(statsPage, "Stats");
        BuildStatsFrame(statsPage, sprites);

        RectTransform tricksPage = CreatePagerPage(content, "TricksPage", PagerPageWidth);
        BuildPagerHeader(tricksPage, "Tricks");
        BuildTrickTiles(tricksPage, sprites);

        RectTransform profilePage = CreatePagerPage(content, "ProfilePage", PagerPageWidth * 2f);
        BuildPagerHeader(profilePage, "Profile");
        BuildProfileTiles(profilePage, sprites);

        BuildPagerArrowButtons(statsPager, sprites);
        SetStatsPagerPage(0);
    }

    private void BuildPagerArrowButtons(RectTransform parent, UiSpriteLibrary sprites)
    {
        pagerPreviousButton = CreatePagerArrow(parent, sprites, "PagerPrevious", "UI/Figma/HomeMain/button_back", new Vector2(-10f, -62f), -1, out pagerPreviousIcon);
        pagerNextButton = CreatePagerArrow(parent, sprites, "PagerNext", "UI/Figma/HomeMain/button_forward", new Vector2(PagerPageWidth + 10f, -62f), 1, out pagerNextIcon);
    }

    private Button CreatePagerArrow(RectTransform parent, UiSpriteLibrary sprites, string name, string resourcePath, Vector2 anchoredPosition, int pageDelta, out Image arrowIcon)
    {
        Image hitArea = UiFactory.CreateImage(name + "HitArea", parent, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        hitArea.rectTransform.anchorMin = new Vector2(0f, 1f);
        hitArea.rectTransform.anchorMax = new Vector2(0f, 1f);
        hitArea.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hitArea.rectTransform.sizeDelta = PagerArrowHitSize;
        hitArea.rectTransform.anchoredPosition = anchoredPosition;

        arrowIcon = UiFactory.CreateImage(name, hitArea.rectTransform, sprites.GetResourceSprite(resourcePath), Color.white);
        arrowIcon.type = Image.Type.Simple;
        arrowIcon.preserveAspect = true;
        arrowIcon.raycastTarget = false;
        arrowIcon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        arrowIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        arrowIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrowIcon.rectTransform.sizeDelta = SelectorArrowSize;
        arrowIcon.rectTransform.anchoredPosition = Vector2.zero;

        return UiFactory.AddButton(hitArea.gameObject, delegate
        {
            SetStatsPagerPage(statsPagerPageIndex + pageDelta);
        });
    }

    private void SetStatsPagerPage(int pageIndex)
    {
        statsPagerPageIndex = Mathf.Clamp(pageIndex, 0, PagerPageCount - 1);
        if (statsPagerContent != null)
        {
            statsPagerContent.anchoredPosition = new Vector2(-PagerPageWidth * statsPagerPageIndex, 0f);
        }

        RefreshPagerArrows();
    }

    private void RefreshPagerArrows()
    {
        bool canGoPrevious = statsPagerPageIndex > 0;
        bool canGoNext = statsPagerPageIndex < PagerPageCount - 1;
        RefreshPagerArrow(pagerPreviousButton, pagerPreviousIcon, canGoPrevious);
        RefreshPagerArrow(pagerNextButton, pagerNextIcon, canGoNext);
    }

    private static void RefreshPagerArrow(Button button, Image icon, bool enabled)
    {
        if (button != null)
        {
            button.interactable = enabled;
        }

        if (icon != null)
        {
            Color color = Color.white;
            color.a = enabled ? 1f : 0.28f;
            icon.color = color;
        }
    }

    private RectTransform CreatePagerPage(RectTransform parent, string name, float x)
    {
        RectTransform page = UiFactory.CreateRect(name, parent);
        page.anchorMin = new Vector2(0f, 1f);
        page.anchorMax = new Vector2(0f, 1f);
        page.pivot = new Vector2(0f, 1f);
        page.sizeDelta = new Vector2(PagerPageWidth, PagerHeight);
        page.anchoredPosition = new Vector2(x, 0f);
        return page;
    }

    private void BuildPagerHeader(RectTransform parent, string title)
    {
        RectTransform header = UiFactory.CreateRect(title + "Header", parent);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(0f, 1f);
        header.pivot = new Vector2(0f, 1f);
        header.sizeDelta = new Vector2(PagerPageWidth, PagerHeaderHeight);
        header.anchoredPosition = Vector2.zero;

        Image line = UiFactory.CreateImage("Line", header, UiTheme.WhiteSprite, UiTheme.NavBrand);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        line.rectTransform.sizeDelta = new Vector2(PagerPageWidth, 1f);
        line.rectTransform.anchoredPosition = new Vector2(0f, -0.5f);

        Image labelMask = UiFactory.CreateImage("LabelMask", header, UiTheme.WhiteSprite, UiTheme.NavBackgroundCream);
        labelMask.type = Image.Type.Simple;
        labelMask.preserveAspect = false;
        labelMask.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.sizeDelta = new Vector2(76f, 19f);
        labelMask.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", header, title, 13, UiTheme.NavBrand, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 12f);
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(80f, 22f);
        label.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void BuildStatsFrame(RectTransform parent, UiSpriteLibrary sprites)
    {
        statsFrame = UiFactory.CreateRect("StatsFrame", parent);
        statsFrame.anchorMin = new Vector2(0.5f, 1f);
        statsFrame.anchorMax = new Vector2(0.5f, 1f);
        statsFrame.pivot = new Vector2(0.5f, 1f);
        statsFrame.sizeDelta = new Vector2(180f, 105.6f);
        statsFrame.anchoredPosition = new Vector2(0f, -22f);

        BuildWalkStaminaStat(statsFrame, sprites);
        BuildBondStat(statsFrame);
        BuildMainStats(statsFrame);
    }

    private void BuildTrickTiles(RectTransform parent, UiSpriteLibrary sprites)
    {
        trickTiles.Clear();

        IReadOnlyList<PawPalTrickDefinition> definitions = PawPalTrickCatalog.CoreDefinitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            PawPalTrickDefinition definition = definitions[i];
            int row = i / 3;
            int column = i % 3;
            float x = column * 71f;
            if (row > 0)
            {
                x = 35f + (i - 3) * 71f;
            }

            Vector2 position = new Vector2(x, -(24f + row * 48f));
            trickTiles.Add(BuildTrickTile(parent, sprites, definition, position));
        }
    }

    private void BuildProfileTiles(RectTransform parent, UiSpriteLibrary sprites)
    {
        Image unusedIcon;
        genderValueLabel = BuildProfileTile(parent, sprites, "Gender", "icon_male_other", new Vector2(0f, -24f), out genderProfileIcon);
        personalityValueLabel = BuildProfileTile(parent, sprites, "Personality", "icon_star_brand", new Vector2(109f, -24f), out unusedIcon);
        furValueLabel = BuildProfileTile(parent, sprites, "Fur", "icon_pawprint_other", new Vector2(0f, -72f), out unusedIcon);
        breedValueLabel = BuildProfileTile(parent, sprites, "Breed", "icon_dog_brand", new Vector2(109f, -72f), out unusedIcon);
    }

    private TextMeshProUGUI BuildProfileTile(RectTransform parent, UiSpriteLibrary sprites, string labelText, string iconName, Vector2 anchoredPosition, out Image icon)
    {
        RectTransform root = UiFactory.CreateRect(labelText.Replace(" ", string.Empty) + "Profile", parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(98f, 42f);
        root.anchoredPosition = anchoredPosition;

        Image background = UiFactory.CreateImage("Background", root, UiTheme.RoundedTenSprite, UiTheme.CardWhite);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.raycastTarget = false;
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        Image border = UiFactory.CreateImage("Border", root, UiTheme.RoundedTenOutlineSprite, SupportCream);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

        Image iconCircle = UiFactory.CreateImage("IconCircle", root, UiTheme.CircleSprite, UiTheme.NavBrand);
        iconCircle.type = Image.Type.Simple;
        iconCircle.preserveAspect = false;
        iconCircle.raycastTarget = false;
        iconCircle.rectTransform.anchorMin = new Vector2(0f, 1f);
        iconCircle.rectTransform.anchorMax = new Vector2(0f, 1f);
        iconCircle.rectTransform.pivot = new Vector2(0f, 1f);
        iconCircle.rectTransform.sizeDelta = new Vector2(24f, 24f);
        iconCircle.rectTransform.anchoredPosition = new Vector2(7f, -9f);

        icon = UiFactory.CreateImage("Icon", iconCircle.rectTransform, sprites.GetWhiteIcon(iconName), UiTheme.White);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(14f, 14f);
        icon.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", root, labelText, 9, TrickLockedText, FontStyles.Normal, TextAlignmentOptions.Left);
        ConfigureCompactLabel(label, UiTheme.NavExtraBoldFont, 8f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 6f;
        label.fontSizeMax = 8f;
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(60f, 13f);
        label.rectTransform.anchoredPosition = new Vector2(36f, -6f);

        TextMeshProUGUI value = UiFactory.CreateLabel("Value", root, "-", 10, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        ConfigureCompactLabel(value, UiTheme.NavExtraBoldFont, 10f);
        value.enableAutoSizing = true;
        value.fontSizeMin = 7f;
        value.fontSizeMax = 10f;
        value.rectTransform.anchorMin = new Vector2(0f, 1f);
        value.rectTransform.anchorMax = new Vector2(0f, 1f);
        value.rectTransform.pivot = new Vector2(0f, 1f);
        value.rectTransform.sizeDelta = new Vector2(58f, 18f);
        value.rectTransform.anchoredPosition = new Vector2(36f, -19f);
        return value;
    }

    private TrickTileView BuildTrickTile(RectTransform parent, UiSpriteLibrary sprites, PawPalTrickDefinition definition, Vector2 anchoredPosition)
    {
        RectTransform root = UiFactory.CreateRect(definition.Id + "Trick", parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(65f, 42f);
        root.anchoredPosition = anchoredPosition;

        Image background = UiFactory.CreateImage("Background", root, UiTheme.RoundedTenSprite, UiTheme.CardWhite);
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.raycastTarget = true;
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);
        UiFactory.AddButton(background.gameObject, delegate
        {
            if (trickRequested != null)
            {
                trickRequested(definition.Id);
            }
        });

        Image border = UiFactory.CreateImage("Border", root, UiTheme.RoundedTenOutlineSprite, UiTheme.NavBrand);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

        Image iconCircle = UiFactory.CreateImage("IconCircle", root, UiTheme.CircleSprite, UiTheme.NavBrand);
        iconCircle.type = Image.Type.Simple;
        iconCircle.preserveAspect = false;
        iconCircle.raycastTarget = false;
        iconCircle.rectTransform.anchorMin = new Vector2(0f, 1f);
        iconCircle.rectTransform.anchorMax = new Vector2(0f, 1f);
        iconCircle.rectTransform.pivot = new Vector2(0f, 1f);
        iconCircle.rectTransform.sizeDelta = new Vector2(24f, 24f);
        iconCircle.rectTransform.anchoredPosition = new Vector2(5f, -9f);

        Image icon = UiFactory.CreateImage("Icon", iconCircle.rectTransform, sprites.GetWhiteIcon(definition.IconName), UiTheme.White);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(13f, 13f);
        icon.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", root, definition.DisplayName, 10, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        ConfigureCompactLabel(label, UiTheme.NavExtraBoldFont, 10f);
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 7f;
        label.fontSizeMax = 10f;
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(30f, 17f);
        label.rectTransform.anchoredPosition = new Vector2(32f, -6f);

        Image requirementPill = UiFactory.CreateImage("RequirementPill", root, UiTheme.RoundedFiveSprite, TrickRequirementFill);
        requirementPill.type = Image.Type.Sliced;
        requirementPill.preserveAspect = false;
        requirementPill.raycastTarget = false;
        requirementPill.rectTransform.anchorMin = new Vector2(0f, 1f);
        requirementPill.rectTransform.anchorMax = new Vector2(0f, 1f);
        requirementPill.rectTransform.pivot = new Vector2(0f, 1f);
        requirementPill.rectTransform.sizeDelta = new Vector2(28f, 15f);
        requirementPill.rectTransform.anchoredPosition = new Vector2(32f, -23f);

        TextMeshProUGUI requirement = UiFactory.CreateLabel("Requirement", requirementPill.rectTransform, "Teach", 8, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(requirement, UiTheme.NavExtraBoldFont, 8f);
        requirement.raycastTarget = false;
        requirement.enableAutoSizing = true;
        requirement.fontSizeMin = 6f;
        requirement.fontSizeMax = 8f;
        UiFactory.Stretch(requirement.rectTransform, 2f, 1f, 2f, 1f);

        return new TrickTileView(definition, background, border, iconCircle, icon, label, requirementPill, requirement);
    }

    private void BuildWalkStaminaStat(RectTransform parent, UiSpriteLibrary sprites)
    {
        RectTransform group = UiFactory.CreateRect("WalkStaminaStat", parent);
        group.anchorMin = new Vector2(0f, 1f);
        group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.sizeDelta = new Vector2(48f, 46f);
        group.anchoredPosition = new Vector2(0f, 0f);

        Image graph = UiFactory.CreateImage("Graph", group, UiTheme.CircleSprite, NeedBluePale);
        graph.type = Image.Type.Simple;
        graph.preserveAspect = false;
        graph.raycastTarget = false;
        graph.rectTransform.anchorMin = new Vector2(0f, 1f);
        graph.rectTransform.anchorMax = new Vector2(0f, 1f);
        graph.rectTransform.pivot = new Vector2(0f, 1f);
        graph.rectTransform.sizeDelta = StatRingSize;
        graph.rectTransform.anchoredPosition = new Vector2(8f, 0f);

        walkStaminaFill = CreateStatRingFill(graph.rectTransform, "Fill");
        CreateStatRingCenter(graph.rectTransform);

        Image icon = UiFactory.CreateImage("Icon", graph.rectTransform, sprites.GetWhiteIcon("icon_energy_brand"), new Color(0.133f, 0.565f, 0.839f, 0.22f));
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(16f, 16f);
        icon.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", group, "Stamina", 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 9f);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(48f, 16f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -29f);
    }

    private void BuildBondStat(RectTransform parent)
    {
        bondValueLabel = BuildLevelStat(parent, "Bond", "1", new Vector2(0f, -55.8f), BondLevelMax, out bondRingFill);
    }

    private void BuildMainStats(RectTransform parent)
    {
        RectTransform main = UiFactory.CreateRect("MainStats", parent);
        main.anchorMin = new Vector2(0f, 1f);
        main.anchorMax = new Vector2(0f, 1f);
        main.pivot = new Vector2(0f, 1f);
        main.sizeDelta = new Vector2(180f, 105.6f);
        main.anchoredPosition = Vector2.zero;

        enduranceValueLabel = BuildLevelStat(main, "Endurance", "2", new Vector2(66f, 0f), StatLevelMax, out enduranceRingFill);
        mobilityValueLabel = BuildLevelStat(main, "Mobility", "4", new Vector2(132f, 0f), StatLevelMax, out mobilityRingFill);
        speedValueLabel = BuildLevelStat(main, "Speed", "3", new Vector2(66f, -55.8f), StatLevelMax, out speedRingFill);
        focusValueLabel = BuildLevelStat(main, "Focus", "5", new Vector2(132f, -55.8f), StatLevelMax, out focusRingFill);
    }

    private TextMeshProUGUI BuildLevelStat(RectTransform parent, string labelText, string value, Vector2 anchoredPosition, float maxValue, out Image ringFill)
    {
        RectTransform group = UiFactory.CreateRect(labelText + "Stat", parent);
        group.anchorMin = new Vector2(0f, 1f);
        group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.sizeDelta = new Vector2(48f, 46f);
        group.anchoredPosition = anchoredPosition;

        Image graph = UiFactory.CreateImage("Graph", group, UiTheme.CircleSprite, NeedBluePale);
        graph.type = Image.Type.Simple;
        graph.preserveAspect = false;
        graph.raycastTarget = false;
        graph.rectTransform.anchorMin = new Vector2(0f, 1f);
        graph.rectTransform.anchorMax = new Vector2(0f, 1f);
        graph.rectTransform.pivot = new Vector2(0f, 1f);
        graph.rectTransform.sizeDelta = StatRingSize;
        graph.rectTransform.anchoredPosition = new Vector2(8f, 0f);

        ringFill = CreateStatRingFill(graph.rectTransform, "Fill");
        ringFill.fillAmount = GetStatLevelFillAmount(ParseStatValue(value), maxValue);
        CreateStatRingCenter(graph.rectTransform);

        TextMeshProUGUI valueLabel = UiFactory.CreateLabel("Value", graph.rectTransform, value, 14, NeedBlue, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(valueLabel, UiTheme.DefaultFont, 13f);
        valueLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        valueLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        valueLabel.rectTransform.pivot = new Vector2(0f, 1f);
        valueLabel.rectTransform.sizeDelta = new Vector2(18f, 18f);
        valueLabel.rectTransform.anchoredPosition = new Vector2(7f, -7f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", group, labelText, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 9f);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(48f, 16f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -29f);
        return valueLabel;
    }

    private static Image CreateStatRingFill(RectTransform parent, string name)
    {
        Image fill = UiFactory.CreateImage(name, parent, UiTheme.CircleSprite, NeedBlue);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        UiFactory.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
        return fill;
    }

    private static void CreateStatRingCenter(RectTransform parent)
    {
        Image center = UiFactory.CreateImage("Center", parent, UiTheme.CircleSprite, UiTheme.NavBackgroundCream);
        center.type = Image.Type.Simple;
        center.preserveAspect = false;
        center.raycastTarget = false;
        center.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        center.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        center.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        center.rectTransform.sizeDelta = new Vector2(22.6f, 22.6f);
        center.rectTransform.anchoredPosition = Vector2.zero;
    }

    private static int ParseStatValue(string value)
    {
        int parsed;
        return int.TryParse(value, out parsed) ? parsed : 0;
    }

    private static float GetStatLevelFillAmount(int value, float maxValue)
    {
        return Mathf.Clamp01(value / Mathf.Max(1f, maxValue));
    }

    private void CreateNeed(RectTransform parent, UiSpriteLibrary sprites, PawPalDogNeed need, string labelText, string iconName, Color32 circleColor, Vector2 anchoredPosition, Vector2 iconSize, float iconTop)
    {
        RectTransform group = UiFactory.CreateRect(labelText + "Need", parent);
        group.anchorMin = new Vector2(0.5f, 1f);
        group.anchorMax = new Vector2(0.5f, 1f);
        group.pivot = new Vector2(0.5f, 1f);
        group.sizeDelta = new Vector2(60f, 50f);
        group.anchoredPosition = anchoredPosition;

        Image circle = UiFactory.CreateImage("Circle", group, UiTheme.CircleSprite, circleColor);
        circle.type = Image.Type.Simple;
        circle.preserveAspect = false;
        circle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        circle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        circle.rectTransform.pivot = new Vector2(0.5f, 1f);
        circle.rectTransform.sizeDelta = new Vector2(31.804f, 31.804f);
        circle.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        needCircles[need] = circle;

        Image fill = UiFactory.CreateImage("Fill", group, UiTheme.CircleSprite, circleColor);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.fillAmount = 1f;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        fill.rectTransform.pivot = new Vector2(0.5f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(31.804f, 31.804f);
        fill.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        needFills[need] = fill;

        Image icon = UiFactory.CreateImage("Icon", group, sprites.GetWhiteIcon(iconName), UiTheme.White);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        icon.rectTransform.pivot = new Vector2(0.5f, 1f);
        icon.rectTransform.sizeDelta = iconSize;
        icon.rectTransform.anchoredPosition = new Vector2(0f, -iconTop);
        needIcons[need] = icon;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", group, labelText, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 11f);
        label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.sizeDelta = new Vector2(60f, 20f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -31f);

        Image hitArea = UiFactory.CreateImage("HitArea", group, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        UiFactory.Stretch(hitArea.rectTransform, 0f, 0f, 0f, 0f);
        UiFactory.AddButton(hitArea.gameObject, delegate
        {
            if (needRequested != null)
            {
                needRequested(need);
            }
        });
    }

    public void BindInteractions(Action onPreviousDog, Action onNextDog, Action<PawPalDogNeed> onNeedSelected)
    {
        previousDogRequested = onPreviousDog;
        nextDogRequested = onNextDog;
        needRequested = onNeedSelected;
    }

    public void BindTrickRequested(Action<PawPalTrickId> onTrickRequested)
    {
        trickRequested = onTrickRequested;
    }

    public void SetDogState(PawPalDogState dog)
    {
        if (dog == null)
        {
            return;
        }

        PawPalDogPersonalityProfiles.EnsureProfile(dog);
        if (dogNameLabel != null)
        {
            dogNameLabel.text = dog.DisplayName;
        }

        RefreshLevelStat(enduranceValueLabel, enduranceRingFill, dog.Endurance);
        RefreshLevelStat(mobilityValueLabel, mobilityRingFill, dog.Mobility);
        RefreshLevelStat(speedValueLabel, speedRingFill, dog.Speed);
        RefreshLevelStat(focusValueLabel, focusRingFill, dog.Focus);
        RefreshLevelStat(bondValueLabel, bondRingFill, PawPalGameRuntime.GetBondLevel(dog), BondLevelMax, PawPalGameRuntime.GetBondProgress01(dog));

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalWalkStaminaSnapshot walkStamina = runtime != null
            ? runtime.GetWalkStaminaSnapshot(dog)
            : new PawPalWalkStaminaSnapshot();
        if (walkStaminaFill != null)
        {
            walkStaminaFill.fillAmount = walkStamina.Fill01;
        }

        RefreshNeedVisual(PawPalDogNeed.Food, dog.Food01);
        RefreshNeedVisual(PawPalDogNeed.Water, dog.Water01);
        RefreshNeedVisual(PawPalDogNeed.Hygiene, dog.Hygiene01);
        RefreshNeedVisual(PawPalDogNeed.Activity, dog.Activity01);
        RefreshTrickTiles(dog);
        RefreshProfile(dog);
    }

    private static void RefreshLevelStat(TextMeshProUGUI valueLabel, Image ringFill, int value)
    {
        RefreshLevelStat(valueLabel, ringFill, value, StatLevelMax, -1f);
    }

    private static void RefreshLevelStat(TextMeshProUGUI valueLabel, Image ringFill, int value, float maxValue, float overrideFillAmount)
    {
        if (valueLabel != null)
        {
            valueLabel.text = value.ToString();
        }

        if (ringFill != null)
        {
            ringFill.fillAmount = overrideFillAmount >= 0f
                ? Mathf.Clamp01(overrideFillAmount)
                : GetStatLevelFillAmount(value, maxValue);
        }
    }

    private void RefreshProfile(PawPalDogState dog)
    {
        if (genderValueLabel != null)
        {
            genderValueLabel.text = PawPalDogPersonalityProfiles.FormatGender(dog.Gender);
        }

        if (genderProfileIcon != null && spriteLibrary != null)
        {
            string iconName = dog.Gender == PawPalDogGender.Female ? "icon_female_other" : "icon_male_other";
            genderProfileIcon.sprite = spriteLibrary.GetWhiteIcon(iconName);
        }

        if (personalityValueLabel != null)
        {
            personalityValueLabel.text = PawPalDogPersonalityProfiles.FormatPersonality(dog.Personality);
        }

        if (furValueLabel != null)
        {
            furValueLabel.text = PawPalDogPersonalityProfiles.FormatProfileText(dog.FurColor);
        }

        if (breedValueLabel != null)
        {
            breedValueLabel.text = PawPalDogPersonalityProfiles.FormatProfileText(dog.Breed);
        }
    }

    private void RefreshTrickTiles(PawPalDogState dog)
    {
        if (dog != null)
        {
            PawPalTrickCatalog.EnsureDogTrickData(dog);
        }

        for (int i = 0; i < trickTiles.Count; i++)
        {
            TrickTileView tile = trickTiles[i];
            if (tile == null)
            {
                continue;
            }

            PawPalDogTrickProgress progress = dog != null ? PawPalTrickCatalog.GetProgress(dog, tile.Definition.Id) : null;
            PawPalTrickLearningStage stage = PawPalTrickCatalog.GetLearningStage(progress, tile.Definition);
            PawPalTrainingTrickPresentation presentation = PawPalTrainingModeView.DescribeTrick(dog, tile.Definition.Id);
            bool learned = stage == PawPalTrickLearningStage.Learned || stage == PawPalTrickLearningStage.Mastered;
            bool locked = presentation.VisualState == PawPalTrainingTrickVisualState.Locked;
            if (tile.Background != null)
            {
                tile.Background.color = locked ? TrickLockedFill : UiTheme.CardWhite;
            }

            if (tile.Border != null)
            {
                tile.Border.color = locked ? SupportCream : learned ? new Color32(103, 178, 151, 255) : UiTheme.NavBrand;
            }

            if (tile.IconCircle != null)
            {
                tile.IconCircle.color = locked ? SupportCream : learned ? new Color32(103, 178, 151, 255) : UiTheme.NavBrand;
            }

            if (tile.Icon != null)
            {
                tile.Icon.color = locked ? new Color(1f, 1f, 1f, 0.62f) : UiTheme.White;
            }

            if (tile.Label != null)
            {
                tile.Label.color = locked ? TrickLockedText : UiTheme.NavBrandDark;
            }

            if (tile.RequirementPill != null)
            {
                tile.RequirementPill.gameObject.SetActive(true);
                tile.RequirementPill.color = locked ? TrickRequirementFill : learned ? new Color32(226, 246, 235, 255) : TrickRequirementFill;
            }

            if (tile.Requirement != null)
            {
                tile.Requirement.text = FormatTrickStage(dog, progress, tile.Definition, stage, locked);
                tile.Requirement.color = locked ? UiTheme.NavBrandDark : learned ? new Color32(55, 132, 104, 255) : UiTheme.NavBrandDark;
            }
        }
    }

    private static string FormatTrickStage(PawPalDogState dog, PawPalDogTrickProgress progress, PawPalTrickDefinition definition, PawPalTrickLearningStage stage, bool locked)
    {
        if (locked)
        {
            return "Lock";
        }

        switch (stage)
        {
            case PawPalTrickLearningStage.Mastered:
                return "Max";
            case PawPalTrickLearningStage.Learned:
                return "Done";
            case PawPalTrickLearningStage.Practicing:
                return Mathf.RoundToInt(PawPalTrickCatalog.GetProgress01(progress, definition) * 100f) + "%";
            case PawPalTrickLearningStage.Discovered:
                return "Go";
            default:
                return "Teach";
        }
    }

    private void RefreshNeedVisual(PawPalDogNeed need, float value01)
    {
        Image circle;
        if (!needCircles.TryGetValue(need, out circle) || circle == null)
        {
            return;
        }

        Color32 fullColor = need == PawPalDogNeed.Food ? NeedBlueLight : NeedBlue;
        Color32 emptyColor = need == PawPalDogNeed.Food ? NeedBluePaleLight : NeedBluePale;
        float clamped = Mathf.Clamp01(value01);
        Color fillColor = Color.Lerp(emptyColor, fullColor, Mathf.Lerp(0.2f, 1f, clamped));
        circle.color = emptyColor;

        Image fill;
        if (needFills.TryGetValue(need, out fill) && fill != null)
        {
            fill.fillAmount = clamped;
            fill.color = fillColor;
        }

        Image icon;
        if (needIcons.TryGetValue(need, out icon) && icon != null)
        {
            icon.color = UiTheme.White;
        }
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
        label.margin = Vector4.zero;
    }

    public void SetExpanded(bool isExpanded)
    {
        expanded = isExpanded;

        float targetHeight = expanded ? ExpandedHeight : CollapsedHeight;
        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(BaseWidth, targetHeight);
        if (layout != null)
        {
            layout.minHeight = targetHeight;
            layout.preferredHeight = targetHeight;
        }

        if (frame != null)
        {
            frame.sizeDelta = new Vector2(BaseWidth, targetHeight);
        }

        if (statsPager != null)
        {
            statsPager.gameObject.SetActive(expanded);
        }
    }

    private sealed class TrickTileView
    {
        public TrickTileView(
            PawPalTrickDefinition definition,
            Image background,
            Image border,
            Image iconCircle,
            Image icon,
            TextMeshProUGUI label,
            Image requirementPill,
            TextMeshProUGUI requirement)
        {
            Definition = definition;
            Background = background;
            Border = border;
            IconCircle = iconCircle;
            Icon = icon;
            Label = label;
            RequirementPill = requirementPill;
            Requirement = requirement;
        }

        public readonly PawPalTrickDefinition Definition;
        public readonly Image Background;
        public readonly Image Border;
        public readonly Image IconCircle;
        public readonly Image Icon;
        public readonly TextMeshProUGUI Label;
        public readonly Image RequirementPill;
        public readonly TextMeshProUGUI Requirement;
    }
}
