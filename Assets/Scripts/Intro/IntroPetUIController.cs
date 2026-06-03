using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum IntroPetUiState
{
    Selection,
    Customize
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
    private RectTransform customizeCard;
    private CanvasGroup uiVisibilityGroup;
    private CanvasGroup selectionCardGroup;
    private RectTransform whistleButtonRoot;
    private RectTransform previousArrowRoot;
    private RectTransform nextArrowRoot;
    private TextMeshProUGUI stageLabel;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI breedLabel;
    private TextMeshProUGUI descriptionLabel;
    private TextMeshProUGUI selectionPersonalityLabel;
    private TextMeshProUGUI customizeStageLabel;
    private TextMeshProUGUI customizeBreedLabel;
    private Image maleToggleFill;
    private Image femaleToggleFill;
    private TextMeshProUGUI maleToggleLabel;
    private TextMeshProUGUI femaleToggleLabel;
    private TMP_InputField nameInput;
    private Button[] furButtons = new Button[0];
    private Image[] furButtonFills = new Image[0];
    private TextMeshProUGUI[] furButtonLabels = new TextMeshProUGUI[0];
    private RectTransform[] furButtonRoots = new RectTransform[0];
    private bool suppressNameEvent;
    private bool previewInteractionPresentationActive;

    public event Action<int> PetStepRequested;
    public event Action<int> FurIndexRequested;
    public event Action<PawPalDogGender> GenderChanged;
    public event Action ContinueRequested;
    public event Action<string> NameChanged;
    public event Action BackRequested;
    public event Action ConfirmAccepted;
    public event Action WhistleRequested;

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
        uiVisibilityGroup = root.gameObject.GetComponent<CanvasGroup>();
        if (uiVisibilityGroup == null)
        {
            uiVisibilityGroup = root.gameObject.AddComponent<CanvasGroup>();
        }

        BuildSelectionCard();
        BuildCustomizeCard();
        SetState(IntroPetUiState.Selection);
        SetUiVisible(true);
    }

    public void SetState(IntroPetUiState nextState)
    {
        if (selectionCard != null)
        {
            selectionCard.gameObject.SetActive(nextState == IntroPetUiState.Selection);
        }

        if (customizeCard != null)
        {
            customizeCard.gameObject.SetActive(nextState == IntroPetUiState.Customize);
        }

        RefreshPreviewInteractionPresentation();
    }

    public void Refresh(IntroPetRuntimeSelection selection, int index, int total)
    {
        if (selection == null)
        {
            return;
        }

        IntroPetDefinition definition = selection.Definition;
        string safeName = selection.SafePetName;
        string breed = definition != null ? definition.BreedLabel : "Pet";
        string lifeStage = IntroPetFormatting.FormatIntroLifeStage(definition);

        if (nameLabel != null)
        {
            nameLabel.text = safeName;
        }

        if (stageLabel != null)
        {
            stageLabel.text = lifeStage;
        }

        if (breedLabel != null)
        {
            breedLabel.text = breed;
        }

        if (descriptionLabel != null)
        {
            descriptionLabel.text = definition != null ? definition.Description : string.Empty;
        }

        if (selectionPersonalityLabel != null)
        {
            selectionPersonalityLabel.text = IntroPetFormatting.FormatPersonality(selection.Personality);
        }

        if (customizeStageLabel != null)
        {
            customizeStageLabel.text = lifeStage;
        }

        if (customizeBreedLabel != null)
        {
            customizeBreedLabel.text = breed;
        }

        RefreshGender(selection.Gender);
        RefreshFurButtons(definition, selection.FurIndex);

        if (nameInput != null && !nameInput.isFocused && nameInput.text != safeName)
        {
            suppressNameEvent = true;
            nameInput.SetTextWithoutNotify(safeName);
            suppressNameEvent = false;
        }
    }

    public void SetUiVisible(bool visible)
    {
        if (uiVisibilityGroup == null)
        {
            return;
        }

        uiVisibilityGroup.alpha = visible ? 1f : 0f;
        uiVisibilityGroup.interactable = visible;
        uiVisibilityGroup.blocksRaycasts = visible;
    }

    public void SetPreviewInteractionPresentation(bool active)
    {
        previewInteractionPresentationActive = active;
        RefreshPreviewInteractionPresentation();
    }

    private void BuildSelectionCard()
    {
        selectionCard = CreateCard(root, "SelectionCard", new Vector2(332f, 174f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f));
        selectionCardGroup = selectionCard.gameObject.GetComponent<CanvasGroup>();
        if (selectionCardGroup == null)
        {
            selectionCardGroup = selectionCard.gameObject.AddComponent<CanvasGroup>();
        }

        CreateSelectionHeader(selectionCard);
        CreateWhistleButton(selectionCard);

        Image nameField = UiFactory.CreateImage("NameField", selectionCard, UiTheme.DogDetailsFieldFillSprite, Color.white);
        nameField.type = Image.Type.Sliced;
        nameField.preserveAspect = false;
        Place(nameField.rectTransform, 75f, 34f, 182f, 24f);

        Image nameFieldBorder = UiFactory.CreateImage("NameFieldBorder", selectionCard, UiTheme.DogDetailsFieldOutlineSprite, SoftLine);
        nameFieldBorder.type = Image.Type.Sliced;
        nameFieldBorder.preserveAspect = false;
        Place(nameFieldBorder.rectTransform, 75f, 34f, 182f, 24f);

        nameLabel = CreateLabel(selectionCard, "NameLabel", "Buddy", 14, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        Place(nameLabel.rectTransform, 83f, 37f, 166f, 18f);

        previousArrowRoot = CreateArrowButton(selectionCard, "PrevBreed", "UI/Figma/HomeMain/button_back", new Vector2(47f, 46f), true, delegate
        {
            RaisePetStep(-1);
        });
        nextArrowRoot = CreateArrowButton(selectionCard, "NextBreed", "UI/Figma/HomeMain/button_forward", new Vector2(285f, 46f), false, delegate
        {
            RaisePetStep(1);
        });

        descriptionLabel = CreateLabel(selectionCard, "DescriptionLabel", string.Empty, 12, MutedText, UiTheme.NavRegularFont, TextAlignmentOptions.TopLeft);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
        descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;
        Place(descriptionLabel.rectTransform, 28f, 66f, 276f, 42f);

        CreateLabel(selectionCard, "SelectionPersonalityCaption", "Personality", 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(selectionCard.Find("SelectionPersonalityCaption") as RectTransform, 24f, 116f, 90f, 18f);

        Image selectionPersonalityChip = UiFactory.CreateImage("SelectionPersonalityChip", selectionCard, UiTheme.RoundedFiveSprite, ChipFill);
        selectionPersonalityChip.type = Image.Type.Sliced;
        selectionPersonalityChip.preserveAspect = false;
        Place(selectionPersonalityChip.rectTransform, 116f, 113f, 96f, 24f);

        selectionPersonalityLabel = CreateLabel(selectionPersonalityChip.rectTransform, "SelectionPersonalityLabel", "Loyal", 12, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(selectionPersonalityLabel.rectTransform, 8f, 2f, 8f, 2f);

        CreateContinueButton(selectionCard, "ContinueButton", "Continue", new Vector2(118f, 142f), new Vector2(96f, 24f), delegate
        {
            Action handler = ContinueRequested;
            if (handler != null)
            {
                handler();
            }
        });
    }

    private void CreateSelectionHeader(RectTransform parent)
    {
        Image stageChip = UiFactory.CreateImage("StageChip", parent, UiTheme.RoundedFiveSprite, ChipFill);
        stageChip.type = Image.Type.Sliced;
        stageChip.preserveAspect = false;
        Place(stageChip.rectTransform, 80f, 8f, 68f, 20f);

        stageLabel = CreateLabel(stageChip.rectTransform, "StageLabel", "Adult", 11, Primary, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(stageLabel.rectTransform, 6f, 2f, 6f, 2f);

        Image breedChip = UiFactory.CreateImage("BreedChip", parent, UiTheme.RoundedFiveSprite, ChipFill);
        breedChip.type = Image.Type.Sliced;
        breedChip.preserveAspect = false;
        Place(breedChip.rectTransform, 156f, 8f, 98f, 20f);

        breedLabel = CreateLabel(breedChip.rectTransform, "BreedLabel", "Labrador", 11, Primary, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(breedLabel.rectTransform, 6f, 2f, 6f, 2f);
    }

    private void CreateWhistleButton(RectTransform parent)
    {
        Image background = UiFactory.CreateImage("WhistleButton", parent, UiTheme.CircleSprite, UiTheme.NavBackgroundCream);
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(54f, 54f);
        backgroundRect.anchoredPosition = new Vector2(0f, 38f);
        whistleButtonRoot = backgroundRect;

        Shadow shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = UiTheme.NavShadow;
        shadow.effectDistance = new Vector2(0f, -1f);
        shadow.useGraphicAlpha = true;

        Image icon = UiFactory.CreateImage(
            "Icon",
            background.rectTransform,
            Resources.Load<Sprite>("UI/Icons/icon_whistle_brand"),
            Color.white);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(60f, 60f);
        icon.rectTransform.anchoredPosition = Vector2.zero;

        Button button = UiFactory.AddButton(background.gameObject, delegate
        {
            Action handler = WhistleRequested;
            if (handler != null)
            {
                handler();
            }
        });
        PawPalUiAudio.AttachTo(button, PawPalUiClickSoundKind.Whistle);
    }

    private void RefreshPreviewInteractionPresentation()
    {
        bool selectionVisible = selectionCard != null && selectionCard.gameObject.activeSelf;
        if (selectionCardGroup != null)
        {
            selectionCardGroup.alpha = 1f;
            selectionCardGroup.interactable = selectionVisible;
            selectionCardGroup.blocksRaycasts = selectionVisible;
        }

        if (whistleButtonRoot != null)
        {
            whistleButtonRoot.gameObject.SetActive(selectionVisible && !previewInteractionPresentationActive);
        }

        if (previousArrowRoot != null)
        {
            previousArrowRoot.gameObject.SetActive(selectionVisible && !previewInteractionPresentationActive);
        }

        if (nextArrowRoot != null)
        {
            nextArrowRoot.gameObject.SetActive(selectionVisible && !previewInteractionPresentationActive);
        }
    }

    private void BuildCustomizeCard()
    {
        customizeCard = CreateCard(root, "CustomizeCard", new Vector2(336f, 214f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f));

        Image customizeStageChip = UiFactory.CreateImage("CustomizeStageChip", customizeCard, UiTheme.RoundedFiveSprite, ChipFill);
        customizeStageChip.type = Image.Type.Sliced;
        customizeStageChip.preserveAspect = false;
        Place(customizeStageChip.rectTransform, 80f, 14f, 68f, 20f);

        customizeStageLabel = CreateLabel(customizeStageChip.rectTransform, "CustomizeStageLabel", "Adult", 11, Primary, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(customizeStageLabel.rectTransform, 6f, 2f, 6f, 2f);

        Image customizeBreedChip = UiFactory.CreateImage("CustomizeBreedChip", customizeCard, UiTheme.RoundedFiveSprite, ChipFill);
        customizeBreedChip.type = Image.Type.Sliced;
        customizeBreedChip.preserveAspect = false;
        Place(customizeBreedChip.rectTransform, 156f, 14f, 98f, 20f);

        customizeBreedLabel = CreateLabel(customizeBreedChip.rectTransform, "CustomizeBreedLabel", "Labrador", 11, Primary, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(customizeBreedLabel.rectTransform, 6f, 2f, 6f, 2f);

        RectTransform inputRoot = UiFactory.CreateRect("NameInputRoot", customizeCard);
        Place(inputRoot, 44f, 45f, 247f, 24f);
        Image inputFill = inputRoot.gameObject.AddComponent<Image>();
        inputFill.sprite = UiTheme.DogDetailsFieldFillSprite;
        inputFill.color = Color.white;
        inputFill.type = Image.Type.Sliced;
        inputFill.preserveAspect = false;

        Image inputBorder = UiFactory.CreateImage("InputBorder", inputRoot, UiTheme.DogDetailsFieldOutlineSprite, SoftLine);
        inputBorder.type = Image.Type.Sliced;
        inputBorder.preserveAspect = false;
        UiFactory.Stretch(inputBorder.rectTransform, 0f, 0f, 0f, 0f);

        nameInput = inputRoot.gameObject.AddComponent<TMP_InputField>();
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.characterLimit = IntroPetRuntimeSelection.MaxNameVisibleCharacters;
        nameInput.targetGraphic = inputFill;

        TextMeshProUGUI placeholder = CreateLabel(inputRoot, "Placeholder", "Pet name", 13, new Color32(190, 151, 138, 255), UiTheme.NavRegularFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(placeholder.rectTransform, 8f, 2f, 8f, 2f);
        TextMeshProUGUI text = CreateLabel(inputRoot, "Text", string.Empty, 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
        nameInput.placeholder = placeholder;
        nameInput.textComponent = text;
        nameInput.onValueChanged.AddListener(HandleNameChanged);

        CreateLabel(customizeCard, "FurCaption", "Fur type:", 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(customizeCard.Find("FurCaption") as RectTransform, 24f, 79f, 88f, 18f);
        BuildFurButtons(customizeCard, 75f);

        CreateLabel(customizeCard, "GenderCaption", "Gender:", 13, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        Place(customizeCard.Find("GenderCaption") as RectTransform, 24f, 115f, 70f, 18f);
        BuildGenderToggle(customizeCard, 109f);

        CreateStyledActionButton(customizeCard, "BackButton", "Back", new Vector2(42f, 180f), new Vector2(92f, 26f), new Color32(209, 209, 209, 255), Color.white, new Color(0f, 0f, 0f, 0.14f), delegate
        {
            Action handler = BackRequested;
            if (handler != null)
            {
                handler();
            }
        });

        CreateStyledActionButton(customizeCard, "ConfirmButton", "Let's go home!", new Vector2(147f, 180f), new Vector2(142f, 26f), SoftBlue, Color.white, new Color(0f, 82f / 255f, 132f / 255f, 0.28f), delegate
        {
            Action handler = ConfirmAccepted;
            if (handler != null)
            {
                handler();
            }
        });
    }

    private void BuildFurButtons(RectTransform parent, float y)
    {
        const int buttonCount = 4;
        furButtons = new Button[buttonCount];
        furButtonFills = new Image[buttonCount];
        furButtonLabels = new TextMeshProUGUI[buttonCount];
        furButtonRoots = new RectTransform[buttonCount];
        float startX = 114f;
        for (int i = 0; i < buttonCount; i++)
        {
            RectTransform button = CreateButton(parent, "FurButton" + i.ToString(), "Default", new Vector2(startX + i * 56f, y), new Vector2(48f, 24f), Color.white, PrimaryDark, null);
            button.GetComponent<Image>().sprite = UiTheme.DogDetailsFieldFillSprite;
            button.GetComponent<Image>().type = Image.Type.Sliced;
            button.GetComponent<Image>().preserveAspect = false;

            Image border = UiFactory.CreateImage("Border", button, UiTheme.DogDetailsFieldOutlineSprite, SoftLine);
            border.type = Image.Type.Sliced;
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
            furButtonRoots[i] = button;
            if (furButtonLabels[i] != null)
            {
                furButtonLabels[i].enableAutoSizing = false;
                furButtonLabels[i].fontSize = 13f;
            }
        }
    }

    private void BuildGenderToggle(RectTransform parent, float y)
    {
        RectTransform toggleRoot = UiFactory.CreateRect("GenderToggle", parent);
        Place(toggleRoot, 106f, y, 120f, 32f);

        Image fill = toggleRoot.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.color = new Color32(245, 233, 224, 255);
        fill.type = Image.Type.Sliced;
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

    private void RefreshGender(PawPalDogGender gender)
    {
        bool maleSelected = gender == PawPalDogGender.Male;
        SetToggleState(maleToggleFill, maleToggleLabel, maleSelected);
        SetToggleState(femaleToggleFill, femaleToggleLabel, !maleSelected);
    }

    private void RefreshFurButtons(IntroPetDefinition definition, int activeIndex)
    {
        FurVariantDefinition[] variants = definition != null ? definition.FurVariants : null;
        int visibleCount = 0;
        float[] widths = new float[furButtons.Length];
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
            string label = GetVariantButtonLabel(variants[i], i);
            furButtonLabels[i].text = label;
            widths[i] = GetFurButtonWidth(furButtonLabels[i], label);
            visibleCount++;
        }

        if (visibleCount == 0)
        {
            return;
        }

        const float startX = 114f;
        const float spacing = 8f;
        float currentX = startX;
        for (int i = 0; i < furButtonRoots.Length; i++)
        {
            if (furButtonRoots[i] == null || furButtons[i] == null || !furButtons[i].gameObject.activeSelf)
            {
                continue;
            }

            RectTransform rootRect = furButtonRoots[i];
            float width = widths[i] > 0f ? widths[i] : 48f;
            Place(rootRect, currentX, 75f, width, 24f);
            currentX += width + spacing;
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
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.color = PanelFill;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;

        Image border = UiFactory.CreateImage("Border", card, UiTheme.RoundedFiveOutlineSprite, SoftLine);
        border.type = Image.Type.Sliced;
        border.preserveAspect = false;
        UiFactory.Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(80f / 255f, 50f / 255f, 38f / 255f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -3f);
        return card;
    }

    private RectTransform CreateArrowButton(RectTransform parent, string name, string resourcePath, Vector2 centerPosition, bool previous, UnityAction onClick)
    {
        Image hitArea = UiFactory.CreateImage(name + "HitArea", parent, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        RectTransform rect = hitArea.rectTransform;
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerPosition.x, -centerPosition.y);
        rect.sizeDelta = new Vector2(56f, 56f);
        Button button = UiFactory.AddButton(hitArea.gameObject, onClick);
        PawPalUiAudio.AttachTo(button, PawPalUiClickSoundKind.Menu);

        Sprite arrowSprite = Resources.Load<Sprite>(resourcePath);
        if (arrowSprite == null)
        {
            TextMeshProUGUI fallback = CreateLabel(rect, "ArrowFallback", previous ? "<" : ">", 18, PrimaryDark, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
            UiFactory.Stretch(fallback.rectTransform, 0f, 0f, 0f, 0f);
            return rect;
        }

        Image arrow = UiFactory.CreateImage("Arrow", rect, arrowSprite, Color.white);
        arrow.raycastTarget = false;
        arrow.type = Image.Type.Simple;
        arrow.preserveAspect = true;
        arrow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.sizeDelta = new Vector2(32f, 48f);
        arrow.rectTransform.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static RectTransform CreateButton(RectTransform parent, string name, string text, Vector2 position, Vector2 size, Color fillColor, Color textColor, UnityAction onClick)
    {
        Image fill = UiFactory.CreateImage(name, parent, UiTheme.RoundedTenSprite, fillColor);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        RectTransform rect = fill.rectTransform;
        Place(rect, position.x, position.y, size.x, size.y);
        UiFactory.AddButton(fill.gameObject, onClick);

        TextMeshProUGUI label = CreateLabel(rect, "Label", text, 13, textColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 4f, 2f, 4f, 2f);
        return rect;
    }

    private static RectTransform CreateContinueButton(RectTransform parent, string name, string text, Vector2 position, Vector2 size, UnityAction onClick)
    {
        return CreateStyledActionButton(parent, name, text, position, size, SoftBlue, Color.white, new Color(0f, 82f / 255f, 132f / 255f, 0.28f), onClick);
    }

    private static RectTransform CreateStyledActionButton(RectTransform parent, string name, string text, Vector2 position, Vector2 size, Color fillColor, Color textColor, Color shadowColor, UnityAction onClick)
    {
        RectTransform rect = CreateButton(parent, name, text, position, size, fillColor, textColor, onClick);
        Image fill = rect.GetComponent<Image>();
        if (fill != null)
        {
            fill.sprite = UiTheme.RoundedTenSprite;
            fill.type = Image.Type.Sliced;
        }

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(0f, -2f);

        TextMeshProUGUI label = rect.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.font = UiTheme.NavExtraBoldFont;
            label.fontSize = 16f;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 16f;
            label.color = Color.white;
            label.rectTransform.anchoredPosition = Vector2.zero;
        }

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

        return label;
    }

    private static float GetFurButtonWidth(TextMeshProUGUI label, string text)
    {
        if (label == null)
        {
            return 48f;
        }

        Vector2 preferred = label.GetPreferredValues(text);
        return Mathf.Clamp(Mathf.Ceil(preferred.x) + 18f, 48f, 96f);
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
