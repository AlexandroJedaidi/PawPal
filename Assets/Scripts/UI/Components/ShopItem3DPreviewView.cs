using System.Collections;
using System.Collections.Generic;
using UnityEngine.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class ShopItem3DPreviewView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
{
    private const string PreviewDogResourcePath = "PawPal/Preview/DogPreviewMannequin";
    private const int PreviewLayer = 31;
    private const int TargetTextureWidth = 512;
    private const float PreviewStageOffset = 10000f;
    private const float DragRotationDegreesPerPixel = 0.45f;
    private const float MouseWheelZoomStep = 0.16f;
    private const float TouchPinchZoomSensitivity = 0.004f;
    private const float MinimumZoom = 0.7f;
    private const float MaximumZoom = 3.2f;
    private const float StandaloneFramePadding = 1.35f;
    private const float SpriteCardFramePadding = 1.18f;
    private const float SpriteCardDepth = 0.035f;
    private const float ProceduralBallRadius = 0.5f;
    private const float ProceduralPawSurfaceZ = -0.493f;
    private const float WearableFocusPadding = 2.8f;
    private const float WearableDogHeightPadding = 0.18f;
    private const int AnimatorBaseLayerIndex = 0;
    private const float PreviewAnimationCrossFadeSeconds = 0.16f;
    private const float PreviewIdleSeconds = 2.2f;
    private const float PreviewSittingStartSeconds = 0.75f;
    private const float PreviewSittingLoopSeconds = 2.15f;
    private const float PreviewSittingEndSeconds = 0.75f;
    private const float PreviewLieStartSeconds = 0.82f;
    private const float PreviewLieLoopSettleSeconds = 0.12f;
    private const float PreviewTriptychSpacing = 0.42f;
    private const float PreviewTriptychForwardOffset = 0.03f;
    private const float PreviewTriptychPadding = 0.98f;
    private const int PreviewNeutralIdleIndex = 99;

    private static readonly int MoveHash = Animator.StringToHash("Move");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DirectionHash = Animator.StringToHash("Direction");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly int SitEndTriggerHash = Animator.StringToHash("SitEndTrigger");

    private static readonly string[] PreferredCollarPlacementReferenceNames =
    {
        "Collar",
        "CollarSimple_C1",
        "CollarSimple_C2",
        "CollarSimple_C3"
    };

    private static readonly string[] PreferredCollarAnchorNames =
    {
        "CollarAnchor",
        "AccessoryAnchor",
        "Accessories",
        "Accessory",
        "NeckSocket",
        "neck",
        "Neck",
        "HeadAimRig",
        "Spine_05",
        "Spine_04",
        "Spine_03"
    };

    private static readonly Dictionary<string, bool> ResourceSpriteExistsCache = new Dictionary<string, bool>();
    private static readonly HashSet<string> MissingBreedPreviewWarnings = new HashSet<string>();

    private UiSpriteLibrary sprites;
    private RectTransform rectTransform;
    private RawImage previewImage;
    private Image fallbackImage;
    private RenderTexture renderTexture;
    private GameObject stageRoot;
    private Transform modelRoot;
    private Transform focusRoot;
    private Camera previewCamera;
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private readonly List<Mesh> runtimeMeshes = new List<Mesh>();
    private readonly List<Camera> sceneCamerasWithPreviewLayer = new List<Camera>();
    private readonly List<Animator> previewAnimators = new List<Animator>();
    private readonly List<PlayableGraph> previewPlayableGraphs = new List<PlayableGraph>();
    private Animator previewDogAnimator;
    private Coroutine previewDogAnimationRoutine;
    private bool previewDogAnimationPending;

    private PawPalShopPreviewMode activePreviewMode;
    private bool isDragging;
    private int activePointerId = int.MinValue;
    private Vector2 lastPointerPosition;
    private float lastPinchDistance;
    private float previewYaw = 22f;
    private float baseOrthographicSize = 1f;
    private float zoomScale = 1f;

    public void Initialize(UiSpriteLibrary spriteLibrary)
    {
        sprites = spriteLibrary;
        rectTransform = GetComponent<RectTransform>();
        EnsureUi();
    }

    public void ShowItem(PawPalCatalogItemDefinition item)
    {
        EnsureUi();
        ClearPreview();

        if (item != null && item.PreviewMode != PawPalShopPreviewMode.SpriteOnly && TryBuildPreview(item))
        {
            return;
        }

        ClearPreview();
        if (item != null && TryBuildProceduralPreview(item))
        {
            return;
        }

        ClearPreview();
        if (item == null || !TryBuildSpritePreview(item))
        {
            ShowSpriteFallback(item);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (modelRoot == null || eventData == null)
        {
            return;
        }

        isDragging = true;
        activePointerId = eventData.pointerId;
        lastPointerPosition = eventData.position;
        lastPinchDistance = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || modelRoot == null || eventData == null || eventData.pointerId != activePointerId)
        {
            return;
        }

        Vector2 currentPosition = eventData.position;
        float deltaX = currentPosition.x - lastPointerPosition.x;
        previewYaw -= deltaX * DragRotationDegreesPerPixel;
        lastPointerPosition = currentPosition;
        ApplyModelRotation();
        RenderPreview();
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null)
        {
            isDragging = false;
            activePointerId = int.MinValue;
            return;
        }

        if (eventData.pointerId == activePointerId)
        {
            isDragging = false;
            activePointerId = int.MinValue;
            eventData.Use();
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (modelRoot == null || eventData == null)
        {
            return;
        }

        AdjustZoom(eventData.scrollDelta.y * MouseWheelZoomStep);
        eventData.Use();
    }

    private void Update()
    {
        if (modelRoot == null)
        {
            return;
        }

        if (HandlePinchZoom())
        {
            return;
        }

        lastPinchDistance = 0f;
        if (HasActivePreviewAnimator())
        {
            RenderPreview();
            return;
        }
    }

    private void OnEnable()
    {
        if (previewDogAnimationPending)
        {
            StartPreviewDogAnimation();
        }

        RenderPreview();
    }

    private void OnDisable()
    {
        StopPreviewDogAnimation(true);
    }

    private void OnDestroy()
    {
        ClearPreview();
    }

    private void EnsureUi()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (previewImage != null && fallbackImage != null)
        {
            return;
        }

        if (previewImage == null)
        {
            RectTransform previewRect = UiFactory.CreateRect("Preview3DImage", transform);
            UiFactory.Stretch(previewRect, 0f, 0f, 0f, 0f);
            previewImage = previewRect.gameObject.AddComponent<RawImage>();
            previewImage.color = Color.white;
            previewImage.raycastTarget = true;
            previewImage.enabled = false;
        }

        if (fallbackImage == null)
        {
            fallbackImage = UiFactory.CreateImage("PreviewFallbackImage", transform, UiTheme.WhiteSprite, Color.white);
            fallbackImage.type = Image.Type.Simple;
            fallbackImage.preserveAspect = true;
            fallbackImage.raycastTarget = false;
            UiFactory.Stretch(fallbackImage.rectTransform, 0f, 0f, 0f, 0f);
            fallbackImage.enabled = false;
        }
    }

    private bool TryBuildPreview(PawPalCatalogItemDefinition item)
    {
        if (item != null && item.PreviewMode == PawPalShopPreviewMode.BreedTriptychDog)
        {
            return TryBuildBreedTriptychPreview(item);
        }

        string prefabPath = ResolvePreviewPrefabPath(item);
        if (string.IsNullOrEmpty(prefabPath))
        {
            return false;
        }

        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null)
        {
            return false;
        }

        activePreviewMode = item.PreviewMode;
        CreatePreviewStage();
        modelRoot = new GameObject("ShopItemPreviewModel").transform;
        modelRoot.SetParent(stageRoot.transform, false);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one;

        if (item.PreviewMode == PawPalShopPreviewMode.WearableOnDog)
        {
            if (!BuildWearablePreview(item, prefab))
            {
                return false;
            }
        }
        else
        {
            GameObject model = Instantiate(prefab, modelRoot, false);
            model.name = prefab.name;
            focusRoot = model.transform;
            StripPreviewOnlyComponents(model);
            ApplyPreviewTint(model, item.ToyTint);
        }

        SetLayerRecursively(stageRoot.transform, PreviewLayer);
        if (!TryFrameModel())
        {
            return false;
        }

        fallbackImage.enabled = false;
        previewImage.enabled = true;
        previewImage.texture = renderTexture;
        previewYaw = item.PreviewMode == PawPalShopPreviewMode.WearableOnDog ? 155f : 24f;
        zoomScale = 1f;
        ApplyModelRotation();
        ApplyCameraZoom();
        RenderPreview();
        return true;
    }

    private bool TryBuildBreedTriptychPreview(PawPalCatalogItemDefinition item)
    {
        PawPalBreedShopPreviewEntry breedEntry;
        if (!PawPalBreedShopPreviewLibrary.TryResolveEntry(item, out breedEntry) || breedEntry == null)
        {
            WarnMissingBreedPreview(item, "missing breed preview entry");
            return false;
        }

        GameObject centerPrefab = PawPalBreedShopPreviewLibrary.LoadCenterPrefab(breedEntry);
        GameObject leftPrefab = PawPalBreedShopPreviewLibrary.LoadLeftPrefab(breedEntry);
        GameObject rightPrefab = PawPalBreedShopPreviewLibrary.LoadRightPrefab(breedEntry);
        if (centerPrefab == null || leftPrefab == null || rightPrefab == null)
        {
            WarnMissingBreedPreview(item, "missing one or more breed preview prefabs");
            return false;
        }

        activePreviewMode = item.PreviewMode;
        CreatePreviewStage();
        modelRoot = new GameObject("ShopBreedPreviewTriptych").transform;
        modelRoot.SetParent(stageRoot.transform, false);
        modelRoot.localPosition = breedEntry.GroupOffset;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one * Mathf.Max(0.01f, breedEntry.GroupScale);

        if (!TryBuildBreedPreviewDog(leftPrefab, breedEntry.BreedKey, new Vector3(-PreviewTriptychSpacing, 0f, PreviewTriptychForwardOffset), Quaternion.Euler(0f, 12f, 0f), PreviewBreedPose.Lie)
            || !TryBuildBreedPreviewDog(centerPrefab, breedEntry.BreedKey, new Vector3(0f, 0f, 0f), Quaternion.identity, PreviewBreedPose.Stand)
            || !TryBuildBreedPreviewDog(rightPrefab, breedEntry.BreedKey, new Vector3(PreviewTriptychSpacing, 0f, PreviewTriptychForwardOffset), Quaternion.Euler(0f, -12f, 0f), PreviewBreedPose.Sit))
        {
            WarnMissingBreedPreview(item, "failed to construct one or more preview dogs");
            return false;
        }

        focusRoot = modelRoot;
        SetLayerRecursively(stageRoot.transform, PreviewLayer);
        if (!TryFrameModel(PreviewTriptychPadding))
        {
            WarnMissingBreedPreview(item, "failed to frame breed triptych preview");
            return false;
        }

        fallbackImage.enabled = false;
        previewImage.enabled = true;
        previewImage.texture = renderTexture;
        previewYaw = 180f;
        zoomScale = 2.15f;
        ApplyModelRotation();
        ApplyCameraZoom();
        RenderPreview();
        return true;
    }

    private bool TryBuildSpritePreview(PawPalCatalogItemDefinition item)
    {
        Sprite sprite = ResolvePreviewSprite(item);
        if (sprite == null || sprite == UiTheme.WhiteSprite)
        {
            return false;
        }

        activePreviewMode = PawPalShopPreviewMode.SpriteOnly;
        CreatePreviewStage();
        modelRoot = new GameObject("ShopItemPreviewSprite").transform;
        modelRoot.SetParent(stageRoot.transform, false);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one;

        GameObject card = new GameObject("SpriteCard");
        card.hideFlags = HideFlags.HideAndDontSave;
        card.transform.SetParent(modelRoot, false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;

        MeshFilter meshFilter = card.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = card.AddComponent<MeshRenderer>();
        Material spriteMaterial = CreateSpritePreviewMaterial(sprite);
        if (spriteMaterial == null)
        {
            return false;
        }

        meshFilter.sharedMesh = CreateSpriteCardMesh(sprite);
        meshRenderer.sharedMaterial = spriteMaterial;
        focusRoot = card.transform;

        SetLayerRecursively(stageRoot.transform, PreviewLayer);
        if (!TryFrameModel(SpriteCardFramePadding))
        {
            return false;
        }

        fallbackImage.enabled = false;
        previewImage.enabled = true;
        previewImage.texture = renderTexture;
        previewYaw = 0f;
        zoomScale = 1f;
        ApplyModelRotation();
        ApplyCameraZoom();
        RenderPreview();
        return true;
    }

    private bool TryBuildProceduralPreview(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.PreviewMode == PawPalShopPreviewMode.WearableOnDog && TryBuildProceduralWearablePreview(item))
        {
            return true;
        }

        if (!ShouldUseProceduralStandalonePreview(item))
        {
            return false;
        }

        activePreviewMode = PawPalShopPreviewMode.StandaloneModel;
        CreatePreviewStage();
        modelRoot = new GameObject("ShopItemPreviewProceduralToy").transform;
        modelRoot.SetParent(stageRoot.transform, false);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one;

        GameObject model = BuildProceduralStandaloneModel(item);
        if (model == null)
        {
            return false;
        }

        focusRoot = model.transform;
        SetLayerRecursively(stageRoot.transform, PreviewLayer);
        if (!TryFrameModel(StandaloneFramePadding))
        {
            return false;
        }

        fallbackImage.enabled = false;
        previewImage.enabled = true;
        previewImage.texture = renderTexture;
        previewYaw = 24f;
        zoomScale = 1f;
        ApplyModelRotation();
        ApplyCameraZoom();
        RenderPreview();
        return true;
    }

    private bool TryBuildBreedPreviewDog(GameObject prefab, string breedKey, Vector3 localPosition, Quaternion localRotation, PreviewBreedPose pose)
    {
        if (prefab == null || modelRoot == null)
        {
            return false;
        }

        GameObject dog = Instantiate(prefab, modelRoot, false);
        dog.name = prefab.name;
        dog.transform.localPosition = localPosition;
        dog.transform.localRotation = localRotation;
        dog.transform.localScale = Vector3.one;
        StripPreviewOnlyComponents(dog, true);

        Animator animator = ConfigurePreviewDogAnimator(dog, breedKey, prefab.name, dog.name);
        if (animator == null)
        {
            return false;
        }

        RegisterPreviewAnimator(animator);
        ApplyBreedPreviewPose(dog, animator, breedKey, pose);
        return true;
    }

    private static bool ShouldUseProceduralStandalonePreview(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        string id = item.Id ?? string.Empty;
        if (id.IndexOf("_ball", System.StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("_bone", System.StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("_roll", System.StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("_wheel", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (item.PreviewMode == PawPalShopPreviewMode.StandaloneModel)
        {
            return true;
        }

        string name = item.DisplayName ?? string.Empty;
        return name.IndexOf("ball", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("roll", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private GameObject BuildProceduralStandaloneModel(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return null;
        }

        string id = item.Id ?? string.Empty;
        if (id.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralBone(item);
        }

        if (id.IndexOf("roll", System.StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("wheel", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralWheel(item);
        }

        if (id.IndexOf("bed_", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralBed(item);
        }

        if (id.IndexOf("bowl", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralBowl(item);
        }

        if (id.IndexOf("carrier", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralCarrier(item);
        }

        if (id.IndexOf("house", System.StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("box", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralHouse(item);
        }

        if (id.IndexOf("scratch", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralScratchingPost(item);
        }

        if (id.IndexOf("sofa", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralSofa(item);
        }

        if (id.IndexOf("mouse", System.StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("cattoy", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralMouseToy(item);
        }

        return BuildProceduralBall(item);
    }

    private GameObject BuildProceduralBall(PawPalCatalogItemDefinition item)
    {
        GameObject ball = CreatePreviewPrimitive("ProceduralBall", PrimitiveType.Sphere, modelRoot);
        ball.transform.localPosition = Vector3.zero;
        ball.transform.localRotation = Quaternion.identity;
        ball.transform.localScale = Vector3.one * (ProceduralBallRadius * 2f);
        AssignPreviewMaterial(ball, CreatePreviewMaterial(ResolveProceduralToyColor(item), "ProceduralBallMaterial", 0.46f));
        CreateProceduralPawMark(ball.transform);
        return ball;
    }

    private GameObject BuildProceduralBone(PawPalCatalogItemDefinition item)
    {
        GameObject bone = new GameObject("ProceduralBone");
        bone.hideFlags = HideFlags.HideAndDontSave;
        bone.transform.SetParent(modelRoot, false);

        Material material = CreatePreviewMaterial(ResolveProceduralToyColor(item), "ProceduralBoneMaterial", 0.38f);
        GameObject bar = CreatePreviewPrimitive("Bar", PrimitiveType.Capsule, bone.transform);
        bar.transform.localPosition = Vector3.zero;
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        bar.transform.localScale = new Vector3(0.16f, 0.36f, 0.16f);
        AssignPreviewMaterial(bar, material);

        CreateBoneLobe(bone.transform, material, new Vector3(-0.38f, 0.12f, 0f));
        CreateBoneLobe(bone.transform, material, new Vector3(-0.38f, -0.12f, 0f));
        CreateBoneLobe(bone.transform, material, new Vector3(0.38f, 0.12f, 0f));
        CreateBoneLobe(bone.transform, material, new Vector3(0.38f, -0.12f, 0f));
        return bone;
    }

    private GameObject BuildProceduralWheel(PawPalCatalogItemDefinition item)
    {
        GameObject wheel = CreatePreviewPrimitive("ProceduralWheel", PrimitiveType.Cylinder, modelRoot);
        wheel.transform.localPosition = Vector3.zero;
        wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        wheel.transform.localScale = new Vector3(0.78f, 0.12f, 0.78f);
        AssignPreviewMaterial(wheel, CreatePreviewMaterial(ResolveProceduralToyColor(item), "ProceduralWheelMaterial", 0.5f));
        return wheel;
    }

    private void CreateBoneLobe(Transform parent, Material material, Vector3 localPosition)
    {
        GameObject lobe = CreatePreviewPrimitive("Lobe", PrimitiveType.Sphere, parent);
        lobe.transform.localPosition = localPosition;
        lobe.transform.localRotation = Quaternion.identity;
        lobe.transform.localScale = new Vector3(0.28f, 0.28f, 0.24f);
        AssignPreviewMaterial(lobe, material);
    }

    private void CreateProceduralPawMark(Transform ball)
    {
        Material pawMaterial = CreatePreviewMaterial(new Color32(36, 37, 36, 255), "ProceduralPawMaterial", 0.24f);
        CreatePawPad(ball, pawMaterial, "MainPad", new Vector3(0f, -0.055f, ProceduralPawSurfaceZ), new Vector3(0.19f, 0.13f, 0.035f));
        CreatePawPad(ball, pawMaterial, "ToeLeft", new Vector3(-0.14f, 0.06f, ProceduralPawSurfaceZ), new Vector3(0.09f, 0.12f, 0.028f));
        CreatePawPad(ball, pawMaterial, "ToeInnerLeft", new Vector3(-0.045f, 0.12f, ProceduralPawSurfaceZ), new Vector3(0.083f, 0.12f, 0.028f));
        CreatePawPad(ball, pawMaterial, "ToeInnerRight", new Vector3(0.045f, 0.12f, ProceduralPawSurfaceZ), new Vector3(0.083f, 0.12f, 0.028f));
        CreatePawPad(ball, pawMaterial, "ToeRight", new Vector3(0.14f, 0.06f, ProceduralPawSurfaceZ), new Vector3(0.09f, 0.12f, 0.028f));
    }

    private void CreatePawPad(Transform parent, Material material, string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject pad = CreatePreviewPrimitive(name, PrimitiveType.Sphere, parent);
        pad.transform.localPosition = localPosition;
        pad.transform.localRotation = Quaternion.identity;
        pad.transform.localScale = localScale;
        AssignPreviewMaterial(pad, material);
    }

    private static GameObject CreatePreviewPrimitive(string name, PrimitiveType primitiveType, Transform parent)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = name;
        primitive.hideFlags = HideFlags.HideAndDontSave;
        primitive.transform.SetParent(parent, false);
        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        return primitive;
    }

    private void AssignPreviewMaterial(GameObject target, Material material)
    {
        if (target == null || material == null)
        {
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private Material CreatePreviewMaterial(Color color, string materialName, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.hideFlags = HideFlags.HideAndDontSave;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", smoothness);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        runtimeMaterials.Add(material);
        return material;
    }

    private static Color ResolveProceduralToyColor(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return Color.white;
        }

        if (!ApproximatelyWhite(item.ToyTint))
        {
            return item.ToyTint;
        }

        switch (item.Id)
        {
            case "toy_bone_1":
                return new Color32(238, 196, 95, 255);
            case "toy_bone_2":
                return new Color32(232, 111, 150, 255);
            case "toy_ball_1":
                return new Color32(167, 218, 54, 255);
            case "toy_ball_2":
                return new Color32(70, 151, 223, 255);
            case "toy_ball_3":
                return new Color32(205, 224, 31, 255);
            case "toy_big_ball_2":
                return new Color32(236, 125, 61, 255);
            case "toy_big_ball_3":
                return new Color32(76, 151, 99, 255);
            case "toy_big_ball_4":
                return new Color32(228, 116, 153, 255);
            default:
                return new Color32(205, 224, 31, 255);
        }
    }

    private bool TryBuildProceduralWearablePreview(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        GameObject dogSource = ResolveWearablePreviewDogSource();
        if (dogSource == null)
        {
            return false;
        }

        activePreviewMode = PawPalShopPreviewMode.WearableOnDog;
        CreatePreviewStage();
        modelRoot = new GameObject("ShopItemPreviewProceduralWearable").transform;
        modelRoot.SetParent(stageRoot.transform, false);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one;

        GameObject dog = Instantiate(dogSource, modelRoot, false);
        dog.name = dogSource.name;
        StripPreviewOnlyComponents(dog, true);
        previewDogAnimator = ConfigurePreviewDogAnimator(dog);
        RegisterPreviewAnimator(previewDogAnimator);

        Transform parent = ResolveFallbackCollarAnchor(dog.transform);
        if (parent == null)
        {
            return false;
        }

        HideBuiltInCollarSources(dog.transform);

        GameObject wearable = BuildProceduralWearableModel(item, parent);
        if (wearable == null)
        {
            return false;
        }

        focusRoot = wearable.transform;
        StartPreviewDogAnimation();
        SetLayerRecursively(stageRoot.transform, PreviewLayer);
        if (!TryFrameModel())
        {
            return false;
        }

        fallbackImage.enabled = false;
        previewImage.enabled = true;
        previewImage.texture = renderTexture;
        previewYaw = 155f;
        zoomScale = 1f;
        ApplyModelRotation();
        ApplyCameraZoom();
        RenderPreview();
        return true;
    }

    private GameObject BuildProceduralBed(PawPalCatalogItemDefinition item)
    {
        GameObject root = new GameObject("ProceduralBed");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(modelRoot, false);

        Material baseMaterial = CreatePreviewMaterial(new Color32(196, 154, 108, 255), "ProceduralBedBase", 0.28f);
        Material cushionMaterial = CreatePreviewMaterial(ResolveProceduralToyColor(item), "ProceduralBedCushion", 0.22f);
        GameObject baseBox = CreatePreviewPrimitive("Base", PrimitiveType.Cube, root.transform);
        baseBox.transform.localScale = new Vector3(1.1f, 0.16f, 0.82f);
        baseBox.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        AssignPreviewMaterial(baseBox, baseMaterial);

        GameObject cushion = CreatePreviewPrimitive("Cushion", PrimitiveType.Cube, root.transform);
        cushion.transform.localScale = new Vector3(0.92f, 0.14f, 0.62f);
        cushion.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        AssignPreviewMaterial(cushion, cushionMaterial);
        return root;
    }

    private GameObject BuildProceduralBowl(PawPalCatalogItemDefinition item)
    {
        GameObject root = new GameObject("ProceduralBowl");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(modelRoot, false);

        Material bowlMaterial = CreatePreviewMaterial(new Color32(184, 194, 206, 255), "ProceduralBowlMaterial", 0.74f);
        if ((item.Id ?? string.Empty).IndexOf("doble", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            CreateBowlHalf(root.transform, bowlMaterial, -0.28f);
            CreateBowlHalf(root.transform, bowlMaterial, 0.28f);
        }
        else
        {
            CreateBowlHalf(root.transform, bowlMaterial, 0f);
        }

        return root;
    }

    private GameObject BuildProceduralCarrier(PawPalCatalogItemDefinition item)
    {
        GameObject root = new GameObject("ProceduralCarrier");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(modelRoot, false);

        Material shell = CreatePreviewMaterial(new Color32(127, 176, 224, 255), "ProceduralCarrierShell", 0.34f);
        Material grate = CreatePreviewMaterial(new Color32(232, 236, 241, 255), "ProceduralCarrierGrate", 0.78f);
        GameObject body = CreatePreviewPrimitive("Body", PrimitiveType.Cube, root.transform);
        body.transform.localScale = new Vector3(1.02f, 0.72f, 0.68f);
        AssignPreviewMaterial(body, shell);

        GameObject door = CreatePreviewPrimitive("Door", PrimitiveType.Cube, root.transform);
        door.transform.localScale = new Vector3(0.08f, 0.52f, 0.5f);
        door.transform.localPosition = new Vector3(0.48f, 0f, 0f);
        AssignPreviewMaterial(door, grate);
        return root;
    }

    private GameObject BuildProceduralHouse(PawPalCatalogItemDefinition item)
    {
        GameObject root = new GameObject("ProceduralHouse");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(modelRoot, false);

        Material wall = CreatePreviewMaterial(new Color32(214, 182, 129, 255), "ProceduralHouseWall", 0.21f);
        Material roof = CreatePreviewMaterial(new Color32(166, 103, 87, 255), "ProceduralHouseRoof", 0.18f);
        GameObject baseBox = CreatePreviewPrimitive("Base", PrimitiveType.Cube, root.transform);
        baseBox.transform.localScale = new Vector3(1.05f, 0.64f, 0.84f);
        AssignPreviewMaterial(baseBox, wall);

        GameObject roofBox = CreatePreviewPrimitive("Roof", PrimitiveType.Cube, root.transform);
        roofBox.transform.localScale = new Vector3(1.18f, 0.18f, 0.94f);
        roofBox.transform.localPosition = new Vector3(0f, 0.39f, 0f);
        roofBox.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        AssignPreviewMaterial(roofBox, roof);
        return root;
    }

    private GameObject BuildProceduralScratchingPost(PawPalCatalogItemDefinition item)
    {
        GameObject root = new GameObject("ProceduralScratchingPost");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(modelRoot, false);

        Material wood = CreatePreviewMaterial(new Color32(161, 127, 78, 255), "ProceduralScratchWood", 0.16f);
        Material fabric = CreatePreviewMaterial(new Color32(212, 186, 144, 255), "ProceduralScratchFabric", 0.12f);
        GameObject baseBox = CreatePreviewPrimitive("Base", PrimitiveType.Cube, root.transform);
        baseBox.transform.localScale = new Vector3(0.82f, 0.08f, 0.82f);
        baseBox.transform.localPosition = new Vector3(0f, -0.36f, 0f);
        AssignPreviewMaterial(baseBox, wood);

        GameObject post = CreatePreviewPrimitive("Post", PrimitiveType.Cylinder, root.transform);
        post.transform.localScale = new Vector3(0.18f, 0.62f, 0.18f);
        post.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        AssignPreviewMaterial(post, fabric);
        return root;
    }

    private GameObject BuildProceduralSofa(PawPalCatalogItemDefinition item)
    {
        GameObject root = new GameObject("ProceduralSofa");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(modelRoot, false);

        Material sofa = CreatePreviewMaterial(new Color32(118, 168, 122, 255), "ProceduralSofaMaterial", 0.24f);
        GameObject seat = CreatePreviewPrimitive("Seat", PrimitiveType.Cube, root.transform);
        seat.transform.localScale = new Vector3(1.12f, 0.24f, 0.74f);
        seat.transform.localPosition = new Vector3(0f, -0.12f, 0f);
        AssignPreviewMaterial(seat, sofa);

        GameObject back = CreatePreviewPrimitive("Back", PrimitiveType.Cube, root.transform);
        back.transform.localScale = new Vector3(1.12f, 0.58f, 0.18f);
        back.transform.localPosition = new Vector3(0f, 0.12f, -0.28f);
        AssignPreviewMaterial(back, sofa);
        return root;
    }

    private GameObject BuildProceduralMouseToy(PawPalCatalogItemDefinition item)
    {
        GameObject root = new GameObject("ProceduralMouseToy");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(modelRoot, false);

        Material material = CreatePreviewMaterial(new Color32(126, 102, 88, 255), "ProceduralMouseMaterial", 0.25f);
        GameObject body = CreatePreviewPrimitive("Body", PrimitiveType.Sphere, root.transform);
        body.transform.localScale = new Vector3(0.62f, 0.42f, 0.86f);
        AssignPreviewMaterial(body, material);

        GameObject head = CreatePreviewPrimitive("Head", PrimitiveType.Sphere, root.transform);
        head.transform.localScale = new Vector3(0.32f, 0.28f, 0.32f);
        head.transform.localPosition = new Vector3(0.22f, 0.02f, 0.34f);
        AssignPreviewMaterial(head, material);
        return root;
    }

    private GameObject BuildProceduralWearableModel(PawPalCatalogItemDefinition item, Transform parent)
    {
        string id = item != null ? item.Id ?? string.Empty : string.Empty;
        if (parent == null || string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (id.IndexOf("ears_", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralEars(item, parent);
        }

        if (id.IndexOf("bow", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BuildProceduralCollarBow(item, parent);
        }

        return BuildProceduralCollar(item, parent);
    }

    private bool BuildWearablePreview(PawPalCatalogItemDefinition item, GameObject wearablePrefab)
    {
        GameObject dogSource = ResolveWearablePreviewDogSource();
        if (dogSource == null)
        {
            GameObject standalone = Instantiate(wearablePrefab, modelRoot, false);
            standalone.name = wearablePrefab.name;
            focusRoot = standalone.transform;
            StripPreviewOnlyComponents(standalone);
            ApplyPreviewTint(standalone, item.CollarTint);
            return true;
        }

        GameObject dog = Instantiate(dogSource, modelRoot, false);
        dog.name = dogSource.name;
        StripPreviewOnlyComponents(dog, true);
        previewDogAnimator = ConfigurePreviewDogAnimator(dog);
        RegisterPreviewAnimator(previewDogAnimator);

        Transform placementReference;
        Transform parent;
        Vector3 localPosition;
        Quaternion localRotation;
        Vector3 localScale;
        if (TryFindExistingCollarPlacementReference(dog.transform, out placementReference))
        {
            parent = placementReference.parent != null ? placementReference.parent : dog.transform;
            localPosition = placementReference.localPosition;
            localRotation = placementReference.localRotation;
            localScale = placementReference.localScale;
        }
        else
        {
            parent = ResolveFallbackCollarAnchor(dog.transform);
            localPosition = item.CollarLocalPosition;
            localRotation = item.CollarLocalRotation;
            localScale = item.CollarLocalScale;
        }

        HideBuiltInCollarSources(dog.transform);

        GameObject wearable = Instantiate(wearablePrefab, parent, false);
        wearable.name = wearablePrefab.name;
        wearable.transform.localPosition = localPosition;
        wearable.transform.localRotation = localRotation;
        wearable.transform.localScale = localScale;
        focusRoot = wearable.transform;
        StripPreviewOnlyComponents(wearable);
        ApplyPreviewTint(wearable, item.CollarTint);
        StartPreviewDogAnimation();
        return true;
    }

    private void CreateBowlHalf(Transform parent, Material material, float xOffset)
    {
        GameObject bowl = CreatePreviewPrimitive("Bowl", PrimitiveType.Cylinder, parent);
        bowl.transform.localScale = new Vector3(0.34f, 0.12f, 0.34f);
        bowl.transform.localPosition = new Vector3(xOffset, -0.04f, 0f);
        AssignPreviewMaterial(bowl, material);
    }

    private GameObject BuildProceduralCollar(PawPalCatalogItemDefinition item, Transform parent)
    {
        GameObject collar = new GameObject("ProceduralCollar");
        collar.hideFlags = HideFlags.HideAndDontSave;
        collar.transform.SetParent(parent, false);
        collar.transform.localPosition = item.CollarLocalPosition;
        collar.transform.localRotation = item.CollarLocalRotation;
        collar.transform.localScale = item.CollarLocalScale;

        Material material = CreatePreviewMaterial(ApproximatelyWhite(item.CollarTint) ? new Color32(210, 84, 90, 255) : item.CollarTint, "ProceduralCollarMaterial", 0.36f);
        GameObject ring = CreatePreviewPrimitive("Ring", PrimitiveType.Cylinder, collar.transform);
        ring.transform.localScale = new Vector3(0.22f, 0.012f, 0.22f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        AssignPreviewMaterial(ring, material);
        return collar;
    }

    private GameObject BuildProceduralCollarBow(PawPalCatalogItemDefinition item, Transform parent)
    {
        GameObject collar = BuildProceduralCollar(item, parent);
        Material bowMaterial = CreatePreviewMaterial(ApproximatelyWhite(item.CollarTint) ? new Color32(228, 116, 153, 255) : item.CollarTint, "ProceduralBowMaterial", 0.26f);
        GameObject left = CreatePreviewPrimitive("BowLeft", PrimitiveType.Cube, collar.transform);
        left.transform.localScale = new Vector3(0.08f, 0.05f, 0.05f);
        left.transform.localPosition = new Vector3(-0.045f, 0.03f, 0.08f);
        left.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
        AssignPreviewMaterial(left, bowMaterial);

        GameObject right = CreatePreviewPrimitive("BowRight", PrimitiveType.Cube, collar.transform);
        right.transform.localScale = new Vector3(0.08f, 0.05f, 0.05f);
        right.transform.localPosition = new Vector3(0.045f, 0.03f, 0.08f);
        right.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
        AssignPreviewMaterial(right, bowMaterial);
        return collar;
    }

    private GameObject BuildProceduralEars(PawPalCatalogItemDefinition item, Transform parent)
    {
        GameObject root = new GameObject("ProceduralEars");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 0.12f, 0.06f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Material material = CreatePreviewMaterial(new Color32(150, 116, 88, 255), "ProceduralEarsMaterial", 0.22f);
        GameObject left = CreatePreviewPrimitive("EarLeft", PrimitiveType.Capsule, root.transform);
        left.transform.localScale = new Vector3(0.06f, 0.12f, 0.06f);
        left.transform.localPosition = new Vector3(-0.08f, 0.1f, 0f);
        left.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        AssignPreviewMaterial(left, material);

        GameObject right = CreatePreviewPrimitive("EarRight", PrimitiveType.Capsule, root.transform);
        right.transform.localScale = new Vector3(0.06f, 0.12f, 0.06f);
        right.transform.localPosition = new Vector3(0.08f, 0.1f, 0f);
        right.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
        AssignPreviewMaterial(right, material);
        return root;
    }

    private static GameObject ResolveWearablePreviewDogSource()
    {
        PawPalRoomPetHandle activePet = PawPalRoomPetRuntime.ResolveActivePet();
        if (activePet != null
            && activePet.IsValid
            && activePet.IsDog
            && activePet.RootTransform != null)
        {
            return activePet.RootTransform.gameObject;
        }

        return Resources.Load<GameObject>(PreviewDogResourcePath);
    }

    private string ResolvePreviewPrefabPath(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(item.PreviewPrefabResourcePath))
        {
            return item.PreviewPrefabResourcePath;
        }

        if (item.PreviewMode == PawPalShopPreviewMode.WearableOnDog)
        {
            return item.CollarPrefabResourcePath;
        }

        if (!string.IsNullOrEmpty(item.RoomPrefabResourcePath))
        {
            return item.RoomPrefabResourcePath;
        }

        return item.CollarPrefabResourcePath;
    }

    private void ShowSpriteFallback(PawPalCatalogItemDefinition item)
    {
        ClearPreview();
        previewImage.enabled = false;
        previewImage.texture = null;

        string spritePath = ResolveFallbackSpritePath(item);
        fallbackImage.sprite = sprites != null ? sprites.GetResourceSprite(spritePath) : UiTheme.WhiteSprite;
        fallbackImage.enabled = true;
    }

    private Sprite ResolvePreviewSprite(PawPalCatalogItemDefinition item)
    {
        string spritePath = ResolveFallbackSpritePath(item);
        if (string.IsNullOrEmpty(spritePath))
        {
            return null;
        }

        if (sprites != null)
        {
            return sprites.GetResourceSprite(spritePath);
        }

        Sprite sprite = Resources.Load<Sprite>(spritePath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(spritePath);
        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static string ResolveFallbackSpritePath(PawPalCatalogItemDefinition item)
    {
        if (item == null)
        {
            return "UI/Figma/Shop/bubble_bone_preview";
        }

        if (!string.IsNullOrEmpty(item.GeneratedShopSpritePath) && ResourceSpriteExists(item.GeneratedShopSpritePath))
        {
            return item.GeneratedShopSpritePath;
        }

        if (!string.IsNullOrEmpty(item.PreviewSpritePath))
        {
            return item.PreviewSpritePath;
        }

        if (item.Id == "toy_bubble_bone")
        {
            return "UI/Figma/Shop/bubble_bone_preview";
        }

        return item.ShopSpritePath;
    }

    private void CreatePreviewStage()
    {
        Vector2Int textureSize = GetPreviewTextureSize();
        renderTexture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32);
        renderTexture.name = "ShopItem3DPreviewTexture";
        renderTexture.antiAliasing = 4;
        renderTexture.Create();

        stageRoot = new GameObject("ShopItem3DPreviewStage");
        stageRoot.hideFlags = HideFlags.HideAndDontSave;
        stageRoot.transform.position = new Vector3(PreviewStageOffset, -PreviewStageOffset, PreviewStageOffset);

        GameObject cameraObject = new GameObject("PreviewCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(stageRoot.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.99f, 0.97f, 0.91f, 0f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.orthographic = true;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 60f;
        previewCamera.targetTexture = renderTexture;
        previewCamera.enabled = false;

        ExcludePreviewLayerFromSceneCameras();
        CreatePreviewLight("KeyLight", new Vector3(-1.15f, 2.6f, -1.65f), 2.85f, 9f);
        CreatePreviewLight("FillLight", new Vector3(1.45f, 1.9f, -1.15f), 1.35f, 8f);
        CreatePreviewLight("RimLight", new Vector3(0.2f, 2.05f, 2.05f), 0.92f, 8f);
    }

    private Vector2Int GetPreviewTextureSize()
    {
        float width = rectTransform != null ? Mathf.Abs(rectTransform.rect.width) : 0f;
        float height = rectTransform != null ? Mathf.Abs(rectTransform.rect.height) : 0f;
        if (width <= 1f || height <= 1f)
        {
            return new Vector2Int(TargetTextureWidth, TargetTextureWidth);
        }

        float aspect = Mathf.Clamp(width / height, 0.5f, 3f);
        int textureHeight = Mathf.Clamp(Mathf.RoundToInt(TargetTextureWidth / aspect), 128, TargetTextureWidth);
        return new Vector2Int(TargetTextureWidth, textureHeight);
    }

    private void CreatePreviewLight(string lightName, Vector3 localPosition, float intensity, float range)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        lightObject.layer = PreviewLayer;
        lightObject.transform.SetParent(stageRoot.transform, false);
        lightObject.transform.localPosition = localPosition;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = intensity;
        light.range = range;
        light.cullingMask = 1 << PreviewLayer;
    }

    private bool TryFrameModel()
    {
        return TryFrameModel(StandaloneFramePadding);
    }

    private bool TryFrameModel(float standaloneFramePadding)
    {
        Bounds fullBounds;
        if (!TryGetRendererBounds(modelRoot, out fullBounds))
        {
            return false;
        }

        Vector3 pivot = modelRoot.position;
        modelRoot.position += pivot - fullBounds.center;

        if (!TryGetRendererBounds(modelRoot, out fullBounds))
        {
            return false;
        }

        Bounds focusBounds;
        if (focusRoot == null || !TryGetRendererBounds(focusRoot, out focusBounds))
        {
            focusBounds = fullBounds;
        }

        bool wearableFocus = activePreviewMode == PawPalShopPreviewMode.WearableOnDog && focusRoot != null;
        Vector3 center = wearableFocus ? focusBounds.center : fullBounds.center;
        float largestExtent = wearableFocus
            ? Mathf.Max(focusBounds.extents.x, focusBounds.extents.y, focusBounds.extents.z)
            : Mathf.Max(fullBounds.extents.x, fullBounds.extents.y, fullBounds.extents.z);

        float size = wearableFocus
            ? Mathf.Max(largestExtent * WearableFocusPadding, fullBounds.size.y * WearableDogHeightPadding)
            : largestExtent * standaloneFramePadding;

        baseOrthographicSize = Mathf.Max(0.05f, size);
        previewCamera.orthographicSize = baseOrthographicSize;

        float cameraDistance = Mathf.Clamp(baseOrthographicSize * 9f, 2.5f, 18f);
        previewCamera.transform.position = center + new Vector3(0f, baseOrthographicSize * 0.08f, -cameraDistance);
        previewCamera.transform.LookAt(center + Vector3.up * baseOrthographicSize * 0.02f);
        return true;
    }

    private Mesh CreateSpriteCardMesh(Sprite sprite)
    {
        Vector2 spriteSize = sprite != null ? sprite.bounds.size : Vector2.one;
        float width = Mathf.Max(0.01f, spriteSize.x);
        float height = Mathf.Max(0.01f, spriteSize.y);
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfDepth = Mathf.Max(SpriteCardDepth, Mathf.Min(width, height) * 0.012f) * 0.5f;

        Rect textureRect = sprite.textureRect;
        Texture texture = sprite.texture;
        float textureWidth = texture != null ? Mathf.Max(1f, texture.width) : 1f;
        float textureHeight = texture != null ? Mathf.Max(1f, texture.height) : 1f;
        float uMin = textureRect.xMin / textureWidth;
        float uMax = textureRect.xMax / textureWidth;
        float vMin = textureRect.yMin / textureHeight;
        float vMax = textureRect.yMax / textureHeight;

        Mesh mesh = new Mesh();
        mesh.name = "ShopSpritePreviewCardMesh";
        mesh.hideFlags = HideFlags.HideAndDontSave;
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, -halfHeight, -halfDepth),
            new Vector3(-halfWidth, halfHeight, -halfDepth),
            new Vector3(halfWidth, halfHeight, -halfDepth),
            new Vector3(halfWidth, -halfHeight, -halfDepth),
            new Vector3(-halfWidth, -halfHeight, halfDepth),
            new Vector3(halfWidth, -halfHeight, halfDepth),
            new Vector3(halfWidth, halfHeight, halfDepth),
            new Vector3(-halfWidth, halfHeight, halfDepth)
        };
        mesh.uv = new[]
        {
            new Vector2(uMin, vMin),
            new Vector2(uMin, vMax),
            new Vector2(uMax, vMax),
            new Vector2(uMax, vMin),
            new Vector2(uMin, vMin),
            new Vector2(uMax, vMin),
            new Vector2(uMax, vMax),
            new Vector2(uMin, vMax)
        };
        mesh.triangles = new[]
        {
            0, 1, 2,
            0, 2, 3,
            4, 5, 6,
            4, 6, 7
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private Material CreateSpritePreviewMaterial(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            return null;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.name = "ShopSpritePreviewMaterial";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.renderQueue = 3000;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", sprite.texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", sprite.texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        runtimeMaterials.Add(material);
        return material;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool HandlePinchZoom()
    {
        if (Input.touchCount < 2)
        {
            return false;
        }

        Touch first = Input.GetTouch(0);
        Touch second = Input.GetTouch(1);
        if (!IsScreenPointInside(first.position) || !IsScreenPointInside(second.position))
        {
            lastPinchDistance = 0f;
            return false;
        }

        isDragging = false;
        activePointerId = int.MinValue;

        float distance = Vector2.Distance(first.position, second.position);
        if (lastPinchDistance > 0f)
        {
            AdjustZoom((distance - lastPinchDistance) * TouchPinchZoomSensitivity);
        }

        lastPinchDistance = distance;
        return true;
    }

    private bool IsScreenPointInside(Vector2 screenPosition)
    {
        if (rectTransform == null)
        {
            return false;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
    }

    private void AdjustZoom(float delta)
    {
        zoomScale = Mathf.Clamp(zoomScale + delta, MinimumZoom, MaximumZoom);
        ApplyCameraZoom();
        RenderPreview();
    }

    private void ApplyCameraZoom()
    {
        if (previewCamera != null)
        {
            previewCamera.orthographicSize = baseOrthographicSize / Mathf.Max(0.01f, zoomScale);
        }
    }

    private void ApplyModelRotation()
    {
        if (modelRoot != null)
        {
            modelRoot.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
        }
    }

    private void RenderPreview()
    {
        if (previewCamera != null)
        {
            previewCamera.Render();
        }
    }

    private static void StripPreviewOnlyComponents(GameObject root, bool keepAnimator = false)
    {
        if (root == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        NavMeshAgent[] agents = root.GetComponentsInChildren<NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] != null)
            {
                agents[i].enabled = false;
            }
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].enabled = keepAnimator;
            }
        }

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        AudioSource[] audioSources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null)
            {
                audioSources[i].enabled = false;
            }
        }
    }

    private static Animator ConfigurePreviewDogAnimator(GameObject dog)
    {
        return ConfigurePreviewDogAnimator(dog, System.Array.Empty<string>());
    }

    private static Animator ConfigurePreviewDogAnimator(GameObject dog, params string[] identityValues)
    {
        if (dog == null)
        {
            return null;
        }

        Animator animator = dog.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return null;
        }

#if UNITY_EDITOR
        ApplyEditorPreviewOverride(animator, identityValues);
#endif
        if (animator.runtimeAnimatorController == null)
        {
            return null;
        }

        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        ResetPreviewDogParameters(animator);
        return animator;
    }

    private static void ApplyBreedPreviewPose(GameObject dog, Animator animator, string breedKey, PreviewBreedPose pose)
    {
        if (dog == null || animator == null)
        {
            return;
        }

        ResetPreviewDogParameters(animator);
        switch (pose)
        {
            case PreviewBreedPose.Sit:
                PlayPreviewPoseStateRuntime(animator, PreviewSittingStartSeconds, "SittingStart", "Sitting_start", "Sit start", "SitStart");
                PlayPreviewPoseStateRuntime(animator, PreviewLieLoopSettleSeconds, "SittingLoop1", "Sitting_loop_1", "Sit loop", "Sit");
                break;
            case PreviewBreedPose.Lie:
                PlayPreviewPoseStateRuntime(animator, PreviewLieStartSeconds, "LieBellyStart", "Lie_belly_start");
                PlayPreviewPoseStateRuntime(animator, PreviewLieLoopSettleSeconds, "LieBellyLoop", "Lie_belly_loop_1", "CatSimple_Lie_side_loop_1");
                break;
            default:
                PlayPreviewPoseStateRuntime(animator, 0.12f, "Idle2", "Idle_2", "Idle_Base");
                break;
        }
    }

    private static void PlayPreviewPoseStateRuntime(Animator animator, float settleSeconds, params string[] stateNames)
    {
        int stateHash = ResolveAnimatorStateHash(animator, stateNames);
        if (stateHash == 0)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, PreviewAnimationCrossFadeSeconds, AnimatorBaseLayerIndex, 0f);
        animator.Update(Mathf.Max(0.01f, settleSeconds));
    }

    private void StartPreviewDogAnimation()
    {
        if (previewDogAnimator == null)
        {
            previewDogAnimationPending = false;
            return;
        }

        if (previewDogAnimationRoutine != null)
        {
            StopCoroutine(previewDogAnimationRoutine);
            previewDogAnimationRoutine = null;
        }

        if (!isActiveAndEnabled)
        {
            previewDogAnimationPending = true;
            return;
        }

        previewDogAnimationPending = false;
        previewDogAnimationRoutine = StartCoroutine(PlayPreviewDogAnimationLoop());
    }

    private void StopPreviewDogAnimation(bool restartWhenEnabled = false)
    {
        previewDogAnimationPending = restartWhenEnabled && previewDogAnimator != null;
        if (previewDogAnimationRoutine == null)
        {
            return;
        }

        StopCoroutine(previewDogAnimationRoutine);
        previewDogAnimationRoutine = null;
    }

    private IEnumerator PlayPreviewDogAnimationLoop()
    {
        yield return null;

        while (previewDogAnimator != null && stageRoot != null)
        {
            yield return PlayPreviewDogState(PreviewIdleSeconds, "Idle2", "Idle_2", "Arm_Labrador|Idle_2", "Idle_Base");
            yield return PlayPreviewDogState(PreviewSittingStartSeconds, "SittingStart", "Sitting_start", "Sit start", "SitStart", "Arm_Labrador|Sitting_start");
            yield return PlayPreviewDogState(PreviewSittingLoopSeconds, "SittingLoop1", "Sitting_loop_1", "Sit loop", "Sit", "Arm_Labrador|Sitting_loop_1");
            yield return PlayPreviewDogState(PreviewSittingEndSeconds, "SittingEnd", "Sitting_end", "Sit end", "SitEnd", "Arm_Labrador|Sitting_end");
            yield return PlayPreviewDogState(PreviewIdleSeconds, "Idle7", "Idle_7", "Arm_Labrador|Idle_7", "Idle_Base");
        }

        previewDogAnimationRoutine = null;
    }

    private IEnumerator PlayPreviewDogState(float duration, params string[] stateNames)
    {
        if (previewDogAnimator == null || stateNames == null || stateNames.Length == 0)
        {
            yield break;
        }

        int stateHash = ResolveAnimatorStateHash(previewDogAnimator, stateNames);
        if (stateHash == 0)
        {
            yield return null;
            yield break;
        }

        ResetPreviewDogParameters(previewDogAnimator);
        previewDogAnimator.CrossFadeInFixedTime(stateHash, PreviewAnimationCrossFadeSeconds, AnimatorBaseLayerIndex, 0f);

        float elapsed = 0f;
        float targetDuration = Mathf.Max(0.05f, duration);
        while (previewDogAnimator != null && elapsed < targetDuration)
        {
            RenderPreview();
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static int ResolveAnimatorStateHash(Animator animator, params string[] stateNames)
    {
        if (animator == null || stateNames == null || animator.layerCount <= AnimatorBaseLayerIndex)
        {
            return 0;
        }

        string layerName = animator.GetLayerName(AnimatorBaseLayerIndex);
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrEmpty(stateName))
            {
                continue;
            }

            int stateHash = Animator.StringToHash(stateName);
            if (animator.HasState(AnimatorBaseLayerIndex, stateHash))
            {
                return stateHash;
            }

            string[] candidates =
            {
                layerName + "." + stateName,
                "Base Layer." + stateName,
                layerName + ".IdleSM." + stateName,
                "Base Layer.IdleSM." + stateName,
                layerName + ".IdleSM.SitSM." + stateName,
                "Base Layer.IdleSM.SitSM." + stateName,
                layerName + ".IdleSM.LieSM." + stateName,
                "Base Layer.IdleSM.LieSM." + stateName
            };

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                stateHash = Animator.StringToHash(candidates[candidateIndex]);
                if (animator.HasState(AnimatorBaseLayerIndex, stateHash))
                {
                    return stateHash;
                }
            }
        }

        return 0;
    }

    private static void ResetPreviewDogParameters(Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        SetAnimatorBoolIfExists(animator, MoveHash, false);
        SetAnimatorFloatIfExists(animator, SpeedHash, 0f);
        SetAnimatorFloatIfExists(animator, DirectionHash, 0f);
        SetAnimatorIntegerIfExists(animator, IdleIndexHash, PreviewNeutralIdleIndex);
        ResetAnimatorTriggerIfExists(animator, SitEndTriggerHash);
    }

    private static void SetAnimatorBoolIfExists(Animator animator, int parameterHash, bool value)
    {
        if (HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterHash, value);
        }
    }

    private static void SetAnimatorFloatIfExists(Animator animator, int parameterHash, float value)
    {
        if (HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(parameterHash, value);
        }
    }

    private static void SetAnimatorIntegerIfExists(Animator animator, int parameterHash, int value)
    {
        if (HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(parameterHash, value);
        }
    }

    private static void ResetAnimatorTriggerIfExists(Animator animator, int parameterHash)
    {
        if (HasAnimatorParameter(animator, parameterHash, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(parameterHash);
        }
    }

    private static bool HasAnimatorParameter(Animator animator, int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasActivePreviewAnimator()
    {
        for (int i = previewAnimators.Count - 1; i >= 0; i--)
        {
            Animator animator = previewAnimators[i];
            if (animator == null)
            {
                previewAnimators.RemoveAt(i);
                continue;
            }

            if (animator.enabled)
            {
                return true;
            }
        }

        return false;
    }

    private void RegisterPreviewAnimator(Animator animator)
    {
        if (animator != null && !previewAnimators.Contains(animator))
        {
            previewAnimators.Add(animator);
        }
    }

    private static void WarnMissingBreedPreview(PawPalCatalogItemDefinition item, string reason)
    {
        string itemId = item != null ? item.Id : "<null>";
        string key = itemId + "|" + reason;
        if (MissingBreedPreviewWarnings.Add(key))
        {
            Debug.LogWarning("ShopItem3DPreviewView: Falling back to sprite preview for '" + itemId + "' because " + reason + ".");
        }
    }

#if UNITY_EDITOR
    private static void ApplyEditorPreviewOverride(Animator animator, params string[] identityValues)
    {
        if (animator == null)
        {
            return;
        }

        PawPalPetAnimationEntry entry;
        if (!PawPalPetAnimationRegistry.TryResolveEntryFromRawValues(out entry, identityValues) || entry == null)
        {
            return;
        }

        RuntimeAnimatorController baseController = PawPalPetAnimationRegistry.ResolveEditorBaseController(entry);
        RuntimeAnimatorController overrideController = PawPalPetAnimationRegistry.ResolveEditorOverrideController(baseController, entry);
        if (overrideController != null)
        {
            animator.runtimeAnimatorController = overrideController;
        }
        else if (baseController != null)
        {
            animator.runtimeAnimatorController = baseController;
        }
    }

    private bool TryPlayLoopingBreedPreviewPose(GameObject dog, Animator animator, string breedKey, PreviewBreedPose pose)
    {
        PawPalPetAnimationEntry entry;
        if (dog == null
            || animator == null
            || !PawPalPetAnimationRegistry.TryResolveEntryFromRawValues(out entry, breedKey, dog.name, animator.name)
            || entry == null)
        {
            return false;
        }

        Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
        AnimationClip clip;
        float normalizedStartTime;
        if (!TryResolveBreedPreviewLoopClip(clips, pose, out clip, out normalizedStartTime) || clip == null)
        {
            return false;
        }

        PlayableGraph graph = PlayableGraph.Create("ShopBreedPreview_" + entry.CanonicalKey + "_" + pose);
        graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "BreedPreviewOutput", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetTime(Mathf.Clamp01(normalizedStartTime) * Mathf.Max(0.01f, clip.length));
        output.SetSourcePlayable(playable);
        graph.Play();

        previewPlayableGraphs.Add(graph);
        animator.enabled = true;
        return true;
    }

    private static bool TryResolveBreedPreviewLoopClip(Dictionary<string, AnimationClip> clips, PreviewBreedPose pose, out AnimationClip clip, out float normalizedStartTime)
    {
        clip = null;
        normalizedStartTime = 0f;
        if (clips == null)
        {
            return false;
        }

        switch (pose)
        {
            case PreviewBreedPose.Lie:
                normalizedStartTime = 0.35f;
                return TryGetImportedPreviewClip(clips, "Lie_belly_loop_1", out clip);
            case PreviewBreedPose.Sit:
                normalizedStartTime = 0.35f;
                return TryGetImportedPreviewClip(clips, "Sitting_loop_1", out clip);
            default:
                normalizedStartTime = 0.2f;
                return TryGetImportedPreviewClip(clips, "Idle_2", out clip)
                    || TryGetImportedPreviewClip(clips, "Idle_1", out clip);
        }
    }

    private static void SettleBreedPreviewPose(GameObject dog, Animator animator, string breedKey, PreviewBreedPose pose)
    {
        if (dog == null || animator == null)
        {
            return;
        }

        if (TrySampleBreedPreviewPose(dog, animator, breedKey, pose))
        {
            return;
        }

        ResetPreviewDogParameters(animator);
        switch (pose)
        {
            case PreviewBreedPose.Sit:
                PlayPreviewPoseState(animator, PreviewSittingStartSeconds, "SittingStart", "Sitting_start", "Sit start", "SitStart");
                PlayPreviewPoseState(animator, PreviewLieLoopSettleSeconds, "SittingLoop1", "Sitting_loop_1", "Sit loop", "Sit");
                break;
            case PreviewBreedPose.Lie:
                PlayPreviewPoseState(animator, PreviewLieStartSeconds, "LieBellyStart", "Lie_belly_start");
                PlayPreviewPoseState(animator, PreviewLieLoopSettleSeconds, "LieBellyLoop", "Lie_belly_loop_1", "CatSimple_Lie_side_loop_1");
                break;
            default:
                PlayPreviewPoseState(animator, 0.1f, "Idle2", "Idle_2", "Idle_Base");
                break;
        }
    }

    private static bool TrySampleBreedPreviewPose(GameObject dog, Animator animator, string breedKey, PreviewBreedPose pose)
    {
        PawPalPetAnimationEntry entry;
        if (dog == null
            || animator == null
            || !PawPalPetAnimationRegistry.TryResolveEntryFromRawValues(out entry, breedKey, dog.name, animator.name)
            || entry == null)
        {
            return false;
        }

        Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
        if (clips == null || clips.Count == 0)
        {
            return false;
        }

        string clipSuffix;
        float normalizedTime;
        switch (pose)
        {
            case PreviewBreedPose.Lie:
                clipSuffix = "Lie_belly_loop_1";
                normalizedTime = 0.35f;
                break;
            case PreviewBreedPose.Sit:
                clipSuffix = "Sitting_loop_1";
                normalizedTime = 0.35f;
                break;
            default:
                clipSuffix = "Idle_2";
                normalizedTime = 0.2f;
                break;
        }

        AnimationClip clip;
        if (!TryGetImportedPreviewClip(clips, clipSuffix, out clip) || clip == null)
        {
            return false;
        }

        clip.SampleAnimation(dog, Mathf.Clamp(normalizedTime, 0f, 1f) * Mathf.Max(0.01f, clip.length));
        animator.enabled = false;
        return true;
    }

    private static bool TryGetImportedPreviewClip(Dictionary<string, AnimationClip> clips, string suffix, out AnimationClip clip)
    {
        clip = null;
        if (clips == null || string.IsNullOrWhiteSpace(suffix))
        {
            return false;
        }

        string canonicalSuffix = suffix.Trim().ToLowerInvariant();
        if (clips.TryGetValue(canonicalSuffix, out clip) && clip != null)
        {
            return true;
        }

        foreach (KeyValuePair<string, AnimationClip> pair in clips)
        {
            if (pair.Value == null)
            {
                continue;
            }

            if (pair.Key.EndsWith("|" + suffix, System.StringComparison.OrdinalIgnoreCase)
                || pair.Key.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            {
                clip = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static void PlayPreviewPoseState(Animator animator, float settleSeconds, params string[] stateNames)
    {
        int stateHash = ResolveAnimatorStateHash(animator, stateNames);
        if (stateHash == 0)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, PreviewAnimationCrossFadeSeconds, AnimatorBaseLayerIndex, 0f);
        animator.Update(Mathf.Max(0.01f, settleSeconds));
    }
#endif

    private void ApplyPreviewTint(GameObject instance, Color tint)
    {
        if (instance == null || ApproximatelyWhite(tint))
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            bool rendererChanged = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                bool materialChanged = false;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", tint);
                    materialChanged = true;
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", tint);
                    materialChanged = true;
                }

                if (materialChanged)
                {
                    rendererChanged = true;
                }

                if (materialChanged && !runtimeMaterials.Contains(material))
                {
                    runtimeMaterials.Add(material);
                }
            }

            if (rendererChanged)
            {
                renderer.materials = materials;
            }
        }
    }

    private static bool ApproximatelyWhite(Color tint)
    {
        return Mathf.Abs(tint.r - 1f) < 0.0001f
            && Mathf.Abs(tint.g - 1f) < 0.0001f
            && Mathf.Abs(tint.b - 1f) < 0.0001f
            && Mathf.Abs(tint.a - 1f) < 0.0001f;
    }

    private static bool TryFindExistingCollarPlacementReference(Transform root, out Transform placementReference)
    {
        placementReference = null;
        if (root == null)
        {
            return false;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            for (int j = 0; j < PreferredCollarPlacementReferenceNames.Length; j++)
            {
                if (string.Equals(candidate.name, PreferredCollarPlacementReferenceNames[j], System.StringComparison.OrdinalIgnoreCase))
                {
                    placementReference = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static Transform ResolveFallbackCollarAnchor(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform containsMatch = null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            for (int j = 0; j < PreferredCollarAnchorNames.Length; j++)
            {
                string preferredName = PreferredCollarAnchorNames[j];
                if (string.Equals(candidate.name, preferredName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                if (containsMatch == null && candidate.name.IndexOf(preferredName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    containsMatch = candidate;
                }
            }
        }

        return containsMatch != null ? containsMatch : root;
    }

    private static void HideBuiltInCollarSources(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform candidate = children[i];
            if (candidate == null)
            {
                continue;
            }

            for (int j = 0; j < PreferredCollarPlacementReferenceNames.Length; j++)
            {
                if (string.Equals(candidate.name, PreferredCollarPlacementReferenceNames[j], System.StringComparison.OrdinalIgnoreCase))
                {
                    candidate.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void ExcludePreviewLayerFromSceneCameras()
    {
        sceneCamerasWithPreviewLayer.Clear();
        int previewMask = 1 << PreviewLayer;
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera sceneCamera = cameras[i];
            if (sceneCamera == null || sceneCamera == previewCamera || (sceneCamera.cullingMask & previewMask) == 0)
            {
                continue;
            }

            sceneCamerasWithPreviewLayer.Add(sceneCamera);
            sceneCamera.cullingMask &= ~previewMask;
        }
    }

    private void RestorePreviewLayerOnSceneCameras()
    {
        int previewMask = 1 << PreviewLayer;
        for (int i = 0; i < sceneCamerasWithPreviewLayer.Count; i++)
        {
            Camera sceneCamera = sceneCamerasWithPreviewLayer[i];
            if (sceneCamera != null)
            {
                sceneCamera.cullingMask |= previewMask;
            }
        }

        sceneCamerasWithPreviewLayer.Clear();
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

    private void ClearPreview()
    {
        isDragging = false;
        activePointerId = int.MinValue;
        lastPinchDistance = 0f;
        RestorePreviewLayerOnSceneCameras();
        StopPreviewDogAnimation(false);
        for (int i = 0; i < previewPlayableGraphs.Count; i++)
        {
            if (previewPlayableGraphs[i].IsValid())
            {
                previewPlayableGraphs[i].Destroy();
            }
        }

        previewPlayableGraphs.Clear();

        previewDogAnimator = null;
        previewAnimators.Clear();
        modelRoot = null;
        focusRoot = null;
        previewCamera = null;
        activePreviewMode = PawPalShopPreviewMode.SpriteOnly;
        baseOrthographicSize = 1f;
        zoomScale = 1f;

        if (previewImage != null)
        {
            previewImage.texture = null;
            previewImage.enabled = false;
        }

        if (fallbackImage != null)
        {
            fallbackImage.enabled = false;
        }

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                Destroy(runtimeMaterials[i]);
            }
        }

        runtimeMaterials.Clear();

        for (int i = 0; i < runtimeMeshes.Count; i++)
        {
            if (runtimeMeshes[i] != null)
            {
                Destroy(runtimeMeshes[i]);
            }
        }

        runtimeMeshes.Clear();

        if (stageRoot != null)
        {
            Destroy(stageRoot);
            stageRoot = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }

    private enum PreviewBreedPose
    {
        Stand,
        Sit,
        Lie
    }
}
