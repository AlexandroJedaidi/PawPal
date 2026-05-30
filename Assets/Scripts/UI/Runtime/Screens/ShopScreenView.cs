using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopScreenView : AppScreenViewBase
{
    private enum FloatingPanelState
    {
        None,
        BuyPoints
    }

    private enum ModalState
    {
        None,
        ItemDetails,
        ItemSuccess
    }

    private enum CardBadgeKind
    {
        None,
        Price,
        Owned,
        Empty
    }

    private enum CatalogScrollTarget
    {
        All,
        Breeds,
        Food,
        Toys,
        Accessories,
        Clothes,
        Beds
    }

    private sealed class CatalogCategoryTile
    {
        public CatalogScrollTarget Target;
        public Image Fill;
        public Image Border;
        public Image Icon;
        public TextMeshProUGUI Label;
        public string SelectedIconName;
        public string UnselectedIconName;
    }

    private const float ShopViewportTop = 162f;
    private const float CatalogTopOffset = 410f;
    private const float CatalogBottomPadding = 24f;

    private static readonly Color32 SupportFill = new Color32(241, 236, 226, 255);
    private static readonly Color32 SupportShadow = new Color32(236, 223, 200, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 CtaBlueDark = new Color32(0, 118, 177, 255);
    private static readonly Color32 GreyText = new Color32(122, 122, 122, 255);
    private static readonly Color32 ComingSoonGrey = new Color32(163, 163, 163, 255);
    private static readonly Color32 ComingSoonLight = new Color32(220, 220, 220, 255);
    private static readonly Color32 TransparentHit = new Color32(255, 255, 255, 1);
    private static readonly Color32 CatalogPanelFill = new Color32(255, 250, 239, 255);
    private static readonly Color32 CatalogPanelBorder = new Color32(238, 219, 188, 255);
    private static readonly Color32 CatalogDivider = new Color32(238, 219, 188, 255);
    private static readonly Color32 CategoryTileFill = new Color32(255, 252, 245, 255);
    private static readonly Color32 CategoryTileShadow = new Color32(229, 207, 178, 255);
    private static readonly Color32 ItemCardBorder = new Color32(231, 215, 188, 255);
    private static readonly Color32 ItemCardShadow = new Color32(208, 188, 154, 255);
    private static readonly Color32 OfferCardFill = new Color32(247, 241, 232, 255);
    private static readonly Color32 OfferCardBorder = new Color32(235, 221, 202, 255);
    private static readonly Color32 OfferCardShadow = new Color32(189, 162, 128, 58);
    private static readonly Color32 OfferAccent = new Color32(210, 112, 86, 255);
    private static readonly Color32 OfferTimerFill = new Color32(249, 232, 222, 255);
    private static readonly Color32 OfferBodyText = new Color32(70, 54, 46, 255);
    private static readonly Rect StandardShopImageRect = new Rect(15.5f, 15f, 84f, 84f);

    private static readonly Dictionary<string, Sprite> RuntimeSpriteCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, bool> ResourceSpriteExistsCache = new Dictionary<string, bool>();

    private RectTransform exactFrame;
    private RectTransform shopViewport;
    private RectTransform scrollContent;
    private ScrollRect shopScrollRect;
    private RectTransform floatingPanelRoot;
    private RectTransform buyPointsPanel;
    private RectTransform modalLayer;
    private Image modalBlocker;
    private RectTransform itemDetailsPanel;
    private RectTransform itemSuccessPanel;
    private RectTransform catalogSectionRoot;
    private TextMeshProUGUI basicCurrencyLabel;
    private TextMeshProUGUI premiumCurrencyLabel;

    private FloatingPanelState floatingPanelState;
    private ModalState modalState;
    private ResponsiveFigmaFrameLayout frameLayout;
    private CatalogScrollTarget selectedCatalogTarget = CatalogScrollTarget.All;
    private readonly Dictionary<CatalogScrollTarget, float> catalogScrollTargets = new Dictionary<CatalogScrollTarget, float>();
    private readonly List<CatalogCategoryTile> categoryTiles = new List<CatalogCategoryTile>();
    private int lastShopRevision = -1;
    private string selectedCatalogItemId = string.Empty;
    private string lastPurchasedCatalogItemId = string.Empty;

    protected override bool UseScreenContainer
    {
        get { return false; }
    }

    private void OnEnable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged += HandleRuntimeStateChanged;
        }

        RefreshRuntimeState();
    }

    private void OnDisable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged -= HandleRuntimeStateChanged;
        }
    }

    protected override void BuildContent()
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);
        BuildFullscreenBackground(root);

        exactFrame = UiFactory.CreateRect("ShopMainFrame", root);
        exactFrame.anchorMin = new Vector2(0.5f, 1f);
        exactFrame.anchorMax = new Vector2(0.5f, 1f);
        exactFrame.pivot = new Vector2(0.5f, 1f);
        exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        exactFrame.anchoredPosition = Vector2.zero;

        BuildBackground(exactFrame);
        BuildStorefront(exactFrame);
        BuildHeader(exactFrame);
        BuildScrollableContent(exactFrame);
        BuildFloatingPanels(exactFrame);
        BuildModals(exactFrame);
        RefreshState();
        RefreshRuntimeState();
    }

    private void BuildFullscreenBackground(RectTransform parent)
    {
        Image background = UiFactory.CreateImage(
            "FullscreenBackground",
            parent,
            sprites.GetResourceSprite("UI/Figma/Shop/background_shop"),
            Color.white);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.raycastTarget = false;
        UiFactory.Stretch(background.rectTransform, 0f, 0f, 0f, 0f);
    }

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            frameLayout = ResponsiveFigmaFrame.Apply(root, exactFrame);
        }

        ApplyViewportLayout();
    }

    private void BuildBackground(RectTransform parent)
    {
        Image background = UiFactory.CreateImage(
            "Background",
            parent,
            sprites.GetResourceSprite("UI/Figma/Shop/background_shop"),
            Color.white);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.raycastTarget = false;

        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(393f, 1131f);
        rect.anchoredPosition = Vector2.zero;
    }

    private void BuildStorefront(RectTransform parent)
    {
        RectTransform root = CreateNode("Storefront", parent, 6f, 0f, 381f, 126f);

        BuildAwningStripe(root, "ShadowLeft", 0f, 5f, 54.43287f, 93f, true, false, new Color32(243, 243, 243, 255));
        BuildAwningStripe(root, "ShadowMiddle1", 54.11935f, 5f, 54.43287f, 93f, false, false, new Color32(243, 243, 243, 255));
        BuildAwningStripe(root, "ShadowMiddle2", 108.23863f, 5f, 55.50019f, 93f, false, false, new Color32(243, 243, 243, 255));
        BuildAwningStripe(root, "ShadowMiddle3", 163.44034f, 5f, 54.43287f, 93f, false, false, new Color32(243, 243, 243, 255));
        BuildAwningStripe(root, "ShadowMiddle4", 217.55966f, 5f, 54.43287f, 93f, false, false, new Color32(243, 243, 243, 255));
        BuildAwningStripe(root, "ShadowMiddle5", 271.67896f, 5f, 54.43287f, 93f, false, false, new Color32(243, 243, 243, 255));
        BuildAwningStripe(root, "ShadowRight", 325.56714f, 5f, 54.43287f, 93f, false, true, new Color32(243, 243, 243, 255));

        BuildAwningStripe(root, "FrontLeft", 0f, 0f, 54.43287f, 93f, true, false, new Color32(255, 86, 86, 255));
        BuildAwningStripe(root, "FrontMiddle1", 54.11935f, 0f, 54.43287f, 93f, false, false, Color.white);
        BuildAwningStripe(root, "FrontMiddle2", 108.23863f, 0f, 55.50019f, 93f, false, false, new Color32(255, 86, 86, 255));
        BuildAwningStripe(root, "FrontMiddle3", 163.44034f, 0f, 54.43287f, 93f, false, false, Color.white);
        BuildAwningStripe(root, "FrontMiddle4", 217.55966f, 0f, 54.43287f, 93f, false, false, new Color32(255, 86, 86, 255));
        BuildAwningStripe(root, "FrontMiddle5", 271.67896f, 0f, 54.43287f, 93f, false, false, Color.white);
        BuildAwningStripe(root, "FrontRight", 325.56714f, 0f, 54.43287f, 93f, false, true, new Color32(255, 86, 86, 255));

        Image sign = UiFactory.CreateImage(
            "ShopSign",
            root,
            sprites.GetResourceSprite("UI/Figma/Shop/storefront_sign"),
            Color.white);
        sign.type = Image.Type.Simple;
        sign.preserveAspect = false;
        sign.raycastTarget = false;
        RectTransform signRect = sign.rectTransform;
        signRect.anchorMin = new Vector2(0f, 1f);
        signRect.anchorMax = new Vector2(0f, 1f);
        signRect.pivot = new Vector2(0f, 1f);
        signRect.sizeDelta = new Vector2(126f, 126f);
        signRect.anchoredPosition = new Vector2(125f, 0f);
    }

    private void BuildHeader(RectTransform parent)
    {
        RectTransform header = CreateNode("HeaderPoints", parent, 0f, 75f, 393f, 76f);

        Image currencyFrame = UiFactory.CreateImage("CurrencyFrame", header, GetRoundedRectSprite(73, 30, 10f, 1.5f, false), UiTheme.NavBrandDark);
        currencyFrame.type = Image.Type.Sliced;
        currencyFrame.preserveAspect = false;
        currencyFrame.rectTransform.anchorMin = new Vector2(0f, 1f);
        currencyFrame.rectTransform.anchorMax = new Vector2(0f, 1f);
        currencyFrame.rectTransform.pivot = new Vector2(0f, 1f);
        currencyFrame.rectTransform.sizeDelta = new Vector2(73f, 30f);
        currencyFrame.rectTransform.anchoredPosition = new Vector2(6f, -33f);

        basicCurrencyLabel = CreateText(header, "CurrencyText", "\u20B15,500", 16, UiTheme.NavBrandDark, UiTheme.NavMediumFont, TextAlignmentOptions.MidlineLeft);
        basicCurrencyLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        basicCurrencyLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        basicCurrencyLabel.rectTransform.pivot = new Vector2(0f, 1f);
        basicCurrencyLabel.rectTransform.sizeDelta = new Vector2(57f, 24f);
        basicCurrencyLabel.rectTransform.anchoredPosition = new Vector2(14f, -37f);

        RectTransform pointsButton = CreateNode("PointsButton", header, 302f, 21.5f, 85f, 53f);
        UiFactory.AddButton(pointsButton.gameObject, ToggleBuyPointsPanel);

        premiumCurrencyLabel = BuildBigPointsPill(pointsButton, 0f, 12f, 85f, 30f, "2200");

        RectTransform addRow = CreateNode("AddRow", pointsButton, 13f, 33f, 72f, 20f);
        Image plusCircle = UiFactory.CreateImage("PlusCircle", addRow, UiTheme.CircleSprite, new Color32(105, 247, 54, 255));
        plusCircle.type = Image.Type.Simple;
        plusCircle.preserveAspect = false;
        plusCircle.rectTransform.anchorMin = new Vector2(0f, 1f);
        plusCircle.rectTransform.anchorMax = new Vector2(0f, 1f);
        plusCircle.rectTransform.pivot = new Vector2(0f, 1f);
        plusCircle.rectTransform.sizeDelta = new Vector2(15f, 15f);
        plusCircle.rectTransform.anchoredPosition = new Vector2(56f, -1f);

        Image plusIcon = UiFactory.CreateImage("PlusIcon", addRow, sprites.GetIcon("icon_plus"), Color.white);
        plusIcon.type = Image.Type.Simple;
        plusIcon.preserveAspect = true;
        plusIcon.raycastTarget = false;
        plusIcon.rectTransform.anchorMin = new Vector2(0f, 1f);
        plusIcon.rectTransform.anchorMax = new Vector2(0f, 1f);
        plusIcon.rectTransform.pivot = new Vector2(0f, 1f);
        plusIcon.rectTransform.sizeDelta = new Vector2(17f, 17f);
        plusIcon.rectTransform.anchoredPosition = new Vector2(55f, 0f);
    }

    private void BuildScrollableContent(RectTransform parent)
    {
        shopViewport = CreateNode("ShopViewport", parent, 6f, ShopViewportTop, 382f, 627f);
        Image viewportHit = shopViewport.gameObject.AddComponent<Image>();
        viewportHit.color = new Color(1f, 1f, 1f, 0.002f);
        viewportHit.raycastTarget = true;
        shopViewport.gameObject.AddComponent<RectMask2D>();

        scrollContent = UiFactory.CreateRect("ScrollContent", shopViewport);
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(0f, 1f);
        scrollContent.pivot = new Vector2(0f, 1f);
        scrollContent.sizeDelta = new Vector2(382f, CatalogTopOffset + 1307f + CatalogBottomPadding);
        scrollContent.anchoredPosition = Vector2.zero;

        shopScrollRect = shopViewport.gameObject.AddComponent<ScrollRect>();
        shopScrollRect.viewport = shopViewport;
        shopScrollRect.content = scrollContent;
        shopScrollRect.horizontal = false;
        shopScrollRect.vertical = true;
        shopScrollRect.movementType = ScrollRect.MovementType.Clamped;
        shopScrollRect.scrollSensitivity = 22f;

        BuildSpecialOfferSection(scrollContent);
        catalogSectionRoot = CreateNode("CatalogSectionRoot", scrollContent, 0f, CatalogTopOffset, 382f, 1307f);
        RebuildCatalogSections();
        ApplyViewportLayout();
    }

    private void ApplyViewportLayout()
    {
        if (shopViewport == null)
        {
            return;
        }

        float frameHeight = frameLayout.VisibleLogicalHeight > 0f ? frameLayout.VisibleLogicalHeight : UiTheme.ReferenceContentHeight;
        float viewportTop = ShopViewportTop;
        float viewportHeight = Mathf.Max(220f, frameHeight - viewportTop);
        shopViewport.sizeDelta = new Vector2(382f, viewportHeight);
        shopViewport.anchoredPosition = new Vector2(6f, -viewportTop);
        UpdateScrollContentHeight();
    }

    private void BuildSpecialOfferSection(RectTransform parent)
    {
        BuildSpecialOfferHeader(parent);

        RectTransform primary = CreateNode("PrimaryOffer", parent, 0f, 76f, 382f, 152f);
        BuildPrimaryOffer(primary);

        RectTransform starter = CreateNode("StarterPackOffer", parent, 0f, 248f, 184f, 132f);
        BuildSpecialOfferCard(starter, "Starter Pack", "Essentials for your new pawfriend", "-20%", "240", "UI/Generated/ShopOffers/special_offer_starter_pack");

        RectTransform random = CreateNode("RandomOffer", parent, 198f, 248f, 184f, 132f);
        BuildRandomOfferCard(random);
    }

    private void BuildCategoriesAndItems(RectTransform parent)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        catalogScrollTargets.Clear();
        categoryTiles.Clear();

        RectTransform section = CreateNode("ItemsAll", parent, 0f, 0f, 382f, 1307f);
        BuildCatalogTitleHeader(section, "CategoriesHeader", 0f, 0f, 382f, 24f, "Categories");
        BuildCategoryRow(section);

        catalogScrollTargets[CatalogScrollTarget.All] = 0f;

        float currentY = 128f;
        currentY = BuildRuntimeProductSection(section, "BreedsSection", currentY, "Breeds", "icon_dog_white", new Rect(7f, 5f, 25f, 24f), PawPalItemCategory.Dogs, CatalogScrollTarget.Breeds);
        currentY = BuildRuntimeProductSection(section, "FoodSection", currentY, "Food", "icon_foodsupply_white", new Rect(5f, 3f, 29f, 29f), PawPalItemCategory.Food, CatalogScrollTarget.Food);
        currentY = BuildRuntimeProductSection(section, "ToysSection", currentY, "Toys", "icon_toys_white", new Rect(4f, 2f, 31f, 31f), PawPalItemCategory.Toys, CatalogScrollTarget.Toys);
        currentY = BuildRuntimeProductSection(section, "AccessoriesSection", currentY, "Accessories", "icon_collar_white", new Rect(3f, 1f, 33f, 33f), PawPalItemCategory.Collars, CatalogScrollTarget.Accessories);
        currentY = BuildRuntimeProductSection(section, "ClothesSection", currentY, "Clothes", "icon_clothing_white", new Rect(5f, 4f, 29f, 29f), PawPalItemCategory.Clothing, CatalogScrollTarget.Clothes);
        currentY = BuildRuntimeProductSection(section, "BedsSection", currentY, "Beds", "icon_dogbed_white", new Rect(4f, 3f, 31f, 31f), PawPalItemCategory.Furniture, CatalogScrollTarget.Beds);

        float catalogHeight = currentY + 8f;
        section.sizeDelta = new Vector2(382f, catalogHeight);
        parent.sizeDelta = new Vector2(382f, catalogHeight);
        RefreshCategoryTileSelection();
        UpdateScrollContentHeight();
    }

    private void BuildFloatingPanels(RectTransform parent)
    {
        floatingPanelRoot = UiFactory.CreateRect("FloatingPanelRoot", parent);
        UiFactory.Stretch(floatingPanelRoot, 0f, 0f, 0f, 0f);

        buyPointsPanel = CreateCenteredNode("BuyPointsPanel", floatingPanelRoot, 362f, 396.39624f, 0f);
        BuildBuyPointsPanel(buyPointsPanel);
    }

    private void BuildModals(RectTransform parent)
    {
        modalLayer = UiFactory.CreateRect("ModalLayer", parent);
        UiFactory.Stretch(modalLayer, 0f, 0f, 0f, 0f);

        modalBlocker = UiFactory.CreateImage("ModalBlocker", modalLayer, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        modalBlocker.type = Image.Type.Simple;
        modalBlocker.preserveAspect = false;
        modalBlocker.raycastTarget = true;
        UiFactory.Stretch(modalBlocker.rectTransform, 0f, 0f, 0f, 0f);

        itemDetailsPanel = CreateCenteredNode("ItemDetailsPanel", modalLayer, 314f, 398f, 0f);

        itemSuccessPanel = CreateCenteredNode("ItemSuccessPanel", modalLayer, 281f, 203f, 0f);
        RebuildModalPanels();
    }

    private void BuildPrimaryOffer(RectTransform parent)
    {
        CreateOfferCardSurface(parent, 382f, 152f, 10f);
        CreateDiscountBadge(parent, "-30%", 23f, 24f, 46f, 23f);

        TextMeshProUGUI title = CreateText(parent, "Title", "Husky Puppy", 20, UiTheme.BodyText, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, 23f, 58f, 150f, 26f);

        TextMeshProUGUI description = CreateText(parent, "Description", "Get an adorable Husky puppy and 4 coat variations!", 13, OfferBodyText, UiTheme.NavRegularFont, TextAlignmentOptions.TopLeft);
        description.textWrappingMode = TextWrappingModes.Normal;
        SetTopLeft(description.rectTransform, 23f, 91f, 146f, 42f);

        Image art = UiFactory.CreateImage("HeroArt", parent, sprites.GetResourceSprite("UI/Generated/ShopOffers/special_offer_husky_hero"), Color.white);
        art.type = Image.Type.Simple;
        art.preserveAspect = true;
        art.raycastTarget = false;
        SetTopLeft(art.rectTransform, 118f, 16f, 252f, 112f);

        CreatePointsPill(parent, "PrimaryPrice", 23f, 122f, 60f, 22f, "700");
        CreateOfferCardBorder(parent, 382f, 152f, 10f);
    }

    private void BuildSpecialOfferCard(
        RectTransform parent,
        string title,
        string description,
        string discount,
        string points,
        string artResource)
    {
        CreateOfferCardSurface(parent, 184f, 132f, 10f);
        CreateDiscountBadge(parent, discount, 15f, 19f, 38f, 21f);

        TextMeshProUGUI titleText = CreateText(parent, "Title", title, 15, UiTheme.BodyText, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        SetTopLeft(titleText.rectTransform, 15f, 53f, 84f, 20f);

        TextMeshProUGUI descriptionText = CreateText(parent, "Description", description, 11, OfferBodyText, UiTheme.NavRegularFont, TextAlignmentOptions.TopLeft);
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        SetTopLeft(descriptionText.rectTransform, 15f, 77f, 82f, 36f);

        Image art = UiFactory.CreateImage("Art", parent, sprites.GetResourceSprite(artResource), Color.white);
        art.type = Image.Type.Simple;
        art.preserveAspect = true;
        art.raycastTarget = false;
        SetTopLeft(art.rectTransform, 82f, 29f, 88f, 72f);

        CreatePointsPill(parent, "Price", 15f, 106f, 58f, 22f, points);
        CreateOfferCardBorder(parent, 184f, 132f, 10f);
    }

    private void BuildRandomOfferCard(RectTransform parent)
    {
        BuildSpecialOfferCard(parent, "Random Box", "A mystery box filled with fun surprises", "-10%", "180", "UI/Generated/ShopOffers/special_offer_random_box");
    }

    private void BuildSpecialOfferHeader(RectTransform parent)
    {
        TextMeshProUGUI title = CreateText(parent, "SpecialOfferTitle", "Special Offer", 22, UiTheme.BodyText, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        SetTopLeft(title.rectTransform, 12f, 0f, 142f, 30f);

        Image paw = UiFactory.CreateImage("SpecialOfferPaw", parent, sprites.GetIcon("icon_paw_brand"), OfferAccent);
        paw.type = Image.Type.Simple;
        paw.preserveAspect = true;
        paw.raycastTarget = false;
        SetTopLeft(paw.rectTransform, 154f, 5f, 18f, 18f);

        TextMeshProUGUI subtitle = CreateText(parent, "SpecialOfferSubtitle", "Limited time deals for your pawfriends", 13, OfferBodyText, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        SetTopLeft(subtitle.rectTransform, 12f, 32f, 245f, 20f);

        Image timerFill = UiFactory.CreateImage("SpecialOfferTimer", parent, GetRoundedRectSprite(100, 24, 12f, 0f, true), OfferTimerFill);
        timerFill.type = Image.Type.Sliced;
        timerFill.preserveAspect = false;
        timerFill.raycastTarget = false;
        SetTopLeft(timerFill.rectTransform, 274f, 4f, 100f, 24f);

        Image timerIcon = UiFactory.CreateImage("TimerIcon", parent, sprites.GetIcon("icon_clock"), OfferAccent);
        timerIcon.type = Image.Type.Simple;
        timerIcon.preserveAspect = true;
        timerIcon.raycastTarget = false;
        SetTopLeft(timerIcon.rectTransform, 282f, 9f, 14f, 14f);

        TextMeshProUGUI timerText = CreateText(parent, "TimerText", "22h 48m left", 12, OfferAccent, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        SetTopLeft(timerText.rectTransform, 300f, 6f, 68f, 20f);
    }

    private void CreateOfferCardSurface(RectTransform parent, float width, float height, float radius)
    {
        Image shadow = UiFactory.CreateImage(
            "Shadow",
            parent,
            GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), radius, 0f, true),
            OfferCardShadow);
        shadow.type = Image.Type.Sliced;
        shadow.preserveAspect = false;
        shadow.raycastTarget = false;
        SetTopLeft(shadow.rectTransform, 0f, 5f, width, height);

        Image fill = UiFactory.CreateImage(
            "Fill",
            parent,
            GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), radius, 0f, true),
            OfferCardFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);
    }

    private void CreateOfferCardBorder(RectTransform parent, float width, float height, float radius)
    {
        Image border = UiFactory.CreateImage(
            "Border",
            parent,
            GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), radius, 1f, false),
            OfferCardBorder);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, height);
    }

    private void CreateDiscountBadge(RectTransform parent, string discount, float x, float y, float width, float height)
    {
        Image badge = UiFactory.CreateImage(
            "DiscountBadge",
            parent,
            GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 6f, 0f, true),
            OfferAccent);
        badge.type = Image.Type.Sliced;
        badge.preserveAspect = false;
        badge.raycastTarget = false;
        SetTopLeft(badge.rectTransform, x, y, width, height);

        TextMeshProUGUI label = CreateText(parent, "DiscountText", discount, 12, Color.white, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(label.rectTransform, x, y, width, height);
    }

    private void BuildCatalogTitleHeader(RectTransform parent, string name, float x, float y, float width, float height, string title)
    {
        RectTransform header = CreateNode(name, parent, x, y, width, height);

        Image leftLine = UiFactory.CreateImage("LeftLine", header, UiTheme.WhiteSprite, CatalogDivider);
        leftLine.type = Image.Type.Simple;
        leftLine.preserveAspect = false;
        leftLine.raycastTarget = false;
        SetTopLeft(leftLine.rectTransform, 24f, 13f, 94f, 1f);

        Image rightLine = UiFactory.CreateImage("RightLine", header, UiTheme.WhiteSprite, CatalogDivider);
        rightLine.type = Image.Type.Simple;
        rightLine.preserveAspect = false;
        rightLine.raycastTarget = false;
        SetTopLeft(rightLine.rectTransform, width - 118f, 13f, 94f, 1f);

        Image leftPaw = UiFactory.CreateImage("LeftPaw", header, sprites.GetIcon("icon_paw_brand"), UiTheme.NavBrand);
        leftPaw.type = Image.Type.Simple;
        leftPaw.preserveAspect = true;
        leftPaw.raycastTarget = false;
        SetTopLeft(leftPaw.rectTransform, 126f, 5f, 16f, 16f);

        TextMeshProUGUI label = CreateText(header, "Label", title, 16, UiTheme.NavBrandDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(label.rectTransform, 146f, 0f, 90f, height);

        Image rightPaw = UiFactory.CreateImage("RightPaw", header, sprites.GetIcon("icon_paw_brand"), UiTheme.NavBrand);
        rightPaw.type = Image.Type.Simple;
        rightPaw.preserveAspect = true;
        rightPaw.raycastTarget = false;
        SetTopLeft(rightPaw.rectTransform, 240f, 5f, 16f, 16f);
    }

    private void BuildCategoryRow(RectTransform parent)
    {
        RectTransform row = CreateNode("CategoriesRow", parent, 0f, 38f, 382f, 76f);
        const float tileWidth = 57f;
        const float tileGap = 6f;
        const float startX = 5f;

        BuildCategoryButton(row, "All", CatalogScrollTarget.All, startX + (tileWidth + tileGap) * 0f, tileWidth, "All", "icon_dog_white", "icon_dog_brand", new Rect(14f, 9f, 29f, 27f));
        BuildCategoryButton(row, "Food", CatalogScrollTarget.Food, startX + (tileWidth + tileGap) * 1f, tileWidth, "Food", "icon_foodsupply_white", "icon_foodsupply_brand", new Rect(13f, 7f, 31f, 31f));
        BuildCategoryButton(row, "Toys", CatalogScrollTarget.Toys, startX + (tileWidth + tileGap) * 2f, tileWidth, "Toys", "icon_toys_white", "icon_toys_brand", new Rect(12f, 6f, 33f, 33f));
        BuildCategoryButton(row, "Accessories", CatalogScrollTarget.Accessories, startX + (tileWidth + tileGap) * 3f, tileWidth, "Accessories", "icon_collar_white", "icon_collar_brand", new Rect(11f, 4f, 35f, 35f));
        BuildCategoryButton(row, "Clothes", CatalogScrollTarget.Clothes, startX + (tileWidth + tileGap) * 4f, tileWidth, "Clothes", "icon_clothing_white", "icon_clothing_brand", new Rect(13f, 8f, 31f, 31f));
        BuildCategoryButton(row, "Beds", CatalogScrollTarget.Beds, startX + (tileWidth + tileGap) * 5f, tileWidth, "Beds", "icon_dogbed_white", "icon_dogbed_brand", new Rect(12f, 7f, 33f, 33f));
    }

    private void BuildProductSection(
        RectTransform parent,
        string name,
        float x,
        float y,
        float headerWidth,
        string title,
        string headerIcon,
        float iconSize,
        Rect iconRect,
        float listWidth,
        float listHeight,
        Action<RectTransform> buildItems)
    {
        RectTransform section = CreateNode(name, parent, x, y, listWidth, listHeight + 13f);
        RectTransform header = BuildSectionHeader(section, "Header", 0f, 0f, headerWidth, 28f, title, headerIcon, iconSize, 15, UiTheme.NavExtraBoldFont, iconRect);

        RectTransform list = CreateNode("List", section, 0f, 13f, listWidth, listHeight);
        BuildListPanel(list, listWidth, listHeight);
        buildItems(list);
        header.SetAsLastSibling();
    }

    private void RebuildCatalogSections()
    {
        if (catalogSectionRoot == null)
        {
            return;
        }

        ClearChildren(catalogSectionRoot);
        BuildCategoriesAndItems(catalogSectionRoot);
    }

    private float BuildRuntimeProductSection(
        RectTransform parent,
        string name,
        float y,
        string title,
        string headerIcon,
        Rect iconRect,
        PawPalItemCategory category,
        CatalogScrollTarget scrollTarget)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return y;
        }

        List<PawPalCatalogItemDefinition> items = new List<PawPalCatalogItemDefinition>();
        IReadOnlyList<PawPalCatalogItemDefinition> categoryItems = runtime.GetCatalogItems(category);
        for (int i = 0; i < categoryItems.Count; i++)
        {
            items.Add(categoryItems[i]);
        }

        catalogScrollTargets[scrollTarget] = y;

        float listWidth = 382f;
        float listHeight = GetRuntimeProductListHeight(items.Count);
        RectTransform section = CreateNode(name, parent, 0f, y, listWidth, listHeight + 18f);

        RectTransform list = CreateNode("List", section, 0f, 18f, listWidth, listHeight);
        BuildListPanel(list, listWidth, listHeight);
        BuildRuntimeCategoryCards(list, items, category);
        RectTransform header = BuildCatalogSectionHeader(section, "Header", title, headerIcon, iconRect);
        header.SetAsLastSibling();
        return y + listHeight + 36f;
    }

    private static float GetRuntimeProductListHeight(int itemCount)
    {
        int rowCount = Mathf.Max(1, Mathf.CeilToInt(itemCount / 3f));
        return 20f + rowCount * 115f + Mathf.Max(0, rowCount - 1) * 7f + 12f;
    }

    private void BuildRuntimeCategoryCards(RectTransform list, List<PawPalCatalogItemDefinition> items, PawPalItemCategory category)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Vector2 position = GetShopCardPosition(category, i);
            if (position.x < 0f)
            {
                continue;
            }

            BuildCatalogProductCard(list, items[i], position.x, position.y, category, i);
        }
    }

    private void BuildCatalogProductCard(RectTransform list, PawPalCatalogItemDefinition item, float x, float y, PawPalItemCategory category, int index)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        CardBadgeKind badgeKind;
        string badgeText;
        float badgeWidth;
        Action onClick = null;

        if (item.DisabledInShop)
        {
            badgeKind = CardBadgeKind.Owned;
            badgeText = category == PawPalItemCategory.Dogs ? "Later" : "Soon";
            badgeWidth = 51f;
        }
        else if (!item.CanPurchaseMultiple && runtime.IsItemOwned(item.Id))
        {
            badgeKind = CardBadgeKind.Owned;
            badgeText = "Owned";
            badgeWidth = 51f;
        }
        else
        {
            badgeKind = CardBadgeKind.Price;
            badgeText = item.Price.ToString();
            badgeWidth = 54f;
            onClick = delegate
            {
                ShowItemDetails(item.Id);
            };
        }

        BuildProductCard(
            list,
            item.Id + "Card",
            x,
            y,
            item.DisplayName,
            GetFieldSpriteForItem(item),
            GetShopSpritePath(item),
            GetShopImageRect(item, category, index),
            badgeKind,
            badgeText,
            badgeWidth,
            CtaBlue,
            Color.white,
            onClick);
    }

    private void BuildListPanel(RectTransform parent, float width, float height)
    {
        Image fill = UiFactory.CreateImage("Fill", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 12f, 0f, true), CatalogPanelFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);
        Shadow fillShadow = fill.gameObject.AddComponent<Shadow>();
        fillShadow.effectColor = new Color(SupportShadow.r / 255f, SupportShadow.g / 255f, SupportShadow.b / 255f, 1f);
        fillShadow.effectDistance = new Vector2(0f, -2f);
        fillShadow.useGraphicAlpha = false;

        Image border = UiFactory.CreateImage("Border", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 12f, 1.5f, false), CatalogPanelBorder);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, height);
    }

    private void BuildProductCard(
        RectTransform parent,
        string name,
        float x,
        float y,
        string title,
        Sprite fieldSprite,
        string imageResource,
        Rect imageRect,
        CardBadgeKind badgeKind,
        string badgeText,
        float badgeWidth,
        Color badgeBarColor,
        Color titleTextColor,
        Action onClick)
    {
        RectTransform card = CreateNode(name, parent, x, y, 115f, 115f);
        if (onClick != null)
        {
            UiFactory.AddButton(card.gameObject, delegate
            {
                floatingPanelState = FloatingPanelState.None;
                onClick();
            });
        }

        BuildItemCardSurface(card);

        Image titleBar = UiFactory.CreateImage("TitleBar", card, GetTopRoundedSprite(115, 15, 10f), badgeBarColor);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        titleBar.raycastTarget = false;
        SetTopLeft(titleBar.rectTransform, 0f, 0f, 115f, 15f);

        TextMeshProUGUI titleLabel = CreateText(card, "Title", title, 12, titleTextColor, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 8f;
        titleLabel.fontSizeMax = 12f;
        SetTopLeft(titleLabel.rectTransform, 0f, 0f, 115f, 15f);

        if (!string.IsNullOrEmpty(imageResource))
        {
            Image art = UiFactory.CreateImage("Art", card, sprites.GetResourceSprite(imageResource), Color.white);
            art.type = Image.Type.Simple;
            art.preserveAspect = true;
            art.raycastTarget = false;
            SetTopLeft(art.rectTransform, imageRect.x, imageRect.y, imageRect.width, imageRect.height);
        }

        switch (badgeKind)
        {
            case CardBadgeKind.Price:
                CreatePointsPill(card, "Price", (115f - badgeWidth) * 0.5f, 91f, badgeWidth, 22f, badgeText);
                break;
            case CardBadgeKind.Owned:
                CreateOwnedPill(card, "Owned", 29f, 92f, 57f, 20f, badgeText);
                break;
            case CardBadgeKind.Empty:
                CreateEmptyPill(card, "Empty", 33f, 91f, badgeWidth, 20f);
                break;
        }
    }

    private void BuildItemCardSurface(RectTransform card)
    {
        Image fill = UiFactory.CreateImage("Field", card, GetRoundedRectSprite(115, 115, 10f, 0f, true), Color.white);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, 115f, 115f);

        Shadow shadow = fill.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(ItemCardShadow.r / 255f, ItemCardShadow.g / 255f, ItemCardShadow.b / 255f, 0.75f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = false;

        Image border = UiFactory.CreateImage("Frame", card, GetRoundedRectSprite(115, 115, 10f, 1.25f, false), ItemCardBorder);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, 115f, 115f);
    }

    private void BuildBuyPointsPanel(RectTransform parent)
    {
        Image hitArea = parent.gameObject.AddComponent<Image>();
        hitArea.sprite = UiTheme.WhiteSprite;
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        hitArea.color = new Color(1f, 1f, 1f, 0.002f);
        hitArea.raycastTarget = true;

        BuildPanelShell(parent, 362f, 396.39624f, 10f, 2f, false);
        BuildSectionHeader(parent, "BuyPointsHeader", 10f, 20f, 342f, 24f, "Buy PawPoints", null, 0f, 16, UiTheme.NavExtraBoldFont);
        CreateCloseButton(parent, "CloseBuyPoints", 331f, 20f, 21f, 21f, delegate
        {
            floatingPanelState = FloatingPanelState.None;
            RefreshState();
        });

        RectTransform packages = CreateNode("Packages", parent, 10f, 64f, 342f, 312.39624f);
        BuildPointsPackage(packages, "Package200", 11.5f, 16.79874f, "200", "1.99\u20AC", null);
        BuildPointsPackage(packages, "Package500", 177.5f, 0f, "500", "4.49\u20AC", "10% saved");
        BuildPointsPackage(packages, "Package1000", 11.5f, 112.79874f, "1000", "8.99\u20AC", "10% saved");
        BuildPointsPackage(packages, "Package2500", 177.5f, 112.79874f, "2500", "19.99\u20AC", "20% saved");
        BuildPointsPackage(packages, "Package3500", 11.5f, 225.59749f, "3500", "25.99\u20AC", "25% saved");
        BuildPointsPackage(packages, "Package5000", 177.5f, 225.59749f, "5000", "34.99\u20AC", "30% saved");
    }

    private void BuildPointsPackage(RectTransform parent, string name, float x, float y, string points, string cost, string discount)
    {
        RectTransform root = CreateNode(name, parent, x, y, 153f, discount == null ? 70f : 86.79874f);
        RectTransform ribbonRoot = null;

        if (!string.IsNullOrEmpty(discount))
        {
            ribbonRoot = UiFactory.CreateRect("Discount", root);
            ribbonRoot.anchorMin = new Vector2(0f, 1f);
            ribbonRoot.anchorMax = new Vector2(0f, 1f);
            ribbonRoot.pivot = new Vector2(0.5f, 0.5f);
            ribbonRoot.sizeDelta = new Vector2(81.59387f, 33.79875f);
            ribbonRoot.anchoredPosition = new Vector2(115.83458f, -16.89937f);
            ribbonRoot.localRotation = Quaternion.Euler(0f, 0f, 11.12f);
            Image ribbon = UiFactory.CreateImage("Ribbon", ribbonRoot, GetRoundedRectSprite(79, 19, 0f, 0f, true), UiTheme.NavBrand);
            ribbon.type = Image.Type.Simple;
            ribbon.preserveAspect = false;
            ribbon.raycastTarget = false;
            SetTopLeft(ribbon.rectTransform, 1f, 7.48737f, 79.455f, 18.824f);
            TextMeshProUGUI ribbonText = CreateText(ribbonRoot, "Text", discount, 14, Color.white, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
            SetTopLeft(ribbonText.rectTransform, 2.22749f, -1.08813f, 77.64242f, 35.07465f);
        }

        RectTransform card = CreateNode("Card", root, 0f, discount == null ? 0f : 16.79874f, 153f, 70f);
        Image fill = UiFactory.CreateImage("Fill", card, GetRoundedRectSprite(153, 70, 5f, 0f, true), SupportFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, 153f, 70f);
        Shadow shadow = fill.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(SupportShadow.r / 255f, SupportShadow.g / 255f, SupportShadow.b / 255f, 1f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = false;

        Image border = UiFactory.CreateImage("Border", card, GetRoundedRectSprite(153, 70, 5f, 1f, false), UiTheme.NavBrand);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, 153f, 70f);

        CreatePackageCostBlock(card, 10f, 10f, points, cost);
        CreateActionButton(card, "BuyButton", "Buy", 98f, 23f, 45f, 24f, true, delegate
        {
            PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
            if (runtime != null)
            {
                runtime.PurchasePointPackage("points_" + points);
            }
        });

        if (ribbonRoot != null)
        {
            ribbonRoot.SetAsLastSibling();
        }
    }

    private void BuildItemDetailsPanel(RectTransform parent)
    {
        ClearChildren(parent);
        BuildPanelShell(parent, 314f, 398f, 10f, 2f, true);

        PawPalCatalogItemDefinition item = GetSelectedCatalogItem();
        if (item == null)
        {
            return;
        }

        BuildItemPreview(parent, item);

        RectTransform header = CreateNode("DetailsHeader", parent, 11.5f, 168f, 291f, 21f);
        Image line = UiFactory.CreateImage("Line", header, GetHorizontalLineSprite(291, 1), UiTheme.NavBrand);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.raycastTarget = false;
        SetTopLeft(line.rectTransform, 0f, 13f, 291f, 1f);

        TextMeshProUGUI details = CreateText(header, "DetailsText", "Details", 14, UiTheme.NavBrand, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(details.rectTransform, 90.40776f, 0f, 109.24272f, 21f);

        RectTransform itemFrame = CreateNode("ItemFrame", parent, 10f, 195f, 294f, 115f);
        BuildProductCard(itemFrame, item.Id + "MiniCard", 0f, 0f, item.DisplayName, GetFieldSpriteForItem(item), GetShopSpritePath(item), GetShopImageRect(item, item.Category, 0), GetModalBadgeKind(item), GetModalBadgeText(item), GetModalBadgeWidth(item), CtaBlue, Color.white, null);

        RectTransform textFrame = CreateNode("DescriptionFrame", itemFrame, 117f, 0f, 177f, 115f);
        TextMeshProUGUI description = CreateText(textFrame, "Description", BuildDetailsDescription(item), 15, Color.black, UiTheme.NavRegularFont, TextAlignmentOptions.MidlineLeft);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;
        SetTopLeft(description.rectTransform, 10f, 10f, 157f, 95f);

        RectTransform purchase = CreateNode("Purchase", parent, 77f, 316f, 160f, 77f);
        TextMeshProUGUI confirm = CreateText(purchase, "Confirm", GetDetailsPrompt(item), 15, UiTheme.NavBrand, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(confirm.rectTransform, 10f, 10.5f, 140f, 22f);

        CreateActionButton(purchase, "Cancel", "Cancel", 9.5f, 42.5f, 69f, 24f, false, delegate
        {
            modalState = ModalState.None;
            RebuildModalPanels();
            RefreshState();
        });
        bool canBuy = CanBuySelectedItem();
        CreateActionButton(purchase, "Buy", canBuy ? "Buy" : "Off", 105.5f, 42.5f, 45f, 24f, canBuy, delegate
        {
            if (!canBuy)
            {
                return;
            }

            PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
            if (runtime != null && runtime.TryPurchaseItem(item.Id))
            {
                lastPurchasedCatalogItemId = item.Id;
                modalState = ModalState.ItemSuccess;
                RebuildModalPanels();
                RefreshState();
            }
        });
    }

    private void BuildItemSuccessPanel(RectTransform parent)
    {
        ClearChildren(parent);
        BuildPanelShell(parent, 281f, 203f, 10f, 2f, false);

        TextMeshProUGUI success = CreateText(parent, "SuccessText", "Your purchase was successful!", 16, UiTheme.NavBrand, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(success.rectTransform, 15f, 10f, 251f, 24f);

        BuildSuccessItemCard(parent, 83f, 44f);

        CreateActionButton(parent, "Continue", "Continue", 96.5f, 169f, 88f, 24f, true, delegate
        {
            modalState = ModalState.None;
            RebuildModalPanels();
            RefreshState();
        });
    }

    private void BuildItemPreview(RectTransform parent, PawPalCatalogItemDefinition item)
    {
        RectTransform previewRoot = CreateNode("ItemPreview", parent, 10f, 8f, 294f, 154f);

        Image fill = UiFactory.CreateImage("PreviewFill", previewRoot, GetRoundedRectSprite(294, 154, 8f, 0f, true), SupportFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, 294f, 154f);

        Image border = UiFactory.CreateImage("PreviewBorder", previewRoot, GetRoundedRectSprite(294, 154, 8f, 1f, false), UiTheme.NavBrand);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, 294f, 154f);

        RectTransform previewContent = CreateNode("PreviewContent", previewRoot, 8f, 6f, 278f, 142f);
        ShopItem3DPreviewView previewView = previewContent.gameObject.AddComponent<ShopItem3DPreviewView>();
        previewView.Initialize(sprites);
        previewView.ShowItem(item);
    }

    private void BuildSuccessItemCard(RectTransform parent, float x, float y)
    {
        PawPalCatalogItemDefinition item = GetLastPurchasedCatalogItem();
        if (item == null)
        {
            return;
        }

        RectTransform card = CreateNode("SuccessItem", parent, x, y, 115f, 115f);
        BuildItemCardSurface(card);

        Image titleBar = UiFactory.CreateImage("TitleBar", card, GetTopRoundedSprite(115, 15, 10f), CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        titleBar.raycastTarget = false;
        SetTopLeft(titleBar.rectTransform, 0f, 0f, 115f, 15f);

        TextMeshProUGUI title = CreateText(card, "Title", item.DisplayName, 12, Color.white, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        title.enableAutoSizing = true;
        title.fontSizeMin = 8f;
        title.fontSizeMax = 12f;
        SetTopLeft(title.rectTransform, 0f, 0f, 115f, 15f);

        Image art = UiFactory.CreateImage("Art", card, sprites.GetResourceSprite(GetSuccessSpritePath(item)), Color.white);
        art.type = Image.Type.Simple;
        art.preserveAspect = true;
        art.raycastTarget = false;
        SetTopLeft(art.rectTransform, StandardShopImageRect.x, StandardShopImageRect.y, StandardShopImageRect.width, StandardShopImageRect.height);
    }

    private void BuildPanelShell(RectTransform parent, float width, float height, float radius, float border, bool shadow)
    {
        Image fill = UiFactory.CreateImage("PanelFill", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), radius, 0f, true), UiTheme.NavBackgroundCream);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);

        if (shadow)
        {
            Shadow panelShadow = fill.gameObject.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            panelShadow.effectDistance = new Vector2(0f, -1f);
            panelShadow.useGraphicAlpha = false;
        }

        Image borderImage = UiFactory.CreateImage("PanelBorder", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), radius, border, false), UiTheme.NavBrand);
        borderImage.type = Image.Type.Sliced;
        borderImage.preserveAspect = false;
        borderImage.raycastTarget = false;
        SetTopLeft(borderImage.rectTransform, 0f, 0f, width, height);
    }

    private RectTransform BuildSectionHeader(
        RectTransform parent,
        string name,
        float x,
        float y,
        float width,
        float height,
        string title,
        string iconName,
        float iconSize,
        int fontSize,
        TMP_FontAsset font,
        Rect? iconRect = null)
    {
        RectTransform header = CreateNode(name, parent, x, y, width, height);
        Image fill = UiFactory.CreateImage("Fill", header, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 5f, 0f, true), UiTheme.NavBrand);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);
        Shadow shadow = fill.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(UiTheme.NavBrandDark.r / 255f, UiTheme.NavBrandDark.g / 255f, UiTheme.NavBrandDark.b / 255f, 1f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = false;

        float textX = 0f;
        float textWidth = width;
        if (!string.IsNullOrEmpty(iconName))
        {
            Rect actualIcon = iconRect ?? new Rect(6f, (height - iconSize) * 0.5f, iconSize, iconSize);
            Image icon = UiFactory.CreateImage("Icon", header, sprites.GetIcon(iconName), Color.white);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetTopLeft(icon.rectTransform, actualIcon.x, actualIcon.y, actualIcon.width, actualIcon.height);
            textX = actualIcon.x + actualIcon.width + 5f;
            textWidth = width - textX - 5f;
        }

        TextMeshProUGUI label = CreateText(header, "Label", title, fontSize, Color.white, font, TextAlignmentOptions.Center);
        SetTopLeft(label.rectTransform, textX, 0f, textWidth, height);
        return header;
    }

    private RectTransform BuildCatalogSectionHeader(RectTransform parent, string name, string title, string iconName, Rect iconRect)
    {
        RectTransform header = CreateNode(name, parent, 16f, 0f, 240f, 40f);

        Image badge = UiFactory.CreateImage("Badge", header, UiTheme.CircleSprite, UiTheme.NavBrand);
        badge.type = Image.Type.Simple;
        badge.preserveAspect = false;
        badge.raycastTarget = false;
        SetTopLeft(badge.rectTransform, 0f, 0f, 38f, 38f);

        Image icon = UiFactory.CreateImage("Icon", header, sprites.GetIcon(iconName), Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetTopLeft(icon.rectTransform, iconRect.x, iconRect.y, iconRect.width, iconRect.height);

        TextMeshProUGUI label = CreateText(header, "Label", title, 16, UiTheme.NavBrandDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        SetTopLeft(label.rectTransform, 50f, 6f, 180f, 25f);
        return header;
    }

    private void BuildCategoryButton(
        RectTransform parent,
        string name,
        CatalogScrollTarget target,
        float x,
        float width,
        string labelText,
        string selectedIconName,
        string unselectedIconName,
        Rect iconRect)
    {
        RectTransform buttonRoot = CreateNode(name, parent, x, 0f, width, 74f);

        Image fill = UiFactory.CreateImage("Fill", buttonRoot, GetRoundedRectSprite(Mathf.RoundToInt(width), 66, 10f, 0f, true), CategoryTileFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, 66f);
        Shadow shadow = fill.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(CategoryTileShadow.r / 255f, CategoryTileShadow.g / 255f, CategoryTileShadow.b / 255f, 1f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = false;

        Image border = UiFactory.CreateImage("Border", buttonRoot, GetRoundedRectSprite(Mathf.RoundToInt(width), 66, 10f, 1.4f, false), CatalogPanelBorder);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, 66f);

        Image icon = UiFactory.CreateImage("Icon", buttonRoot, sprites.GetIcon(unselectedIconName), Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetTopLeft(icon.rectTransform, iconRect.x, iconRect.y, iconRect.width, iconRect.height);

        TextMeshProUGUI label = CreateText(buttonRoot, "Label", labelText, 11, UiTheme.NavBrandDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 11f;
        SetTopLeft(label.rectTransform, 3f, 47f, width - 6f, 17f);

        categoryTiles.Add(new CatalogCategoryTile
        {
            Target = target,
            Fill = fill,
            Border = border,
            Icon = icon,
            Label = label,
            SelectedIconName = selectedIconName,
            UnselectedIconName = unselectedIconName
        });

        UiFactory.AddButton(buttonRoot.gameObject, delegate
        {
            ScrollToCatalogTarget(target);
        });
    }

    private void RefreshCategoryTileSelection()
    {
        for (int i = 0; i < categoryTiles.Count; i++)
        {
            CatalogCategoryTile tile = categoryTiles[i];
            if (tile == null || tile.Fill == null || tile.Border == null || tile.Icon == null || tile.Label == null)
            {
                continue;
            }

            bool selected = tile.Target == selectedCatalogTarget;
            tile.Fill.color = selected ? UiTheme.NavBrand : CategoryTileFill;
            tile.Border.color = selected ? UiTheme.NavBrandDark : CatalogPanelBorder;
            tile.Icon.sprite = sprites.GetIcon(selected ? tile.SelectedIconName : tile.UnselectedIconName);
            tile.Icon.color = Color.white;
            tile.Label.color = selected ? Color.white : UiTheme.NavBrandDark;
        }
    }

    private void ScrollToCatalogTarget(CatalogScrollTarget target)
    {
        selectedCatalogTarget = target;
        RefreshCategoryTileSelection();

        if (scrollContent == null)
        {
            return;
        }

        float targetY;
        if (!catalogScrollTargets.TryGetValue(target, out targetY))
        {
            targetY = 0f;
        }

        float catalogTop = catalogSectionRoot != null ? -catalogSectionRoot.anchoredPosition.y : 0f;
        float desiredY = catalogTop + targetY;
        float clampedY = Mathf.Clamp(desiredY, 0f, GetMaxScrollOffset());

        if (shopScrollRect != null)
        {
            shopScrollRect.StopMovement();
        }

        scrollContent.anchoredPosition = new Vector2(0f, clampedY);
        Canvas.ForceUpdateCanvases();
    }

    private float GetMaxScrollOffset()
    {
        if (scrollContent == null || shopViewport == null)
        {
            return 0f;
        }

        float contentHeight = scrollContent.sizeDelta.y;
        float viewportHeight = shopViewport.sizeDelta.y;
        return Mathf.Max(0f, contentHeight - viewportHeight);
    }

    private void UpdateScrollContentHeight()
    {
        if (scrollContent == null)
        {
            return;
        }

        float contentHeight = scrollContent.sizeDelta.y;
        if (catalogSectionRoot != null)
        {
            float catalogTop = -catalogSectionRoot.anchoredPosition.y;
            contentHeight = catalogTop + catalogSectionRoot.sizeDelta.y + CatalogBottomPadding;
        }

        if (shopViewport != null)
        {
            contentHeight = Mathf.Max(contentHeight, shopViewport.sizeDelta.y);
        }

        scrollContent.sizeDelta = new Vector2(382f, contentHeight);
    }

    private TextMeshProUGUI BuildBigPointsPill(RectTransform parent, float x, float y, float width, float height, string text)
    {
        Image pill = UiFactory.CreateImage("Pill", parent, GetRoundedRectSprite(85, 30, 10f, 1.5f, false), CtaBlueDark);
        pill.type = Image.Type.Sliced;
        pill.preserveAspect = false;
        pill.raycastTarget = false;
        SetTopLeft(pill.rectTransform, x + 1f, y + 0.5f, 84f, 25f);

        Image pillFill = UiFactory.CreateImage("PillFill", parent, GetRoundedRectSprite(84, 25, 10f, 0f, true), Color.white);
        pillFill.type = Image.Type.Sliced;
        pillFill.preserveAspect = false;
        pillFill.raycastTarget = false;
        SetTopLeft(pillFill.rectTransform, x + 1f, y + 0.5f, 84f, 25f);

        Image pillBorder = UiFactory.CreateImage("PillBorder", parent, GetRoundedRectSprite(84, 25, 10f, 1.5f, false), CtaBlueDark);
        pillBorder.type = Image.Type.Sliced;
        pillBorder.preserveAspect = false;
        pillBorder.raycastTarget = false;
        SetTopLeft(pillBorder.rectTransform, x + 1f, y + 0.5f, 84f, 25f);

        Image iconCircle = UiFactory.CreateImage("IconCircle", parent, UiTheme.CircleSprite, CtaBlue);
        iconCircle.type = Image.Type.Simple;
        iconCircle.preserveAspect = false;
        iconCircle.raycastTarget = false;
        SetTopLeft(iconCircle.rectTransform, x, y, 30f, 30f);
        Outline iconBorder = iconCircle.gameObject.AddComponent<Outline>();
        iconBorder.effectColor = CtaBlueDark;
        iconBorder.effectDistance = Vector2.zero;

        Image pawIcon = UiFactory.CreateImage("PawIcon", parent, sprites.GetIcon("icon_paw_white"), Color.white);
        pawIcon.type = Image.Type.Simple;
        pawIcon.preserveAspect = true;
        pawIcon.raycastTarget = false;
        SetTopLeft(pawIcon.rectTransform, x + 3.5f, y + 3.5f, 23f, 23f);

        TextMeshProUGUI label = CreateText(parent, "PointsText", text, 16, CtaBlue, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(label.rectTransform, x + 30f, y + 0.5f, 55f, 25f);
        return label;
    }

    private void CreatePackageCostBlock(RectTransform parent, float x, float y, string points, string cost)
    {
        RectTransform costBlock = CreateNode("CostBlock", parent, x, y, points.Length >= 4 ? 73f : 65f, 50f);
        CreatePackagePointsPill(costBlock, 0f, 0f, points.Length >= 4 ? 73f : 65f, points);
        TextMeshProUGUI costText = CreateText(costBlock, "CostText", cost, 13, UiTheme.NavBrandDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(costText.rectTransform, 0f, 31f, cost.Length >= 5 ? 48f : 40f, 19f);
    }

    private void CreatePackagePointsPill(RectTransform parent, float x, float y, float width, string value)
    {
        Image pillFill = UiFactory.CreateImage("PillFill", parent, GetRoundedRectSprite(Mathf.RoundToInt(width - 1f), 25, 10f, 0f, true), Color.white);
        pillFill.type = Image.Type.Sliced;
        pillFill.preserveAspect = false;
        pillFill.raycastTarget = false;
        SetTopLeft(pillFill.rectTransform, x + 1f, y + 0.5f, width - 1f, 25f);

        Image pillBorder = UiFactory.CreateImage("PillBorder", parent, GetRoundedRectSprite(Mathf.RoundToInt(width - 1f), 25, 10f, 1f, false), CtaBlueDark);
        pillBorder.type = Image.Type.Sliced;
        pillBorder.preserveAspect = false;
        pillBorder.raycastTarget = false;
        SetTopLeft(pillBorder.rectTransform, x + 1f, y + 0.5f, width - 1f, 25f);

        Image circle = UiFactory.CreateImage("Circle", parent, UiTheme.CircleSprite, CtaBlue);
        circle.type = Image.Type.Simple;
        circle.preserveAspect = false;
        circle.raycastTarget = false;
        SetTopLeft(circle.rectTransform, x, y, 26f, 26f);

        Image paw = UiFactory.CreateImage("Paw", parent, sprites.GetIcon("icon_paw_white"), Color.white);
        paw.type = Image.Type.Simple;
        paw.preserveAspect = true;
        paw.raycastTarget = false;
        SetTopLeft(paw.rectTransform, x + 1.5f, y + 1.5f, 23f, 23f);

        TextMeshProUGUI text = CreateText(parent, "PointsValue", value, 13, CtaBlue, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(text.rectTransform, x + 30f, y + 3f, width - 39f, 19f);
    }

    private void CreateCloseButton(RectTransform parent, string name, float x, float y, float width, float height, Action onClick)
    {
        RectTransform buttonRect = CreateNode(name, parent, x, y, width, height);
        Image fill = UiFactory.CreateImage(
            "Fill",
            buttonRect,
            GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 0f, true),
            Color.white);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = true;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);

        Image border = UiFactory.CreateImage(
            "Border",
            buttonRect,
            GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 1f, false),
            UiTheme.NavBrand);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 0f, 0f, width, height);

        TextMeshProUGUI text = CreateText(buttonRect, "Label", "X", 15, UiTheme.NavBrand, UiTheme.DefaultFont, TextAlignmentOptions.Center);
        SetTopLeft(text.rectTransform, 0f, -1f, width, height);

        UiFactory.AddButton(buttonRect.gameObject, delegate
        {
            if (onClick != null)
            {
                onClick();
            }
        });
    }

    private void CreatePointsPill(RectTransform parent, string name, float x, float y, float width, float height, string value)
    {
        RectTransform pillRoot = CreateNode(name, parent, x, y, width, height);

        float pillWidth = Mathf.Max(20f, width - 10f);
        Image fill = UiFactory.CreateImage("Fill", pillRoot, GetRoundedRectSprite(Mathf.RoundToInt(pillWidth), 20, 10f, 0f, true), Color.white);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 10f, 1f, pillWidth, 20f);

        Image border = UiFactory.CreateImage("Border", pillRoot, GetRoundedRectSprite(Mathf.RoundToInt(pillWidth), 20, 10f, 1f, false), CtaBlueDark);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 10f, 1f, pillWidth, 20f);

        TextMeshProUGUI label = CreateText(pillRoot, "Value", value, 13, CtaBlue, UiTheme.NavExtraBoldFont, TextAlignmentOptions.MidlineRight);
        SetTopLeft(label.rectTransform, 24f, 1f, Mathf.Max(10f, width - 28f), 20f);

        Image circle = UiFactory.CreateImage("Circle", pillRoot, UiTheme.CircleSprite, CtaBlue);
        circle.type = Image.Type.Simple;
        circle.preserveAspect = false;
        circle.raycastTarget = false;
        SetTopLeft(circle.rectTransform, 0f, 0f, 22f, 22f);

        Image circleBorder = UiFactory.CreateImage("CircleBorder", pillRoot, GetRoundedRectSprite(22, 22, 11f, 1f, false), CtaBlueDark);
        circleBorder.type = Image.Type.Sliced;
        circleBorder.preserveAspect = false;
        circleBorder.raycastTarget = false;
        SetTopLeft(circleBorder.rectTransform, 0f, 0f, 22f, 22f);

        Image paw = UiFactory.CreateImage("Paw", pillRoot, sprites.GetIcon("icon_paw_white"), Color.white);
        paw.type = Image.Type.Simple;
        paw.preserveAspect = true;
        paw.raycastTarget = false;
        SetTopLeft(paw.rectTransform, 2f, 2f, 18f, 18f);
    }

    private void CreateOwnedPill(RectTransform parent, string name, float x, float y, float width, float height, string value)
    {
        Image fill = UiFactory.CreateImage(name, parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 0f, true), ComingSoonGrey);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, x, y, width, height);

        TextMeshProUGUI label = CreateText(parent, name + "Text", value, 14, Color.white, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(label.rectTransform, x + 3f, y + 1f, width - 6f, height - 2f);
    }

    private void CreateEmptyPill(RectTransform parent, string name, float x, float y, float width, float height)
    {
        Image fill = UiFactory.CreateImage(name + "Fill", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 0f, true), Color.white);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, x, y, width, height);

        Image border = UiFactory.CreateImage(name + "Border", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 1f, false), UiTheme.NavBrandDark);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, x, y, width, height);
    }

    private void CreateActionButton(RectTransform parent, string name, string label, float x, float y, float width, float height, bool primary, Action onClick)
    {
        RectTransform buttonRect = CreateNode(name, parent, x, y, width, height);
        Image fill = UiFactory.CreateImage(
            "Fill",
            buttonRect,
            GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 0f, true),
            primary ? CtaBlue : ComingSoonLight);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = true;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);

        Image underline = UiFactory.CreateImage(
            "Underline",
            buttonRect,
            GetBottomUnderlineSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 2f),
            primary ? CtaBlueDark : ComingSoonGrey);
        underline.type = Image.Type.Sliced;
        underline.preserveAspect = false;
        underline.raycastTarget = false;
        SetTopLeft(underline.rectTransform, 0f, 0f, width, height);

        TextMeshProUGUI text = CreateText(buttonRect, "Label", label, 16, primary ? Color.white : ComingSoonGrey, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(text.rectTransform, 0f, 0f, width, height);

        UiFactory.AddButton(buttonRect.gameObject, delegate
        {
            if (onClick != null)
            {
                onClick();
            }
        });
    }

    private PawPalCatalogItemDefinition GetSelectedCatalogItem()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        return runtime != null ? runtime.GetCatalogItem(selectedCatalogItemId) : null;
    }

    private PawPalCatalogItemDefinition GetLastPurchasedCatalogItem()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        return runtime != null ? runtime.GetCatalogItem(lastPurchasedCatalogItemId) : null;
    }

    private bool CanBuySelectedItem()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        PawPalCatalogItemDefinition item = GetSelectedCatalogItem();
        if (runtime == null || item == null || item.DisabledInShop)
        {
            return false;
        }

        if (!item.CanPurchaseMultiple && runtime.IsItemOwned(item.Id))
        {
            return false;
        }

        return runtime.CanAfford(item.Id);
    }

    private string BuildDetailsDescription(PawPalCatalogItemDefinition item)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (item == null)
        {
            return string.Empty;
        }

        if (runtime == null)
        {
            return item.Description;
        }

        if (item.Category == PawPalItemCategory.Food)
        {
            return item.Description + "\nOwned: x" + runtime.GetItemQuantity(item.Id);
        }

        if (item.DisabledInShop)
        {
            return item.DisabledReason;
        }

        if (!item.CanPurchaseMultiple && runtime.IsItemOwned(item.Id))
        {
            return item.Description + "\nAlready owned.";
        }

        return item.Description;
    }

    private string GetDetailsPrompt(PawPalCatalogItemDefinition item)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (item == null)
        {
            return "Unavailable";
        }

        if (item.DisabledInShop)
        {
            return "Not available";
        }

        if (runtime != null && !item.CanPurchaseMultiple && runtime.IsItemOwned(item.Id))
        {
            return "Already owned";
        }

        return "Confirm purchase?";
    }

    private CardBadgeKind GetModalBadgeKind(PawPalCatalogItemDefinition item)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (item == null)
        {
            return CardBadgeKind.Empty;
        }

        if (item.DisabledInShop || (runtime != null && !item.CanPurchaseMultiple && runtime.IsItemOwned(item.Id)))
        {
            return CardBadgeKind.Owned;
        }

        return CardBadgeKind.Price;
    }

    private string GetModalBadgeText(PawPalCatalogItemDefinition item)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (item == null)
        {
            return string.Empty;
        }

        if (item.DisabledInShop)
        {
            return "Soon";
        }

        if (runtime != null && !item.CanPurchaseMultiple && runtime.IsItemOwned(item.Id))
        {
            return "Owned";
        }

        return item.Price.ToString();
    }

    private float GetModalBadgeWidth(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return 45f;
        }

        if (item.DisabledInShop)
        {
            return 51f;
        }

        return 54f;
    }

    private string GetSuccessSpritePath(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return "UI/Figma/Shop/bubble_bone_success";
        }

        if (item.Id == "toy_bubble_bone")
        {
            return GetShopSpritePath(item);
        }

        return GetShopSpritePath(item);
    }

    private string GetShopSpritePath(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(item.GeneratedShopSpritePath) && ResourceSpriteExists(item.GeneratedShopSpritePath))
        {
            return item.GeneratedShopSpritePath;
        }

        return item.ShopSpritePath;
    }

    private Sprite GetFieldSpriteForItem(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return CreateStandardFieldSprite("shop_default_field", new Color32(255, 245, 228, 26), new Color32(255, 245, 228, 26));
        }

        switch (item.Id)
        {
            case "toy_bubble_bone":
                return CreateTintedFieldSprite("shop_bubble_bone_field", new Color32(232, 187, 204, 140), new Color32(214, 125, 163, 26));
            case "toy_bouncy_ball":
                return CreateTintedFieldSprite("shop_bouncy_ball_field", new Color32(222, 226, 159, 140), new Color32(193, 203, 72, 26));
            case "toy_turbo_roll":
                return CreateTintedFieldSprite("shop_turbo_roll_field", new Color32(168, 184, 223, 140), new Color32(85, 120, 201, 26));
            case "toy_star_ball":
                return CreateTintedFieldSprite("shop_star_ball_field", new Color32(235, 229, 174, 140), new Color32(219, 209, 103, 26));
            default:
                return CreateStandardFieldSprite("shop_" + item.Id + "_field", new Color32(255, 245, 228, 26), new Color32(255, 245, 228, 26));
        }
    }

    private Rect GetShopImageRect(PawPalCatalogItemDefinition item, PawPalItemCategory category, int index)
    {
        if (item == null)
        {
            return Rect.zero;
        }

        return StandardShopImageRect;
    }

    private Vector2 GetShopCardPosition(PawPalItemCategory category, int index)
    {
        int column = index % 3;
        int row = index / 3;
        return new Vector2(10f + column * 122f, 20f + row * 122f);
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }

    private void BuildAwningStripe(RectTransform parent, string name, float x, float y, float width, float height, bool leftOuter, bool rightOuter, Color32 color)
    {
        Image stripe = UiFactory.CreateImage(name, parent, GetAwningStripeSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), leftOuter, rightOuter), color);
        stripe.type = Image.Type.Sliced;
        stripe.preserveAspect = false;
        stripe.raycastTarget = false;
        SetTopLeft(stripe.rectTransform, x, y, width, height);
    }

    private void ToggleBuyPointsPanel()
    {
        if (modalState != ModalState.None)
        {
            return;
        }

        floatingPanelState = floatingPanelState == FloatingPanelState.BuyPoints ? FloatingPanelState.None : FloatingPanelState.BuyPoints;
        RefreshState();
    }

    private void ShowBubbleBoneDetails()
    {
        ShowItemDetails("toy_bubble_bone");
    }

    private void ShowItemDetails(string itemId)
    {
        selectedCatalogItemId = itemId;
        floatingPanelState = FloatingPanelState.None;
        modalState = ModalState.ItemDetails;
        RebuildModalPanels();
        RefreshState();
    }

    private void RebuildModalPanels()
    {
        if (itemDetailsPanel != null)
        {
            if (modalState == ModalState.ItemDetails)
            {
                BuildItemDetailsPanel(itemDetailsPanel);
            }
            else
            {
                ClearChildren(itemDetailsPanel);
            }
        }

        if (itemSuccessPanel != null)
        {
            if (modalState == ModalState.ItemSuccess)
            {
                BuildItemSuccessPanel(itemSuccessPanel);
            }
            else
            {
                ClearChildren(itemSuccessPanel);
            }
        }
    }

    private void RefreshState()
    {
        if (buyPointsPanel != null)
        {
            buyPointsPanel.gameObject.SetActive(floatingPanelState == FloatingPanelState.BuyPoints);
        }

        bool showModalLayer = modalState != ModalState.None;
        if (modalLayer != null)
        {
            modalLayer.gameObject.SetActive(showModalLayer);
        }

        if (modalBlocker != null)
        {
            modalBlocker.gameObject.SetActive(showModalLayer);
        }

        if (itemDetailsPanel != null)
        {
            itemDetailsPanel.gameObject.SetActive(modalState == ModalState.ItemDetails);
        }

        if (itemSuccessPanel != null)
        {
            itemSuccessPanel.gameObject.SetActive(modalState == ModalState.ItemSuccess);
        }
    }

    private void HandleRuntimeStateChanged()
    {
        RefreshRuntimeState();
    }

    private void RefreshRuntimeState()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        if (basicCurrencyLabel != null)
        {
            basicCurrencyLabel.text = runtime.GetBasicCurrencyText();
        }

        if (premiumCurrencyLabel != null)
        {
            premiumCurrencyLabel.text = runtime.GetPremiumCurrencyText();
        }

        if (lastShopRevision != runtime.ShopRevision)
        {
            lastShopRevision = runtime.ShopRevision;
            RebuildCatalogSections();
            RebuildModalPanels();
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

    private static RectTransform CreateCenteredNode(string name, RectTransform parent, float width, float height, float yOffset)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, yOffset);
        return rect;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string value, int fontSize, Color32 color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = UiFactory.CreateLabel(name, parent, value, fontSize, color, FontStyles.Normal, alignment);
        text.font = font;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static bool ResourceSpriteExists(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return false;
        }

        bool exists;
        if (ResourceSpriteExistsCache.TryGetValue(resourcePath, out exists))
        {
            return exists;
        }

        exists = Resources.Load<Sprite>(resourcePath) != null || Resources.Load<Texture2D>(resourcePath) != null;
        ResourceSpriteExistsCache[resourcePath] = exists;
        return exists;
    }

    private static Sprite CreateStandardFieldSprite(string key, Color32 midColor, Color32 outerColor)
    {
        return CreateTintedFieldSprite(key, midColor, outerColor);
    }

    private static Sprite CreateTintedFieldSprite(string key, Color32 midColor, Color32 outerColor)
    {
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        const int width = 115;
        const int height = 115;
        const float radius = 10f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(42.857f, 73.214f);
        float maxDistance = 90f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsInsideRoundedRect(x, y, width, height, radius))
                {
                    texture.SetPixel(x, y, new Color32(255, 255, 255, 0));
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                Color pixel = Color.Lerp(new Color32(250, 248, 245, 255), midColor, Mathf.Clamp01(distance * 1.25f));
                pixel = Color.Lerp(pixel, outerColor, Mathf.Clamp01(distance));

                if (distance > 0.78f)
                {
                    pixel = Color.Lerp(pixel, new Color32(255, 255, 255, 40), 0.15f);
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsInsideRoundedRect(x, y, width, height, radius))
                {
                    continue;
                }

                if (x > 1 && x < width - 2 && y > 1 && y < height - 2)
                {
                    continue;
                }

                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    Color current = texture.GetPixel(x, y);
                    texture.SetPixel(x, y, Color.Lerp(current, new Color32(0, 0, 0, 90), 0.2f));
                }
            }
        }

        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = key + "_Sprite";
        RuntimeSpriteCache[key] = sprite;
        return sprite;
    }

    private static Sprite GetRoundedRectSprite(int width, int height, float radius, float border, bool fill)
    {
        string key = "rr_" + width + "_" + height + "_" + radius + "_" + border + "_" + fill;
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32 transparent = new Color32(255, 255, 255, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, width, height, radius);
                bool borderPixel = border > 0f && inside && !IsInsideRoundedRect(x, y, width, height, radius, border);
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
        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = key;
        RuntimeSpriteCache[key] = sprite;
        return sprite;
    }

    private static Sprite GetTopRoundedSprite(int width, int height, float radius)
    {
        string key = "top_" + width + "_" + height + "_" + radius;
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = IsInsideTopRoundedRect(x, y, width, height, radius);
                texture.SetPixel(x, y, inside ? UiTheme.White : new Color32(255, 255, 255, 0));
            }
        }

        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, 0f, 0f));
        sprite.name = key;
        RuntimeSpriteCache[key] = sprite;
        return sprite;
    }

    private static Sprite GetBottomUnderlineSprite(int width, int height, float radius, float thickness)
    {
        string key = "underline_" + width + "_" + height + "_" + radius + "_" + thickness;
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32 transparent = new Color32(255, 255, 255, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, width, height, radius);
                bool bottomBand = y <= thickness + (x < radius || x >= width - radius ? 1f : 0f);
                texture.SetPixel(x, y, inside && bottomBand ? UiTheme.White : transparent);
            }
        }

        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = key;
        RuntimeSpriteCache[key] = sprite;
        return sprite;
    }

    private static Sprite GetHorizontalLineSprite(int width, int height)
    {
        string key = "line_" + width + "_" + height;
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, UiTheme.White);
            }
        }

        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = key;
        RuntimeSpriteCache[key] = sprite;
        return sprite;
    }

    private static Sprite GetAwningStripeSprite(int width, int height, bool leftOuter, bool rightOuter)
    {
        string key = "awning_" + width + "_" + height + "_" + leftOuter + "_" + rightOuter;
        Sprite sprite;
        if (RuntimeSpriteCache.TryGetValue(key, out sprite))
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = key;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        const float topRadius = 20f;
        const float bottomRadius = 25f;
        Color32 transparent = new Color32(255, 255, 255, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = true;

                if (leftOuter && x < topRadius && y >= height - topRadius)
                {
                    Vector2 center = new Vector2(topRadius - 1f, height - topRadius);
                    inside = Vector2.Distance(new Vector2(x, y), center) <= topRadius;
                }
                else if (rightOuter && x >= width - topRadius && y >= height - topRadius)
                {
                    Vector2 center = new Vector2(width - topRadius, height - topRadius);
                    inside = Vector2.Distance(new Vector2(x, y), center) <= topRadius;
                }

                if (inside && x < bottomRadius && y < bottomRadius)
                {
                    Vector2 center = new Vector2(bottomRadius - 1f, bottomRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, y), center) <= bottomRadius;
                }

                if (inside && x >= width - bottomRadius && y < bottomRadius)
                {
                    Vector2 center = new Vector2(width - bottomRadius, bottomRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, y), center) <= bottomRadius;
                }

                texture.SetPixel(x, y, inside ? UiTheme.White : transparent);
            }
        }

        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = key;
        RuntimeSpriteCache[key] = sprite;
        return sprite;
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
}
