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
    private static readonly Color32 NeedWarning = new Color32(232, 125, 103, 255);
    private static readonly Color32 NeedWarningPale = new Color32(250, 214, 203, 255);
    private static readonly Color32 HandleWhite = new Color32(248, 248, 248, 255);
    private static readonly Color32 SupportCream = new Color32(236, 223, 200, 255);
    private static readonly Color32 EnergyLabelBlack = new Color32(0, 0, 0, 255);

    private const float BaseWidth = 246f;
    private const float CollapsedHeight = 117f;
    private const float ExpandedHeight = 251f;
    private static readonly Vector2 SelectorArrowSize = new Vector2(16f, 24f);

    private LayoutElement layout;
    private RectTransform frame;
    private Image background;
    private RectTransform statsHeader;
    private RectTransform statsFrame;
    private TextMeshProUGUI dogNameLabel;
    private TextMeshProUGUI enduranceValueLabel;
    private TextMeshProUGUI mobilityValueLabel;
    private TextMeshProUGUI speedValueLabel;
    private TextMeshProUGUI focusValueLabel;
    private readonly Dictionary<PawPalDogNeed, Image> needCircles = new Dictionary<PawPalDogNeed, Image>();
    private readonly Dictionary<PawPalDogNeed, Image> needFills = new Dictionary<PawPalDogNeed, Image>();
    private readonly Dictionary<PawPalDogNeed, Image> needIcons = new Dictionary<PawPalDogNeed, Image>();
    private Action toggleRequested;
    private Action previousDogRequested;
    private Action nextDogRequested;
    private Action<PawPalDogNeed> needRequested;
    private bool expanded;

    public void Initialize(UiSpriteLibrary sprites, Action onToggleRequested)
    {
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
        BuildStatsHeader(frame);
        BuildStatsFrame(frame, sprites);
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

    private void BuildStatsHeader(RectTransform parent)
    {
        statsHeader = UiFactory.CreateRect("StatsHeader", parent);
        statsHeader.anchorMin = new Vector2(0f, 1f);
        statsHeader.anchorMax = new Vector2(0f, 1f);
        statsHeader.pivot = new Vector2(0f, 1f);
        statsHeader.sizeDelta = new Vector2(220f, 19f);
        statsHeader.anchoredPosition = new Vector2(13f, -117f);

        Image line = UiFactory.CreateImage("Line", statsHeader, UiTheme.WhiteSprite, UiTheme.NavBrand);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        line.rectTransform.sizeDelta = new Vector2(220f, 1f);
        line.rectTransform.anchoredPosition = new Vector2(0f, -0.5f);

        Image labelMask = UiFactory.CreateImage("LabelMask", statsHeader, UiTheme.WhiteSprite, UiTheme.NavBackgroundCream);
        labelMask.type = Image.Type.Simple;
        labelMask.preserveAspect = false;
        labelMask.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelMask.rectTransform.sizeDelta = new Vector2(79f, 19f);
        labelMask.rectTransform.anchoredPosition = new Vector2(-0.5f, 0f);

        TextMeshProUGUI label = UiFactory.CreateLabel("StatsLabel", statsHeader, "Stats", 13, UiTheme.NavBrand, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 12f);
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(82f, 22f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 0f);
    }

    private void BuildStatsFrame(RectTransform parent, UiSpriteLibrary sprites)
    {
        statsFrame = UiFactory.CreateRect("StatsFrame", parent);
        statsFrame.anchorMin = new Vector2(0f, 1f);
        statsFrame.anchorMax = new Vector2(0f, 1f);
        statsFrame.pivot = new Vector2(0f, 1f);
        statsFrame.sizeDelta = new Vector2(207f, 105.6f);
        statsFrame.anchoredPosition = new Vector2(19.5f, -139f);

        BuildEnergyStat(statsFrame, sprites);
        BuildMainStats(statsFrame, sprites);
    }

    private void BuildEnergyStat(RectTransform parent, UiSpriteLibrary sprites)
    {
        RectTransform group = UiFactory.CreateRect("EnergyStat", parent);
        group.anchorMin = new Vector2(0f, 1f);
        group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.sizeDelta = new Vector2(47f, 49.8f);
        group.anchoredPosition = new Vector2(0f, -27.9f);

        Image graph = UiFactory.CreateImage("Graph", group, sprites.GetResourceSprite("UI/Figma/HomeStats/graph_energy"), Color.white);
        graph.type = Image.Type.Simple;
        graph.preserveAspect = false;
        graph.rectTransform.anchorMin = new Vector2(0f, 1f);
        graph.rectTransform.anchorMax = new Vector2(0f, 1f);
        graph.rectTransform.pivot = new Vector2(0f, 1f);
        graph.rectTransform.sizeDelta = new Vector2(31.8f, 31.8f);
        graph.rectTransform.anchoredPosition = new Vector2(7.6f, 0f);

        Image icon = UiFactory.CreateImage("Icon", graph.rectTransform, sprites.GetResourceSprite("UI/Figma/HomeStats/icon_energy"), Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0f, 1f);
        icon.rectTransform.anchorMax = new Vector2(0f, 1f);
        icon.rectTransform.pivot = new Vector2(0f, 1f);
        icon.rectTransform.sizeDelta = new Vector2(18f, 18f);
        icon.rectTransform.anchoredPosition = new Vector2(6.9f, -7.1f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", group, "Energy", 13, EnergyLabelBlack, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 11f);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(47f, 20f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -31.8f);
    }

    private void BuildMainStats(RectTransform parent, UiSpriteLibrary sprites)
    {
        RectTransform main = UiFactory.CreateRect("MainStats", parent);
        main.anchorMin = new Vector2(0f, 1f);
        main.anchorMax = new Vector2(0f, 1f);
        main.pivot = new Vector2(0f, 1f);
        main.sizeDelta = new Vector2(152f, 105.6f);
        main.anchoredPosition = new Vector2(55f, 0f);

        enduranceValueLabel = BuildLevelStat(main, sprites, "Endurance", "2", "UI/Figma/HomeStats/graph_endurance", new Vector2(0f, 0f));
        mobilityValueLabel = BuildLevelStat(main, sprites, "Mobility", "4", "UI/Figma/HomeStats/graph_mobility", new Vector2(86f, 0f));
        speedValueLabel = BuildLevelStat(main, sprites, "Speed", "3", "UI/Figma/HomeStats/graph_speed", new Vector2(0f, -55.8f));
        focusValueLabel = BuildLevelStat(main, sprites, "Focus", "5", "UI/Figma/HomeStats/graph_focus", new Vector2(86f, -55.8f));
    }

    private TextMeshProUGUI BuildLevelStat(RectTransform parent, UiSpriteLibrary sprites, string labelText, string value, string graphPath, Vector2 anchoredPosition)
    {
        RectTransform group = UiFactory.CreateRect(labelText + "Stat", parent);
        group.anchorMin = new Vector2(0f, 1f);
        group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.sizeDelta = new Vector2(66f, 49.8f);
        group.anchoredPosition = anchoredPosition;

        Image graph = UiFactory.CreateImage("Graph", group, sprites.GetResourceSprite(graphPath), Color.white);
        graph.type = Image.Type.Simple;
        graph.preserveAspect = false;
        graph.rectTransform.anchorMin = new Vector2(0f, 1f);
        graph.rectTransform.anchorMax = new Vector2(0f, 1f);
        graph.rectTransform.pivot = new Vector2(0f, 1f);
        graph.rectTransform.sizeDelta = new Vector2(31.8f, 31.8f);
        graph.rectTransform.anchoredPosition = new Vector2(17.1f, 0f);

        TextMeshProUGUI valueLabel = UiFactory.CreateLabel("Value", graph.rectTransform, value, 14, UiTheme.NavBrand, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(valueLabel, UiTheme.DefaultFont, 13f);
        valueLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        valueLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        valueLabel.rectTransform.pivot = new Vector2(0f, 1f);
        valueLabel.rectTransform.sizeDelta = new Vector2(18f, 18f);
        valueLabel.rectTransform.anchoredPosition = new Vector2(7f, -7f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", group, labelText, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        ConfigureCompactLabel(label, UiTheme.DefaultFont, 11f);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(66f, 20f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -31.8f);
        return valueLabel;
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

    public void SetDogState(PawPalDogState dog)
    {
        if (dog == null)
        {
            return;
        }

        if (dogNameLabel != null)
        {
            dogNameLabel.text = dog.DisplayName;
        }

        if (enduranceValueLabel != null)
        {
            enduranceValueLabel.text = dog.Endurance.ToString();
        }

        if (mobilityValueLabel != null)
        {
            mobilityValueLabel.text = dog.Mobility.ToString();
        }

        if (speedValueLabel != null)
        {
            speedValueLabel.text = dog.Speed.ToString();
        }

        if (focusValueLabel != null)
        {
            focusValueLabel.text = dog.Focus.ToString();
        }

        RefreshNeedVisual(PawPalDogNeed.Food, dog.Food01);
        RefreshNeedVisual(PawPalDogNeed.Water, dog.Water01);
        RefreshNeedVisual(PawPalDogNeed.Hygiene, dog.Hygiene01);
        RefreshNeedVisual(PawPalDogNeed.Activity, dog.Activity01);
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
        float healthyBlend = Mathf.InverseLerp(0.25f, 0.7f, clamped);
        Color lowColor = Color.Lerp(NeedWarningPale, NeedWarning, Mathf.Clamp01(1f - clamped));
        Color healthyColor = Color.Lerp(emptyColor, fullColor, clamped);
        Color fillColor = Color.Lerp(lowColor, healthyColor, healthyBlend);
        circle.color = Color.Lerp(NeedWarningPale, emptyColor, healthyBlend);

        Image fill;
        if (needFills.TryGetValue(need, out fill) && fill != null)
        {
            fill.fillAmount = clamped;
            fill.color = fillColor;
        }

        Image icon;
        if (needIcons.TryGetValue(need, out icon) && icon != null)
        {
            float iconAlpha = Mathf.Lerp(0.45f, 1f, clamped);
            icon.color = new Color(1f, 1f, 1f, iconAlpha);
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

        if (statsHeader != null)
        {
            statsHeader.gameObject.SetActive(expanded);
        }

        if (statsFrame != null)
        {
            statsFrame.gameObject.SetActive(expanded);
        }
    }
}
