using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class InventoryPanelView : MonoBehaviour
{
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 QuantityBlue = new Color32(62, 132, 215, 255);

    private ScrollRect scrollRect;
    private UiSpriteLibrary spriteLibrary;
    private RectTransform contentRoot;
    private Action onGetMoreTapped;
    private Action<string> onCollarTapped;
    private Action<string> onToyTapped;
    private int lastInventoryRevision = -1;
    private string lastDogId = string.Empty;

    public void Initialize(UiSpriteLibrary sprites)
    {
        spriteLibrary = sprites;

        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(255f, 258f);

        Image background = gameObject.GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }

        background.sprite = UiTheme.WhiteSprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = UiTheme.NavBackgroundCream;

        CreatePanelBorder(root);

        RectTransform viewport = UiFactory.CreateRect("Viewport", transform);
        UiFactory.Stretch(viewport, 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.002f);
        viewportImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        contentRoot = UiFactory.CreateRect("Content", viewport);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(0f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.sizeDelta = new Vector2(209f, 400f);
        contentRoot.anchoredPosition = new Vector2(23f, -10f);

        scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = contentRoot;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 20f;
    }

    public void Configure(Action getMoreTapped, Action<string> collarTapped, Action<string> toyTapped)
    {
        onGetMoreTapped = getMoreTapped;
        onCollarTapped = collarTapped;
        onToyTapped = toyTapped;
    }

    public void RefreshRuntimeState(PawPalGameRuntime runtime)
    {
        if (runtime == null || contentRoot == null)
        {
            return;
        }

        string activeDogId = runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty;
        if (lastInventoryRevision == runtime.InventoryRevision && lastDogId == activeDogId)
        {
            return;
        }

        lastInventoryRevision = runtime.InventoryRevision;
        lastDogId = activeDogId;
        Rebuild(runtime);
    }

    public void ResetScrollPosition()
    {
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void Rebuild(PawPalGameRuntime runtime)
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        float currentY = 0f;
        currentY = BuildSection(runtime, PawPalItemCategory.Food, "Food", currentY);
        currentY = BuildSection(runtime, PawPalItemCategory.Toys, "Toys", currentY);
        currentY = BuildSection(runtime, PawPalItemCategory.Collars, "Collars & Leashes", currentY);

        contentRoot.sizeDelta = new Vector2(209f, Mathf.Max(340f, currentY + 16f));
        ResetScrollPosition();
    }

    private float BuildSection(PawPalGameRuntime runtime, PawPalItemCategory category, string title, float y)
    {
        BuildSectionHeader(contentRoot, title.Replace(" ", string.Empty) + "Header", title, y);
        y += 34f;

        List<PawPalCatalogItemDefinition> sectionItems = new List<PawPalCatalogItemDefinition>();
        for (int i = 0; i < runtime.CatalogItems.Count; i++)
        {
            PawPalCatalogItemDefinition item = runtime.CatalogItems[i];
            if (!item.AppearsInInventory || item.Category != category)
            {
                continue;
            }

            int quantity = runtime.GetItemQuantity(item.Id);
            if (quantity <= 0)
            {
                continue;
            }

            sectionItems.Add(item);
        }

        sectionItems.Sort(delegate(PawPalCatalogItemDefinition left, PawPalCatalogItemDefinition right)
        {
            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
        });

        int visualCount = Mathf.Max(1, sectionItems.Count) + 1;
        int rowCount = Mathf.CeilToInt(visualCount / 2f);

        RectTransform grid = UiFactory.CreateRect(title.Replace(" ", string.Empty) + "Grid", contentRoot);
        grid.anchorMin = new Vector2(0f, 1f);
        grid.anchorMax = new Vector2(0f, 1f);
        grid.pivot = new Vector2(0f, 1f);
        grid.sizeDelta = new Vector2(209f, rowCount * 104f);
        grid.anchoredPosition = new Vector2(0f, -y);

        int cardIndex = 0;
        for (int i = 0; i < sectionItems.Count; i++)
        {
            BuildInventoryItem(grid, runtime, sectionItems[i], cardIndex++);
        }

        BuildGetMoreItem(grid, cardIndex);
        y += grid.sizeDelta.y + 18f;
        return y;
    }

    private void BuildSectionHeader(RectTransform parent, string name, string title, float y)
    {
        Image header = UiFactory.CreateImage(name, parent, UiTheme.InventorySectionHeaderSprite, UiTheme.NavBrand);
        header.type = Image.Type.Sliced;
        header.preserveAspect = false;
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(0f, 1f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.sizeDelta = new Vector2(209f, 24f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -y);

        Shadow shadow = header.gameObject.AddComponent<Shadow>();
        shadow.effectColor = UiTheme.NavBrandDark;
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", header.rectTransform, title, 16, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void BuildInventoryItem(RectTransform parent, PawPalGameRuntime runtime, PawPalCatalogItemDefinition item, int cardIndex)
    {
        Vector2 cardPosition = GetCardPosition(cardIndex);
        RectTransform group = UiFactory.CreateRect(item.Id, parent);
        group.anchorMin = new Vector2(0f, 1f);
        group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.sizeDelta = new Vector2(92f, 92f);
        group.anchoredPosition = new Vector2(cardPosition.x, -cardPosition.y);

        Image card = UiFactory.CreateImage("Card", group, UiTheme.GetInventoryCardSprite(item.InventoryCardTheme), Color.white);
        card.type = Image.Type.Simple;
        card.preserveAspect = false;
        UiFactory.Stretch(card.rectTransform, 0f, 0f, 0f, 0f);

        bool equipped = item.Category == PawPalItemCategory.Collars && runtime.ActiveDog != null && runtime.GetEquippedCollarItemId(runtime.ActiveDog.Id) == item.Id;
        bool activeToy = item.Category == PawPalItemCategory.Toys && runtime.IsToyActiveInScene(item.Id);
        if (equipped || activeToy)
        {
            Image selectedBorder = UiFactory.CreateImage("SelectedBorder", group, UiTheme.InventoryCardOutlineSprite, UiTheme.NavBrand);
            selectedBorder.type = Image.Type.Sliced;
            selectedBorder.preserveAspect = false;
            UiFactory.Stretch(selectedBorder.rectTransform, 0f, 0f, 0f, 0f);
        }

        RectTransform imageRect = UiFactory.CreateRect("ItemImage", group);
        imageRect.anchorMin = new Vector2(0f, 1f);
        imageRect.anchorMax = new Vector2(0f, 1f);
        imageRect.pivot = new Vector2(0f, 1f);
        imageRect.sizeDelta = GetImageSize(item);
        Vector2 imagePos = GetImagePosition(item.InventoryCardTheme);
        imageRect.anchoredPosition = new Vector2(imagePos.x, -imagePos.y);

        Image itemImage = UiFactory.CreateImage("Image", imageRect, spriteLibrary.GetResourceSprite(item.InventorySpritePath), Color.white);
        itemImage.type = Image.Type.Simple;
        itemImage.preserveAspect = true;
        UiFactory.Stretch(itemImage.rectTransform, 0f, 0f, 0f, 0f);

        Image titleBar = UiFactory.CreateImage("TitleBar", group, UiTheme.InventoryItemTitleBarSprite, new Color32(236, 223, 200, 255));
        titleBar.type = Image.Type.Simple;
        titleBar.preserveAspect = false;
        titleBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleBar.rectTransform.pivot = new Vector2(0f, 1f);
        titleBar.rectTransform.sizeDelta = new Vector2(92f, 15f);
        titleBar.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", titleBar.rectTransform, item.DisplayName, 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

        if (item.Category == PawPalItemCategory.Food)
        {
            BuildQuantityBadge(group, runtime.GetItemQuantity(item.Id));
        }
        else if (equipped)
        {
            BuildStatusBadge(group, "E");
        }
        else if (activeToy)
        {
            BuildStatusBadge(group, "A");
        }

        if (item.Category == PawPalItemCategory.Toys && onToyTapped != null)
        {
            UiFactory.AddButton(group.gameObject, delegate
            {
                onToyTapped(item.Id);
            });
        }
        else if (item.Category == PawPalItemCategory.Collars && onCollarTapped != null)
        {
            UiFactory.AddButton(group.gameObject, delegate
            {
                onCollarTapped(item.Id);
            });
        }
    }

    private void BuildGetMoreItem(RectTransform parent, int cardIndex)
    {
        Vector2 cardPosition = GetCardPosition(cardIndex);
        RectTransform group = UiFactory.CreateRect("GetMore" + cardIndex, parent);
        group.anchorMin = new Vector2(0f, 1f);
        group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.sizeDelta = new Vector2(92f, 92f);
        group.anchoredPosition = new Vector2(cardPosition.x, -cardPosition.y);

        Image card = UiFactory.CreateImage("Card", group, UiTheme.GetInventoryCardSprite(InventoryCardTheme.GetMoreBlue), Color.white);
        card.type = Image.Type.Simple;
        card.preserveAspect = false;
        UiFactory.Stretch(card.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform imageRect = UiFactory.CreateRect("ItemImage", group);
        imageRect.anchorMin = new Vector2(0f, 1f);
        imageRect.anchorMax = new Vector2(0f, 1f);
        imageRect.pivot = new Vector2(0f, 1f);
        imageRect.sizeDelta = new Vector2(23.333f, 23.333f);
        imageRect.anchoredPosition = new Vector2(34.96f, -39.83f);

        Image itemImage = UiFactory.CreateImage("Image", imageRect, spriteLibrary.GetResourceSprite("UI/Figma/HomeInventory/get_more_plus"), Color.white);
        itemImage.type = Image.Type.Simple;
        itemImage.preserveAspect = true;
        UiFactory.Stretch(itemImage.rectTransform, 0f, 0f, 0f, 0f);

        Image titleBar = UiFactory.CreateImage("TitleBar", group, UiTheme.InventoryItemTitleBarSprite, CtaBlue);
        titleBar.type = Image.Type.Simple;
        titleBar.preserveAspect = false;
        titleBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleBar.rectTransform.pivot = new Vector2(0f, 1f);
        titleBar.rectTransform.sizeDelta = new Vector2(92f, 15f);
        titleBar.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", titleBar.rectTransform, "Get more!", 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

        if (onGetMoreTapped != null)
        {
            UiFactory.AddButton(group.gameObject, delegate
            {
                onGetMoreTapped();
            });
        }
    }

    private void BuildStatusBadge(RectTransform group, string text)
    {
        Image usedCircle = UiFactory.CreateImage("StatusCircle", group, UiTheme.CircleSprite, new Color32(50, 187, 255, 255));
        usedCircle.type = Image.Type.Simple;
        usedCircle.preserveAspect = false;
        usedCircle.rectTransform.anchorMin = new Vector2(0f, 1f);
        usedCircle.rectTransform.anchorMax = new Vector2(0f, 1f);
        usedCircle.rectTransform.pivot = new Vector2(0f, 1f);
        usedCircle.rectTransform.sizeDelta = new Vector2(19f, 19f);
        usedCircle.rectTransform.anchoredPosition = new Vector2(73.211f, -72f);

        TextMeshProUGUI usedText = UiFactory.CreateLabel("StatusText", group, text, 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        usedText.font = UiTheme.NavExtraBoldFont;
        usedText.rectTransform.anchorMin = new Vector2(0f, 1f);
        usedText.rectTransform.anchorMax = new Vector2(0f, 1f);
        usedText.rectTransform.pivot = new Vector2(0f, 1f);
        usedText.rectTransform.sizeDelta = new Vector2(19f, 19f);
        usedText.rectTransform.anchoredPosition = new Vector2(73.211f, -72f);
    }

    private void BuildQuantityBadge(RectTransform group, int quantity)
    {
        Image quantityCircle = UiFactory.CreateImage("QuantityCircle", group, UiTheme.CircleSprite, QuantityBlue);
        quantityCircle.type = Image.Type.Simple;
        quantityCircle.preserveAspect = false;
        quantityCircle.rectTransform.anchorMin = new Vector2(0f, 1f);
        quantityCircle.rectTransform.anchorMax = new Vector2(0f, 1f);
        quantityCircle.rectTransform.pivot = new Vector2(0f, 1f);
        quantityCircle.rectTransform.sizeDelta = new Vector2(25f, 25f);
        quantityCircle.rectTransform.anchoredPosition = new Vector2(63f, -64f);

        TextMeshProUGUI quantityText = UiFactory.CreateLabel("QuantityText", group, "x" + quantity, 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        quantityText.font = UiTheme.NavExtraBoldFont;
        quantityText.rectTransform.anchorMin = new Vector2(0f, 1f);
        quantityText.rectTransform.anchorMax = new Vector2(0f, 1f);
        quantityText.rectTransform.pivot = new Vector2(0f, 1f);
        quantityText.rectTransform.sizeDelta = new Vector2(25f, 25f);
        quantityText.rectTransform.anchoredPosition = new Vector2(63f, -64f);
    }

    private static void CreatePanelBorder(RectTransform root)
    {
        CreateBorder(root, "TopBorder", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f));
        CreateBorder(root, "BottomBorder", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
        CreateBorder(root, "LeftBorder", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f));
        CreateBorder(root, "RightBorder", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f));
    }

    private static void CreateBorder(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        Image border = UiFactory.CreateImage(name, parent, UiTheme.WhiteSprite, UiTheme.NavBrand);
        border.type = Image.Type.Simple;
        border.preserveAspect = false;
        border.rectTransform.anchorMin = anchorMin;
        border.rectTransform.anchorMax = anchorMax;
        border.rectTransform.pivot = pivot;
        border.rectTransform.sizeDelta = sizeDelta;
        border.rectTransform.anchoredPosition = Vector2.zero;
    }

    private static Vector2 GetCardPosition(int index)
    {
        int column = index % 2;
        int row = index / 2;
        return new Vector2(column == 0 ? 6f : 111f, row * 104f);
    }

    private static Vector2 GetImageSize(PawPalCatalogItemDefinition item)
    {
        switch (item.InventoryCardTheme)
        {
            case InventoryCardTheme.Bone:
                return item.Category == PawPalItemCategory.Food ? new Vector2(58f, 58f) : new Vector2(90f, 48f);
            case InventoryCardTheme.Ball:
                return new Vector2(66f, 67f);
            case InventoryCardTheme.Roll:
                return item.Category == PawPalItemCategory.Food ? new Vector2(58f, 58f) : new Vector2(58f, 57f);
            case InventoryCardTheme.Band:
                return new Vector2(72f, 47f);
            case InventoryCardTheme.Loop:
                return new Vector2(77f, 43f);
            case InventoryCardTheme.Charm:
                return new Vector2(77f, 52f);
            default:
                return new Vector2(23.333f, 23.333f);
        }
    }

    private static Vector2 GetImagePosition(InventoryCardTheme theme)
    {
        switch (theme)
        {
            case InventoryCardTheme.Bone:
                return new Vector2(0.422f, 28f);
            case InventoryCardTheme.Ball:
                return new Vector2(13f, 18f);
            case InventoryCardTheme.Roll:
                return new Vector2(17f, 23f);
            case InventoryCardTheme.Band:
                return new Vector2(10.06f, 25f);
            case InventoryCardTheme.Loop:
                return new Vector2(8.77f, 30.25f);
            case InventoryCardTheme.Charm:
                return new Vector2(8.77f, 25f);
            default:
                return new Vector2(34.96f, 39.83f);
        }
    }
}
