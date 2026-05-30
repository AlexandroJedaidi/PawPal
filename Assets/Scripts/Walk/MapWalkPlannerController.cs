using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class MapWalkPlannerController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private static readonly Color32 Cream = new Color32(252, 248, 232, 255);
    private static readonly Color32 Coral = new Color32(223, 120, 97, 255);
    private static readonly Color32 CoralDark = new Color32(138, 75, 60, 255);
    private static readonly Color32 InvalidRed = new Color32(210, 76, 72, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 Muted = new Color32(163, 163, 163, 255);

    private const float RouteLineWidth = 8f;
    private const float MinimumPointSpacing = 9f;

    private readonly List<Vector2> routePoints = new List<Vector2>();
    private readonly List<Image> routeSegments = new List<Image>();

    private RectTransform root;
    private RectTransform routeLayer;
    private RectTransform captureLayer;
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
    private bool drawing;
    private bool built;
    private int lastEdgeIndex = -1;
    private string validationMessage = string.Empty;

    public void Initialize(AppShellController appShell, UiSpriteLibrary spriteLibrary, Action onBackRequested)
    {
        shell = appShell;
        sprites = spriteLibrary;
        backRequested = onBackRequested;
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
        ShowFeedback("Draw from Home.");
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

    public void OnPointerDown(PointerEventData eventData)
    {
        Vector2 mapPoint;
        if (!TryGetMapPoint(eventData, out mapPoint))
        {
            return;
        }

        if (!PawPalWalkRouteGraph.IsNearHome(mapPoint))
        {
            drawing = false;
            ShowFeedback("Route must start at Home.");
            return;
        }

        drawing = true;
        routePoints.Clear();
        routePoints.Add(PawPalWalkRouteGraph.HomePosition);
        lastEdgeIndex = -1;
        ShowFeedback(string.Empty);
        AppendPoint(mapPoint, true);
        RefreshPlanUi();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!drawing)
        {
            return;
        }

        Vector2 mapPoint;
        if (!TryGetMapPoint(eventData, out mapPoint))
        {
            return;
        }

        AppendPoint(mapPoint, false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!drawing)
        {
            return;
        }

        drawing = false;
        Vector2 mapPoint;
        if (TryGetMapPoint(eventData, out mapPoint) && PawPalWalkRouteGraph.IsNearHome(mapPoint))
        {
            AppendPoint(mapPoint, true);
        }

        RefreshPlanUi();
    }

    private void BuildUi()
    {
        routeLayer = CreateNode("RouteLayer", root, 0f, 0f, PawPalWalkRouteGraph.MapWidth, PawPalWalkRouteGraph.MapHeight);

        captureLayer = CreateNode("RouteInputLayer", root, 0f, 0f, PawPalWalkRouteGraph.MapWidth, 635f);
        Image captureImage = captureLayer.gameObject.AddComponent<Image>();
        captureImage.sprite = UiTheme.WhiteSprite;
        captureImage.color = new Color(1f, 1f, 1f, 0.002f);
        captureImage.raycastTarget = true;

        BuildTopHud();
        BuildSummaryPanel();
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

        staminaLabel = CreateLabel(hud, "StaminaLabel", "Stamina 100/100", 12, CoralDark, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
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
        distanceLabel.rectTransform.sizeDelta = new Vector2(150f, 20f);
        distanceLabel.rectTransform.anchoredPosition = new Vector2(14f, -10f);

        stopsLabel = CreateLabel(summaryPanel, "Stops", "Stops: none", 12, CoralDark, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        stopsLabel.textWrappingMode = TextWrappingModes.Normal;
        stopsLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        stopsLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        stopsLabel.rectTransform.pivot = new Vector2(0f, 1f);
        stopsLabel.rectTransform.sizeDelta = new Vector2(340f, 34f);
        stopsLabel.rectTransform.anchoredPosition = new Vector2(14f, -32f);

        feedbackLabel = CreateLabel(summaryPanel, "Feedback", "Draw from Home.", 12, InvalidRed, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        feedbackLabel.textWrappingMode = TextWrappingModes.Normal;
        feedbackLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        feedbackLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        feedbackLabel.rectTransform.pivot = new Vector2(0f, 1f);
        feedbackLabel.rectTransform.sizeDelta = new Vector2(340f, 28f);
        feedbackLabel.rectTransform.anchoredPosition = new Vector2(14f, -66f);

        CreateSmallButton(summaryPanel, "ClearRoute", "Clear", 14f, 101f, 74f, delegate
        {
            ClearRoute();
        });

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

    private bool AppendPoint(Vector2 mapPoint, bool allowDuplicateHome)
    {
        Vector2 snapped;
        int edgeIndex;
        if (!PawPalWalkRouteGraph.TrySnapToWalkablePath(mapPoint, out snapped, out edgeIndex))
        {
            ShowFeedback("Draw along the streets.");
            return false;
        }

        return AppendExactPoint(snapped, allowDuplicateHome, edgeIndex);
    }

    private bool AppendExactPoint(Vector2 snapped, bool allowDuplicateHome, int edgeIndex)
    {
        if (routePoints.Count == 0)
        {
            routePoints.Add(PawPalWalkRouteGraph.HomePosition);
        }

        Vector2 lastPoint = routePoints[routePoints.Count - 1];
        if (!allowDuplicateHome && Vector2.Distance(lastPoint, snapped) < MinimumPointSpacing)
        {
            return false;
        }

        if (PawPalWalkRouteGraph.IsNearHome(snapped))
        {
            snapped = PawPalWalkRouteGraph.HomePosition;
            if (PawPalWalkRouteGraph.IsNearHome(lastPoint) && !allowDuplicateHome)
            {
                return false;
            }
        }

        List<Vector2> pointsToAdd = new List<Vector2>();
        if (lastEdgeIndex >= 0 && edgeIndex >= 0 && lastEdgeIndex != edgeIndex && !PawPalWalkRouteGraph.IsWalkableSegment(lastPoint, snapped))
        {
            Vector2 sharedNode;
            if (!PawPalWalkRouteGraph.TryGetSharedNode(lastEdgeIndex, edgeIndex, out sharedNode))
            {
                ShowFeedback("Stay on connected streets.");
                return false;
            }

            if (Vector2.Distance(lastPoint, sharedNode) >= MinimumPointSpacing)
            {
                pointsToAdd.Add(sharedNode);
            }
        }
        else if (!PawPalWalkRouteGraph.IsWalkableSegment(lastPoint, snapped))
        {
            ShowFeedback("Stay on connected streets.");
            return false;
        }

        if (pointsToAdd.Count == 0 || Vector2.Distance(pointsToAdd[pointsToAdd.Count - 1], snapped) >= MinimumPointSpacing || allowDuplicateHome)
        {
            pointsToAdd.Add(snapped);
        }

        if (pointsToAdd.Count == 0)
        {
            return false;
        }

        if (!CanAffordPreview(pointsToAdd))
        {
            ShowFeedback("Too tired for this route.");
            return false;
        }

        for (int i = 0; i < pointsToAdd.Count; i++)
        {
            routePoints.Add(pointsToAdd[i]);
        }

        lastEdgeIndex = edgeIndex;
        ShowFeedback(string.Empty);
        RefreshPlanUi();
        return true;
    }

    private bool CanAffordPreview(IList<Vector2> pointsToAdd)
    {
        List<Vector2> preview = new List<Vector2>(routePoints);
        for (int i = 0; i < pointsToAdd.Count; i++)
        {
            preview.Add(pointsToAdd[i]);
        }

        PawPalWalkRoutePlan previewPlan = PawPalWalkRouteGraph.BuildPlan(preview);
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalWalkStaminaSnapshot stamina = runtime != null
            ? runtime.GetActiveDogWalkStaminaSnapshot()
            : new PawPalWalkStaminaSnapshot();
        return previewPlan.StaminaCost <= stamina.Current + 0.001f;
    }

    private PawPalWalkRoutePlan BuildCurrentPlan()
    {
        return PawPalWalkRouteGraph.BuildPlan(routePoints);
    }

    private void RefreshPlanUi()
    {
        RefreshRouteLines();

        PawPalWalkRoutePlan plan = BuildCurrentPlan();
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalWalkStaminaSnapshot stamina = runtime != null
            ? runtime.GetActiveDogWalkStaminaSnapshot()
            : new PawPalWalkStaminaSnapshot();

        validationMessage = PawPalWalkRouteGraph.ValidatePlan(plan, stamina.Current);
        bool valid = string.IsNullOrEmpty(validationMessage);

        if (distanceLabel != null)
        {
            distanceLabel.text = "Distance " + Mathf.RoundToInt(plan.RouteDistance) + "  Cost " + Mathf.RoundToInt(plan.StaminaCost);
        }

        if (stopsLabel != null)
        {
            stopsLabel.text = "Stops: " + BuildStopsText(plan);
        }

        if (feedbackLabel != null && !string.IsNullOrEmpty(validationMessage))
        {
            feedbackLabel.text = validationMessage;
            feedbackLabel.color = InvalidRed;
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

    private string BuildStopsText(PawPalWalkRoutePlan plan)
    {
        if (plan == null || plan.PlannedStops.Count == 0)
        {
            return "none";
        }

        string text = string.Empty;
        for (int i = 0; i < plan.PlannedStops.Count; i++)
        {
            if (i > 0)
            {
                text += ", ";
            }

            text += plan.PlannedStops[i].DisplayName;
        }

        return text;
    }

    private void RefreshRouteLines()
    {
        for (int i = 0; i < routeSegments.Count; i++)
        {
            if (routeSegments[i] != null)
            {
                Destroy(routeSegments[i].gameObject);
            }
        }

        routeSegments.Clear();
        if (routePoints.Count < 2)
        {
            return;
        }

        for (int i = 1; i < routePoints.Count; i++)
        {
            routeSegments.Add(CreateRouteSegment(routePoints[i - 1], routePoints[i]));
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
        image.color = Coral;
        image.raycastTarget = false;
        return image;
    }

    private void ClearRoute()
    {
        routePoints.Clear();
        routePoints.Add(PawPalWalkRouteGraph.HomePosition);
        lastEdgeIndex = -1;
        validationMessage = string.Empty;
        ShowFeedback("Draw from Home.");
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
        string message = PawPalWalkRouteGraph.ValidatePlan(plan, stamina.Current);
        if (!string.IsNullOrEmpty(message))
        {
            ShowFeedback(message);
            return;
        }

        plan.ReturnSceneName = PawPalWalkSceneFlow.HomeSceneName;
        if (!runtime.TryStartWalkSession(plan, out message))
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

        feedbackLabel.text = string.IsNullOrEmpty(message) ? "You might find presents or meet other dogs!" : message;
        feedbackLabel.color = string.IsNullOrEmpty(message) ? CoralDark : InvalidRed;
    }

    private bool TryGetMapPoint(PointerEventData eventData, out Vector2 mapPoint)
    {
        mapPoint = Vector2.zero;
        if (root == null || eventData == null)
        {
            return false;
        }

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return false;
        }

        mapPoint = new Vector2(localPoint.x, -localPoint.y);
        return mapPoint.x >= 0f && mapPoint.x <= PawPalWalkRouteGraph.MapWidth
            && mapPoint.y >= 0f && mapPoint.y <= PawPalWalkRouteGraph.MapHeight;
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
