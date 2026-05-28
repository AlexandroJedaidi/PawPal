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

    private static readonly Color32 SupportFill = new Color32(241, 236, 226, 255);
    private static readonly Color32 SupportShadow = new Color32(236, 223, 200, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 CtaBlueDark = new Color32(0, 118, 177, 255);
    private static readonly Color32 GreyText = new Color32(122, 122, 122, 255);
    private static readonly Color32 ComingSoonGrey = new Color32(163, 163, 163, 255);
    private static readonly Color32 ComingSoonLight = new Color32(220, 220, 220, 255);
    private static readonly Color32 TransparentHit = new Color32(255, 255, 255, 1);

    private static readonly Dictionary<string, Sprite> RuntimeSpriteCache = new Dictionary<string, Sprite>();

    private RectTransform exactFrame;
    private RectTransform scrollContent;
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

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            exactFrame.anchorMin = new Vector2(0.5f, 1f);
            exactFrame.anchorMax = new Vector2(0.5f, 1f);
            exactFrame.pivot = new Vector2(0.5f, 1f);
            exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
            exactFrame.anchoredPosition = Vector2.zero;
        }
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
        RectTransform viewport = CreateNode("ShopViewport", parent, 6f, 162f, 382f, 627f);
        Image viewportHit = viewport.gameObject.AddComponent<Image>();
        viewportHit.color = new Color(1f, 1f, 1f, 0.002f);
        viewportHit.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        scrollContent = UiFactory.CreateRect("ScrollContent", viewport);
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(0f, 1f);
        scrollContent.pivot = new Vector2(0f, 1f);
        scrollContent.sizeDelta = new Vector2(382f, 1752f);
        scrollContent.anchoredPosition = Vector2.zero;

        ScrollRect scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = scrollContent;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 22f;

        BuildSpecialOfferSection(scrollContent);
        catalogSectionRoot = CreateNode("CatalogSectionRoot", scrollContent, 0f, 445f, 382f, 1307f);
        RebuildCatalogSections();
    }

    private void BuildSpecialOfferSection(RectTransform parent)
    {
        BuildSectionHeader(parent, "SpecialOfferHeader", 0f, 0f, 382f, 24f, "Special offer", null, 0f, 16, UiTheme.NavExtraBoldFont);

        RectTransform primary = CreateNode("PrimaryOffer", parent, 0f, 37f, 382f, 176f);
        BuildPrimaryOffer(primary);

        RectTransform starter = CreateNode("StarterPackOffer", parent, 0f, 226f, 212f, 164f);
        BuildSpecialOfferCard(
            starter,
            "Starter pack",
            204f,
            204f,
            "240",
            "UI/Figma/Shop/offer_puppy_left",
            new Rect(27f, 42f, 74.47718f, 87.66422f),
            "UI/Figma/Shop/starter_pack_toy",
            new Rect(99.61368f, 54.63922f, 85.3733f, 42.13074f),
            "Clothing",
            new Rect(86.06926f, 96.76996f, 58.46215f, 18.53753f));

        RectTransform random = CreateNode("RandomOffer", parent, 218f, 226f, 164f, 164f);
        BuildRandomOfferCard(random);
    }

    private void BuildCategoriesAndItems(RectTransform parent)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            return;
        }

        RectTransform section = CreateNode("ItemsAll", parent, 0f, 0f, 382f, 1307f);
        BuildSectionHeader(section, "CategoriesHeader", 0f, 0f, 382f, 24f, "Categories", null, 0f, 16, UiTheme.NavExtraBoldFont);
        BuildCategoryRow(section);

        float currentY = 112f;
        currentY = BuildRuntimeProductSection(section, "DogsSection", currentY, 72f, "Dogs", "icon_dog_white", 24f, new Rect(8f, 4f, 34f, 31f), 382f, 147f, PawPalItemCategory.Dogs);
        currentY = BuildRuntimeProductSection(section, "FoodSection", currentY, 73f, "Food", "icon_foodsupply_white", 32f, new Rect(8f, 2f, 36f, 36f), 382f, 147f, PawPalItemCategory.Food);
        currentY = BuildRuntimeProductSection(section, "ToysSection", currentY, 71f, "Toys", "icon_toys_white", 36.676f, new Rect(6.4f, 1f, 38f, 38f), 382f, 267f, PawPalItemCategory.Toys);
        currentY = BuildRuntimeProductSection(section, "CollarsSection", currentY, 170f, "Collars & Leashes", "icon_collar_white", 40f, new Rect(5.6f, 0f, 40f, 40f), 382f, 147f, PawPalItemCategory.Collars);
        currentY = BuildRuntimeProductSection(section, "ClothingSection", currentY, 101f, "Clothing", "icon_clothing_white", 33f, new Rect(8.2f, 3f, 34f, 34f), 382f, 147f, PawPalItemCategory.Clothing);
        currentY = BuildRuntimeProductSection(section, "FurnitureSection", currentY, 110f, "Furniture", "icon_dogbed_white", 35f, new Rect(7f, 2f, 37f, 37f), 382f, 147f, PawPalItemCategory.Furniture);

        section.sizeDelta = new Vector2(382f, currentY + 24f);
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
        Image borderBack = UiFactory.CreateImage("BorderBack", parent, GetRoundedRectSprite(380, 161, 10f, 3f, false), UiTheme.NavBrandDark);
        borderBack.type = Image.Type.Sliced;
        borderBack.preserveAspect = false;
        SetTopLeft(borderBack.rectTransform, 1.00262f, 15f, 379.99475f, 161f);

        Image outerBorder = UiFactory.CreateImage("OuterBorder", parent, GetRoundedRectSprite(382, 161, 10f, 3f, false), UiTheme.NavBrand);
        outerBorder.type = Image.Type.Sliced;
        outerBorder.preserveAspect = false;
        SetTopLeft(outerBorder.rectTransform, 0f, 12f, 382f, 161f);

        Image innerFill = UiFactory.CreateImage("InnerFill", parent, GetRoundedRectSprite(374, 152, 6f, 0f, true), Color.white);
        innerFill.type = Image.Type.Sliced;
        innerFill.preserveAspect = false;
        SetTopLeft(innerFill.rectTransform, 4.01049f, 18f, 373.979f, 152f);

        Image titleBar = UiFactory.CreateImage("TitleBar", parent, GetTopRoundedSprite(375, 20, 6f), CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        SetTopLeft(titleBar.rectTransform, 3.5092f, 18f, 374.98163f, 20f);

        TextMeshProUGUI title = CreateText(parent, "Title", "Husky Puppy", 15, Color.white, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(title.rectTransform, 3.5092f, 18f, 374.98163f, 20f);

        Image timerBg = UiFactory.CreateImage("TimerBg", parent, GetRoundedRectSprite(97, 28, 5f, 1f, true), UiTheme.NavBrand);
        timerBg.type = Image.Type.Sliced;
        timerBg.preserveAspect = false;
        SetTopLeft(timerBg.rectTransform, 284.90628f, 0f, 97.09371f, 28f);
        Outline timerBorder = timerBg.gameObject.AddComponent<Outline>();
        timerBorder.effectColor = UiTheme.NavBackgroundCream;
        timerBorder.effectDistance = Vector2.zero;

        Image timerIcon = UiFactory.CreateImage("TimerIcon", parent, sprites.GetIcon("icon_clock"), Color.white);
        timerIcon.type = Image.Type.Simple;
        timerIcon.preserveAspect = true;
        timerIcon.raycastTarget = false;
        SetTopLeft(timerIcon.rectTransform, 289.90628f, 5f, 18f, 18f);

        TextMeshProUGUI timerText = CreateText(parent, "TimerText", "22h 48m", 15, Color.white, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(timerText.rectTransform, 312.90628f, 3f, 64f, 22f);

        Image puppyLeft = UiFactory.CreateImage("PuppyLeft", parent, sprites.GetResourceSprite("UI/Figma/Shop/offer_puppy_left"), Color.white);
        puppyLeft.type = Image.Type.Simple;
        puppyLeft.preserveAspect = false;
        puppyLeft.raycastTarget = false;
        SetTopLeft(puppyLeft.rectTransform, 59.33504f, 46f, 75.02695f, 99.16666f);

        Image puppyRight = UiFactory.CreateImage("PuppyRight", parent, sprites.GetResourceSprite("UI/Figma/Shop/offer_puppies_right"), Color.white);
        puppyRight.type = Image.Type.Simple;
        puppyRight.preserveAspect = false;
        puppyRight.raycastTarget = false;
        SetTopLeft(puppyRight.rectTransform, 145.47711f, 46f, 172.28413f, 99.16666f);

        CreatePointsPill(parent, "PrimaryPrice", 168.68806f, 146f, 52.96021f, 22f, "700");
    }

    private void BuildSpecialOfferCard(
        RectTransform parent,
        string title,
        float titleWidth,
        float fillWidth,
        string points,
        string leftImageResource,
        Rect leftImageRect,
        string rightImageResource,
        Rect rightImageRect,
        string caption,
        Rect captionRect)
    {
        Image borderBack = UiFactory.CreateImage("BorderBack", parent, GetRoundedRectSprite(211, 161, 10f, 3f, false), UiTheme.NavBrandDark);
        borderBack.type = Image.Type.Sliced;
        borderBack.preserveAspect = false;
        SetTopLeft(borderBack.rectTransform, 0.56f, 3f, 210.887f, 161f);

        Image outerBorder = UiFactory.CreateImage("OuterBorder", parent, GetRoundedRectSprite(212, 161, 10f, 3f, false), UiTheme.NavBrand);
        outerBorder.type = Image.Type.Sliced;
        outerBorder.preserveAspect = false;
        SetTopLeft(outerBorder.rectTransform, 0f, 0f, 212f, 161f);

        Image innerFill = UiFactory.CreateImage("InnerFill", parent, GetRoundedRectSprite(204, 152, 6f, 0f, true), Color.white);
        innerFill.type = Image.Type.Sliced;
        innerFill.preserveAspect = false;
        SetTopLeft(innerFill.rectTransform, 4f, 6f, 204f, 152f);

        Image titleBar = UiFactory.CreateImage("TitleBar", parent, GetTopRoundedSprite(204, 20, 6f), CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        SetTopLeft(titleBar.rectTransform, 4f, 6f, fillWidth, 20f);

        TextMeshProUGUI titleText = CreateText(parent, "Title", title, 15, Color.white, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(titleText.rectTransform, 4f, 6f, titleWidth, 20f);

        Image leftImage = UiFactory.CreateImage("LeftArt", parent, sprites.GetResourceSprite(leftImageResource), Color.white);
        leftImage.type = Image.Type.Simple;
        leftImage.preserveAspect = false;
        leftImage.raycastTarget = false;
        SetTopLeft(leftImage.rectTransform, leftImageRect.x, leftImageRect.y, leftImageRect.width, leftImageRect.height);

        Image rightImage = UiFactory.CreateImage("RightArt", parent, sprites.GetResourceSprite(rightImageResource), Color.white);
        rightImage.type = Image.Type.Simple;
        rightImage.preserveAspect = false;
        rightImage.raycastTarget = false;
        SetTopLeft(rightImage.rectTransform, rightImageRect.x, rightImageRect.y, rightImageRect.width, rightImageRect.height);

        TextMeshProUGUI captionText = CreateText(parent, "Caption", caption, 13, UiTheme.NavBrand, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(captionText.rectTransform, captionRect.x, captionRect.y, captionRect.width, captionRect.height);

        CreatePointsPill(parent, "Price", 79f, 134f, 54f, 22f, points);
    }

    private void BuildRandomOfferCard(RectTransform parent)
    {
        Image borderBack = UiFactory.CreateImage("BorderBack", parent, GetRoundedRectSprite(163, 161, 10f, 3f, false), UiTheme.NavBrandDark);
        borderBack.type = Image.Type.Sliced;
        borderBack.preserveAspect = false;
        SetTopLeft(borderBack.rectTransform, 0.43044f, 3f, 163.13911f, 161f);

        Image outerBorder = UiFactory.CreateImage("OuterBorder", parent, GetRoundedRectSprite(164, 161, 10f, 3f, false), UiTheme.NavBrand);
        outerBorder.type = Image.Type.Sliced;
        outerBorder.preserveAspect = false;
        SetTopLeft(outerBorder.rectTransform, 0f, 0f, 164f, 161f);

        Image innerFill = UiFactory.CreateImage("InnerFill", parent, GetRoundedRectSprite(156, 152, 6f, 0f, true), Color.white);
        innerFill.type = Image.Type.Sliced;
        innerFill.preserveAspect = false;
        SetTopLeft(innerFill.rectTransform, 4f, 6f, 156f, 152f);

        Image titleBar = UiFactory.CreateImage("TitleBar", parent, GetTopRoundedSprite(156, 20, 6f), CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        SetTopLeft(titleBar.rectTransform, 4f, 6f, 156f, 20f);

        TextMeshProUGUI title = CreateText(parent, "Title", "Random", 15, Color.white, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(title.rectTransform, 4f, 6f, 156f, 20f);

        Image lootbox = UiFactory.CreateImage("Lootbox", parent, sprites.GetResourceSprite("UI/Figma/Shop/random_lootbox"), Color.white);
        lootbox.type = Image.Type.Simple;
        lootbox.preserveAspect = false;
        lootbox.raycastTarget = false;
        SetTopLeft(lootbox.rectTransform, 31f, 43f, 102f, 86f);

        CreatePointsPill(parent, "Price", 55f, 134f, 54f, 22f, "180");
    }

    private void BuildCategoryRow(RectTransform parent)
    {
        RectTransform row = CreateNode("CategoriesRow", parent, 0f, 47f, 382f, 42f);
        BuildCategoryButton(row, "Dogs", 0f, "icon_dog_white", new Rect(8f, 3.99998f, 34f, 31f));
        BuildCategoryButton(row, "Food", 66.2f, "icon_foodsupply_white", new Rect(7.8f, 2f, 36f, 36f));
        BuildCategoryButton(row, "Toys", 132.4f, "icon_toys_white", new Rect(6.4f, 1f, 38f, 38f));
        BuildCategoryButton(row, "Collar", 198.6f, "icon_collar_white", new Rect(5.6f, 0f, 40f, 40f));
        BuildCategoryButton(row, "Clothing", 264.8f, "icon_clothing_white", new Rect(8.2f, 3f, 34f, 34f));
        BuildCategoryButton(row, "Bed", 331f, "icon_dogbed_white", new Rect(7f, 2f, 37f, 37f));
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
        float headerWidth,
        string title,
        string headerIcon,
        float iconSize,
        Rect iconRect,
        float listWidth,
        float listHeight,
        PawPalItemCategory category)
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

        RectTransform section = CreateNode(name, parent, 0f, y, listWidth, listHeight + 13f);
        RectTransform header = BuildSectionHeader(section, "Header", 0f, 0f, headerWidth, 28f, title, headerIcon, iconSize, 15, UiTheme.NavExtraBoldFont, iconRect);

        RectTransform list = CreateNode("List", section, 0f, 13f, listWidth, listHeight);
        BuildListPanel(list, listWidth, listHeight);
        BuildRuntimeCategoryCards(list, items, category);
        header.SetAsLastSibling();
        return y + listHeight + 36f;
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
            badgeWidth = item.Price >= 100 ? 54f : 45f;
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
            item.ShopSpritePath,
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
        Image fill = UiFactory.CreateImage("Fill", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 0f, true), SupportFill);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, width, height);
        Shadow fillShadow = fill.gameObject.AddComponent<Shadow>();
        fillShadow.effectColor = new Color(SupportShadow.r / 255f, SupportShadow.g / 255f, SupportShadow.b / 255f, 1f);
        fillShadow.effectDistance = new Vector2(0f, -1f);
        fillShadow.useGraphicAlpha = false;

        Image border = UiFactory.CreateImage("Border", parent, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 3f, false), UiTheme.NavBrand);
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

        Image field = UiFactory.CreateImage("Field", card, fieldSprite, Color.white);
        field.type = Image.Type.Sliced;
        field.preserveAspect = false;
        field.raycastTarget = false;
        SetTopLeft(field.rectTransform, 0f, 0f, 115f, 115f);

        Image titleBar = UiFactory.CreateImage("TitleBar", card, GetTopRoundedSprite(115, 15, 10f), badgeBarColor);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        titleBar.raycastTarget = false;
        SetTopLeft(titleBar.rectTransform, 0f, 0f, 115f, 15f);

        TextMeshProUGUI titleLabel = CreateText(card, "Title", title, 12, titleTextColor, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(titleLabel.rectTransform, 0f, 0f, 115f, 15f);

        if (!string.IsNullOrEmpty(imageResource))
        {
            Image art = UiFactory.CreateImage("Art", card, sprites.GetResourceSprite(imageResource), Color.white);
            art.type = Image.Type.Simple;
            art.preserveAspect = false;
            art.raycastTarget = false;
            SetTopLeft(art.rectTransform, imageRect.x, imageRect.y, imageRect.width, imageRect.height);
        }

        switch (badgeKind)
        {
            case CardBadgeKind.Price:
                CreatePointsPill(card, "Price", (115f - badgeWidth) * 0.5f, 91f, badgeWidth, 22f, badgeText);
                break;
            case CardBadgeKind.Owned:
                CreateOwnedPill(card, "Owned", 32f, 92f, 51f, 20f, badgeText);
                break;
            case CardBadgeKind.Empty:
                CreateEmptyPill(card, "Empty", 33f, 91f, badgeWidth, 20f);
                break;
        }
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

        Image preview = UiFactory.CreateImage("Preview", parent, sprites.GetResourceSprite(GetDetailsPreviewSpritePath(item)), Color.white);
        preview.type = Image.Type.Simple;
        preview.preserveAspect = false;
        preview.raycastTarget = false;
        SetTopLeft(preview.rectTransform, 82f, 2f, 150f, 160f);

        RectTransform header = CreateNode("DetailsHeader", parent, 11.5f, 168f, 291f, 21f);
        Image line = UiFactory.CreateImage("Line", header, GetHorizontalLineSprite(291, 1), UiTheme.NavBrand);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
        line.raycastTarget = false;
        SetTopLeft(line.rectTransform, 0f, 13f, 291f, 1f);

        TextMeshProUGUI details = CreateText(header, "DetailsText", "Details", 14, UiTheme.NavBrand, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(details.rectTransform, 90.40776f, 0f, 109.24272f, 21f);

        RectTransform itemFrame = CreateNode("ItemFrame", parent, 10f, 195f, 294f, 115f);
        BuildProductCard(itemFrame, item.Id + "MiniCard", 0f, 0f, item.DisplayName, GetFieldSpriteForItem(item), item.ShopSpritePath, GetShopImageRect(item, item.Category, 0), GetModalBadgeKind(item), GetModalBadgeText(item), GetModalBadgeWidth(item), CtaBlue, Color.white, null);

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
            RefreshState();
        });
    }

    private void BuildSuccessItemCard(RectTransform parent, float x, float y)
    {
        PawPalCatalogItemDefinition item = GetLastPurchasedCatalogItem();
        if (item == null)
        {
            return;
        }

        RectTransform card = CreateNode("SuccessItem", parent, x, y, 115f, 115f);
        Image field = UiFactory.CreateImage("Field", card, GetFieldSpriteForItem(item), Color.white);
        field.type = Image.Type.Sliced;
        field.preserveAspect = false;
        field.raycastTarget = false;
        SetTopLeft(field.rectTransform, 0f, 0f, 115f, 115f);

        Image titleBar = UiFactory.CreateImage("TitleBar", card, GetTopRoundedSprite(115, 15, 10f), CtaBlue);
        titleBar.type = Image.Type.Sliced;
        titleBar.preserveAspect = false;
        titleBar.raycastTarget = false;
        SetTopLeft(titleBar.rectTransform, 0f, 0f, 115f, 15f);

        TextMeshProUGUI title = CreateText(card, "Title", item.DisplayName, 12, Color.white, UiTheme.NavBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(title.rectTransform, 0f, 0f, 115f, 15f);

        Image art = UiFactory.CreateImage("Art", card, sprites.GetResourceSprite(GetSuccessSpritePath(item)), Color.white);
        art.type = Image.Type.Simple;
        art.preserveAspect = false;
        art.raycastTarget = false;
        SetTopLeft(art.rectTransform, 1f, 33f, 112f, 61f);
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

    private void BuildCategoryButton(RectTransform parent, string name, float x, string iconName, Rect iconRect)
    {
        RectTransform buttonRoot = CreateNode(name, parent, x, 0f, 51f, 42f);
        Image fill = UiFactory.CreateImage("Fill", buttonRoot, GetRoundedRectSprite(51, 42, 10f, 0f, true), UiTheme.NavBrand);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 0f, 0f, 51f, 42f);

        Image underline = UiFactory.CreateImage("Underline", buttonRoot, GetRoundedRectSprite(51, 42, 10f, 2f, false), UiTheme.NavBrandDark);
        underline.type = Image.Type.Sliced;
        underline.preserveAspect = false;
        underline.raycastTarget = false;
        SetTopLeft(underline.rectTransform, 0f, 0f, 51f, 42f);

        Image icon = UiFactory.CreateImage("Icon", buttonRoot, sprites.GetIcon(iconName), Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetTopLeft(icon.rectTransform, iconRect.x, iconRect.y, iconRect.width, iconRect.height);
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

        Image circle = UiFactory.CreateImage("Circle", pillRoot, UiTheme.CircleSprite, CtaBlue);
        circle.type = Image.Type.Simple;
        circle.preserveAspect = false;
        circle.raycastTarget = false;
        SetTopLeft(circle.rectTransform, 0f, 0f, 22f, 22f);

        Image paw = UiFactory.CreateImage("Paw", pillRoot, sprites.GetIcon("icon_paw_white"), Color.white);
        paw.type = Image.Type.Simple;
        paw.preserveAspect = true;
        paw.raycastTarget = false;
        SetTopLeft(paw.rectTransform, 2f, 2f, 18f, 18f);

        Image fill = UiFactory.CreateImage("Fill", pillRoot, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 0f, true), Color.white);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        SetTopLeft(fill.rectTransform, 10f, 1f, width - 10f, height - 2f);

        Image border = UiFactory.CreateImage("Border", pillRoot, GetRoundedRectSprite(Mathf.RoundToInt(width), Mathf.RoundToInt(height), 10f, 1f, false), CtaBlueDark);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        border.raycastTarget = false;
        SetTopLeft(border.rectTransform, 10f, 1f, width - 10f, height - 2f);

        TextMeshProUGUI label = CreateText(pillRoot, "Value", value, 13, CtaBlue, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        SetTopLeft(label.rectTransform, 24f, 1f, width - 24f, 20f);
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

        return item.Price >= 100 ? 54f : 45f;
    }

    private string GetDetailsPreviewSpritePath(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return "UI/Figma/Shop/bubble_bone_preview";
        }

        if (item.Id == "toy_bubble_bone")
        {
            return "UI/Figma/Shop/bubble_bone_preview";
        }

        return item.ShopSpritePath;
    }

    private string GetSuccessSpritePath(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return "UI/Figma/Shop/bubble_bone_success";
        }

        if (item.Id == "toy_bubble_bone")
        {
            return "UI/Figma/Shop/bubble_bone_success";
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
            case "collar_ocean_band":
                return CreateTintedFieldSprite("shop_ocean_band_field", new Color32(156, 169, 194, 140), new Color32(62, 90, 142, 26));
            case "collar_maple_loop":
                return CreateTintedFieldSprite("shop_maple_loop_field", new Color32(230, 213, 183, 140), new Color32(209, 178, 120, 26));
            case "collar_cherry_charm":
                return CreateTintedFieldSprite("shop_cherry_charm_field", new Color32(186, 185, 184, 140), new Color32(122, 122, 122, 26));
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

        switch (item.Id)
        {
            case "dog_husky":
                return new Rect(17f, 18f, 81f, 63f);
            case "dog_rottweiler":
                return new Rect(6f, 22f, 103f, 63f);
            case "food_basic":
            case "food_premium":
                return new Rect(21f, 18f, 69f, 69f);
            case "toy_bubble_bone":
                return new Rect(11f, 29f, 92f, 50f);
            case "toy_bouncy_ball":
            case "toy_star_ball":
                return item.Id == "toy_bouncy_ball" ? new Rect(11f, 7f, 93f, 93f) : new Rect(28f, 22f, 60f, 59f);
            case "toy_turbo_roll":
                return new Rect(30f, 22f, 60f, 59f);
            case "collar_ocean_band":
                return new Rect(15f, 24f, 84f, 51f);
            case "collar_maple_loop":
                return new Rect(11f, 28f, 88f, 50f);
            case "collar_cherry_charm":
                return new Rect(15f, 22f, 84f, 57f);
            default:
                return Rect.zero;
        }
    }

    private Vector2 GetShopCardPosition(PawPalItemCategory category, int index)
    {
        switch (category)
        {
            case PawPalItemCategory.Toys:
                switch (index)
                {
                    case 0: return new Vector2(10f, 20f);
                    case 1: return new Vector2(132f, 20f);
                    case 2: return new Vector2(254f, 20f);
                    case 3: return new Vector2(10f, 142f);
                    default: return new Vector2(-1f, -1f);
                }
            case PawPalItemCategory.Collars:
                switch (index)
                {
                    case 0: return new Vector2(10f, 20f);
                    case 1: return new Vector2(132f, 20f);
                    case 2: return new Vector2(254f, 20f);
                    default: return new Vector2(-1f, -1f);
                }
            default:
                switch (index)
                {
                    case 0: return new Vector2(10f, 20f);
                    case 1: return new Vector2(132f, 20f);
                    default: return new Vector2(-1f, -1f);
                }
        }
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
            BuildItemDetailsPanel(itemDetailsPanel);
        }

        if (itemSuccessPanel != null)
        {
            BuildItemSuccessPanel(itemSuccessPanel);
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
