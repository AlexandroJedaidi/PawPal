using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class MapWalkPlannerController : MonoBehaviour
{
    private sealed class NodeButtonState
    {
        public PawPalWalkMapRouteNodeDefinition Node;
        public Button Button;
        public Image Fill;
        public TextMeshProUGUI Label;
        public GameObject BadgeRoot;
        public TextMeshProUGUI Badge;
    }

    private static readonly Color32 Cream = new Color32(252, 248, 232, 255);
    private static readonly Color32 Coral = new Color32(223, 120, 97, 255);
    private static readonly Color32 CoralDark = new Color32(138, 75, 60, 255);
    private static readonly Color32 InvalidRed = new Color32(210, 76, 72, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Muted = new Color32(163, 163, 163, 255);
    private static readonly Color32 RoutePreview = new Color32(37, 199, 63, 255);

    private const float RouteLineWidth = 8f;

    private readonly List<string> selectedVisitNodeIds = new List<string>();
    private readonly List<Image> routeSegments = new List<Image>();
    private readonly List<NodeButtonState> nodeButtons = new List<NodeButtonState>();

    private RectTransform root;
    private RectTransform routeLayer;
    private RectTransform nodeLayer;
    private RectTransform summaryPanel;
    private TextMeshProUGUI dogLabel;
    private TextMeshProUGUI staminaLabel;
    private TextMeshProUGUI distanceLabel;
    private TextMeshProUGUI stopsLabel;
    private TextMeshProUGUI feedbackLabel;
    private TextMeshProUGUI startButtonLabel;
    private Image staminaFill;
    private Button startButton;
    private AppShellController shell;
    private UiSpriteLibrary sprites;
    private Action backRequested;
    private PawPalWalkMapRouteDefinition routeDefinition;
    private PawPalWalkGraphSnapshot routeSnapshot;
    private string snapshotFailureMessage = string.Empty;
    private string validationMessage = string.Empty;
    private bool built;

    public void Initialize(AppShellController appShell, UiSpriteLibrary spriteLibrary, Action onBackRequested)
    {
        shell = appShell;
        sprites = spriteLibrary;
        backRequested = onBackRequested;
        routeDefinition = PawPalWalkMapRouteDefinitions.CreateDefault();
        PrepareSnapshot();

        root = GetComponent<RectTransform>();
        if (!built)
        {
            BuildUi();
            built = true;
        }

        ClearRoute();
        RefreshRuntimeState();
    }

    public void EnterPlanner()
    {
        ClearRoute();
        RefreshRuntimeState();
        ShowFeedback("Tap places in the order you want to visit.");
    }

    public void RefreshRuntimeState()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalDogState dog = runtime != null ? runtime.ActiveDog : null;
        PawPalWalkStaminaSnapshot stamina = runtime != null
            ? runtime.GetActiveDogWalkStaminaSnapshot()
            : new PawPalWalkStaminaSnapshot();

        if (dogLabel != null)
        {
            dogLabel.text = dog != null ? dog.DisplayName : "Choose a dog";
        }

        if (staminaLabel != null)
        {
            staminaLabel.text = "Stamina " + stamina.CompactText;
        }

        if (staminaFill != null)
        {
            staminaFill.fillAmount = stamina.Fill01;
        }

        RefreshPlanUi();
    }

    private void PrepareSnapshot()
    {
        routeSnapshot = null;
        snapshotFailureMessage = string.Empty;
        if (routeDefinition == null || !routeDefinition.TryBuildSnapshot(out routeSnapshot, out snapshotFailureMessage))
        {
            routeSnapshot = null;
            if (string.IsNullOrEmpty(snapshotFailureMessage))
            {
                snapshotFailureMessage = "Map route definition is missing.";
            }
        }
    }

    private void BuildUi()
    {
        routeLayer = CreateNode("RouteLayer", root, 0f, 0f, PawPalWalkRouteGraph.MapWidth, PawPalWalkRouteGraph.MapHeight);
        nodeLayer = CreateNode("RouteNodeLayer", root, 0f, 0f, PawPalWalkRouteGraph.MapWidth, PawPalWalkRouteGraph.MapHeight);
        BuildNodeButtons();
        BuildTopHud();
        BuildSummaryPanel();
    }

    private void BuildNodeButtons()
    {
        nodeButtons.Clear();
        if (routeDefinition == null)
        {
            return;
        }

        for (int i = 0; i < routeDefinition.Nodes.Count; i++)
        {
            PawPalWalkMapRouteNodeDefinition node = routeDefinition.Nodes[i];
            if (node == null || !node.Selectable)
            {
                continue;
            }

            RectTransform rect = CreateNode(
                "VisitNode_" + node.NodeId,
                nodeLayer,
                node.MapPosition.x - node.BoxSize.x * 0.5f,
                node.MapPosition.y - node.BoxSize.y * 0.5f,
                node.BoxSize.x,
                node.BoxSize.y);

            Image fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = UiTheme.RoundedTenSprite;
            fill.type = Image.Type.Sliced;
            fill.color = new Color32(252, 248, 232, 218);
            fill.raycastTarget = true;
            CreateOutline(rect, Coral, 1f);

            Button button = UiFactory.AddButton(rect.gameObject, delegate { });
            string capturedNodeId = node.NodeId;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { ToggleNode(capturedNodeId); });

            TextMeshProUGUI label = CreateLabel(rect, "Label", node.DisplayName, 11, CoralDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
            UiFactory.Stretch(label.rectTransform, 5f, 4f, 5f, 4f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform badgeRoot = UiFactory.CreateRect("OrderBadge", rect);
            badgeRoot.anchorMin = new Vector2(1f, 0f);
            badgeRoot.anchorMax = new Vector2(1f, 0f);
            badgeRoot.pivot = new Vector2(0.5f, 0.5f);
            badgeRoot.sizeDelta = new Vector2(24f, 24f);
            badgeRoot.anchoredPosition = new Vector2(-4f, 4f);

            Image badgeFill = badgeRoot.gameObject.AddComponent<Image>();
            badgeFill.sprite = UiTheme.CircleSprite;
            badgeFill.color = Coral;
            badgeFill.raycastTarget = false;
            TextMeshProUGUI badge = CreateLabel(badgeRoot, "Label", string.Empty, 12, White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
            UiFactory.Stretch(badge.rectTransform, 0f, 0f, 0f, 0f);
            badgeRoot.SetAsLastSibling();
            badgeRoot.gameObject.SetActive(false);

            nodeButtons.Add(new NodeButtonState
            {
                Node = node,
                Button = button,
                Fill = fill,
                Label = label,
                BadgeRoot = badgeRoot.gameObject,
                Badge = badge
            });
        }
    }

    private void BuildTopHud()
    {
        RectTransform hud = CreateNode("WalkPlannerTopHud", root, 14f, 12f, 365f, 50f);
        Image fill = hud.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = new Color32(252, 248, 232, 232);
        CreateOutline(hud, Coral, 1f);

        dogLabel = CreateLabel(hud, "DogName", "Pepper", 15, CoralDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        dogLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        dogLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        dogLabel.rectTransform.pivot = new Vector2(0f, 1f);
        dogLabel.rectTransform.sizeDelta = new Vector2(130f, 20f);
        dogLabel.rectTransform.anchoredPosition = new Vector2(12f, -8f);

        RectTransform bar = CreateNode("StaminaBar", hud, 146f, 14f, 130f, 12f);
        Image barBack = bar.gameObject.AddComponent<Image>();
        barBack.sprite = UiTheme.RoundedTenSprite;
        barBack.type = Image.Type.Sliced;
        barBack.color = new Color32(236, 223, 200, 255);

        staminaFill = UiFactory.CreateImage("Fill", bar, UiTheme.RoundedTenSprite, Coral);
        staminaFill.type = Image.Type.Filled;
        staminaFill.fillMethod = Image.FillMethod.Horizontal;
        staminaFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        UiFactory.Stretch(staminaFill.rectTransform, 0f, 0f, 0f, 0f);

        staminaLabel = CreateLabel(hud, "StaminaLabel", "Stamina", 12, CoralDark, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        staminaLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        staminaLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        staminaLabel.rectTransform.pivot = new Vector2(0f, 1f);
        staminaLabel.rectTransform.sizeDelta = new Vector2(142f, 18f);
        staminaLabel.rectTransform.anchoredPosition = new Vector2(146f, -28f);

        CreateSmallButton(hud, "BackButton", "Back", 303f, 9f, 50f, delegate
        {
            if (backRequested != null)
            {
                backRequested();
            }
        });
    }

    private void BuildSummaryPanel()
    {
        summaryPanel = CreateNode("WalkRouteSummary", root, 12f, 635f, 369f, 137f);
        Image fill = summaryPanel.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;
        CreateOutline(summaryPanel, Coral, 1f);

        distanceLabel = CreateLabel(summaryPanel, "Distance", "Distance 0", 13, CoralDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        distanceLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        distanceLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        distanceLabel.rectTransform.pivot = new Vector2(0f, 1f);
        distanceLabel.rectTransform.sizeDelta = new Vector2(240f, 20f);
        distanceLabel.rectTransform.anchoredPosition = new Vector2(14f, -10f);

        stopsLabel = CreateLabel(summaryPanel, "Stops", "Stops: none", 12, CoralDark, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        stopsLabel.textWrappingMode = TextWrappingModes.Normal;
        stopsLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        stopsLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        stopsLabel.rectTransform.pivot = new Vector2(0f, 1f);
        stopsLabel.rectTransform.sizeDelta = new Vector2(340f, 34f);
        stopsLabel.rectTransform.anchoredPosition = new Vector2(14f, -32f);

        feedbackLabel = CreateLabel(summaryPanel, "Feedback", "Tap places in order.", 12, InvalidRed, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        feedbackLabel.textWrappingMode = TextWrappingModes.Normal;
        feedbackLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        feedbackLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        feedbackLabel.rectTransform.pivot = new Vector2(0f, 1f);
        feedbackLabel.rectTransform.sizeDelta = new Vector2(340f, 28f);
        feedbackLabel.rectTransform.anchoredPosition = new Vector2(14f, -66f);

        CreateSmallButton(summaryPanel, "ClearRoute", "Clear", 14f, 101f, 74f, ClearRoute);
        CreateSmallButton(summaryPanel, "UndoRoute", "Undo", 96f, 101f, 74f, UndoLastVisit);

        RectTransform startRect = CreateSmallButton(summaryPanel, "StartWalk", "Start Walk", 239f, 101f, 116f, TryStartWalk);
        startButton = startRect.GetComponent<Button>();
        startButtonLabel = startRect.GetComponentInChildren<TextMeshProUGUI>();
    }

    private RectTransform CreateSmallButton(RectTransform parent, string name, string labelText, float x, float y, float width, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreateNode(name, parent, x, y, width, 26f);
        Image fill = rect.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Coral;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
        shadow.effectDistance = new Vector2(0f, -1f);
        shadow.useGraphicAlpha = true;

        UiFactory.AddButton(rect.gameObject, onClick);
        TextMeshProUGUI label = CreateLabel(rect, "Label", labelText, 12, White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 6f, 0f, 6f, 0f);
        return rect;
    }

    private void ToggleNode(string nodeId)
    {
        int existingIndex = selectedVisitNodeIds.IndexOf(nodeId);
        if (existingIndex >= 0)
        {
            selectedVisitNodeIds.RemoveAt(existingIndex);
        }
        else
        {
            selectedVisitNodeIds.Add(nodeId);
        }

        RefreshPlanUi();
    }

    private void UndoLastVisit()
    {
        if (selectedVisitNodeIds.Count > 0)
        {
            selectedVisitNodeIds.RemoveAt(selectedVisitNodeIds.Count - 1);
        }

        RefreshPlanUi();
    }

    private PawPalWalkRoutePlan BuildCurrentPlan()
    {
        if (routeSnapshot == null || selectedVisitNodeIds.Count == 0)
        {
            return null;
        }

        PawPalWalkRoutePlan plan;
        string failureMessage;
        if (!PawPalWalkGraphService.TryBuildPlanThroughVisitNodeIds(
                routeSnapshot,
                routeDefinition.HomeStartNodeId,
                routeDefinition.HomeEndNodeId,
                selectedVisitNodeIds,
                out plan,
                out failureMessage))
        {
            validationMessage = failureMessage;
            return null;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.ApplyWalkPersonalityToPlan(runtime.ActiveDog, plan);
        }

        return plan;
    }

    private void RefreshPlanUi()
    {
        validationMessage = string.Empty;
        PawPalWalkRoutePlan plan = BuildCurrentPlan();
        RefreshRouteLines(plan);
        RefreshNodeButtonStates();

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalWalkStaminaSnapshot stamina = runtime != null
            ? runtime.GetActiveDogWalkStaminaSnapshot()
            : new PawPalWalkStaminaSnapshot();

        if (routeSnapshot == null)
        {
            validationMessage = snapshotFailureMessage;
        }
        else if (selectedVisitNodeIds.Count == 0)
        {
            validationMessage = "Tap places in the order you want to visit.";
        }
        else if (plan != null)
        {
            validationMessage = PawPalWalkGraphService.ValidatePlan(plan, stamina.Current);
        }

        bool valid = string.IsNullOrEmpty(validationMessage);

        if (distanceLabel != null)
        {
            float routeDistance = plan != null ? plan.RouteDistance : 0f;
            float staminaCost = plan != null ? plan.StaminaCost : 0f;
            distanceLabel.text = "Distance " + Mathf.RoundToInt(routeDistance) + "  Effort " + GetEffortLabel(staminaCost, stamina.Max);
        }

        if (stopsLabel != null)
        {
            stopsLabel.text = "Stops: " + BuildStopsText();
        }

        if (feedbackLabel != null)
        {
            if (valid)
            {
                ShowFeedback(string.Empty);
            }
            else
            {
                feedbackLabel.text = validationMessage;
                feedbackLabel.color = InvalidRed;
            }
        }

        if (startButton != null)
        {
            startButton.interactable = valid;
            Image buttonFill = startButton.GetComponent<Image>();
            if (buttonFill != null)
            {
                buttonFill.color = valid ? Coral : Muted;
            }
        }

        if (startButtonLabel != null)
        {
            startButtonLabel.color = White;
        }
    }

    private void RefreshNodeButtonStates()
    {
        for (int i = 0; i < nodeButtons.Count; i++)
        {
            NodeButtonState state = nodeButtons[i];
            if (state == null || state.Node == null)
            {
                continue;
            }

            int orderIndex = selectedVisitNodeIds.IndexOf(state.Node.NodeId);
            bool selected = orderIndex >= 0;
            if (state.Fill != null)
            {
                state.Fill.color = selected ? Coral : new Color32(252, 248, 232, 218);
            }

            if (state.Label != null)
            {
                state.Label.color = selected ? White : CoralDark;
            }

            if (state.Badge != null)
            {
                state.Badge.text = selected ? (orderIndex + 1).ToString() : string.Empty;
            }

            if (state.BadgeRoot != null)
            {
                state.BadgeRoot.SetActive(selected);
            }
        }
    }

    private string BuildStopsText()
    {
        if (selectedVisitNodeIds.Count == 0)
        {
            return "none";
        }

        string text = string.Empty;
        for (int i = 0; i < selectedVisitNodeIds.Count; i++)
        {
            PawPalWalkMapRouteNodeDefinition node = routeDefinition != null ? routeDefinition.FindNode(selectedVisitNodeIds[i]) : null;
            if (i > 0)
            {
                text += " > ";
            }

            text += node != null && !string.IsNullOrWhiteSpace(node.DisplayName)
                ? node.DisplayName
                : selectedVisitNodeIds[i];
        }

        return text;
    }

    private static string GetEffortLabel(float staminaCost, float maxStamina)
    {
        float ratio = maxStamina > 0.001f ? staminaCost / maxStamina : 0f;
        if (ratio < 0.2f)
        {
            return "Light";
        }

        if (ratio < 0.45f)
        {
            return "Medium";
        }

        if (ratio < 0.7f)
        {
            return "High";
        }

        return "Very High";
    }

    private void RefreshRouteLines(PawPalWalkRoutePlan plan)
    {
        for (int i = 0; i < routeSegments.Count; i++)
        {
            if (routeSegments[i] != null)
            {
                Destroy(routeSegments[i].gameObject);
            }
        }

        routeSegments.Clear();
        if (plan == null || plan.RoutePoints == null || plan.RoutePoints.Count < 2)
        {
            return;
        }

        for (int i = 1; i < plan.RoutePoints.Count; i++)
        {
            routeSegments.Add(CreateRouteSegment(plan.RoutePoints[i - 1].ToVector2(), plan.RoutePoints[i].ToVector2()));
        }
    }

    private Image CreateRouteSegment(Vector2 start, Vector2 end)
    {
        RectTransform rect = UiFactory.CreateRect("RouteSegment", routeLayer);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 startLocal = new Vector2(start.x, -start.y);
        Vector2 endLocal = new Vector2(end.x, -end.y);
        Vector2 delta = endLocal - startLocal;
        rect.sizeDelta = new Vector2(delta.magnitude, RouteLineWidth);
        rect.anchoredPosition = (startLocal + endLocal) * 0.5f;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = UiTheme.RoundedTenSprite;
        image.type = Image.Type.Sliced;
        image.color = RoutePreview;
        image.raycastTarget = false;
        return image;
    }

    private void ClearRoute()
    {
        selectedVisitNodeIds.Clear();
        validationMessage = string.Empty;
        ShowFeedback("Tap places in the order you want to visit.");
        RefreshPlanUi();
    }

    private void TryStartWalk()
    {
        PawPalWalkRoutePlan plan = BuildCurrentPlan();
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            ShowFeedback("Game is still loading.");
            return;
        }

        PawPalWalkStaminaSnapshot stamina = runtime.GetActiveDogWalkStaminaSnapshot();
        string message = plan != null
            ? PawPalWalkGraphService.ValidatePlan(plan, stamina.Current)
            : "Tap places in the order you want to visit.";
        if (!string.IsNullOrEmpty(message))
        {
            ShowFeedback(message);
            return;
        }

        if (!runtime.TryStartDeferredWalkSession(
                selectedVisitNodeIds,
                routeDefinition.HomeStartNodeId,
                routeDefinition.HomeEndNodeId,
                PawPalWalkSceneFlow.HomeSceneName,
                out message))
        {
            ShowFeedback(message);
            return;
        }

        if (!PawPalWalkSceneFlow.LoadWalkScene())
        {
            runtime.CancelActiveWalkSession();
            ShowFeedback("Walk scene is not ready.");
        }
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel == null)
        {
            return;
        }

        feedbackLabel.text = string.IsNullOrEmpty(message) ? "Home is fixed as the start and finish." : message;
        feedbackLabel.color = string.IsNullOrEmpty(message) ? CoralDark : InvalidRed;
    }

    private static RectTransform CreateNode(string name, RectTransform parent, float x, float y, float width, float height)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
        return rect;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text, int fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Normal, alignment);
        label.font = font;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static void CreateOutline(RectTransform target, Color color, float distance)
    {
        Outline outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }
}
