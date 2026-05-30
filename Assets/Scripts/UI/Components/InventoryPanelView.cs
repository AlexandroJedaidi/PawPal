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
    private static readonly Color32 CatalogDivider = new Color32(238, 219, 188, 255);
    private static readonly Color32 ItemCardBorder = new Color32(231, 215, 188, 255);
    private static readonly Color32 ItemCardShadow = new Color32(208, 188, 154, 255);
    private static readonly Color32 GetMoreFill = new Color32(180, 232, 255, 255);
    private static readonly Vector2 StandardItemImageSize = new Vector2(76f, 76f);
    private static readonly Vector2 StandardItemImagePosition = new Vector2(11f, 17f);
    private const float ContentWidth = 209f;
    private const float InventoryCardSize = 98f;
    private const float InventoryCardTitleHeight = 15f;
    private const float InventoryCardRowHeight = 110f;
    private const int RuntimeShapeSupersample = 4;

    private static readonly Dictionary<string, Sprite> RuntimeSpriteCache = new Dictionary<string, Sprite>();

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
        contentRoot.sizeDelta = new Vector2(ContentWidth, 400f);
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

        contentRoot.sizeDelta = new Vector2(ContentWidth, Mathf.Max(340f, currentY + 16f));
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
        grid.sizeDelta = new Vector2(ContentWidth, rowCount * InventoryCardRowHeight);
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
        RectTransform header = UiFactory.CreateRect(name, parent);
        SetTopLeft(header, 0f, y, ContentWidth, 24f);

        float labelWidth = Mathf.Clamp(title.Length * 8.5f + 16f, 54f, 126f);
        float labelX = Mathf.Round((ContentWidth - labelWidth) * 0.5f);
        float pawSize = 14f;
        float leftPawX = labelX - pawSize - 5f;
        float rightPawX = labelX + labelWidth + 5f;

        Image leftLine = UiFactory.CreateImage("LeftLine", header, UiTheme.WhiteSprite, CatalogDivider);
        leftLine.type = Image.Type.Simple;
        leftLine.preserveAspect = false;
        leftLine.raycastTarget = false;
        SetTopLeft(leftLine.rectTransform, 0f, 13f, Mathf.Max(0f, leftPawX - 6f), 1f);

        Image rightLine = UiFactory.CreateImage("RightLine", header, UiTheme.WhiteSprite, CatalogDivider);
        rightLine.type = Image.Type.Simple;
        rightLine.preserveAspect = false;
        rightLine.raycastTarget = false;
        float rightLineX = rightPawX + pawSize + 6f;
        SetTopLeft(rightLine.rectTransform, rightLineX, 13f, Mathf.Max(0f, ContentWidth - rightLineX), 1f);

        Image leftPaw = UiFactory.CreateImage("LeftPaw", header, spriteLibrary.GetIcon("icon_paw_brand"), UiTheme.NavBrand);
        leftPaw.type = Image.Type.Simple;
        leftPaw.preserveAspect = true;
        leftPaw.raycastTarget = false;
        SetTopLeft(leftPaw.rectTransform, leftPawX, 5f, pawSize, pawSize);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", header, title, 16, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 16f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        SetTopLeft(label.rectTransform, labelX, 0f, labelWidth, 24f);

        Image rightPaw = UiFactory.CreateImage("RightPaw", header, spriteLibrary.GetIcon("icon_paw_brand"), UiTheme.NavBrand);
        rightPaw.type = Image.Type.Simple;
        rightPaw.preserveAspect = true;
        rightPaw.raycastTarget = false;
        SetTopLeft(rightPaw.rectTransform, rightPawX, 5f, pawSize, pawSize);
    }

    private void BuildInventoryItem(RectTransform parent, PawPalGameRuntime runtime, PawPalCatalogItemDefinition item, int cardIndex)
    {
        Vector2 cardPosition = GetCardPosition(cardIndex);
        RectTransform group = UiFactory.CreateRect(item.Id, parent);
        group.anchorMin = new Vector2(0f, 1f);
        group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.sizeDelta = new Vector2(InventoryCardSize, InventoryCardSize);
        group.anchoredPosition = new Vector2(cardPosition.x, -cardPosition.y);

        BuildInventoryCardSurface(group, Color.white);

        Image titleBar = UiFactory.CreateImage("TitleBar", group, GetTopRoundedSprite(Mathf.RoundToInt(InventoryCardSize), Mathf.RoundToInt(InventoryCardTitleHeight), 10f), CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        titleBar.raycastTarget = false;
        SetTopLeft(titleBar.rectTransform, 0f, 0f, InventoryCardSize, InventoryCardTitleHeight);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", group, item.DisplayName, 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 12f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        SetTopLeft(label.rectTransform, 2f, 0f, InventoryCardSize - 4f, InventoryCardTitleHeight);

        bool equipped = item.Category == PawPalItemCategory.Collars && runtime.ActiveDog != null && runtime.GetEquippedCollarItemId(runtime.ActiveDog.Id) == item.Id;
        bool activeToy = item.Category == PawPalItemCategory.Toys && runtime.IsToyActiveInScene(item.Id);
        if (equipped || activeToy)
        {
            Image selectedBorder = UiFactory.CreateImage("SelectedBorder", group, GetRoundedRectSprite(Mathf.RoundToInt(InventoryCardSize), Mathf.RoundToInt(InventoryCardSize), 10f, 2f, false), UiTheme.NavBrand);
            selectedBorder.type = Image.Type.Sliced;
            selectedBorder.preserveAspect = false;
            selectedBorder.raycastTarget = false;
            UiFactory.Stretch(selectedBorder.rectTransform, 0f, 0f, 0f, 0f);
        }

        RectTransform imageRect = UiFactory.CreateRect("ItemImage", group);
        imageRect.anchorMin = new Vector2(0f, 1f);
        imageRect.anchorMax = new Vector2(0f, 1f);
        imageRect.pivot = new Vector2(0f, 1f);
        imageRect.sizeDelta = StandardItemImageSize;
        imageRect.anchoredPosition = new Vector2(StandardItemImagePosition.x, -StandardItemImagePosition.y);

        Image itemImage = UiFactory.CreateImage("Image", imageRect, spriteLibrary.GetResourceSprite(item.InventorySpritePath), Color.white);
        itemImage.type = Image.Type.Simple;
        itemImage.preserveAspect = true;
        UiFactory.Stretch(itemImage.rectTransform, 0f, 0f, 0f, 0f);

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
        group.sizeDelta = new Vector2(InventoryCardSize, InventoryCardSize);
        group.anchoredPosition = new Vector2(cardPosition.x, -cardPosition.y);

        BuildInventoryCardSurface(group, GetMoreFill);

        Image titleBar = UiFactory.CreateImage("TitleBar", group, GetTopRoundedSprite(Mathf.RoundToInt(InventoryCardSize), Mathf.RoundToInt(InventoryCardTitleHeight), 10f), CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        titleBar.raycastTarget = false;
        SetTopLeft(titleBar.rectTransform, 0f, 0f, InventoryCardSize, InventoryCardTitleHeight);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", group, "Get more!", 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 12f;
        SetTopLeft(label.rectTransform, 2f, 0f, InventoryCardSize - 4f, InventoryCardTitleHeight);

        RectTransform imageRect = UiFactory.CreateRect("ItemImage", group);
        imageRect.anchorMin = new Vector2(0f, 1f);
        imageRect.anchorMax = new Vector2(0f, 1f);
        imageRect.pivot = new Vector2(0f, 1f);
        imageRect.sizeDelta = new Vector2(23.333f, 23.333f);
        imageRect.anchoredPosition = new Vector2(37f, -40f);

        Image itemImage = UiFactory.CreateImage("Image", imageRect, spriteLibrary.GetResourceSprite("UI/Figma/HomeInventory/get_more_plus"), Color.white);
        itemImage.type = Image.Type.Simple;
        itemImage.preserveAspect = true;
        UiFactory.Stretch(itemImage.rectTransform, 0f, 0f, 0f, 0f);

        if (onGetMoreTapped != null)
        {
            UiFactory.AddButton(group.gameObject, delegate
            {
                onGetMoreTapped();
            });
        }
    }

    private void BuildInventoryCardSurface(RectTransform group, Color32 fillColor)
    {
        Image fill = UiFactory.CreateImage(
            "Field",
            group,
            GetRoundedRectSprite(Mathf.RoundToInt(InventoryCardSize), Mathf.RoundToInt(InventoryCardSize), 10f, 0f, true),
            fillColor);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        UiFactory.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);

        Shadow shadow = fill.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(ItemCardShadow.r / 255f, ItemCardShadow.g / 255f, ItemCardShadow.b / 255f, 0.75f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = false;

        Image border = UiFactory.CreateImage(
            "Frame",
            group,
            GetRoundedRectSprite(Mathf.RoundToInt(InventoryCardSize), Mathf.RoundToInt(InventoryCardSize), 10f, 1.25f, false),
            ItemCardBorder);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);
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
        usedCircle.rectTransform.anchoredPosition = new Vector2(InventoryCardSize - 24f, -(InventoryCardSize - 24f));

        TextMeshProUGUI usedText = UiFactory.CreateLabel("StatusText", group, text, 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        usedText.font = UiTheme.NavExtraBoldFont;
        usedText.rectTransform.anchorMin = new Vector2(0f, 1f);
        usedText.rectTransform.anchorMax = new Vector2(0f, 1f);
        usedText.rectTransform.pivot = new Vector2(0f, 1f);
        usedText.rectTransform.sizeDelta = new Vector2(19f, 19f);
        usedText.rectTransform.anchoredPosition = new Vector2(InventoryCardSize - 24f, -(InventoryCardSize - 24f));
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
        quantityCircle.rectTransform.anchoredPosition = new Vector2(InventoryCardSize - 31f, -(InventoryCardSize - 33f));

        TextMeshProUGUI quantityText = UiFactory.CreateLabel("QuantityText", group, "x" + quantity, 12, UiTheme.White, FontStyles.Normal, TextAlignmentOptions.Center);
        quantityText.font = UiTheme.NavExtraBoldFont;
        quantityText.rectTransform.anchorMin = new Vector2(0f, 1f);
        quantityText.rectTransform.anchorMax = new Vector2(0f, 1f);
        quantityText.rectTransform.pivot = new Vector2(0f, 1f);
        quantityText.rectTransform.sizeDelta = new Vector2(25f, 25f);
        quantityText.rectTransform.anchoredPosition = new Vector2(InventoryCardSize - 31f, -(InventoryCardSize - 33f));
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

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(SnapUi(width), SnapUi(height));
        rect.anchoredPosition = new Vector2(SnapUi(x), -SnapUi(y));
    }

    private static float SnapUi(float value)
    {
        return Mathf.Round(value);
    }

    private static Sprite GetRoundedRectSprite(int width, int height, float radius, float border, bool fill)
    {
        int shapeScale = GetShapeSupersample(width, height);
        string key = "inventory_rr_" + width + "_" + height + "_" + radius + "_" + border + "_" + fill + "_s" + shapeScale;
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        int textureWidth = Mathf.Max(1, width * shapeScale);
        int textureHeight = Mathf.Max(1, height * shapeScale);
        float scaledRadius = radius * shapeScale;
        float scaledBorder = border * shapeScale;
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32 transparent = new Color32(255, 255, 255, 0);

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, textureWidth, textureHeight, scaledRadius);
                bool borderPixel = scaledBorder > 0f && inside && !IsInsideRoundedRect(x, y, textureWidth, textureHeight, scaledRadius, scaledBorder);
                if (fill && inside)
                {
                    texture.SetPixel(x, y, UiTheme.White);
                }
                else if (!fill && borderPixel)
                {
                    texture.SetPixel(x, y, UiTheme.White);
                }
                else
                {
                    texture.SetPixel(x, y, transparent);
                }
            }
        }

        texture.Apply();
        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            100f * shapeScale,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(scaledRadius, scaledRadius, scaledRadius, scaledRadius));
        sprite.name = key;
        RuntimeSpriteCache[key] = sprite;
        return sprite;
    }

    private static Sprite GetTopRoundedSprite(int width, int height, float radius)
    {
        int shapeScale = GetShapeSupersample(width, height);
        string key = "inventory_top_" + width + "_" + height + "_" + radius + "_s" + shapeScale;
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        int textureWidth = Mathf.Max(1, width * shapeScale);
        int textureHeight = Mathf.Max(1, height * shapeScale);
        float scaledRadius = radius * shapeScale;
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32 transparent = new Color32(255, 255, 255, 0);

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                bool inside = IsInsideTopRoundedRect(x, y, textureWidth, textureHeight, scaledRadius);
                texture.SetPixel(x, y, inside ? UiTheme.White : transparent);
            }
        }

        texture.Apply();
        sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            100f * shapeScale,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(scaledRadius, scaledRadius, 0f, 0f));
        sprite.name = key;
        RuntimeSpriteCache[key] = sprite;
        return sprite;
    }

    private static int GetShapeSupersample(int width, int height)
    {
        int maxDimension = Mathf.Max(Mathf.Abs(width), Mathf.Abs(height));
        return maxDimension <= 160 ? RuntimeShapeSupersample : 2;
    }

    private static bool IsInsideRoundedRect(float x, float y, float width, float height, float radius, float inset)
    {
        float innerWidth = width - (inset * 2f);
        float innerHeight = height - (inset * 2f);
        if (innerWidth <= 0f || innerHeight <= 0f)
        {
            return false;
        }

        return IsInsideRoundedRect(x - inset, y - inset, innerWidth, innerHeight, Mathf.Max(0f, radius - inset));
    }

    private static bool IsInsideRoundedRect(float x, float y, float width, float height, float radius)
    {
        if (radius <= 0f)
        {
            return x >= 0f && x < width && y >= 0f && y < height;
        }

        float clampedX = Mathf.Clamp(x, radius, width - radius - 1f);
        float clampedY = Mathf.Clamp(y, radius, height - radius - 1f);
        return Vector2.Distance(new Vector2(x, y), new Vector2(clampedX, clampedY)) <= radius;
    }

    private static bool IsInsideTopRoundedRect(float x, float y, float width, float height, float radius)
    {
        int topY = Mathf.RoundToInt(height - 1f - y);
        if (x < radius && topY < radius)
        {
            Vector2 center = new Vector2(radius - 1f, radius - 1f);
            return Vector2.Distance(new Vector2(x, topY), center) <= radius;
        }

        if (x >= width - radius && topY < radius)
        {
            Vector2 center = new Vector2(width - radius, radius - 1f);
            return Vector2.Distance(new Vector2(x, topY), center) <= radius;
        }

        return true;
    }

    private static Vector2 GetCardPosition(int index)
    {
        int column = index % 2;
        int row = index / 2;
        return new Vector2(column == 0 ? 2f : 109f, row * InventoryCardRowHeight);
    }

}
