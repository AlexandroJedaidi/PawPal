using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
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
    private const float WearableFocusPadding = 2.8f;
    private const float WearableDogHeightPadding = 0.18f;
    private const int AnimatorBaseLayerIndex = 0;
    private const float PreviewAnimationCrossFadeSeconds = 0.16f;
    private const float PreviewIdleSeconds = 2.2f;
    private const float PreviewSittingStartSeconds = 0.75f;
    private const float PreviewSittingLoopSeconds = 2.15f;
    private const float PreviewSittingEndSeconds = 0.75f;
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
    private readonly List<Camera> sceneCamerasWithPreviewLayer = new List<Camera>();
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

        if (item == null || item.PreviewMode == PawPalShopPreviewMode.SpriteOnly || !TryBuildPreview(item))
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
        if (previewDogAnimator != null && previewDogAnimator.enabled)
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

    private bool BuildWearablePreview(PawPalCatalogItemDefinition item, GameObject wearablePrefab)
    {
        GameObject dogPrefab = Resources.Load<GameObject>(PreviewDogResourcePath);
        if (dogPrefab == null)
        {
            GameObject standalone = Instantiate(wearablePrefab, modelRoot, false);
            standalone.name = wearablePrefab.name;
            focusRoot = standalone.transform;
            StripPreviewOnlyComponents(standalone);
            ApplyPreviewTint(standalone, item.CollarTint);
            return true;
        }

        GameObject dog = Instantiate(dogPrefab, modelRoot, false);
        dog.name = dogPrefab.name;
        StripPreviewOnlyComponents(dog, true);
        previewDogAnimator = ConfigurePreviewDogAnimator(dog);

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
        CreatePreviewLight("KeyLight", new Vector3(-1.8f, 2.2f, -2.2f), 2.4f, 8f);
        CreatePreviewLight("FillLight", new Vector3(2.2f, 1.4f, -1.2f), 0.9f, 7f);
        CreatePreviewLight("RimLight", new Vector3(0f, 1.8f, 2.4f), 0.75f, 7f);
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
            : largestExtent * StandaloneFramePadding;

        baseOrthographicSize = Mathf.Max(0.05f, size);
        previewCamera.orthographicSize = baseOrthographicSize;

        float cameraDistance = Mathf.Clamp(baseOrthographicSize * 9f, 2.5f, 18f);
        previewCamera.transform.position = center + new Vector3(0f, baseOrthographicSize * 0.08f, -cameraDistance);
        previewCamera.transform.LookAt(center + Vector3.up * baseOrthographicSize * 0.02f);
        return true;
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
        if (dog == null)
        {
            return null;
        }

        Animator animator = dog.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
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
                "Base Layer.IdleSM.SitSM." + stateName
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

        previewDogAnimator = null;
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
}
