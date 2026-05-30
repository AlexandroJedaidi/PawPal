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
        rowLayout = UiFactory.AddHorizontalLayout(tabsRow.gameObject, 0f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = true;
        rowLayout.enabled = false;

        CreateItem(AppScreenId.Home, "Home", "icon_home_brand", "icon_home_white", false, shell, sprites);
        CreateItem(AppScreenId.Shop, "Shop", "icon_shop_brand", "icon_shop_white", false, shell, sprites);
        CreateItem(AppScreenId.Map, "Map", "icon_map_brand", "icon_map_white", true, shell, sprites);
        CreateItem(AppScreenId.Profile, "Profile", "icon_profile_brand", "icon_profile_white", false, shell, sprites);
        CreateItem(AppScreenId.Settings, "Settings", "icon_settings_notfilled", "icon_settings_white", false, shell, sprites);
    }

    public void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform parentRect = rootRect != null ? rootRect.parent as RectTransform : null;
        float availableWidth = parentRect != null && parentRect.rect.width > 0f ? parentRect.rect.width : UiTheme.ReferenceWidth;
        float scale = UiTheme.GetHudVisualScale(availableWidth);

        float visualHeight = UiTheme.ReferenceNavHeight * scale;
        float rowHeight = 60f * scale;
        float shadowHeight = 4f * scale;
        float slotWidth = items.Count > 0 ? availableWidth / items.Count : availableWidth;

        CurrentHeight = visualHeight;
        UiFactory.AnchorBottomStretch(rootRect, 0f, 0f, 0f, visualHeight);
        UiFactory.AnchorBottomStretch(backgroundRect, 0f, 0f, 0f, visualHeight);

        RectTransform shadowRect = topShadow.rectTransform;
        shadowRect.anchorMin = new Vector2(0f, 1f);
        shadowRect.anchorMax = new Vector2(1f, 1f);
        shadowRect.pivot = new Vector2(0.5f, 0f);
        shadowRect.offsetMin = new Vector2(0f, 0f);
        shadowRect.offsetMax = new Vector2(0f, shadowHeight);

        UiFactory.AnchorBottomStretch(contentRoot, 0f, 0f, 0f, visualHeight);
        tabsRow.anchorMin = new Vector2(0.5f, 0.5f);
        tabsRow.anchorMax = new Vector2(0.5f, 0.5f);
        tabsRow.pivot = new Vector2(0.5f, 0.5f);
        tabsRow.sizeDelta = new Vector2(availableWidth, rowHeight);
        tabsRow.anchoredPosition = Vector2.zero;
        tabsRow.localScale = Vector3.one;
        if (rowLayout != null)
        {
            rowLayout.enabled = false;
        }

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform itemRect = items[i].GetComponent<RectTransform>();
            if (itemRect != null)
            {
                itemRect.anchorMin = new Vector2(0.5f, 0.5f);
                itemRect.anchorMax = new Vector2(0.5f, 0.5f);
                itemRect.pivot = new Vector2(0.5f, 0.5f);
                itemRect.anchoredPosition = new Vector2((-availableWidth * 0.5f) + slotWidth * (i + 0.5f), 0f);
            }

            items[i].ApplyLayout(scale, slotWidth);
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
