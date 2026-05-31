using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class InventoryNameBarView : MonoBehaviour
{
    private static readonly Vector2 SelectorArrowSize = new Vector2(16f, 24f);

    private TextMeshProUGUI dogNameLabel;
    private Action previousDogRequested;
    private Action nextDogRequested;

    public void Initialize(UiSpriteLibrary sprites)
    {
        RectTransform root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(255f, 44f);

        Image background = gameObject.GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }

        background.sprite = UiTheme.InventoryNameBarSprite;
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.color = UiTheme.NavBackgroundCream;

        RectTransform row = UiFactory.CreateRect("Frame_Name", root);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.sizeDelta = new Vector2(235f, 24f);
        row.anchoredPosition = new Vector2(10f, -10f);

        CreateArrow(row, sprites, "BackArrow", "UI/Figma/HomeMain/button_back", new Vector2(6f, -12f), true);
        CreateNameField(row);
        CreateArrow(row, sprites, "ForwardArrow", "UI/Figma/HomeMain/button_forward", new Vector2(229f, -12f), false);
    }

    private void CreateArrow(RectTransform parent, UiSpriteLibrary sprites, string name, string resourcePath, Vector2 anchoredPosition, bool previousDog)
    {
        Image hitArea = UiFactory.CreateImage(name + "HitArea", parent, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        hitArea.rectTransform.anchorMin = new Vector2(0f, 1f);
        hitArea.rectTransform.anchorMax = new Vector2(0f, 1f);
        hitArea.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hitArea.rectTransform.sizeDelta = new Vector2(28f, 28f);
        hitArea.rectTransform.anchoredPosition = anchoredPosition;

        Image arrow = UiFactory.CreateImage(name, hitArea.rectTransform, sprites.GetResourceSprite(resourcePath), Color.white);
        arrow.type = Image.Type.Simple;
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        arrow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.sizeDelta = SelectorArrowSize;
        arrow.rectTransform.anchoredPosition = Vector2.zero;
        UiFactory.AddButton(hitArea.gameObject, delegate
        {
            if (previousDog)
            {
                if (previousDogRequested != null)
                {
                    previousDogRequested();
                }
            }
            else if (nextDogRequested != null)
            {
                nextDogRequested();
            }
        });
    }

    private void CreateNameField(RectTransform parent)
    {
        Image field = UiFactory.CreateImage("NameField", parent, UiTheme.DogDetailsFieldFillSprite, UiTheme.White);
        field.type = Image.Type.Sliced;
        field.preserveAspect = false;
        field.rectTransform.anchorMin = new Vector2(0f, 1f);
        field.rectTransform.anchorMax = new Vector2(0f, 1f);
        field.rectTransform.pivot = new Vector2(0f, 1f);
        field.rectTransform.sizeDelta = new Vector2(182f, 24f);
        field.rectTransform.anchoredPosition = new Vector2(27f, 0f);

        Image outline = UiFactory.CreateImage("Outline", field.rectTransform, UiTheme.DogDetailsFieldOutlineSprite, UiTheme.NavBrand);
        outline.type = Image.Type.Sliced;
        outline.preserveAspect = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        dogNameLabel = UiFactory.CreateLabel("DogName", field.rectTransform, "Pepper", 15, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        dogNameLabel.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(dogNameLabel.rectTransform, 0f, 0f, 0f, 0f);
    }

    public void SetDogName(string dogName)
    {
        if (dogNameLabel != null)
        {
            dogNameLabel.text = string.IsNullOrEmpty(dogName) ? "Dog" : dogName;
        }
    }

    public void BindDogSwitching(Action onPreviousDogRequested, Action onNextDogRequested)
    {
        previousDogRequested = onPreviousDogRequested;
        nextDogRequested = onNextDogRequested;
    }
}
