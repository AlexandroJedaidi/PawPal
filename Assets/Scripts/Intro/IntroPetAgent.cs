using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class IntroPetAgent : MonoBehaviour
{
    private IntroPetSelectionController controller;
    private IntroPetDefinition definition;
    private FurVariantDefinition activeVariant;
    private GameObject sourcePrefab;
    private PawPalRoomPetHandle roomPet;
    private bool selected;
    private bool warnedMissingVariantMaterial;
    private bool warnedMissingSelectedAnimation;

    public int SelectionIndex { get; private set; }

    public IntroPetDefinition Definition
    {
        get { return definition; }
    }

    public FurVariantDefinition ActiveVariant
    {
        get { return activeVariant; }
    }

    public GameObject SourcePrefab
    {
        get { return sourcePrefab; }
    }

    public Transform FocusTransform
    {
        get
        {
            return roomPet != null && roomPet.IsValid && roomPet.FocusTransform != null
                ? roomPet.FocusTransform
                : transform;
        }
    }

    public void Initialize(
        IntroPetSelectionController owner,
        IntroPetDefinition petDefinition,
        FurVariantDefinition variant,
        GameObject prefab,
        int index,
        PawPalRoomPetHandle runtimePet)
    {
        controller = owner;
        definition = petDefinition;
        activeVariant = variant;
        sourcePrefab = prefab;
        roomPet = runtimePet;
        SelectionIndex = index;

        if (variant != null && variant.ReplacementMaterial != null && !PetVariantApplier.ApplyMaterial(gameObject, variant))
        {
            WarnMissingVariantMaterial();
        }

        PetVariantApplier.EnsureTapCollider(gameObject);
    }

    public void SetSelectionIndex(int index)
    {
        SelectionIndex = index;
    }

    public void SetSelected(bool isSelected, Camera camera)
    {
        selected = isSelected;
        if (!selected)
        {
            if (roomPet != null && roomPet.IsValid)
            {
                roomPet.StartRoaming();
            }

            return;
        }

        if (roomPet != null && roomPet.IsValid)
        {
            roomPet.PrepareForPlayerInteraction(true);
            roomPet.PauseForSocial(false);
            StartCoroutine(roomPet.FaceTarget(camera != null ? camera.transform : null, 0.24f));
        }

        PlaySelectedReaction();
    }

    public void ApplyFurVariant(FurVariantDefinition variant)
    {
        activeVariant = variant;
        if (variant != null && variant.ReplacementMaterial != null && !PetVariantApplier.ApplyMaterial(gameObject, variant))
        {
            WarnMissingVariantMaterial();
        }
    }

    private void OnMouseDown()
    {
        if (IsPointerOverUi())
        {
            return;
        }

        if (controller != null)
        {
            controller.SelectAgent(this);
        }
    }

    private void PlaySelectedReaction()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (definition != null && definition.AnimationSet != null && definition.AnimationSet.TryPlaySelectedReaction(animator))
        {
            return;
        }

        if (!warnedMissingSelectedAnimation)
        {
            warnedMissingSelectedAnimation = true;
            Debug.LogWarning("IntroPetSelection could not find a selected reaction animation for " + (definition != null ? definition.DisplayName : name) + ".");
        }
    }

    private void WarnMissingVariantMaterial()
    {
        if (warnedMissingVariantMaterial)
        {
            return;
        }

        warnedMissingVariantMaterial = true;
        Debug.LogWarning("IntroPetSelection could not apply fur material '" + activeVariant.SafeDisplayName + "' to " + (definition != null ? definition.DisplayName : name) + ".");
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            return eventSystem.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }

        return eventSystem.IsPointerOverGameObject();
    }
}
