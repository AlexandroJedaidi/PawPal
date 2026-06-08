using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PawPalAgilityTrialSelectionOverlay : MonoBehaviour
{
    private static readonly Color32 Cream = new Color32(252, 248, 232, 255);
    private static readonly Color32 Coral = new Color32(223, 120, 97, 255);
    private static readonly Color32 CoralDark = new Color32(138, 75, 60, 255);
    private static readonly Color32 BodyBrown = new Color32(138, 75, 60, 255);
    private static readonly Color32 Gray = new Color32(163, 163, 163, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);

    private readonly List<TextMeshProUGUI> levelLabels = new List<TextMeshProUGUI>();
    private readonly List<Image> levelFills = new List<Image>();

    private RectTransform root;
    private TextMeshProUGUI detailLabel;
    private TextMeshProUGUI messageLabel;
    private PawPalAgilityTrialConfig config;
    private PawPalAgilityLevelId selectedLevel = PawPalAgilityLevelId.Beginner;

    public static PawPalAgilityTrialSelectionOverlay Show(RectTransform parent)
    {
        if (parent == null)
        {
            return null;
        }

        PawPalAgilityTrialSelectionOverlay existing = parent.GetComponentInChildren<PawPalAgilityTrialSelectionOverlay>(true);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.Refresh();
            return existing;
        }

        RectTransform overlayRoot = UiFactory.CreateRect("AgilityTrialSelectionOverlay", parent);
        UiFactory.Stretch(overlayRoot, 0f, 0f, 0f, 0f);
        PawPalAgilityTrialSelectionOverlay overlay = overlayRoot.gameObject.AddComponent<PawPalAgilityTrialSelectionOverlay>();
        overlay.Build();
        overlay.Refresh();
        return overlay;
    }

    private void Build()
    {
        root = GetComponent<RectTransform>();
        config = PawPalAgilityTrialConfig.LoadOrCreateDefault();

        Image dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.42f);
        dim.raycastTarget = true;

        RectTransform panel = UiFactory.CreateRect("Panel", root);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(352f, 486f);
        panel.anchoredPosition = new Vector2(0f, 6f);

        Image fill = panel.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Cream;

        TextMeshProUGUI title = CreateLabel(panel, "Title", "Agility Trial", 22, CoralDark, TextAlignmentOptions.Center);
        UiFactory.Stretch(title.rectTransform, 16f, 438f, 16f, 18f);

        TextMeshProUGUI intro = CreateLabel(panel, "Intro", "Choose a level, then practice the course or enter a scored trial.", 13, BodyBrown, TextAlignmentOptions.Center);
        intro.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Stretch(intro.rectTransform, 24f, 390f, 24f, 50f);

        for (int i = 0; i < config.Levels.Count; i++)
        {
            PawPalAgilityLevelDefinition level = config.Levels[i];
            if (level != null)
            {
                CreateLevelRow(panel, level, 22f, 103f + i * 48f);
            }
        }

        detailLabel = CreateLabel(panel, "Detail", string.Empty, 13, BodyBrown, TextAlignmentOptions.Left);
        detailLabel.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Stretch(detailLabel.rectTransform, 24f, 104f, 24f, 298f);

        messageLabel = CreateLabel(panel, "Message", string.Empty, 12, CoralDark, TextAlignmentOptions.Center);
        messageLabel.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Stretch(messageLabel.rectTransform, 24f, 74f, 24f, 372f);

        CreateButton(panel, "PracticeButton", "Practice", 24f, 24f, 92f, StartPractice);
        CreateButton(panel, "TrialButton", "Start Trial", 130f, 24f, 112f, StartScored);
        CreateButton(panel, "CloseButton", "Close", 256f, 24f, 72f, Close);
    }

    private void CreateLevelRow(RectTransform parent, PawPalAgilityLevelDefinition level, float x, float y)
    {
        RectTransform row = UiFactory.CreateRect("Level_" + level.LevelId, parent);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.sizeDelta = new Vector2(308f, 40f);
        row.anchoredPosition = new Vector2(x, -y);
        Image fill = row.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Cream;
        levelFills.Add(fill);

        TextMeshProUGUI label = CreateLabel(row, "Label", level.DisplayName, 15, Coral, TextAlignmentOptions.Left);
        UiFactory.Stretch(label.rectTransform, 12f, 8f, 116f, 8f);
        levelLabels.Add(label);

        TextMeshProUGUI fee = CreateLabel(row, "Fee", level.EntryFeeBasicCurrency + " entry", 12, BodyBrown, TextAlignmentOptions.Right);
        UiFactory.Stretch(fee.rectTransform, 170f, 10f, 12f, 10f);

        PawPalAgilityLevelId captured = level.LevelId;
        UiFactory.AddButton(row.gameObject, delegate
        {
            selectedLevel = captured;
            Refresh();
        });
    }

    private void Refresh()
    {
        if (config == null)
        {
            config = PawPalAgilityTrialConfig.LoadOrCreateDefault();
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dog = runtime != null ? runtime.ActiveDog : null;
        PawPalAgilityLevelDefinition level = config.GetLevel(selectedLevel);
        PawPalAgilityEntryStatus status = runtime != null && dog != null
            ? runtime.GetAgilityEntryStatus(dog.Id, selectedLevel)
            : new PawPalAgilityEntryStatus { PracticeMessage = "Choose a pet first.", ScoredMessage = "Choose a pet first." };

        for (int i = 0; i < config.Levels.Count && i < levelLabels.Count; i++)
        {
            PawPalAgilityLevelDefinition candidate = config.Levels[i];
            bool selected = candidate != null && candidate.LevelId == selectedLevel;
            levelLabels[i].color = selected ? White : Coral;
            levelFills[i].color = selected ? Coral : Cream;
        }

        if (detailLabel != null && level != null)
        {
            PawPalAgilityLevelProgressState progress = runtime != null && dog != null
                ? runtime.GetAgilityLevelProgress(dog.Id, selectedLevel)
                : null;
            string best = progress != null && progress.BestMedal != PawPalAgilityMedal.None
                ? progress.BestMedal + " / " + progress.BestScore
                : "No score yet";
            detailLabel.text = level.DisplayName
                + "\nBest: " + best
                + "\nTarget: " + level.TargetTimeSeconds.ToString("0") + "s"
                + "\nFault tolerance: " + level.FaultTolerance;
        }

        if (messageLabel != null)
        {
            messageLabel.text = status.CanPractice ? status.ScoredMessage : status.PracticeMessage;
        }
    }

    private void StartPractice()
    {
        TryStart(PawPalAgilityTrialMode.Practice);
    }

    private void StartScored()
    {
        TryStart(PawPalAgilityTrialMode.Scored);
    }

    private void TryStart(PawPalAgilityTrialMode mode)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dog = runtime != null ? runtime.ActiveDog : null;
        if (runtime == null || dog == null)
        {
            SetMessage("Choose a pet first.");
            return;
        }

        string failure;
        if (!runtime.TryStartAgilityTrial(dog.Id, selectedLevel, mode, PawPalWalkSceneFlow.HomeSceneName, out failure))
        {
            SetMessage(failure);
            Refresh();
            return;
        }

        if (!PawPalAgilityTrialSceneFlow.LoadAgilityScene())
        {
            runtime.CancelActiveAgilityTrial();
            SetMessage("Could not load the Agility Trial scene.");
        }
    }

    private void SetMessage(string message)
    {
        if (messageLabel != null)
        {
            messageLabel.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }
    }

    private void Close()
    {
        gameObject.SetActive(false);
    }

    private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text, int size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, size, color, FontStyles.Normal, alignment);
        label.font = UiTheme.NavRegularFont;
        return label;
    }

    private static void CreateButton(RectTransform parent, string name, string text, float x, float y, float width, UnityEngine.Events.UnityAction action)
    {
        RectTransform button = UiFactory.CreateRect(name, parent);
        button.anchorMin = new Vector2(0f, 0f);
        button.anchorMax = new Vector2(0f, 0f);
        button.pivot = new Vector2(0f, 0f);
        button.sizeDelta = new Vector2(width, 40f);
        button.anchoredPosition = new Vector2(x, y);
        Image fill = button.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.color = Coral;
        UiFactory.AddButton(button.gameObject, action);
        TextMeshProUGUI label = UiFactory.CreateLabel("Label", button, text, 14, White, FontStyles.Bold, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 8f, 0f, 8f, 0f);
    }
}
