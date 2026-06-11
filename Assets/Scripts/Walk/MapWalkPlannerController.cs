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

    private static readonly Color32 Coral = new Color32(223, 120, 97, 255);
    private static readonly Color32 CoralDark = new Color32(138, 75, 60, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Muted = new Color32(163, 163, 163, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 CtaBlueDark = new Color32(0, 118, 177, 255);
    private static readonly Color32 RoutePreview = new Color32(37, 199, 63, 255);

    private const float RouteLineWidth = 8f;

    private readonly List<string> selectedVisitNodeIds = new List<string>();
    private readonly List<Image> routeSegments = new List<Image>();
    private readonly List<NodeButtonState> nodeButtons = new List<NodeButtonState>();

    private RectTransform root;
    private RectTransform routeLayer;
    private RectTransform nodeLayer;
    private RectTransform actionStack;
    private TextMeshProUGUI startButtonLabel;
    private Image startButtonUnderline;
    private Button startButton;
    private PawPalWalkMapRouteDefinition routeDefinition;
    private PawPalWalkGraphSnapshot routeSnapshot;
    private string snapshotFailureMessage = string.Empty;
    private string validationMessage = string.Empty;
    private string lockedDogId = string.Empty;
    private bool built;

    public event Action<float> PlanPreviewChanged;

    public void SetLockedDogId(string dogId)
    {
        lockedDogId = string.IsNullOrWhiteSpace(dogId) ? string.Empty : dogId.Trim();
        RefreshPlanUi();
    }

    public void Initialize(AppShellController appShell, UiSpriteLibrary spriteLibrary, Action onBackRequested)
    {
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
    }

    public void RefreshRuntimeState()
    {
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
        BuildActionStack();
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

    private void BuildActionStack()
    {
        actionStack = CreateNode("WalkPlannerActions", root, 281f, 604f, 100f, 100f);
        CreateActionStackButton(actionStack, "ClearRoute", "Clear", 0f, 0f, 100f, false, ClearRoute);

        RectTransform startRect = CreateActionStackButton(actionStack, "StartWalk", "Start Walk", 0f, 36f, 100f, true, TryStartWalk);
        startButton = startRect.GetComponent<Button>();
        startButtonLabel = startRect.GetComponentInChildren<TextMeshProUGUI>();
        Transform underline = startRect.Find("Underline");
        startButtonUnderline = underline != null ? underline.GetComponent<Image>() : null;
    }

    private RectTransform CreateActionStackButton(RectTransform parent, string name, string labelText, float x, float y, float width, bool primary, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreateNode(name, parent, x, y, width, 28f);
        Image fill = rect.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = primary ? CtaBlue : Coral;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
        shadow.effectDistance = new Vector2(0f, -1f);
        shadow.useGraphicAlpha = true;

        UiFactory.AddButton(rect.gameObject, onClick);
        Image underline = UiFactory.CreateImage("Underline", rect, UiTheme.WhiteSprite, primary ? CtaBlueDark : CoralDark);
        underline.type = Image.Type.Simple;
        underline.preserveAspect = false;
        underline.raycastTarget = false;
        underline.rectTransform.anchorMin = new Vector2(0f, 1f);
        underline.rectTransform.anchorMax = new Vector2(1f, 1f);
        underline.rectTransform.pivot = new Vector2(0.5f, 1f);
        underline.rectTransform.offsetMin = new Vector2(0f, -28f);
        underline.rectTransform.offsetMax = new Vector2(0f, -26f);

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
            runtime.ApplyWalkPersonalityToPlan(ResolveLockedDog(runtime), plan);
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
            ? runtime.GetWalkStaminaSnapshot(ResolveLockedDog(runtime))
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
        RaisePlanPreviewChanged(plan != null ? plan.StaminaCost : 0f);

        if (startButton != null)
        {
            startButton.interactable = valid;
            Image buttonFill = startButton.GetComponent<Image>();
            if (buttonFill != null)
            {
                buttonFill.color = valid ? CtaBlue : Muted;
            }

            if (startButtonUnderline != null)
            {
                startButtonUnderline.color = valid ? CtaBlueDark : Muted;
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

        EnsureLockedDogSelected(runtime);
        PawPalWalkStaminaSnapshot stamina = runtime.GetWalkStaminaSnapshot(ResolveLockedDog(runtime));
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

    private PawPalDogState ResolveLockedDog(PawPalGameRuntime runtime)
    {
        if (runtime == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(lockedDogId))
        {
            IReadOnlyList<PawPalDogState> dogs = runtime.Dogs;
            for (int i = 0; i < dogs.Count; i++)
            {
                PawPalDogState dog = dogs[i];
                if (dog != null && string.Equals(dog.Id, lockedDogId, StringComparison.OrdinalIgnoreCase))
                {
                    return dog;
                }
            }
        }

        return runtime.ActiveDog;
    }

    private void EnsureLockedDogSelected(PawPalGameRuntime runtime)
    {
        if (runtime == null || string.IsNullOrEmpty(lockedDogId))
        {
            return;
        }

        PawPalDogState activeDog = runtime.ActiveDog;
        if (activeDog != null && string.Equals(activeDog.Id, lockedDogId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        runtime.SelectDogById(lockedDogId, false);
    }

    private void ShowFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.LogWarning("Map walk planner: " + message);
    }

    private void RaisePlanPreviewChanged(float previewCost)
    {
        Action<float> handler = PlanPreviewChanged;
        if (handler != null)
        {
            handler(Mathf.Max(0f, previewCost));
        }
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
