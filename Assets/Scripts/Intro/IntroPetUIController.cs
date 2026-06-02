using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum IntroPetUiState
{
    Selection,
    Confirm
}

public sealed class IntroPetUIController : MonoBehaviour
{
    private static readonly Color PanelFill = UiTheme.NavBackgroundCream;
    private static readonly Color Primary = UiTheme.NavBrand;
    private static readonly Color PrimaryDark = UiTheme.NavBrandDark;
    private static readonly Color SoftLine = new Color32(238, 205, 190, 255);
    private static readonly Color MutedText = new Color32(137, 104, 92, 255);
    private static readonly Color SoftBlue = new Color32(57, 169, 226, 255);
    private static readonly Color ChipFill = new Color32(255, 241, 232, 255);

    private RectTransform root;
    private RectTransform selectionCard;
    private RectTransform confirmCard;
    private TextMeshProUGUI breedLabel;
    private TextMeshProUGUI speciesLabel;
    private TextMeshProUGUI counterLabel;
    private TextMeshProUGUI descriptionLabel;
    private TextMeshProUGUI personalityLabel;
    private Image maleToggleFill;
    private Image femaleToggleFill;
    private TextMeshProUGUI maleToggleLabel;
    private TextMeshProUGUI femaleToggleLabel;
    private TMP_InputField nameInput;
    private TextMeshProUGUI confirmPromptLabel;
    private TextMeshProUGUI confirmContextLabel;
    private Button[] furButtons = new Button[0];
    private Image[] furButtonFills = new Image[0];
    private TextMeshProUGUI[] furButtonLabels = new TextMeshProUGUI[0];
    private bool suppressNameEvent;
    private IntroPetUiState state;

    public event Action<int> PetStepRequested;
    public event Action<int> FurIndexRequested;
    public event Action<PawPalDogGender> GenderChanged;
    public event Action ContinueRequested;
    public event Action<string> NameChanged;
    public event Action BackRequested;
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

        BuildSelectionCard();
        BuildConfirmCard();
        SetState(IntroPetUiState.Selection);
    }

    public void SetState(IntroPetUiState nextState)
    {
        state = nextState;
        if (selectionCard != null)
        {
            selectionCard.gameObject.SetActive(state == IntroPetUiState.Selection);
        }

        if (confirmCard != null)
        {
            confirmCard.gameObject.SetActive(state == IntroPetUiState.Confirm);
        }
    }

    public void Refresh(IntroPetRuntimeSelection selection, int index, int total)
    {
        if (selection == null)
        {
            return;
        }

        IntroPetDefinition definition = selection.Definition;
        string safeName = selection.SafePetName;
        breedLabel.text = definition != null ? definition.BreedLabel : "Pet";
        speciesLabel.text = definition != null ? definition.SpeciesLabel : "Pet";
        counterLabel.text = (index + 1).ToString() + " / " + Mathf.Max(1, total).ToString();
        descriptionLabel.text = definition != null ? definition.Description : string.Empty;
        personalityLabel.text = IntroPetFormatting.FormatPersonality(selection.Personality);
        confirmContextLabel.text = (definition != null ? definition.BreedLabel : "Pet") + " / " + IntroPetFormatting.FormatGender(selection.Gender);
        confirmPromptLabel.text = "Name your new companion and bring " + safeName + " home.";

        RefreshGender(selection.Gender);
        RefreshFurButtons(definition, selection.FurIndex);

        if (nameInput != null && !nameInput.isFocused && nameInput.text != safeName)
        {
            suppressNameEvent = true;
            nameInput.SetTextWithoutNotify(safeName);
            suppressNameEvent = false;
        }
    }

    private void BuildSelectionCard()
    {
        selectionCard = CreateCard(root, "SelectionCard", new Vector2(370f, 251f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f));

        CreateMetadataChips(selectionCard);

        Image breedField = UiFactory.CreateImage("BreedField", selectionCard, UiTheme.DogDetailsFieldFillSprite, Color.white);
        breedField.preserveAspect = false;
        Place(breedField.rectTransform, 92f, 28f, 186f, 24f);
        Image breedFieldBorder = UiFactory.CreateImage("BreedFieldBorder", selectionCard, UiTheme.DogDetailsFieldOutlineSprite, SoftLine);
        breedFieldBorder.preserveAspect = false;
        Place(breedFieldBorder.rectTransform, 92f, 28f, 186f, 24f);
        breedLabel = CreateLabel(selectionCard, "BreedLabel", "Labrador", 14, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        Place(breedLabel.rectTransform, 100f, 31f, 170f, 18f);

        CreateArrowButton(selectionCard, "PrevBreed", new Vector2(44f, 28f), new Vector2(24f, 24f), true, delegate
        {
            RaisePetStep(-1);
        });
        CreateArrowButton(selectionCard, "NextBreed", new Vector2(302f, 28f), new Vector2(24f, 24f), false, delegate
        {
            RaisePetStep(1);
        });

        descriptionLabel = CreateLabel(selectionCard, "DescriptionLabel", string.Empty, 12, MutedText, UiTheme.NavRegularFont, TextAlignmentOptions.TopLeft);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;
        Place(descriptionLabel.rectTransform, 32f, 67f, 306f, 48f);

        CreateLabel(selectionCard, "FurCaption", "Fur type:", 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(selectionCard.Find("FurCaption") as RectTransform, 32f, 122f, 88f, 18f);
        BuildFurButtons(selectionCard);

        CreateLabel(selectionCard, "GenderCaption", "Gender:", 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(selectionCard.Find("GenderCaption") as RectTransform, 32f, 164f, 70f, 18f);
        BuildGenderToggle(selectionCard);

        CreateLabel(selectionCard, "PersonalityCaption", "Personality", 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(selectionCard.Find("PersonalityCaption") as RectTransform, 32f, 196f, 90f, 18f);
        Image personalityChip = UiFactory.CreateImage("PersonalityChip", selectionCard, UiTheme.RoundedFiveSprite, ChipFill);
        Place(personalityChip.rectTransform, 125f, 192f, 96f, 24f);
        personalityLabel = CreateLabel(personalityChip.rectTransform, "PersonalityLabel", "Loyal", 12, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(personalityLabel.rectTransform, 8f, 2f, 8f, 2f);

        RectTransform continueButton = CreateButton(selectionCard, "ContinueButton", "Continue", new Vector2(246f, 186f), new Vector2(92f, 30f), SoftBlue, Color.white, delegate
        {
            Action handler = ContinueRequested;
            if (handler != null)
            {
                handler();
            }
        });
        continueButton.GetComponent<Image>().sprite = UiTheme.RoundedFiveSprite;
    }

    private void CreateMetadataChips(RectTransform parent)
    {
        Image speciesChip = UiFactory.CreateImage("SpeciesChip", parent, UiTheme.RoundedFiveSprite, ChipFill);
        speciesChip.preserveAspect = false;
        Place(speciesChip.rectTransform, 225f, 8f, 52f, 22f);
        speciesLabel = CreateLabel(speciesChip.rectTransform, "SpeciesLabel", "Dog", 11, Primary, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(speciesLabel.rectTransform, 6f, 2f, 6f, 2f);

        counterLabel = CreateLabel(parent, "CounterLabel", "1 / 5", 11, MutedText, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Right);
        Place(counterLabel.rectTransform, 286f, 10f, 52f, 18f);
    }

    private void BuildFurButtons(RectTransform parent)
    {
        const int buttonCount = 4;
        furButtons = new Button[buttonCount];
        furButtonFills = new Image[buttonCount];
        furButtonLabels = new TextMeshProUGUI[buttonCount];
        float startX = 114f;
        for (int i = 0; i < buttonCount; i++)
        {
            RectTransform button = CreateButton(parent, "FurButton" + i.ToString(), "Default", new Vector2(startX + i * 56f, 118f), new Vector2(48f, 24f), Color.white, PrimaryDark, null);
            button.GetComponent<Image>().sprite = UiTheme.DogDetailsFieldFillSprite;
            button.GetComponent<Image>().preserveAspect = false;
            Image border = UiFactory.CreateImage("Border", button, UiTheme.DogDetailsFieldOutlineSprite, SoftLine);
            border.preserveAspect = false;
            UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);
            int furIndex = i;
            Button clickable = UiFactory.AddButton(button.gameObject, delegate
            {
                RaiseFurIndex(furIndex);
            });

            furButtons[i] = clickable;
            furButtonFills[i] = button.GetComponent<Image>();
            furButtonLabels[i] = button.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void BuildGenderToggle(RectTransform parent)
    {
        RectTransform toggleRoot = UiFactory.CreateRect("GenderToggle", parent);
        Place(toggleRoot, 114f, 158f, 120f, 32f);
        Image fill = toggleRoot.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.color = new Color32(245, 233, 224, 255);
        fill.preserveAspect = false;

        RectTransform male = CreateButton(toggleRoot, "MaleToggle", "Male", new Vector2(3f, 3f), new Vector2(54f, 26f), Color.white, PrimaryDark, delegate
        {
            RaiseGender(PawPalDogGender.Male);
        });
        maleToggleFill = male.GetComponent<Image>();
        maleToggleLabel = male.GetComponentInChildren<TextMeshProUGUI>();

        RectTransform female = CreateButton(toggleRoot, "FemaleToggle", "Female", new Vector2(63f, 3f), new Vector2(54f, 26f), Color.white, PrimaryDark, delegate
        {
            RaiseGender(PawPalDogGender.Female);
        });
        femaleToggleFill = female.GetComponent<Image>();
        femaleToggleLabel = female.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void BuildConfirmCard()
    {
        confirmCard = CreateCard(root, "ConfirmCard", new Vector2(362f, 167f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f));

        confirmContextLabel = CreateLabel(confirmCard, "ConfirmContextLabel", "Labrador / Male", 14, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        Place(confirmContextLabel.rectTransform, 30f, 18f, 302f, 20f);

        confirmPromptLabel = CreateLabel(confirmCard, "ConfirmPromptLabel", string.Empty, 13, MutedText, UiTheme.NavRegularFont, TextAlignmentOptions.Center);
        confirmPromptLabel.textWrappingMode = TextWrappingModes.Normal;
        Place(confirmPromptLabel.rectTransform, 42f, 46f, 278f, 36f);

        RectTransform inputRoot = UiFactory.CreateRect("NameInputRoot", confirmCard);
        Place(inputRoot, 57f, 93f, 247f, 24f);
        Image inputFill = inputRoot.gameObject.AddComponent<Image>();
        inputFill.sprite = UiTheme.DogDetailsFieldFillSprite;
        inputFill.color = Color.white;
        inputFill.preserveAspect = false;
        Image inputBorder = UiFactory.CreateImage("InputBorder", inputRoot, UiTheme.DogDetailsFieldOutlineSprite, SoftLine);
        inputBorder.preserveAspect = false;
        UiFactory.Stretch(inputBorder.rectTransform, 0f, 0f, 0f, 0f);

        nameInput = inputRoot.gameObject.AddComponent<TMP_InputField>();
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.characterLimit = IntroPetRuntimeSelection.MaxNameVisibleCharacters;
        nameInput.targetGraphic = inputFill;

        TextMeshProUGUI placeholder = CreateLabel(inputRoot, "Placeholder", "Buddy", 13, new Color32(190, 151, 138, 255), UiTheme.NavRegularFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(placeholder.rectTransform, 8f, 2f, 8f, 2f);
        TextMeshProUGUI text = CreateLabel(inputRoot, "Text", string.Empty, 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
        nameInput.placeholder = placeholder;
        nameInput.textComponent = text;
        nameInput.onValueChanged.AddListener(HandleNameChanged);

        RectTransform backButton = CreateButton(confirmCard, "BackButton", "Back", new Vector2(57f, 125f), new Vector2(92f, 26f), Color.white, PrimaryDark, delegate
        {
            Action handler = BackRequested;
            if (handler != null)
            {
                handler();
            }
        });
        backButton.GetComponent<Image>().sprite = UiTheme.RoundedFiveOutlineSprite;

        RectTransform confirmButton = CreateButton(confirmCard, "ConfirmButton", "Let's go home!", new Vector2(162f, 125f), new Vector2(142f, 26f), SoftBlue, Color.white, delegate
        {
            Action handler = ConfirmAccepted;
            if (handler != null)
            {
                handler();
            }
        });
        confirmButton.GetComponent<Image>().sprite = UiTheme.RoundedFiveSprite;
    }

    private void RefreshGender(PawPalDogGender gender)
    {
        bool maleSelected = gender == PawPalDogGender.Male;
        SetToggleState(maleToggleFill, maleToggleLabel, maleSelected);
        SetToggleState(femaleToggleFill, femaleToggleLabel, !maleSelected);
    }

    private void RefreshFurButtons(IntroPetDefinition definition, int activeIndex)
    {
        FurVariantDefinition[] variants = definition != null ? definition.FurVariants : null;
        for (int i = 0; i < furButtons.Length; i++)
        {
            bool isVisible = variants != null && i < variants.Length;
            if (furButtons[i] != null)
            {
                furButtons[i].gameObject.SetActive(isVisible);
            }

            if (!isVisible)
            {
                continue;
            }

            bool isActive = i == activeIndex;
            furButtonFills[i].color = isActive ? Primary : Color.white;
            furButtonLabels[i].color = isActive ? Color.white : PrimaryDark;
            furButtonLabels[i].text = GetVariantButtonLabel(variants[i], i);
        }
    }

    private void SetToggleState(Image fill, TextMeshProUGUI label, bool selected)
    {
        if (fill != null)
        {
            fill.sprite = UiTheme.RoundedTenSprite;
            fill.color = selected ? Primary : Color.white;
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

    private void RaiseFurIndex(int index)
    {
        Action<int> handler = FurIndexRequested;
        if (handler != null)
        {
            handler(index);
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

    private static RectTransform CreateCard(Transform parent, string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
    {
        RectTransform card = UiFactory.CreateRect(name, parent);
        card.anchorMin = anchorMin;
        card.anchorMax = anchorMax;
        card.pivot = new Vector2(0.5f, 0f);
        card.anchoredPosition = anchoredPosition;
        card.sizeDelta = size;

        Image fill = card.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.color = PanelFill;
        fill.preserveAspect = false;

        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(80f / 255f, 50f / 255f, 38f / 255f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -3f);
        return card;
    }

    private static void CreateArrowButton(RectTransform parent, string name, Vector2 position, Vector2 size, bool flipX, UnityAction onClick)
    {
        Image hitArea = UiFactory.CreateImage(name, parent, UiTheme.CircleSprite, new Color32(255, 255, 255, 0));
        RectTransform rect = hitArea.rectTransform;
        Place(rect, position.x, position.y, size.x, size.y);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        UiFactory.AddButton(hitArea.gameObject, onClick);

        Sprite arrowSprite = Resources.Load<Sprite>("UI/Icons/icon_nextarrow");
        if (arrowSprite == null)
        {
            TextMeshProUGUI fallback = CreateLabel(rect, "ArrowFallback", flipX ? "<" : ">", 18, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
            UiFactory.Stretch(fallback.rectTransform, 0f, 0f, 0f, 0f);
            return;
        }

        Image arrow = UiFactory.CreateImage("Arrow", rect, arrowSprite, PrimaryDark);
        UiFactory.Stretch(arrow.rectTransform, 4f, 4f, 4f, 4f);
        arrow.type = Image.Type.Simple;
        arrow.preserveAspect = true;
        if (flipX)
        {
            arrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    private static RectTransform CreateButton(RectTransform parent, string name, string text, Vector2 position, Vector2 size, Color fillColor, Color textColor, UnityAction onClick)
    {
        Image fill = UiFactory.CreateImage(name, parent, UiTheme.RoundedTenSprite, fillColor);
        fill.preserveAspect = false;
        RectTransform rect = fill.rectTransform;
        Place(rect, position.x, position.y, size.x, size.y);
        UiFactory.AddButton(fill.gameObject, onClick);

        TextMeshProUGUI label = CreateLabel(rect, "Label", text, 13, textColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
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

    private static string GetVariantButtonLabel(FurVariantDefinition variant, int index)
    {
        string label = variant != null ? variant.SafeDisplayName : "Fur " + (index + 1).ToString();
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "Fur " + (index + 1).ToString();
        }

        return label.Length > 9 ? label.Substring(0, 9) : label;
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
