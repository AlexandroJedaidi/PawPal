using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class PawPalPhotoModeView : MonoBehaviour
{
    private const float ControlSize = 70f;
    private const float SecondaryControlSize = 52f;
    private const float UtilityControlSize = 30f;
    private const float CollapsedSheetHeight = 178f;
    private const float ExpandedSheetHeight = 318f;
    private const float PoseOptionWidth = 96f;
    private const float PoseOptionHeight = 25f;
    private const float PoseOptionGap = 8f;
    private const float ZoomStepAmount = 0.08f;

    private static Sprite photoSheetSprite;

    private enum AlbumFilterMode
    {
        All,
        ActiveDog,
        Favorites
    }

    private readonly List<Texture2D> loadedTextures = new List<Texture2D>();
    private readonly List<Sprite> loadedSprites = new List<Sprite>();

    private UiSpriteLibrary sprites;
    private RectTransform hudRoot;
    private RectTransform bottomControls;
    private RectTransform poseMenuRoot;
    private RectTransform previewRoot;
    private RectTransform albumRoot;
    private RectTransform albumPanel;
    private RectTransform albumContent;
    private RectTransform detailRoot;
    private RectTransform titleEditorRoot;
    private RectTransform deleteConfirmRoot;
    private RectTransform toastRoot;
    private TextMeshProUGUI toastLabel;
    private TextMeshProUGUI albumEmptyLabel;
    private TextMeshProUGUI detailTitle;
    private TextMeshProUGUI detailDateLabel;
    private TextMeshProUGUI detailDogLabel;
    private TextMeshProUGUI favoriteLabel;
    private TMP_InputField titleInput;
    private RawImage previewImage;
    private RawImage detailImage;
    private AspectRatioFitter previewFitter;
    private AspectRatioFitter detailFitter;
    private Slider zoomSlider;
    private TextMeshProUGUI zoomValueLabel;
    private Coroutine toastRoutine;
    private PawPalPhotoAlbumStore currentAlbumStore;
    private AlbumFilterMode albumFilter = AlbumFilterMode.All;
    private PawPalPhotoRecord featuredAlbumRecord;
    private PawPalPhotoRecord detailRecord;
    private PawPalPhotoRecord titleEditRecord;
    private PawPalPhotoRecord deleteConfirmRecord;
    private bool deleteConfirmFromDetail;
    private bool poseMenuExpanded;

    public event Action CloseRequested;
    public event Action ShutterRequested;
    public event Action AlbumRequested;
    public event Action<PawPalPhotoPoseId> PoseSelected;
    public event Action AttentionRequested;
    public event Action KeepRequested;
    public event Action RetakeRequested;
    public event Action DeletePreviewRequested;
    public event Action AlbumBackRequested;
    public event Action<PawPalPhotoRecord> AlbumPhotoSelected;
    public event Action<PawPalPhotoRecord> AlbumFavoriteToggled;
    public event Action<PawPalPhotoRecord> AlbumShareRequested;
    public event Action<PawPalPhotoRecord> AlbumDeleteRequested;
    public event Action<PawPalPhotoRecord, string> AlbumTitleChanged;
    public event Action DetailBackRequested;
    public event Action<PawPalPhotoRecord> DetailDeleteRequested;
    public event Action<PawPalPhotoRecord> DetailFavoriteToggled;
    public event Action<float> ZoomChanged;

    public void Initialize(UiSpriteLibrary spriteLibrary)
    {
        sprites = spriteLibrary;

        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        BuildHud(root);
        BuildPreview(root);
        BuildAlbum(root);
        BuildDetail(root);
        BuildTitleEditor(root);
        BuildDeleteConfirm(root);
        BuildToast(root);
        HideAll();
    }

    public void HideAll()
    {
        ClearLoadedAlbumAssets();
        currentAlbumStore = null;
        featuredAlbumRecord = null;
        detailRecord = null;
        titleEditRecord = null;
        deleteConfirmRecord = null;

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(false);
        }

        SetPoseMenuExpanded(false);

        if (previewRoot != null)
        {
            previewRoot.gameObject.SetActive(false);
        }

        if (albumRoot != null)
        {
            albumRoot.gameObject.SetActive(false);
        }

        if (detailRoot != null)
        {
            detailRoot.gameObject.SetActive(false);
        }

        if (titleEditorRoot != null)
        {
            titleEditorRoot.gameObject.SetActive(false);
        }

        if (deleteConfirmRoot != null)
        {
            deleteConfirmRoot.gameObject.SetActive(false);
        }

        if (toastRoot != null)
        {
            toastRoot.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    public void ShowLiveMode()
    {
        gameObject.SetActive(true);
        ClearLoadedAlbumAssets();
        currentAlbumStore = null;
        featuredAlbumRecord = null;
        detailRecord = null;
        titleEditRecord = null;
        deleteConfirmRecord = null;

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(true);
        }

        SetPoseMenuExpanded(false);

        if (bottomControls != null)
        {
            bottomControls.gameObject.SetActive(true);
        }

        if (previewRoot != null)
        {
            previewRoot.gameObject.SetActive(false);
        }

        if (albumRoot != null)
        {
            albumRoot.gameObject.SetActive(false);
        }

        if (detailRoot != null)
        {
            detailRoot.gameObject.SetActive(false);
        }

        if (titleEditorRoot != null)
        {
            titleEditorRoot.gameObject.SetActive(false);
        }

        if (deleteConfirmRoot != null)
        {
            deleteConfirmRoot.gameObject.SetActive(false);
        }
    }

    public void SetCaptureHudVisible(bool visible)
    {
        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(visible);
        }

        if (!visible && toastRoot != null)
        {
            toastRoot.gameObject.SetActive(false);
        }
    }

    public void SetZoom01(float zoom01)
    {
        float clampedZoom = Mathf.Clamp01(zoom01);
        if (zoomSlider != null)
        {
            zoomSlider.SetValueWithoutNotify(clampedZoom);
        }

        RefreshZoomValueLabel(clampedZoom);
    }

    public void ShowPreview(Texture2D texture)
    {
        gameObject.SetActive(true);

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(false);
        }

        SetPoseMenuExpanded(false);

        if (albumRoot != null)
        {
            albumRoot.gameObject.SetActive(false);
        }

        if (detailRoot != null)
        {
            detailRoot.gameObject.SetActive(false);
        }

        if (previewImage != null)
        {
            previewImage.texture = texture;
        }

        if (previewFitter != null && texture != null && texture.height > 0)
        {
            previewFitter.aspectRatio = texture.width / (float)texture.height;
        }

        if (previewRoot != null)
        {
            previewRoot.gameObject.SetActive(true);
        }
    }

    public void ShowAlbum(PawPalPhotoAlbumStore store)
    {
        gameObject.SetActive(true);
        ClearLoadedAlbumAssets();
        currentAlbumStore = store;
        detailRecord = null;
        titleEditRecord = null;

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(false);
        }

        SetPoseMenuExpanded(false);

        if (previewRoot != null)
        {
            previewRoot.gameObject.SetActive(false);
        }

        if (detailRoot != null)
        {
            detailRoot.gameObject.SetActive(false);
        }

        if (titleEditorRoot != null)
        {
            titleEditorRoot.gameObject.SetActive(false);
        }

        if (deleteConfirmRoot != null)
        {
            deleteConfirmRoot.gameObject.SetActive(false);
        }

        RebuildAlbumGrid(store);

        if (albumRoot != null)
        {
            albumRoot.gameObject.SetActive(true);
        }
    }

    public void ShowDetail(PawPalPhotoAlbumStore store, PawPalPhotoRecord record)
    {
        gameObject.SetActive(true);
        ClearLoadedAlbumAssets();
        currentAlbumStore = store;
        detailRecord = record;
        titleEditRecord = null;

        if (albumRoot != null)
        {
            albumRoot.gameObject.SetActive(false);
        }

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(false);
        }

        if (previewRoot != null)
        {
            previewRoot.gameObject.SetActive(false);
        }

        if (titleEditorRoot != null)
        {
            titleEditorRoot.gameObject.SetActive(false);
        }

        if (deleteConfirmRoot != null)
        {
            deleteConfirmRoot.gameObject.SetActive(false);
        }

        Texture2D fullTexture = store != null ? store.LoadPhotoTexture(record) : null;
        if (fullTexture != null)
        {
            loadedTextures.Add(fullTexture);
        }

        if (detailImage != null)
        {
            detailImage.texture = fullTexture;
        }

        if (detailFitter != null && fullTexture != null && fullTexture.height > 0)
        {
            detailFitter.aspectRatio = fullTexture.width / (float)fullTexture.height;
        }

        if (detailTitle != null)
        {
            detailTitle.text = BuildDetailTitle(record);
        }

        if (detailDateLabel != null)
        {
            detailDateLabel.text = FormatPhotoDate(record);
        }

        if (detailDogLabel != null)
        {
            detailDogLabel.text = string.IsNullOrWhiteSpace(record != null ? record.DogName : string.Empty) ? "PawFriends" : record.DogName;
        }

        RefreshFavoriteLabel(record);

        if (detailRoot != null)
        {
            detailRoot.gameObject.SetActive(true);
        }
    }

    public void ShowToast(string message)
    {
        if (toastRoot == null || toastLabel == null)
        {
            return;
        }

        gameObject.SetActive(true);

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastLabel.text = message ?? string.Empty;
        toastRoot.gameObject.SetActive(true);
        toastRoutine = StartCoroutine(HideToastAfterDelay(2f));
    }

    private void BuildHud(RectTransform root)
    {
        hudRoot = UiFactory.CreateRect("PhotoHud", root);
        UiFactory.Stretch(hudRoot, 0f, 0f, 0f, 0f);

        bottomControls = UiFactory.CreateRect("BottomControls", hudRoot);
        bottomControls.anchorMin = new Vector2(0.5f, 0f);
        bottomControls.anchorMax = new Vector2(0.5f, 0f);
        bottomControls.pivot = new Vector2(0.5f, 0f);
        bottomControls.sizeDelta = new Vector2(360f, CollapsedSheetHeight);
        bottomControls.anchoredPosition = Vector2.zero;

        Image sheetBackground = bottomControls.gameObject.AddComponent<Image>();
        sheetBackground.sprite = GetPhotoSheetSprite();
        sheetBackground.type = Image.Type.Sliced;
        sheetBackground.color = UiTheme.NavBackgroundCream;
        sheetBackground.raycastTarget = true;

        Shadow sheetShadow = bottomControls.gameObject.AddComponent<Shadow>();
        sheetShadow.effectColor = new Color(0.45f, 0.27f, 0.22f, 0.18f);
        sheetShadow.effectDistance = new Vector2(0f, 4f);
        sheetShadow.useGraphicAlpha = true;

        Color softBrandOutline = UiTheme.NavBrand;
        softBrandOutline.a = 0.34f;

        RectTransform sheen = UiFactory.CreateRect("TopSheen", bottomControls);
        sheen.anchorMin = new Vector2(0f, 1f);
        sheen.anchorMax = new Vector2(1f, 1f);
        sheen.pivot = new Vector2(0.5f, 1f);
        sheen.offsetMin = new Vector2(34f, -2f);
        sheen.offsetMax = new Vector2(-34f, 0f);
        Image sheenImage = sheen.gameObject.AddComponent<Image>();
        sheenImage.sprite = UiTheme.WhiteSprite;
        sheenImage.color = new Color(1f, 1f, 1f, 0.5f);
        sheenImage.raycastTarget = false;

        RectTransform controlLayer = UiFactory.CreateRect("ControlLayer", bottomControls);
        controlLayer.anchorMin = new Vector2(0f, 0f);
        controlLayer.anchorMax = new Vector2(1f, 0f);
        controlLayer.pivot = new Vector2(0.5f, 0f);
        controlLayer.offsetMin = Vector2.zero;
        controlLayer.offsetMax = new Vector2(0f, CollapsedSheetHeight);

        CreateStyledCircleButton(
            controlLayer,
            "ClosePhotoMode",
            new Vector2(-154f, -31f),
            UtilityControlSize,
            null,
            "X",
            UiTheme.NavBackgroundCream,
            UiTheme.NavBrandDark,
            softBrandOutline,
            true,
            delegate { Raise(CloseRequested); });

        CreateStyledCircleButton(
            controlLayer,
            "ZoomOut",
            new Vector2(-116f, -31f),
            UtilityControlSize,
            null,
            "-",
            UiTheme.NavBackgroundCream,
            UiTheme.NavBrand,
            softBrandOutline,
            true,
            delegate { StepZoom(-ZoomStepAmount); });

        zoomSlider = CreateSlider(controlLayer, new Vector2(0f, -31f), new Vector2(160f, 28f));
        zoomValueLabel = CreateZoomValueChip(controlLayer, new Vector2(0f, -31f));
        RefreshZoomValueLabel(zoomSlider != null ? zoomSlider.value : 0.35f);

        CreateStyledCircleButton(
            controlLayer,
            "ZoomIn",
            new Vector2(116f, -31f),
            UtilityControlSize,
            null,
            "+",
            UiTheme.NavBackgroundCream,
            UiTheme.NavBrand,
            softBrandOutline,
            true,
            delegate { StepZoom(ZoomStepAmount); });

        CreateStyledCircleButton(
            controlLayer,
            "OpenAlbum",
            new Vector2(154f, -31f),
            UtilityControlSize,
            GetIcon("icon_cam_brand"),
            string.Empty,
            UiTheme.NavBackgroundCream,
            Color.white,
            softBrandOutline,
            true,
            delegate { Raise(AlbumRequested); });

        CreateStyledCircleButton(
            controlLayer,
            "FocusPet",
            new Vector2(-96f, -94f),
            SecondaryControlSize,
            GetIcon("icon_paw_brand"),
            string.Empty,
            UiTheme.NavBackgroundCream,
            Color.white,
            softBrandOutline,
            true,
            delegate { TogglePoseMenu(); });

        CreateControlCaption(controlLayer, "FocusLabel", "Pose", -96f, -138f);

        CreateStyledCircleButton(
            controlLayer,
            "CallAttention",
            new Vector2(96f, -94f),
            SecondaryControlSize,
            GetIcon("icon_whistle_brand"),
            string.Empty,
            UiTheme.NavBackgroundCream,
            Color.white,
            softBrandOutline,
            true,
            delegate { Raise(AttentionRequested); });

        CreateControlCaption(controlLayer, "AttentionLabel", "Whistle", 96f, -138f);

        Color shutterRingColor = UiTheme.NavBrand;
        shutterRingColor.a = 0.42f;
        CreateStyledCircleButton(
            controlLayer,
            "Shutter",
            new Vector2(0f, -91f),
            ControlSize,
            GetWhiteResourceSprite("UI/Figma/HomeMain/icon_cam"),
            string.Empty,
            UiTheme.NavBrand,
            Color.white,
            shutterRingColor,
            true,
            delegate { Raise(ShutterRequested); });

        BuildPoseMenu(bottomControls);
        SetPoseMenuExpanded(false);
    }

    private void BuildPoseMenu(RectTransform parent)
    {
        poseMenuRoot = UiFactory.CreateRect("PoseMenu", parent);
        poseMenuRoot.anchorMin = new Vector2(0.5f, 1f);
        poseMenuRoot.anchorMax = new Vector2(0.5f, 1f);
        poseMenuRoot.pivot = new Vector2(0.5f, 1f);
        poseMenuRoot.sizeDelta = new Vector2(320f, 124f);
        poseMenuRoot.anchoredPosition = new Vector2(0f, -14f);

        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Bark, "Bark", 0, 0);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Idle1, "Idle 1", 1, 0);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Idle2, "Idle 2", 2, 0);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Idle3, "Idle 3", 0, 1);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Idle4, "Idle 4", 1, 1);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Idle6, "Idle 6", 2, 1);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Idle7, "Idle 7", 0, 2);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.LieSleep, "Lie Sleep", 1, 2);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.LieLoop1, "Lie Loop 1", 2, 2);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.LieLoop2, "Lie Loop 2", 0, 3);
        CreatePoseOptionButton(poseMenuRoot, PawPalPhotoPoseId.Scratch, "Scratch", 1, 3);
    }

    private void CreatePoseOptionButton(RectTransform parent, PawPalPhotoPoseId poseId, string labelText, int column, int row)
    {
        RectTransform root = UiFactory.CreateRect(labelText.Replace(" ", string.Empty) + "Pose", parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(PoseOptionWidth, PoseOptionHeight);
        root.anchoredPosition = new Vector2(
            column * (PoseOptionWidth + PoseOptionGap),
            -row * (PoseOptionHeight + PoseOptionGap));

        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.CardWhite;
        background.raycastTarget = true;

        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(UiTheme.NavBrand.r, UiTheme.NavBrand.g, UiTheme.NavBrand.b, 0.32f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", root, labelText, 10, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        label.raycastTarget = false;
        UiFactory.Stretch(label.rectTransform, 4f, 2f, 4f, 2f);

        UiFactory.AddButton(root.gameObject, delegate
        {
            SetPoseMenuExpanded(false);
            Raise(PoseSelected, poseId);
        });
    }

    private void TogglePoseMenu()
    {
        SetPoseMenuExpanded(!poseMenuExpanded);
    }

    private void SetPoseMenuExpanded(bool expanded)
    {
        poseMenuExpanded = expanded && poseMenuRoot != null && bottomControls != null;

        if (bottomControls != null)
        {
            float height = poseMenuExpanded ? ExpandedSheetHeight : CollapsedSheetHeight;
            bottomControls.sizeDelta = new Vector2(bottomControls.sizeDelta.x, height);
        }

        if (poseMenuRoot != null)
        {
            poseMenuRoot.gameObject.SetActive(poseMenuExpanded);
        }
    }

    private void BuildPreview(RectTransform root)
    {
        previewRoot = UiFactory.CreateRect("PhotoPreview", root);
        UiFactory.Stretch(previewRoot, 0f, 0f, 0f, 0f);
        CreateBlocker(previewRoot, 0.55f);

        RectTransform panel = CreatePanel(previewRoot, "PreviewPanel", 336f, 520f);

        previewImage = UiFactory.CreateRect("PreviewImage", panel).gameObject.AddComponent<RawImage>();
        previewImage.color = Color.white;
        previewImage.raycastTarget = false;
        UiFactory.Stretch(previewImage.rectTransform, 16f, 98f, 16f, 62f);

        previewFitter = previewImage.gameObject.AddComponent<AspectRatioFitter>();
        previewFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        previewFitter.aspectRatio = 1f;

        CreateLabel(panel, "PreviewTitle", "Preview", 18, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(0f, 28f));
        CreateTextButton(panel, "KeepButton", "Keep", 24f, 456f, 82f, 38f, true, delegate { Raise(KeepRequested); });
        CreateTextButton(panel, "RetakeButton", "Retake", 126f, 456f, 86f, 38f, false, delegate { Raise(RetakeRequested); });
        CreateTextButton(panel, "DeleteButton", "Delete", 232f, 456f, 82f, 38f, false, delegate { Raise(DeletePreviewRequested); });
    }

    private void BuildAlbum(RectTransform root)
    {
        albumRoot = UiFactory.CreateRect("PhotoAlbum", root);
        UiFactory.Stretch(albumRoot, 0f, 0f, 0f, 0f);
        CreateBlocker(albumRoot, 0.55f);

        albumPanel = CreatePanel(albumRoot, "AlbumPanel", 360f, 720f);
        Shadow panelShadow = albumPanel.gameObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0.22f, 0.13f, 0.1f, 0.25f);
        panelShadow.effectDistance = new Vector2(0f, -4f);

        Image paw = UiFactory.CreateImage("AlbumPaw", albumPanel, GetIcon("icon_paw_brand"), Color.white);
        paw.type = Image.Type.Simple;
        paw.preserveAspect = true;
        SetTopLeft(paw.rectTransform, 24f, 30f, 58f, 58f);

        TextMeshProUGUI title = CreateLabel(albumPanel, "AlbumTitle", "Photo Album", 27, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -34f), new Vector2(190f, 34f));
        title.alignment = TextAlignmentOptions.Left;

        TextMeshProUGUI subtitle = UiFactory.CreateLabel("AlbumSubtitle", albumPanel, "All your sweet memories", 14, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        subtitle.font = UiTheme.NavRegularFont;
        subtitle.alpha = 0.72f;
        SetTopLeft(subtitle.rectTransform, 94f, 70f, 220f, 22f);

        CreateStyledCircleButton(
            albumPanel,
            "AlbumBack",
            new Vector2(148f, -36f),
            50f,
            null,
            "X",
            UiTheme.NavBackgroundCream,
            UiTheme.NavBrandDark,
            UiTheme.NavBrand,
            true,
            delegate { Raise(AlbumBackRequested); });

        RectTransform layoutPill = CreatePill(albumPanel, "LayoutControl", 254f, 104f, 74f, 40f, false);
        TextMeshProUGUI layoutLabel = UiFactory.CreateLabel("Label", layoutPill, "Grid v", 13, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        layoutLabel.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(layoutLabel.rectTransform, 8f, 4f, 8f, 4f);

        RectTransform viewport = UiFactory.CreateRect("Viewport", albumPanel);
        UiFactory.Stretch(viewport, 14f, 74f, 14f, 154f);
        Image viewportMask = viewport.gameObject.AddComponent<Image>();
        viewportMask.sprite = UiTheme.RoundedTenSprite;
        viewportMask.type = Image.Type.Sliced;
        viewportMask.color = new Color(1f, 1f, 1f, 0.05f);
        viewportMask.raycastTarget = true;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        albumContent = UiFactory.CreateRect("Content", viewport);
        albumContent.anchorMin = new Vector2(0f, 1f);
        albumContent.anchorMax = new Vector2(1f, 1f);
        albumContent.pivot = new Vector2(0.5f, 1f);
        albumContent.offsetMin = Vector2.zero;
        albumContent.offsetMax = Vector2.zero;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = albumContent;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        albumEmptyLabel = CreateLabel(albumPanel, "EmptyAlbum", "No photos yet", 17, UiTheme.NavBrandDark, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 28f));

        RectTransform actionBar = CreatePill(albumPanel, "AlbumActionBar", 16f, 654f, 328f, 46f, false);
        CreateAlbumAction(actionBar, "ShareAction", "Share", 0f, delegate { Raise(AlbumShareRequested, featuredAlbumRecord); });
        CreateAlbumAction(actionBar, "FavoriteAction", "Favorites", 109f, delegate { Raise(AlbumFavoriteToggled, featuredAlbumRecord); });
        CreateAlbumAction(actionBar, "DeleteAction", "Delete", 218f, delegate { ShowDeleteConfirm(featuredAlbumRecord, false); });
    }

    private void BuildDetail(RectTransform root)
    {
        detailRoot = UiFactory.CreateRect("PhotoDetail", root);
        UiFactory.Stretch(detailRoot, 0f, 0f, 0f, 0f);
        CreateBlocker(detailRoot, 0.58f);

        RectTransform panel = CreatePanel(detailRoot, "DetailPanel", 360f, 720f);
        Shadow panelShadow = panel.gameObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0.22f, 0.13f, 0.1f, 0.25f);
        panelShadow.effectDistance = new Vector2(0f, -4f);

        CreateStyledCircleButton(
            panel,
            "DetailBack",
            new Vector2(-148f, -36f),
            50f,
            null,
            "<",
            UiTheme.NavBackgroundCream,
            UiTheme.NavBrandDark,
            UiTheme.NavBrand,
            true,
            delegate { Raise(DetailBackRequested); });

        CreateStyledCircleButton(
            panel,
            "DetailClose",
            new Vector2(148f, -36f),
            50f,
            null,
            "X",
            UiTheme.NavBackgroundCream,
            UiTheme.NavBrandDark,
            UiTheme.NavBrand,
            true,
            delegate { Raise(AlbumBackRequested); });

        detailTitle = CreateLabel(panel, "DetailTitle", string.Empty, 24, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -30f), new Vector2(220f, 34f));
        detailTitle.alignment = TextAlignmentOptions.Left;

        detailDateLabel = UiFactory.CreateLabel("DetailDate", panel, string.Empty, 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        detailDateLabel.font = UiTheme.NavRegularFont;
        detailDateLabel.alpha = 0.72f;
        SetTopLeft(detailDateLabel.rectTransform, 74f, 66f, 190f, 22f);

        detailDogLabel = UiFactory.CreateLabel("DetailDog", panel, string.Empty, 13, UiTheme.NavBrand, FontStyles.Bold, TextAlignmentOptions.Left);
        detailDogLabel.font = UiTheme.NavExtraBoldFont;
        SetTopLeft(detailDogLabel.rectTransform, 74f, 92f, 190f, 24f);

        detailImage = UiFactory.CreateRect("DetailImage", panel).gameObject.AddComponent<RawImage>();
        detailImage.color = Color.white;
        detailImage.raycastTarget = false;
        SetTopLeft(detailImage.rectTransform, 20f, 132f, 320f, 410f);

        detailFitter = detailImage.gameObject.AddComponent<AspectRatioFitter>();
        detailFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        detailFitter.aspectRatio = 1f;

        RectTransform editButton = CreatePill(panel, "EditTitleButton", 22f, 558f, 146f, 40f, false);
        TextMeshProUGUI editLabel = UiFactory.CreateLabel("Label", editButton, "Edit caption", 13, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        editLabel.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(editLabel.rectTransform, 8f, 4f, 8f, 4f);
        UiFactory.AddButton(editButton.gameObject, delegate { ShowTitleEditor(detailRecord); });

        RectTransform shareButton = CreatePill(panel, "ShareDetailButton", 192f, 558f, 146f, 40f, true);
        TextMeshProUGUI shareLabel = UiFactory.CreateLabel("Label", shareButton, "Share", 13, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        shareLabel.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(shareLabel.rectTransform, 8f, 4f, 8f, 4f);
        UiFactory.AddButton(shareButton.gameObject, delegate { Raise(AlbumShareRequested, detailRecord); });

        CreateTextButton(panel, "FavoriteButton", "Favorite", 22f, 624f, 96f, 38f, true, delegate
        {
            Raise(DetailFavoriteToggled, detailRecord);
        });

        favoriteLabel = CreateLabel(panel, "FavoriteLabel", string.Empty, 14, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(16f, -594f), new Vector2(-32f, 24f));
        favoriteLabel.alignment = TextAlignmentOptions.Left;

        CreateTextButton(panel, "DeleteDetailButton", "Delete", 244f, 624f, 92f, 38f, false, delegate
        {
            ShowDeleteConfirm(detailRecord, true);
        });
    }

    private void BuildToast(RectTransform root)
    {
        toastRoot = UiFactory.CreateRect("PhotoToast", root);
        toastRoot.anchorMin = new Vector2(0.5f, 1f);
        toastRoot.anchorMax = new Vector2(0.5f, 1f);
        toastRoot.pivot = new Vector2(0.5f, 1f);
        toastRoot.sizeDelta = new Vector2(300f, 44f);
        toastRoot.anchoredPosition = new Vector2(0f, -82f);

        Image background = toastRoot.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.NavBackgroundCream;
        background.raycastTarget = false;

        Outline outline = toastRoot.gameObject.AddComponent<Outline>();
        outline.effectColor = UiTheme.NavBrand;
        outline.effectDistance = new Vector2(1f, -1f);

        toastLabel = UiFactory.CreateLabel("Message", toastRoot, string.Empty, 14, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        toastLabel.font = UiTheme.NavExtraBoldFont;
        toastLabel.textWrappingMode = TextWrappingModes.NoWrap;
        toastLabel.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Stretch(toastLabel.rectTransform, 12f, 6f, 12f, 6f);
    }

    private void BuildTitleEditor(RectTransform root)
    {
        titleEditorRoot = UiFactory.CreateRect("PhotoTitleEditor", root);
        UiFactory.Stretch(titleEditorRoot, 0f, 0f, 0f, 0f);
        CreateBlocker(titleEditorRoot, 0.44f);

        RectTransform panel = CreatePanel(titleEditorRoot, "TitleEditorPanel", 320f, 196f);
        TextMeshProUGUI title = CreateLabel(panel, "Title", "Edit caption", 19, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(0f, 30f));
        title.alignment = TextAlignmentOptions.Center;

        titleInput = CreateTitleInput(panel);

        CreateTextButton(panel, "CancelTitle", "Cancel", 34f, 142f, 106f, 36f, false, delegate
        {
            HideTitleEditor();
        });

        CreateTextButton(panel, "SaveTitle", "Save", 180f, 142f, 106f, 36f, true, delegate
        {
            if (titleEditRecord != null && titleInput != null)
            {
                Raise(AlbumTitleChanged, titleEditRecord, titleInput.text);
            }

            HideTitleEditor();
        });

        titleEditorRoot.gameObject.SetActive(false);
    }

    private void BuildDeleteConfirm(RectTransform root)
    {
        deleteConfirmRoot = UiFactory.CreateRect("PhotoDeleteConfirm", root);
        UiFactory.Stretch(deleteConfirmRoot, 0f, 0f, 0f, 0f);
        CreateBlocker(deleteConfirmRoot, 0.44f);

        RectTransform panel = CreatePanel(deleteConfirmRoot, "DeleteConfirmPanel", 306f, 172f);
        TextMeshProUGUI title = CreateLabel(panel, "Title", "Delete photo?", 19, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(0f, 28f));
        title.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI message = UiFactory.CreateLabel("Message", panel, "This memory will be removed from your album.", 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        message.font = UiTheme.NavRegularFont;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.alpha = 0.72f;
        SetTopLeft(message.rectTransform, 32f, 62f, 242f, 42f);

        CreateTextButton(panel, "CancelDelete", "Cancel", 30f, 120f, 104f, 36f, false, delegate
        {
            HideDeleteConfirm();
        });

        CreateTextButton(panel, "ConfirmDelete", "Delete", 172f, 120f, 104f, 36f, true, delegate
        {
            PawPalPhotoRecord record = deleteConfirmRecord;
            bool fromDetail = deleteConfirmFromDetail;
            HideDeleteConfirm();

            if (fromDetail)
            {
                Raise(DetailDeleteRequested, record);
            }
            else
            {
                Raise(AlbumDeleteRequested, record);
            }
        });

        deleteConfirmRoot.gameObject.SetActive(false);
    }

    private void RebuildAlbumGrid(PawPalPhotoAlbumStore store)
    {
        if (albumContent == null)
        {
            return;
        }

        for (int i = albumContent.childCount - 1; i >= 0; i--)
        {
            Destroy(albumContent.GetChild(i).gameObject);
        }

        List<PawPalPhotoRecord> records = GetFilteredAlbumRecords(store);
        int count = records.Count;
        if (albumEmptyLabel != null)
        {
            albumEmptyLabel.gameObject.SetActive(false);
        }

        if (store == null || store.Records == null || store.Records.Count == 0)
        {
            featuredAlbumRecord = null;
            albumContent.sizeDelta = new Vector2(0f, 420f);
            albumContent.anchoredPosition = Vector2.zero;
            CreateEmptyAlbumState(albumContent);
            return;
        }

        featuredAlbumRecord = SelectFeaturedRecord(records, store);
        float y = 8f;
        if (featuredAlbumRecord != null)
        {
            CreateAlbumHeroCard(albumContent, store, featuredAlbumRecord, y);
            y += 138f;
        }

        string activeDogName = GetActiveDogName();
        CreateFilterPill(albumContent, "AllFilter", "All", AlbumFilterMode.All, albumFilter == AlbumFilterMode.All, 6f, y, 92f);
        CreateFilterPill(albumContent, "DogFilter", string.IsNullOrWhiteSpace(activeDogName) ? "Dog" : activeDogName, AlbumFilterMode.ActiveDog, albumFilter == AlbumFilterMode.ActiveDog, 112f, y, 98f);
        CreateFilterPill(albumContent, "FavoritesFilter", "Favorites", AlbumFilterMode.Favorites, albumFilter == AlbumFilterMode.Favorites, 224f, y, 98f);
        y += 56f;

        if (count == 0)
        {
            CreateFilteredEmptyState(albumContent, y);
            albumContent.sizeDelta = new Vector2(0f, y + 170f);
            albumContent.anchoredPosition = Vector2.zero;
            return;
        }

        const float cardWidth = 154f;
        const float cardHeight = 178f;
        const float spacingX = 16f;
        const float spacingY = 14f;
        float leftX = 6f;
        for (int i = 0; i < count; i++)
        {
            float x = leftX + (i % 2) * (cardWidth + spacingX);
            float rowY = y + (i / 2) * (cardHeight + spacingY);
            CreateAlbumPhotoCard(albumContent, store, records[i], x, rowY, cardWidth, cardHeight);
        }

        int rows = Mathf.Max(1, Mathf.CeilToInt(count / 2f));
        albumContent.sizeDelta = new Vector2(0f, y + rows * (cardHeight + spacingY) + 10f);
        albumContent.anchoredPosition = Vector2.zero;
    }

    private void CreateAlbumHeroCard(RectTransform parent, PawPalPhotoAlbumStore store, PawPalPhotoRecord record, float y)
    {
        RectTransform root = CreateCard(parent, "FeaturedPhoto", 6f, y, 318f, 128f);
        Sprite thumbnail = LoadThumbnailSprite(store, record);

        Image image = UiFactory.CreateImage("Image", root, thumbnail, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        SetTopLeft(image.rectTransform, 10f, 10f, 162f, 108f);

        TextMeshProUGUI title = UiFactory.CreateLabel("Title", root, BuildPhotoTitle(record), 22, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Left);
        title.font = UiTheme.NavExtraBoldFont;
        SetTopLeft(title.rectTransform, 188f, 38f, 104f, 34f);

        TextMeshProUGUI dog = UiFactory.CreateLabel("Dog", root, record != null ? record.DogName : string.Empty, 13, UiTheme.NavBrand, FontStyles.Bold, TextAlignmentOptions.Left);
        dog.font = UiTheme.NavExtraBoldFont;
        SetTopLeft(dog.rectTransform, 188f, 74f, 96f, 22f);

        TextMeshProUGUI date = UiFactory.CreateLabel("Date", root, FormatPhotoDate(record), 12, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        date.font = UiTheme.NavRegularFont;
        date.alpha = 0.72f;
        SetTopLeft(date.rectTransform, 188f, 98f, 106f, 20f);

        CreateHeartButton(root, "HeroHeart", record, 276f, 16f, 30f);

        UiFactory.AddButton(root.gameObject, delegate
        {
            Raise(AlbumPhotoSelected, record);
        });
    }

    private void CreateAlbumPhotoCard(RectTransform parent, PawPalPhotoAlbumStore store, PawPalPhotoRecord record, float x, float y, float width, float height)
    {
        RectTransform root = CreateCard(parent, "PhotoCard", x, y, width, height);
        Sprite thumbnail = LoadThumbnailSprite(store, record);

        Image image = UiFactory.CreateImage("Image", root, thumbnail, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        SetTopLeft(image.rectTransform, 0f, 0f, width, 104f);

        TextMeshProUGUI title = UiFactory.CreateLabel("Title", root, BuildPhotoTitle(record), 13, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Left);
        title.font = UiTheme.NavExtraBoldFont;
        SetTopLeft(title.rectTransform, 12f, 114f, width - 52f, 22f);

        TextMeshProUGUI date = UiFactory.CreateLabel("Date", root, FormatPhotoDate(record), 11, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        date.font = UiTheme.NavRegularFont;
        date.alpha = 0.72f;
        SetTopLeft(date.rectTransform, 12f, 138f, width - 52f, 20f);

        CreateHeartButton(root, "CardHeart", record, width - 40f, 132f, 28f);

        UiFactory.AddButton(root.gameObject, delegate
        {
            Raise(AlbumPhotoSelected, record);
        });
    }

    private List<PawPalPhotoRecord> GetFilteredAlbumRecords(PawPalPhotoAlbumStore store)
    {
        List<PawPalPhotoRecord> records = new List<PawPalPhotoRecord>();
        IReadOnlyList<PawPalPhotoRecord> source = store != null ? store.Records : null;
        if (source == null)
        {
            return records;
        }

        string activeDogId = GetActiveDogId();
        string activeDogName = GetActiveDogName();
        for (int i = 0; i < source.Count; i++)
        {
            PawPalPhotoRecord record = source[i];
            if (record == null)
            {
                continue;
            }

            if (albumFilter == AlbumFilterMode.Favorites && !record.Favorite)
            {
                continue;
            }

            if (albumFilter == AlbumFilterMode.ActiveDog && !MatchesActiveDog(record, activeDogId, activeDogName))
            {
                continue;
            }

            records.Add(record);
        }

        return records;
    }

    private PawPalPhotoRecord SelectFeaturedRecord(List<PawPalPhotoRecord> filteredRecords, PawPalPhotoAlbumStore store)
    {
        if (filteredRecords != null && filteredRecords.Count > 0)
        {
            for (int i = 0; i < filteredRecords.Count; i++)
            {
                if (filteredRecords[i] != null && filteredRecords[i].Favorite)
                {
                    return filteredRecords[i];
                }
            }

            return filteredRecords[0];
        }

        IReadOnlyList<PawPalPhotoRecord> allRecords = store != null ? store.Records : null;
        return allRecords != null && allRecords.Count > 0 ? allRecords[0] : null;
    }

    private bool MatchesActiveDog(PawPalPhotoRecord record, string activeDogId, string activeDogName)
    {
        if (record == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(activeDogId) && string.Equals(record.DogId, activeDogId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrEmpty(activeDogName)
            && string.Equals(record.DogName, activeDogName, StringComparison.OrdinalIgnoreCase);
    }

    private string GetActiveDogId()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        return runtime != null && runtime.ActiveDog != null ? runtime.ActiveDog.Id : string.Empty;
    }

    private string GetActiveDogName()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        return runtime != null && runtime.ActiveDog != null ? runtime.ActiveDog.DisplayName : string.Empty;
    }

    private Sprite LoadThumbnailSprite(PawPalPhotoAlbumStore store, PawPalPhotoRecord record)
    {
        Texture2D thumbnail = store != null ? store.LoadThumbnailTexture(record) : null;
        if (thumbnail != null)
        {
            loadedTextures.Add(thumbnail);
            Sprite sprite = Sprite.Create(thumbnail, new Rect(0f, 0f, thumbnail.width, thumbnail.height), new Vector2(0.5f, 0.5f), 100f);
            loadedSprites.Add(sprite);
            return sprite;
        }

        return UiTheme.WhiteSprite;
    }

    private void CreateEmptyAlbumState(RectTransform parent)
    {
        TextMeshProUGUI title = UiFactory.CreateLabel("EmptyTitle", parent, "No photos yet", 20, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        title.font = UiTheme.NavExtraBoldFont;
        SetTopLeft(title.rectTransform, 24f, 120f, 280f, 28f);

        TextMeshProUGUI subtitle = UiFactory.CreateLabel("EmptySubtitle", parent, "Take a photo to start your album.", 13, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.font = UiTheme.NavRegularFont;
        subtitle.alpha = 0.72f;
        SetTopLeft(subtitle.rectTransform, 24f, 154f, 280f, 24f);
    }

    private void CreateFilteredEmptyState(RectTransform parent, float y)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel("FilteredEmpty", parent, "Nothing in this view yet", 15, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        SetTopLeft(label.rectTransform, 20f, y + 46f, 288f, 28f);
    }

    private void CreateFilterPill(RectTransform parent, string name, string label, AlbumFilterMode filterMode, bool activeFilter, float x, float y, float width)
    {
        RectTransform pill = CreatePill(parent, name, x, y, width, 38f, activeFilter);
        TextMeshProUGUI text = UiFactory.CreateLabel("Label", pill, label, 13, activeFilter ? Color.white : UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        text.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(text.rectTransform, 8f, 4f, 8f, 4f);

        UiFactory.AddButton(pill.gameObject, delegate
        {
            albumFilter = filterMode;
            RebuildAlbumGrid(currentAlbumStore);
        });
    }

    private RectTransform CreateCard(RectTransform parent, string name, float x, float y, float width, float height)
    {
        RectTransform root = UiFactory.CreateRect(name, parent);
        SetTopLeft(root, x, y, width, height);

        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.CardWhite;
        background.raycastTarget = true;

        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(UiTheme.NavBrand.r, UiTheme.NavBrand.g, UiTheme.NavBrand.b, 0.22f);
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = root.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.22f, 0.13f, 0.1f, 0.14f);
        shadow.effectDistance = new Vector2(0f, -2f);
        return root;
    }

    private RectTransform CreatePill(RectTransform parent, string name, float x, float y, float width, float height, bool activePill)
    {
        RectTransform root = UiFactory.CreateRect(name, parent);
        SetTopLeft(root, x, y, width, height);

        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = activePill ? UiTheme.NavBrand : UiTheme.NavBackgroundCream;
        background.raycastTarget = true;

        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(UiTheme.NavBrand.r, UiTheme.NavBrand.g, UiTheme.NavBrand.b, activePill ? 0.55f : 0.24f);
        outline.effectDistance = new Vector2(1f, -1f);
        return root;
    }

    private void CreateHeartButton(RectTransform parent, string name, PawPalPhotoRecord record, float x, float y, float size)
    {
        RectTransform heart = CreatePill(parent, name, x, y, size, size, false);
        TextMeshProUGUI label = UiFactory.CreateLabel("Label", heart, record != null && record.Favorite ? "<3" : "<>", 13, UiTheme.NavBrand, FontStyles.Bold, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(label.rectTransform, 2f, 2f, 2f, 2f);
        UiFactory.AddButton(heart.gameObject, delegate
        {
            Raise(AlbumFavoriteToggled, record);
        });
    }

    private void CreateAlbumAction(RectTransform parent, string name, string label, float x, Action onClick)
    {
        RectTransform root = UiFactory.CreateRect(name, parent);
        SetTopLeft(root, x, 0f, 109f, 46f);
        TextMeshProUGUI text = UiFactory.CreateLabel("Label", root, label, 13, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        text.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(text.rectTransform, 4f, 6f, 4f, 6f);
        UiFactory.AddButton(root.gameObject, delegate
        {
            if (onClick != null)
            {
                onClick();
            }
        });
    }

    private TMP_InputField CreateTitleInput(RectTransform parent)
    {
        RectTransform inputRoot = UiFactory.CreateRect("TitleInput", parent);
        SetTopLeft(inputRoot, 28f, 70f, 264f, 44f);

        Image background = inputRoot.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.CardWhite;
        background.raycastTarget = true;

        Outline outline = inputRoot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(UiTheme.NavBrand.r, UiTheme.NavBrand.g, UiTheme.NavBrand.b, 0.34f);
        outline.effectDistance = new Vector2(1f, -1f);

        TMP_InputField input = inputRoot.gameObject.AddComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 48;
        input.targetGraphic = background;

        RectTransform textArea = UiFactory.CreateRect("Text Area", inputRoot);
        UiFactory.Stretch(textArea, 12f, 4f, 12f, 4f);

        TextMeshProUGUI text = UiFactory.CreateLabel("Text", textArea, string.Empty, 15, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        text.font = UiTheme.NavRegularFont;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UiFactory.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

        TextMeshProUGUI placeholder = UiFactory.CreateLabel("Placeholder", textArea, "Add a caption", 15, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Left);
        placeholder.font = UiTheme.NavRegularFont;
        placeholder.alpha = 0.42f;
        UiFactory.Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);

        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private void ShowTitleEditor(PawPalPhotoRecord record)
    {
        if (titleEditorRoot == null || titleInput == null || record == null)
        {
            return;
        }

        titleEditRecord = record;
        titleInput.text = string.IsNullOrWhiteSpace(record.Title) ? string.Empty : record.Title;
        titleEditorRoot.gameObject.SetActive(true);
        titleInput.Select();
        titleInput.ActivateInputField();
    }

    private void HideTitleEditor()
    {
        titleEditRecord = null;
        if (titleEditorRoot != null)
        {
            titleEditorRoot.gameObject.SetActive(false);
        }
    }

    private void ShowDeleteConfirm(PawPalPhotoRecord record, bool fromDetail)
    {
        if (deleteConfirmRoot == null || record == null)
        {
            return;
        }

        deleteConfirmRecord = record;
        deleteConfirmFromDetail = fromDetail;
        deleteConfirmRoot.gameObject.SetActive(true);
    }

    private void HideDeleteConfirm()
    {
        deleteConfirmRecord = null;
        deleteConfirmFromDetail = false;
        if (deleteConfirmRoot != null)
        {
            deleteConfirmRoot.gameObject.SetActive(false);
        }
    }

    private Slider CreateSlider(RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform sliderRoot = UiFactory.CreateRect("ZoomSlider", parent);
        sliderRoot.anchorMin = new Vector2(0.5f, 1f);
        sliderRoot.anchorMax = new Vector2(0.5f, 1f);
        sliderRoot.pivot = new Vector2(0.5f, 0.5f);
        sliderRoot.sizeDelta = size;
        sliderRoot.anchoredPosition = anchoredPosition;

        RectTransform background = UiFactory.CreateRect("Background", sliderRoot);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.pivot = new Vector2(0.5f, 0.5f);
        background.offsetMin = new Vector2(0f, -3f);
        background.offsetMax = new Vector2(0f, 3f);
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.sprite = UiTheme.RoundedFiveSprite;
        backgroundImage.type = Image.Type.Sliced;
        backgroundImage.color = new Color(0.86f, 0.78f, 0.72f, 0.52f);
        backgroundImage.raycastTarget = true;

        RectTransform fillArea = UiFactory.CreateRect("Fill Area", sliderRoot);
        fillArea.anchorMin = new Vector2(0f, 0f);
        fillArea.anchorMax = new Vector2(1f, 1f);
        fillArea.offsetMin = new Vector2(0f, 0f);
        fillArea.offsetMax = new Vector2(0f, 0f);

        RectTransform fill = UiFactory.CreateRect("Fill", fillArea);
        UiFactory.Stretch(fill, 0f, 10f, 0f, 10f);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = UiTheme.RoundedFiveSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = UiTheme.NavBrand;
        fillImage.raycastTarget = false;

        RectTransform handleArea = UiFactory.CreateRect("Handle Slide Area", sliderRoot);
        UiFactory.Stretch(handleArea, 0f, 0f, 0f, 0f);

        RectTransform handle = UiFactory.CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(28f, 28f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = UiTheme.CircleSprite;
        handleImage.type = Image.Type.Simple;
        handleImage.color = UiTheme.NavBackgroundCream;

        Shadow handleShadow = handle.gameObject.AddComponent<Shadow>();
        handleShadow.effectColor = new Color(0.45f, 0.27f, 0.22f, 0.2f);
        handleShadow.effectDistance = new Vector2(0f, -1f);

        Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.35f;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(delegate(float value)
        {
            RefreshZoomValueLabel(value);
            Action<float> handler = ZoomChanged;
            if (handler != null)
            {
                handler(value);
            }
        });

        return slider;
    }

    private TextMeshProUGUI CreateZoomValueChip(RectTransform parent, Vector2 anchoredPosition)
    {
        RectTransform chip = UiFactory.CreateRect("ZoomValueChip", parent);
        chip.anchorMin = new Vector2(0.5f, 1f);
        chip.anchorMax = new Vector2(0.5f, 1f);
        chip.pivot = new Vector2(0.5f, 0.5f);
        chip.sizeDelta = new Vector2(46f, 28f);
        chip.anchoredPosition = anchoredPosition;

        Image background = chip.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.NavBackgroundCream;
        background.raycastTarget = false;

        Shadow shadow = chip.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.45f, 0.27f, 0.22f, 0.16f);
        shadow.effectDistance = new Vector2(0f, -1f);

        TextMeshProUGUI label = UiFactory.CreateLabel("Label", chip, string.Empty, 10, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        label.raycastTarget = false;
        UiFactory.Stretch(label.rectTransform, 4f, 4f, 4f, 4f);
        return label;
    }

    private void RefreshZoomValueLabel(float zoom01)
    {
        if (zoomValueLabel == null)
        {
            return;
        }

        float displayZoom = Mathf.Lerp(0.5f, 2f, Mathf.Clamp01(zoom01));
        zoomValueLabel.text = displayZoom.ToString("0.0", CultureInfo.InvariantCulture) + "x";
    }

    private void StepZoom(float delta)
    {
        if (zoomSlider == null)
        {
            Action<float> handler = ZoomChanged;
            if (handler != null)
            {
                handler(Mathf.Clamp01(delta));
            }

            return;
        }

        zoomSlider.value = Mathf.Clamp01(zoomSlider.value + delta);
    }

    private RectTransform CreatePanel(RectTransform parent, string name, float width, float height)
    {
        RectTransform panel = UiFactory.CreateRect(name, parent);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = Vector2.zero;

        Image background = panel.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = UiTheme.NavBackgroundCream;
        background.raycastTarget = true;

        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = UiTheme.NavBrand;
        outline.effectDistance = new Vector2(1f, -1f);
        return panel;
    }

    private Image CreateBlocker(RectTransform parent, float alpha)
    {
        Image blocker = UiFactory.CreateImage("ModalBlocker", parent, UiTheme.WhiteSprite, new Color(0f, 0f, 0f, alpha));
        blocker.type = Image.Type.Simple;
        blocker.preserveAspect = false;
        blocker.raycastTarget = true;
        UiFactory.Stretch(blocker.rectTransform, 0f, 0f, 0f, 0f);
        return blocker;
    }

    private void CreateCircleButton(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, float size, Sprite icon, string label, Action onClick)
    {
        RectTransform root = UiFactory.CreateRect(name, parent);
        root.anchorMin = anchorMin;
        root.anchorMax = anchorMax;
        root.pivot = pivot;
        root.sizeDelta = new Vector2(size, size);
        root.anchoredPosition = anchoredPosition;

        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.CircleSprite;
        background.type = Image.Type.Simple;
        background.color = UiTheme.NavBackgroundCream;

        Shadow shadow = root.gameObject.AddComponent<Shadow>();
        shadow.effectColor = UiTheme.NavShadow;
        shadow.effectDistance = new Vector2(0f, -1f);

        if (icon != null)
        {
            Image iconImage = UiFactory.CreateImage("Icon", root, icon, Color.white);
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.sizeDelta = new Vector2(size * 0.55f, size * 0.55f);
            iconImage.rectTransform.anchoredPosition = Vector2.zero;
        }

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = UiFactory.CreateLabel("Label", root, label, 16, UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
            text.font = UiTheme.NavExtraBoldFont;
            UiFactory.Stretch(text.rectTransform, 4f, 4f, 4f, 4f);
        }

        UiFactory.AddButton(root.gameObject, delegate
        {
            if (onClick != null)
            {
                onClick();
            }
        });
    }

    private RectTransform CreateStyledCircleButton(RectTransform parent, string name, Vector2 anchoredPosition, float size, Sprite icon, string label, Color backgroundColor, Color contentColor, Color ringColor, bool showRing, Action onClick)
    {
        RectTransform root = UiFactory.CreateRect(name, parent);
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(size, size);
        root.anchoredPosition = anchoredPosition;

        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.CircleSprite;
        background.type = Image.Type.Simple;
        background.color = backgroundColor;
        background.raycastTarget = true;

        Shadow shadow = root.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.45f, 0.27f, 0.22f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        if (showRing)
        {
            Image ring = UiFactory.CreateImage("Ring", root, UiTheme.CircleOutlineSprite, ringColor);
            ring.type = Image.Type.Simple;
            ring.preserveAspect = false;
            ring.raycastTarget = false;
            UiFactory.Stretch(ring.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (icon != null)
        {
            Image iconImage = UiFactory.CreateImage("Icon", root, icon, contentColor);
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            iconImage.rectTransform.sizeDelta = new Vector2(size * 0.46f, size * 0.46f);
            iconImage.rectTransform.anchoredPosition = Vector2.zero;
        }

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = UiFactory.CreateLabel("Label", root, label, size <= UtilityControlSize ? 16 : 15, contentColor, FontStyles.Bold, TextAlignmentOptions.Center);
            text.font = UiTheme.NavExtraBoldFont;
            text.raycastTarget = false;
            UiFactory.Stretch(text.rectTransform, 2f, 2f, 2f, 2f);
        }

        Button button = UiFactory.AddButton(root.gameObject, delegate
        {
            if (onClick != null)
            {
                onClick();
            }
        });

        if (button != null && string.Equals(name, "CallAttention", StringComparison.Ordinal))
        {
            PawPalUiAudio.AttachTo(button, PawPalUiClickSoundKind.Whistle);
        }

        return root;
    }

    private void CreateControlCaption(RectTransform parent, string name, string text, float x, float y)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, 11, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        label.font = UiTheme.NavRegularFont;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(72f, 18f);
        label.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    private void CreateTextButton(RectTransform parent, string name, string label, float x, float y, float width, float height, bool primary, Action onClick)
    {
        RectTransform root = UiFactory.CreateRect(name, parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(width, height);
        root.anchoredPosition = new Vector2(x, -y);

        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedTenSprite;
        background.type = Image.Type.Sliced;
        background.color = primary ? UiTheme.NavBrand : UiTheme.CardWhite;

        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = UiTheme.NavBrand;
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI text = UiFactory.CreateLabel("Label", root, label, 14, primary ? Color.white : UiTheme.NavBrandDark, FontStyles.Bold, TextAlignmentOptions.Center);
        text.font = UiTheme.NavExtraBoldFont;
        UiFactory.Stretch(text.rectTransform, 8f, 4f, 8f, 4f);

        UiFactory.AddButton(root.gameObject, delegate
        {
            if (onClick != null)
            {
                onClick();
            }
        });
    }

    private TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text, int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Bold, TextAlignmentOptions.Center);
        label.font = UiTheme.NavExtraBoldFont;
        label.rectTransform.anchorMin = anchorMin;
        label.rectTransform.anchorMax = anchorMax;
        label.rectTransform.pivot = pivot;
        label.rectTransform.sizeDelta = sizeDelta;
        label.rectTransform.anchoredPosition = anchoredPosition;
        return label;
    }

    private static void SetTopLeft(RectTransform rectTransform, float x, float y, float width, float height)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = new Vector2(x, -y);
    }

    private void RefreshFavoriteLabel(PawPalPhotoRecord record)
    {
        if (favoriteLabel != null)
        {
            favoriteLabel.text = record != null && record.Favorite ? "Favorite photo" : "Not in favorites";
        }
    }

    private string BuildDetailTitle(PawPalPhotoRecord record)
    {
        return BuildPhotoTitle(record);
    }

    private string BuildPhotoTitle(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return "Photo";
        }

        if (!string.IsNullOrWhiteSpace(record.Title))
        {
            return record.Title.Trim();
        }

        string dogName = string.IsNullOrWhiteSpace(record.DogName) ? "Dog" : record.DogName;
        return dogName + " photo";
    }

    private string FormatPhotoDate(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return string.Empty;
        }

        DateTime created = record.CreatedUtcTicks > 0 ? new DateTime(record.CreatedUtcTicks, DateTimeKind.Utc).ToLocalTime() : DateTime.Now;
        return created.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    private Sprite GetIcon(string iconName)
    {
        return sprites != null ? sprites.GetIcon(iconName) : UiTheme.WhiteSprite;
    }

    private Sprite GetResourceSprite(string resourcePath)
    {
        return sprites != null ? sprites.GetResourceSprite(resourcePath) : UiTheme.WhiteSprite;
    }

    private Sprite GetWhiteResourceSprite(string resourcePath)
    {
        return sprites != null ? sprites.GetWhiteResourceSprite(resourcePath) : UiTheme.WhiteSprite;
    }

    private static Sprite GetPhotoSheetSprite()
    {
        if (photoSheetSprite == null)
        {
            photoSheetSprite = BuildTopRoundedSheetSprite("PhotoModeBottomSheet", 96, 96, 28f);
        }

        return photoSheetSprite;
    }

    private static Sprite BuildTopRoundedSheetSprite(string name, int width, int height, float topRadius)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32 transparent = new Color32(255, 255, 255, 0);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = true;
                int topY = height - 1 - y;

                if (x < topRadius && topY < topRadius)
                {
                    Vector2 center = new Vector2(topRadius - 1f, topRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, topY), center) <= topRadius;
                }
                else if (x >= width - topRadius && topY < topRadius)
                {
                    Vector2 center = new Vector2(width - topRadius, topRadius - 1f);
                    inside = Vector2.Distance(new Vector2(x, topY), center) <= topRadius;
                }

                texture.SetPixel(x, y, inside ? UiTheme.White : transparent);
            }
        }

        texture.Apply();
        Vector4 border = new Vector4(topRadius, topRadius, topRadius, topRadius);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
    }

    private IEnumerator HideToastAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, delay));
        if (toastRoot != null)
        {
            toastRoot.gameObject.SetActive(false);
        }

        toastRoutine = null;
    }

    private void ClearLoadedAlbumAssets()
    {
        if (detailImage != null)
        {
            detailImage.texture = null;
        }

        for (int i = 0; i < loadedSprites.Count; i++)
        {
            if (loadedSprites[i] != null)
            {
                Destroy(loadedSprites[i]);
            }
        }

        for (int i = 0; i < loadedTextures.Count; i++)
        {
            if (loadedTextures[i] != null)
            {
                Destroy(loadedTextures[i]);
            }
        }

        loadedSprites.Clear();
        loadedTextures.Clear();
    }

    private void Raise(Action handler)
    {
        if (handler != null)
        {
            handler();
        }
    }

    private void Raise(Action<PawPalPhotoRecord> handler, PawPalPhotoRecord record)
    {
        if (handler != null)
        {
            handler(record);
        }
    }

    private void Raise(Action<PawPalPhotoPoseId> handler, PawPalPhotoPoseId poseId)
    {
        if (handler != null)
        {
            handler(poseId);
        }
    }

    private void Raise(Action<PawPalPhotoRecord, string> handler, PawPalPhotoRecord record, string value)
    {
        if (handler != null)
        {
            handler(record, value);
        }
    }
}
