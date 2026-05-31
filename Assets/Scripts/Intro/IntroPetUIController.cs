using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class IntroPetUIController : MonoBehaviour
{
    private static readonly Color PanelFill = UiTheme.NavBackgroundCream;
    private static readonly Color Primary = UiTheme.NavBrand;
    private static readonly Color PrimaryDark = UiTheme.NavBrandDark;
    private static readonly Color SoftLine = new Color32(238, 205, 190, 255);
    private static readonly Color MutedText = new Color32(137, 104, 92, 255);
    private static readonly Color SoftBlue = new Color32(57, 169, 226, 255);

    private RectTransform root;
    private RectTransform modalLayer;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI speciesLabel;
    private TextMeshProUGUI descriptionLabel;
    private TextMeshProUGUI furLabel;
    private TextMeshProUGUI personalityLabel;
    private TextMeshProUGUI selectionCounterLabel;
    private TextMeshProUGUI confirmModalLabel;
    private TMP_InputField nameInput;
    private Image maleButtonFill;
    private Image femaleButtonFill;
    private TextMeshProUGUI maleButtonLabel;
    private TextMeshProUGUI femaleButtonLabel;
    private Image[] swatchFills = new Image[0];
    private Image[] swatchBorders = new Image[0];
    private bool suppressNameEvent;

    public event Action<int> PetStepRequested;
    public event Action<int> FurStepRequested;
    public event Action<int> FurIndexRequested;
    public event Action<PawPalDogGender> GenderChanged;
    public event Action<string> NameChanged;
    public event Action ConfirmRequested;
    public event Action ConfirmAccepted;

    public void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect != null)
        {
            UiFactory.Stretch(canvasRect, 0f, 0f, 0f, 0f);
        }

        root = UiFactory.CreateRect("SafeAreaRoot", transform);
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);
        root.gameObject.AddComponent<SafeAreaFitter>();

        BuildSelectionChrome();
        BuildBottomPanel();
        BuildConfirmModal();
    }

    public void Refresh(IntroPetRuntimeSelection selection, int index, int total)
    {
        if (selection == null)
        {
            return;
        }

        IntroPetDefinition definition = selection.Definition;
        titleLabel.text = definition != null ? definition.BreedLabel : "Pet";
        speciesLabel.text = definition != null ? definition.SpeciesLabel : "Pet";
        descriptionLabel.text = definition != null ? definition.Description : string.Empty;
        furLabel.text = selection.FurVariant != null ? selection.FurVariant.SafeDisplayName : "Default";
        personalityLabel.text = IntroPetFormatting.FormatPersonality(selection.Personality);
        selectionCounterLabel.text = (index + 1).ToString() + " / " + Mathf.Max(1, total).ToString();

        RefreshGender(selection.Gender);
        RefreshSwatches(definition, selection.FurIndex);

        string safeName = selection.SafePetName;
        if (nameInput != null && !nameInput.isFocused && nameInput.text != safeName)
        {
            suppressNameEvent = true;
            nameInput.SetTextWithoutNotify(safeName);
            suppressNameEvent = false;
        }

        if (confirmModalLabel != null)
        {
            confirmModalLabel.text = "Bring " + safeName + " home?";
        }
    }

    public void ShowConfirmModal(bool visible, IntroPetRuntimeSelection selection)
    {
        if (modalLayer == null)
        {
            return;
        }

        if (selection != null && confirmModalLabel != null)
        {
            confirmModalLabel.text = "Bring " + selection.SafePetName + " home?";
        }

        modalLayer.gameObject.SetActive(visible);
    }

    private void BuildSelectionChrome()
    {
        RectTransform previousButton = CreateButton(root, "PreviousPet", "<", new Vector2(14f, 368f), new Vector2(48f, 48f), PanelFill, PrimaryDark, delegate
        {
            RaisePetStep(-1);
        });
        previousButton.anchorMin = new Vector2(0f, 0.5f);
        previousButton.anchorMax = new Vector2(0f, 0.5f);
        previousButton.pivot = new Vector2(0f, 0.5f);
        previousButton.anchoredPosition = new Vector2(14f, 40f);

        RectTransform nextButton = CreateButton(root, "NextPet", ">", new Vector2(331f, 368f), new Vector2(48f, 48f), PanelFill, PrimaryDark, delegate
        {
            RaisePetStep(1);
        });
        nextButton.anchorMin = new Vector2(1f, 0.5f);
        nextButton.anchorMax = new Vector2(1f, 0.5f);
        nextButton.pivot = new Vector2(1f, 0.5f);
        nextButton.anchoredPosition = new Vector2(-14f, 40f);
    }

    private void BuildBottomPanel()
    {
        RectTransform panel = UiFactory.CreateRect("SelectionPanel", root);
        UiFactory.AnchorBottomStretch(panel, 14f, 14f, 14f, 292f);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = UiTheme.RoundedTenSprite;
        panelImage.color = PanelFill;
        Shadow shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(80f / 255f, 50f / 255f, 38f / 255f, 0.2f);
        shadow.effectDistance = new Vector2(0f, -3f);

        titleLabel = CreateLabel(panel, "Breed", "Labrador", 20, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(titleLabel.rectTransform, 18f, 15f, 205f, 28f);

        speciesLabel = CreateLabel(panel, "Species", "Dog", 11, Primary, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        Place(speciesLabel.rectTransform, 242f, 17f, 48f, 22f);

        selectionCounterLabel = CreateLabel(panel, "SelectionCounter", "1 / 6", 11, MutedText, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Right);
        Place(selectionCounterLabel.rectTransform, 294f, 18f, 48f, 22f);

        descriptionLabel = CreateLabel(panel, "Description", string.Empty, 12, MutedText, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;
        Place(descriptionLabel.rectTransform, 18f, 48f, 324f, 42f);

        CreateLabel(panel, "FurCaption", "Fur", 11, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(panel.Find("FurCaption") as RectTransform, 18f, 101f, 42f, 20f);

        RectTransform previousFur = CreateButton(panel, "PreviousFur", "<", new Vector2(68f, 96f), new Vector2(30f, 28f), UiTheme.CardWhite, PrimaryDark, delegate
        {
            RaiseFurStep(-1);
        });
        previousFur.GetComponent<Image>().sprite = UiTheme.RoundedFiveOutlineSprite;

        furLabel = CreateLabel(panel, "FurName", "Default", 12, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        Place(furLabel.rectTransform, 104f, 100f, 98f, 20f);

        RectTransform nextFur = CreateButton(panel, "NextFur", ">", new Vector2(207f, 96f), new Vector2(30f, 28f), UiTheme.CardWhite, PrimaryDark, delegate
        {
            RaiseFurStep(1);
        });
        nextFur.GetComponent<Image>().sprite = UiTheme.RoundedFiveOutlineSprite;

        BuildSwatches(panel);
        BuildGenderButtons(panel);

        CreateLabel(panel, "PersonalityCaption", "Personality", 11, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(panel.Find("PersonalityCaption") as RectTransform, 18f, 151f, 92f, 18f);

        Image personalityChip = UiFactory.CreateImage("PersonalityChip", panel, UiTheme.RoundedFiveSprite, new Color32(255, 241, 232, 255));
        Place(personalityChip.rectTransform, 108f, 146f, 118f, 28f);
        personalityLabel = CreateLabel(personalityChip.rectTransform, "Personality", "Loyal", 12, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(personalityLabel.rectTransform, 8f, 2f, 8f, 2f);

        BuildNameInput(panel);

        RectTransform confirm = CreateButton(panel, "Confirm", "Let's go home", new Vector2(217f, 224f), new Vector2(125f, 40f), SoftBlue, Color.white, delegate
        {
            Action handler = ConfirmRequested;
            if (handler != null)
            {
                handler();
            }
        });
        confirm.GetComponent<Image>().sprite = UiTheme.RoundedTenSprite;
    }

    private void BuildSwatches(RectTransform panel)
    {
        swatchFills = new Image[4];
        swatchBorders = new Image[4];
        for (int i = 0; i < swatchFills.Length; i++)
        {
            RectTransform swatch = UiFactory.CreateRect("FurSwatch" + i.ToString(), panel);
            Place(swatch, 249f + i * 24f, 98f, 20f, 20f);
            Image border = UiFactory.CreateImage("Border", swatch, UiTheme.CircleOutlineSprite, SoftLine);
            UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);
            Image fill = UiFactory.CreateImage("Fill", swatch, UiTheme.CircleSprite, Color.white);
            UiFactory.Stretch(fill.rectTransform, 4f, 4f, 4f, 4f);
            int swatchIndex = i;
            UiFactory.AddButton(swatch.gameObject, delegate
            {
                Action<int> handler = FurIndexRequested;
                if (handler != null)
                {
                    handler(swatchIndex);
                }
            });
            swatchFills[i] = fill;
            swatchBorders[i] = border;
        }
    }

    private void BuildGenderButtons(RectTransform panel)
    {
        CreateLabel(panel, "GenderCaption", "Gender", 11, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(panel.Find("GenderCaption") as RectTransform, 18f, 196f, 62f, 18f);

        RectTransform male = CreateButton(panel, "GenderMale", "Male", new Vector2(82f, 188f), new Vector2(76f, 30f), UiTheme.CardWhite, PrimaryDark, delegate
        {
            RaiseGender(PawPalDogGender.Male);
        });
        maleButtonFill = male.GetComponent<Image>();
        maleButtonLabel = male.GetComponentInChildren<TextMeshProUGUI>();

        RectTransform female = CreateButton(panel, "GenderFemale", "Female", new Vector2(160f, 188f), new Vector2(76f, 30f), UiTheme.CardWhite, PrimaryDark, delegate
        {
            RaiseGender(PawPalDogGender.Female);
        });
        femaleButtonFill = female.GetComponent<Image>();
        femaleButtonLabel = female.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void BuildNameInput(RectTransform panel)
    {
        CreateLabel(panel, "NameCaption", "Name", 11, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(panel.Find("NameCaption") as RectTransform, 18f, 237f, 52f, 18f);

        RectTransform inputRoot = UiFactory.CreateRect("NameInput", panel);
        Place(inputRoot, 67f, 226f, 137f, 36f);
        Image inputFill = inputRoot.gameObject.AddComponent<Image>();
        inputFill.sprite = UiTheme.RoundedTenOutlineSprite;
        inputFill.color = UiTheme.CardWhite;

        nameInput = inputRoot.gameObject.AddComponent<TMP_InputField>();
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.characterLimit = IntroPetRuntimeSelection.MaxNameVisibleCharacters;
        nameInput.targetGraphic = inputFill;

        TextMeshProUGUI placeholder = CreateLabel(inputRoot, "Placeholder", "Buddy", 13, new Color32(190, 151, 138, 255), UiTheme.NavRegularFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(placeholder.rectTransform, 8f, 3f, 8f, 3f);

        TextMeshProUGUI text = CreateLabel(inputRoot, "Text", string.Empty, 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(text.rectTransform, 8f, 3f, 8f, 3f);

        nameInput.placeholder = placeholder;
        nameInput.textComponent = text;
        nameInput.onValueChanged.AddListener(HandleNameChanged);
    }

    private void BuildConfirmModal()
    {
        modalLayer = UiFactory.CreateRect("ConfirmModal", root);
        UiFactory.Stretch(modalLayer, 0f, 0f, 0f, 0f);
        Image blocker = modalLayer.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.36f);
        UiFactory.AddButton(modalLayer.gameObject, delegate
        {
            ShowConfirmModal(false, null);
        });

        RectTransform panel = UiFactory.CreateRect("Panel", modalLayer);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -18f);
        panel.sizeDelta = new Vector2(304f, 154f);
        Image fill = panel.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.color = PanelFill;

        confirmModalLabel = CreateLabel(panel, "Title", "Bring Buddy home?", 20, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        Place(confirmModalLabel.rectTransform, 18f, 23f, 268f, 30f);

        TextMeshProUGUI detail = CreateLabel(panel, "Detail", "Your choice will load into the home scene for this play session.", 12, MutedText, UiTheme.NavRegularFont, TextAlignmentOptions.Center);
        detail.textWrappingMode = TextWrappingModes.Normal;
        Place(detail.rectTransform, 28f, 59f, 248f, 34f);

        CreateButton(panel, "Cancel", "Back", new Vector2(35f, 105f), new Vector2(101f, 35f), UiTheme.CardWhite, PrimaryDark, delegate
        {
            ShowConfirmModal(false, null);
        });

        CreateButton(panel, "Accept", "Confirm", new Vector2(164f, 105f), new Vector2(105f, 35f), SoftBlue, Color.white, delegate
        {
            Action handler = ConfirmAccepted;
            if (handler != null)
            {
                handler();
            }
        });

        modalLayer.gameObject.SetActive(false);
    }

    private void RefreshGender(PawPalDogGender gender)
    {
        bool maleSelected = gender == PawPalDogGender.Male;
        SetSegment(maleButtonFill, maleButtonLabel, maleSelected);
        SetSegment(femaleButtonFill, femaleButtonLabel, !maleSelected);
    }

    private void RefreshSwatches(IntroPetDefinition definition, int activeIndex)
    {
        int count = definition != null && definition.FurVariants != null ? definition.FurVariants.Length : 0;
        for (int i = 0; i < swatchFills.Length; i++)
        {
            bool visible = i < count;
            if (swatchFills[i] != null)
            {
                swatchFills[i].transform.parent.gameObject.SetActive(visible);
                swatchFills[i].color = visible && definition.FurVariants[i] != null ? definition.FurVariants[i].SwatchColor : Color.white;
            }

            if (swatchBorders[i] != null)
            {
                swatchBorders[i].color = i == activeIndex ? Primary : SoftLine;
            }
        }
    }

    private void SetSegment(Image fill, TextMeshProUGUI label, bool selected)
    {
        if (fill != null)
        {
            fill.sprite = UiTheme.RoundedTenSprite;
            fill.color = selected ? Primary : UiTheme.CardWhite;
        }

        if (label != null)
        {
            label.color = selected ? Color.white : PrimaryDark;
        }
    }

    private void HandleNameChanged(string value)
    {
        if (suppressNameEvent)
        {
            return;
        }

        string sanitized = IntroPetRuntimeSelection.SanitizeName(value);
        if (value != sanitized)
        {
            suppressNameEvent = true;
            nameInput.SetTextWithoutNotify(sanitized);
            suppressNameEvent = false;
        }

        Action<string> handler = NameChanged;
        if (handler != null)
        {
            handler(sanitized);
        }
    }

    private void RaisePetStep(int direction)
    {
        Action<int> handler = PetStepRequested;
        if (handler != null)
        {
            handler(direction);
        }
    }

    private void RaiseFurStep(int direction)
    {
        Action<int> handler = FurStepRequested;
        if (handler != null)
        {
            handler(direction);
        }
    }

    private void RaiseGender(PawPalDogGender gender)
    {
        Action<PawPalDogGender> handler = GenderChanged;
        if (handler != null)
        {
            handler(gender);
        }
    }

    private static RectTransform CreateButton(RectTransform parent, string name, string text, Vector2 position, Vector2 size, Color fillColor, Color textColor, UnityAction onClick)
    {
        Image fill = UiFactory.CreateImage(name, parent, UiTheme.RoundedTenSprite, fillColor);
        RectTransform rect = fill.rectTransform;
        Place(rect, position.x, position.y, size.x, size.y);
        UiFactory.AddButton(fill.gameObject, onClick);

        TextMeshProUGUI label = CreateLabel(rect, "Label", text, 14, textColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 4f, 2f, 4f, 2f);
        return rect;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, int fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Normal, alignment);
        label.font = font != null ? font : UiTheme.DefaultFont;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(8, fontSize - 3);
        label.fontSizeMax = fontSize;
        return label;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
