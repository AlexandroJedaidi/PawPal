using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BottomNav : MonoBehaviour
{
    private readonly List<BottomNavItemView> items = new List<BottomNavItemView>();
    private RectTransform rootRect;
    private RectTransform backgroundRect;
    private RectTransform contentRoot;
    private RectTransform tabsRow;
    private Image background;
    private Image topShadow;
    private HorizontalLayoutGroup rowLayout;

    public float CurrentHeight { get; private set; }

    public void Initialize(AppShellController shell, UiSpriteLibrary sprites)
    {
        rootRect = GetComponent<RectTransform>();
        Image rootImage = gameObject.GetComponent<Image>();
        if (rootImage != null)
        {
            DestroyImmediate(rootImage);
        }

        backgroundRect = UiFactory.CreateRect("BarBackground", transform);
        background = backgroundRect.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.WhiteSprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = UiTheme.NavBackgroundCream;
        background.raycastTarget = false;

        topShadow = UiFactory.CreateImage("TopShadow", transform, UiTheme.NavTopShadowSprite, UiTheme.NavShadow);
        topShadow.type = Image.Type.Simple;
        topShadow.preserveAspect = false;
        topShadow.raycastTarget = false;

        contentRoot = UiFactory.CreateRect("ContentRoot", transform);
        tabsRow = UiFactory.CreateRect("TabsRow", contentRoot);
        rowLayout = UiFactory.AddHorizontalLayout(tabsRow.gameObject, 17f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = false;

        CreateItem(AppScreenId.Home, "Home", "icon_home_brand", "icon_home_white", false, shell, sprites);
        CreateItem(AppScreenId.Shop, "Shop", "icon_shop_brand", "icon_shop_white", false, shell, sprites);
        CreateItem(AppScreenId.Map, "Map", "icon_map_brand", "icon_map_white", true, shell, sprites);
        CreateItem(AppScreenId.Profile, "Profile", "icon_profile_brand", "icon_profile_white", false, shell, sprites);
        CreateItem(AppScreenId.Settings, "Settings", "icon_settings_notfilled", "icon_settings_white", false, shell, sprites);
    }

    public void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform canvasRect = GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
        float canvasWidth = Mathf.Max(canvasRect.rect.width, 1f);
        float canvasHeight = Mathf.Max(canvasRect.rect.height, 1f);
        float unsafeLeft = Screen.safeArea.xMin * (canvasWidth / Mathf.Max(Screen.width, 1f));
        float unsafeRight = (Screen.width - Screen.safeArea.xMax) * (canvasWidth / Mathf.Max(Screen.width, 1f));
        float unsafeBottom = Screen.safeArea.yMin * (canvasHeight / Mathf.Max(Screen.height, 1f));
        float safeWidth = Mathf.Max(0f, canvasWidth - unsafeLeft - unsafeRight);
        float scale = Mathf.Min(1f, safeWidth / UiTheme.ReferenceWidth);

        float visualHeight = 66f * scale;
        float rowWidth = 368f * scale;
        float rowHeight = 60f * scale;
        float shadowHeight = 4f * scale;
        float spacing = 17f * scale;

        CurrentHeight = visualHeight;
        UiFactory.AnchorBottomStretch(rootRect, -unsafeLeft, -unsafeBottom, -unsafeRight, visualHeight + unsafeBottom);
        UiFactory.AnchorBottomStretch(backgroundRect, 0f, unsafeBottom, 0f, visualHeight);

        RectTransform shadowRect = topShadow.rectTransform;
        shadowRect.anchorMin = new Vector2(0f, 1f);
        shadowRect.anchorMax = new Vector2(1f, 1f);
        shadowRect.pivot = new Vector2(0.5f, 0f);
        shadowRect.offsetMin = new Vector2(0f, 0f);
        shadowRect.offsetMax = new Vector2(0f, shadowHeight);

        UiFactory.AnchorBottomStretch(contentRoot, 0f, unsafeBottom, 0f, visualHeight);
        tabsRow.anchorMin = new Vector2(0.5f, 0.5f);
        tabsRow.anchorMax = new Vector2(0.5f, 0.5f);
        tabsRow.pivot = new Vector2(0.5f, 0.5f);
        tabsRow.sizeDelta = new Vector2(rowWidth, rowHeight);
        tabsRow.anchoredPosition = Vector2.zero;
        rowLayout.spacing = spacing;

        for (int i = 0; i < items.Count; i++)
        {
            items[i].ApplyLayout(scale);
        }
    }

    public void SetSelected(AppScreenId screenId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].SetSelected(items[i].ScreenId == screenId);
        }
    }

    private void CreateItem(AppScreenId screenId, string label, string defaultIcon, string selectedIcon, bool isMap, AppShellController shell, UiSpriteLibrary sprites)
    {
        RectTransform itemRect = UiFactory.CreateRect(screenId + "Item", tabsRow);
        BottomNavItemView item = itemRect.gameObject.AddComponent<BottomNavItemView>();
        item.Initialize(screenId, sprites, label, defaultIcon, selectedIcon, isMap, delegate
        {
            shell.ShowScreen(screenId);
        });
        items.Add(item);
    }
}
