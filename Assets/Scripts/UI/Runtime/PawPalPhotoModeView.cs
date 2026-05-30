using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class PawPalPhotoModeView : MonoBehaviour
{
    private const float ControlSize = 54f;
    private const float SmallControlSize = 44f;

    private readonly List<Texture2D> loadedTextures = new List<Texture2D>();
    private readonly List<Sprite> loadedSprites = new List<Sprite>();

    private UiSpriteLibrary sprites;
    private RectTransform hudRoot;
    private RectTransform bottomControls;
    private RectTransform previewRoot;
    private RectTransform albumRoot;
    private RectTransform albumContent;
    private RectTransform detailRoot;
    private RectTransform toastRoot;
    private TextMeshProUGUI toastLabel;
    private TextMeshProUGUI albumEmptyLabel;
    private TextMeshProUGUI detailTitle;
    private TextMeshProUGUI favoriteLabel;
    private RawImage previewImage;
    private RawImage detailImage;
    private AspectRatioFitter previewFitter;
    private AspectRatioFitter detailFitter;
    private Slider zoomSlider;
    private Coroutine toastRoutine;
    private PawPalPhotoRecord detailRecord;

    public event Action CloseRequested;
    public event Action ShutterRequested;
    public event Action AlbumRequested;
    public event Action FocusRequested;
    public event Action AttentionRequested;
    public event Action KeepRequested;
    public event Action RetakeRequested;
    public event Action DeletePreviewRequested;
    public event Action AlbumBackRequested;
    public event Action<PawPalPhotoRecord> AlbumPhotoSelected;
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
        BuildToast(root);
        HideAll();
    }

    public void HideAll()
    {
        ClearLoadedAlbumAssets();
        detailRecord = null;

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(false);
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
        detailRecord = null;

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(true);
        }

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
        if (zoomSlider != null)
        {
            zoomSlider.SetValueWithoutNotify(Mathf.Clamp01(zoom01));
        }
    }

    public void ShowPreview(Texture2D texture)
    {
        gameObject.SetActive(true);

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(false);
        }

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

        if (hudRoot != null)
        {
            hudRoot.gameObject.SetActive(false);
        }

        if (previewRoot != null)
        {
            previewRoot.gameObject.SetActive(false);
        }

        if (detailRoot != null)
        {
            detailRoot.gameObject.SetActive(false);
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
        detailRecord = record;

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

        CreateCircleButton(
            hudRoot,
            "ClosePhotoMode",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -18f),
            SmallControlSize,
            null,
            "X",
            delegate { Raise(CloseRequested); });

        CreateCircleButton(
            hudRoot,
            "OpenAlbum",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-18f, -18f),
            SmallControlSize,
            GetIcon("icon_cam_brand"),
            string.Empty,
            delegate { Raise(AlbumRequested); });

        bottomControls = UiFactory.CreateRect("BottomControls", hudRoot);
        bottomControls.anchorMin = new Vector2(0.5f, 0f);
        bottomControls.anchorMax = new Vector2(0.5f, 0f);
        bottomControls.pivot = new Vector2(0.5f, 0f);
        bottomControls.sizeDelta = new Vector2(360f, 134f);
        bottomControls.anchoredPosition = new Vector2(0f, 18f);

        Image bottomGlow = bottomControls.gameObject.AddComponent<Image>();
        bottomGlow.sprite = UiTheme.RoundedTenSprite;
        bottomGlow.type = Image.Type.Sliced;
        bottomGlow.color = new Color(0f, 0f, 0f, 0.18f);
        bottomGlow.raycastTarget = false;

        CreateCircleButton(
            bottomControls,
            "FocusPet",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(-98f, -13f),
            SmallControlSize,
            GetIcon("icon_paw_brand"),
            string.Empty,
            delegate { Raise(FocusRequested); });

        CreateCircleButton(
            bottomControls,
            "CallAttention",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(98f, -13f),
            SmallControlSize,
            GetIcon("icon_audio"),
            string.Empty,
            delegate { Raise(AttentionRequested); });

        CreateCircleButton(
            bottomControls,
            "Shutter",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -3f),
            ControlSize,
            GetResourceSprite("UI/Figma/HomeMain/icon_cam"),
            string.Empty,
            delegate { Raise(ShutterRequested); });

        zoomSlider = CreateSlider(bottomControls);
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

        RectTransform panel = CreatePanel(albumRoot, "AlbumPanel", 360f, 650f);
        CreateCircleButton(
            panel,
            "AlbumBack",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(14f, -14f),
            36f,
            null,
            "X",
            delegate { Raise(AlbumBackRequested); });

        CreateLabel(panel, "AlbumTitle", "Photo Album", 19, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(0f, 30f));

        RectTransform viewport = UiFactory.CreateRect("Viewport", panel);
        UiFactory.Stretch(viewport, 14f, 18f, 14f, 66f);
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

        GridLayoutGroup grid = albumContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(96f, 116f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.childAlignment = TextAnchor.UpperCenter;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = albumContent;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        albumEmptyLabel = CreateLabel(panel, "EmptyAlbum", "No photos yet", 16, UiTheme.NavBrandDark, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 28f));
    }

    private void BuildDetail(RectTransform root)
    {
        detailRoot = UiFactory.CreateRect("PhotoDetail", root);
        UiFactory.Stretch(detailRoot, 0f, 0f, 0f, 0f);
        CreateBlocker(detailRoot, 0.58f);

        RectTransform panel = CreatePanel(detailRoot, "DetailPanel", 360f, 650f);
        CreateCircleButton(
            panel,
            "DetailBack",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(14f, -14f),
            36f,
            null,
            "<",
            delegate { Raise(DetailBackRequested); });

        detailTitle = CreateLabel(panel, "DetailTitle", string.Empty, 17, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(0f, 30f));

        detailImage = UiFactory.CreateRect("DetailImage", panel).gameObject.AddComponent<RawImage>();
        detailImage.color = Color.white;
        detailImage.raycastTarget = false;
        UiFactory.Stretch(detailImage.rectTransform, 16f, 104f, 16f, 66f);

        detailFitter = detailImage.gameObject.AddComponent<AspectRatioFitter>();
        detailFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        detailFitter.aspectRatio = 1f;

        CreateTextButton(panel, "FavoriteButton", "Favorite", 24f, 580f, 96f, 38f, true, delegate
        {
            Raise(DetailFavoriteToggled, detailRecord);
        });

        favoriteLabel = CreateLabel(panel, "FavoriteLabel", string.Empty, 14, UiTheme.NavBrandDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(16f, -540f), new Vector2(-32f, 24f));
        favoriteLabel.alignment = TextAlignmentOptions.Left;

        CreateTextButton(panel, "DeleteDetailButton", "Delete", 234f, 580f, 92f, 38f, false, delegate
        {
            Raise(DetailDeleteRequested, detailRecord);
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

        IReadOnlyList<PawPalPhotoRecord> records = store != null ? store.Records : null;
        int count = records != null ? records.Count : 0;
        if (albumEmptyLabel != null)
        {
            albumEmptyLabel.gameObject.SetActive(count == 0);
        }

        for (int i = 0; i < count; i++)
        {
            PawPalPhotoRecord record = records[i];
            Texture2D thumbnail = store.LoadThumbnailTexture(record);
            Sprite sprite = thumbnail != null ? Sprite.Create(thumbnail, new Rect(0f, 0f, thumbnail.width, thumbnail.height), new Vector2(0.5f, 0.5f), 100f) : UiTheme.WhiteSprite;
            if (thumbnail != null)
            {
                loadedTextures.Add(thumbnail);
            }

            if (sprite != null && sprite != UiTheme.WhiteSprite)
            {
                loadedSprites.Add(sprite);
            }

            CreateAlbumThumbnail(albumContent, record, sprite);
        }

        int rows = Mathf.Max(1, Mathf.CeilToInt(count / 3f));
        albumContent.sizeDelta = new Vector2(0f, rows * 126f + 16f);
        albumContent.anchoredPosition = Vector2.zero;
    }

    private void CreateAlbumThumbnail(RectTransform parent, PawPalPhotoRecord record, Sprite thumbnail)
    {
        RectTransform root = UiFactory.CreateRect("PhotoThumb", parent);
        root.sizeDelta = new Vector2(96f, 116f);

        Image image = UiFactory.CreateImage("Image", root, thumbnail, Color.white);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.rectTransform.anchorMin = new Vector2(0f, 1f);
        image.rectTransform.anchorMax = new Vector2(0f, 1f);
        image.rectTransform.pivot = new Vector2(0f, 1f);
        image.rectTransform.sizeDelta = new Vector2(96f, 96f);
        image.rectTransform.anchoredPosition = Vector2.zero;

        if (record != null && record.Favorite)
        {
            TextMeshProUGUI star = UiFactory.CreateLabel("Favorite", root, "*", 18, UiTheme.NavBrand, FontStyles.Bold, TextAlignmentOptions.Center);
            star.rectTransform.anchorMin = new Vector2(1f, 1f);
            star.rectTransform.anchorMax = new Vector2(1f, 1f);
            star.rectTransform.pivot = new Vector2(1f, 1f);
            star.rectTransform.sizeDelta = new Vector2(22f, 22f);
            star.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
        }

        TextMeshProUGUI caption = UiFactory.CreateLabel("Caption", root, record != null ? record.DogName : string.Empty, 11, UiTheme.NavBrandDark, FontStyles.Normal, TextAlignmentOptions.Center);
        caption.font = UiTheme.NavRegularFont;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.rectTransform.anchorMin = new Vector2(0f, 0f);
        caption.rectTransform.anchorMax = new Vector2(1f, 0f);
        caption.rectTransform.pivot = new Vector2(0.5f, 0f);
        caption.rectTransform.offsetMin = new Vector2(0f, 0f);
        caption.rectTransform.offsetMax = new Vector2(0f, 18f);

        UiFactory.AddButton(root.gameObject, delegate
        {
            Raise(AlbumPhotoSelected, record);
        });
    }

    private Slider CreateSlider(RectTransform parent)
    {
        RectTransform sliderRoot = UiFactory.CreateRect("ZoomSlider", parent);
        sliderRoot.anchorMin = new Vector2(0.5f, 0f);
        sliderRoot.anchorMax = new Vector2(0.5f, 0f);
        sliderRoot.pivot = new Vector2(0.5f, 0f);
        sliderRoot.sizeDelta = new Vector2(230f, 26f);
        sliderRoot.anchoredPosition = new Vector2(0f, 17f);

        RectTransform background = UiFactory.CreateRect("Background", sliderRoot);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.pivot = new Vector2(0.5f, 0.5f);
        background.offsetMin = new Vector2(0f, -3f);
        background.offsetMax = new Vector2(0f, 3f);
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.sprite = UiTheme.RoundedFiveSprite;
        backgroundImage.type = Image.Type.Sliced;
        backgroundImage.color = new Color(1f, 1f, 1f, 0.65f);

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

        RectTransform handleArea = UiFactory.CreateRect("Handle Slide Area", sliderRoot);
        UiFactory.Stretch(handleArea, 0f, 0f, 0f, 0f);

        RectTransform handle = UiFactory.CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(22f, 22f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = UiTheme.CircleSprite;
        handleImage.type = Image.Type.Simple;
        handleImage.color = UiTheme.NavBackgroundCream;

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
            Action<float> handler = ZoomChanged;
            if (handler != null)
            {
                handler(value);
            }
        });

        return slider;
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

    private void RefreshFavoriteLabel(PawPalPhotoRecord record)
    {
        if (favoriteLabel != null)
        {
            favoriteLabel.text = record != null && record.Favorite ? "Favorite photo" : string.Empty;
        }
    }

    private string BuildDetailTitle(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return "Photo";
        }

        DateTime created = record.CreatedUtcTicks > 0 ? new DateTime(record.CreatedUtcTicks, DateTimeKind.Utc).ToLocalTime() : DateTime.Now;
        string dogName = string.IsNullOrWhiteSpace(record.DogName) ? "Dog" : record.DogName;
        return dogName + " - " + created.ToString("MMM d, h:mm tt");
    }

    private Sprite GetIcon(string iconName)
    {
        return sprites != null ? sprites.GetIcon(iconName) : UiTheme.WhiteSprite;
    }

    private Sprite GetResourceSprite(string resourcePath)
    {
        return sprites != null ? sprites.GetResourceSprite(resourcePath) : UiTheme.WhiteSprite;
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
}
