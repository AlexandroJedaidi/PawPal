using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HomeAddonsView : MonoBehaviour
{
    private const float BaseWidth = 180f;
    private const float BaseHeight = 38f;
    private const float ButtonSize = 38f;
    private const string MicIconResourcePath = "UI/Figma/HomeMain/icon_mic";
    private const string WhistleIconResourcePath = "UI/Icons/icon_whistle_brand";
    private const string WhistleIconName = "icon_whistle_brand";
    private const string WhistleSelectedIconName = "icon_whistle_white";
    private static readonly Vector2 MicIconOffset = Vector2.zero;

    private UiSpriteLibrary sprites;
    private Image micBackground;
    private Image micBorder;
    private Image micIcon;
    private Image inventoryBackground;
    private Image inventoryBorder;
    private Image inventoryIcon;
    private Image whistleBackground;
    private Image whistleBorder;
    private Image whistleIcon;

    public void Initialize(UiSpriteLibrary spriteLibrary, Action onMicTapped, Action onInventoryTapped, Action onWhistleTapped)
    {
        sprites = spriteLibrary;
        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(BaseWidth, BaseHeight);

        micBackground = CreateAddonButton(root, sprites, "MicButton", MicIconResourcePath, 0f, 29f, MicIconOffset, onMicTapped, false);
        micIcon = FindIcon(micBackground);
        micBorder = CreateSelectedBorder(micBackground.rectTransform);

        inventoryBackground = CreateAddonButton(root, sprites, "InventoryButton", "UI/Figma/HomeMain/icon_inventory", 71f, 29f, Vector2.zero, onInventoryTapped, true);
        inventoryIcon = inventoryBackground.transform.Find("Icon").GetComponent<Image>();
        inventoryBorder = CreateSelectedBorder(inventoryBackground.rectTransform);

        whistleBackground = CreateAddonButton(root, sprites, "WhistleButton", WhistleIconResourcePath, 142f, 25f, Vector2.zero, onWhistleTapped, false);
        whistleIcon = FindIcon(whistleBackground);
        whistleBorder = CreateSelectedBorder(whistleBackground.rectTransform);

        SetMicSelected(false);
        SetInventorySelected(false);
        SetWhistleSelected(false);
    }

    private Image CreateAddonButton(RectTransform parent, UiSpriteLibrary sprites, string name, string resourcePath, float x, float iconSize, Vector2 iconOffset, Action onClick, bool inventoryButton)
    {
        Image background = UiFactory.CreateImage(name, parent, UiTheme.CircleSprite, UiTheme.NavBackgroundCream);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.rectTransform.anchorMin = new Vector2(0f, 1f);
        background.rectTransform.anchorMax = new Vector2(0f, 1f);
        background.rectTransform.pivot = new Vector2(0f, 1f);
        background.rectTransform.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        background.rectTransform.anchoredPosition = new Vector2(x, 0f);

        Shadow shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = UiTheme.NavShadow;
        shadow.effectDistance = new Vector2(0f, -1f);
        shadow.useGraphicAlpha = true;

        Image icon = UiFactory.CreateImage("Icon", background.rectTransform, sprites.GetResourceSprite(resourcePath), Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        icon.rectTransform.anchoredPosition = iconOffset;

        Button button = UiFactory.AddButton(background.gameObject, onClick == null ? null : new UnityEngine.Events.UnityAction(onClick));
        if (button != null && string.Equals(name, "WhistleButton", StringComparison.Ordinal))
        {
            PawPalUiAudio.AttachTo(button, PawPalUiClickSoundKind.Whistle);
        }

        return background;
    }

    public void SetMicSelected(bool selected)
    {
        if (sprites == null)
        {
            return;
        }

        ApplyButtonSelected(
            micBackground,
            micIcon,
            micBorder,
            selected,
            sprites.GetResourceSprite(MicIconResourcePath),
            sprites.GetWhiteResourceSprite(MicIconResourcePath));
    }

    public void SetInventorySelected(bool selected)
    {
        if (sprites == null)
        {
            return;
        }

        ApplyButtonSelected(
            inventoryBackground,
            inventoryIcon,
            inventoryBorder,
            selected,
            sprites.GetIcon("icon_inventory_brand"),
            sprites.GetIcon("icon_inventory_white"));
    }

    public void SetWhistleSelected(bool selected)
    {
        if (sprites == null)
        {
            return;
        }

        ApplyButtonSelected(
            whistleBackground,
            whistleIcon,
            whistleBorder,
            selected,
            sprites.GetIcon(WhistleIconName),
            sprites.GetIcon(WhistleSelectedIconName));
    }

    private static Image CreateSelectedBorder(RectTransform parent)
    {
        Image border = UiFactory.CreateImage("SelectedBorder", parent, UiTheme.CircleOutlineSprite, UiTheme.NavBrandDark);
        border.type = Image.Type.Simple;
        border.preserveAspect = false;
        border.raycastTarget = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);
        return border;
    }

    private static Image FindIcon(Image background)
    {
        Transform iconTransform = background != null ? background.transform.Find("Icon") : null;
        return iconTransform != null ? iconTransform.GetComponent<Image>() : null;
    }

    private static void ApplyButtonSelected(Image background, Image icon, Image border, bool selected, Sprite defaultIcon, Sprite selectedIcon)
    {
        if (background == null || icon == null)
        {
            return;
        }

        background.sprite = UiTheme.CircleSprite;
        background.color = selected ? UiTheme.NavBrand : UiTheme.NavBackgroundCream;
        icon.sprite = selected && selectedIcon != null ? selectedIcon : defaultIcon;
        icon.color = Color.white;
        icon.gameObject.SetActive(true);

        if (border != null)
        {
            border.gameObject.SetActive(selected);
        }
    }
}
