using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HomeAddonsView : MonoBehaviour
{
    private const float BaseWidth = 180f;
    private const float BaseHeight = 38f;
    private const float ButtonSize = 38f;
    private static readonly Vector2 MicIconOffset = Vector2.zero;

    private UiSpriteLibrary sprites;
    private Image inventoryBackground;
    private Image inventoryBorder;
    private Image inventoryIcon;

    public void Initialize(UiSpriteLibrary spriteLibrary, Action onMicTapped, Action onInventoryTapped, Action onCameraTapped)
    {
        sprites = spriteLibrary;
        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(BaseWidth, BaseHeight);

        CreateAddonButton(root, sprites, "MicButton", "UI/Figma/HomeMain/icon_mic", 0f, 29f, MicIconOffset, onMicTapped, false);
        inventoryBackground = CreateAddonButton(root, sprites, "InventoryButton", "UI/Figma/HomeMain/icon_inventory", 71f, 29f, Vector2.zero, onInventoryTapped, true);
        inventoryIcon = inventoryBackground.transform.Find("Icon").GetComponent<Image>();
        inventoryBorder = UiFactory.CreateImage("SelectedBorder", inventoryBackground.rectTransform, UiTheme.CircleOutlineSprite, UiTheme.NavBrandDark);
        inventoryBorder.type = Image.Type.Simple;
        inventoryBorder.preserveAspect = false;
        inventoryBorder.raycastTarget = false;
        UiFactory.Stretch(inventoryBorder.rectTransform, 0f, 0f, 0f, 0f);
        CreateAddonButton(root, sprites, "CamButton", "UI/Figma/HomeMain/icon_cam", 142f, 24f, Vector2.zero, onCameraTapped, false);
        SetInventorySelected(false);
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

        UiFactory.AddButton(background.gameObject, onClick == null ? null : new UnityEngine.Events.UnityAction(onClick));
        return background;
    }

    public void SetInventorySelected(bool selected)
    {
        if (inventoryBackground == null || inventoryIcon == null || sprites == null)
        {
            return;
        }

        if (selected)
        {
            inventoryBackground.sprite = UiTheme.CircleSprite;
            inventoryBackground.color = UiTheme.NavBrand;
            inventoryIcon.sprite = sprites.GetIcon("icon_inventory_white");
            inventoryIcon.color = Color.white;
            inventoryIcon.gameObject.SetActive(true);
            if (inventoryBorder != null)
            {
                inventoryBorder.gameObject.SetActive(true);
            }
        }
        else
        {
            inventoryBackground.sprite = UiTheme.CircleSprite;
            inventoryBackground.color = UiTheme.NavBackgroundCream;
            inventoryIcon.sprite = sprites.GetIcon("icon_inventory_brand");
            inventoryIcon.color = Color.white;
            inventoryIcon.gameObject.SetActive(true);
            if (inventoryBorder != null)
            {
                inventoryBorder.gameObject.SetActive(false);
            }
        }
    }
}
