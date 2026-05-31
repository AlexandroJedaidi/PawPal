using UnityEngine;

public enum IntroPetSpecies
{
    Dog,
    Cat
}

[CreateAssetMenu(menuName = "PawPal/Intro/Pet Definition", fileName = "IntroPet")]
public sealed class IntroPetDefinition : ScriptableObject
{
    public string PetId = "pet";
    public string DisplayName = "Pet";
    public IntroPetSpecies Species;
    public string BreedName = "Mixed breed";
    [TextArea(2, 4)] public string Description = "A friendly companion.";
    public GameObject BasePrefab;
    public Vector3 IntroScale = Vector3.one;
    public Vector3 HomeScale = Vector3.one;
    public Vector3 IntroCameraOffset = new Vector3(0f, 1.25f, -3f);
    public Vector3 HomeCameraOffset = new Vector3(0f, 1.1f, -3.1f);
    public float RoamRadius = 3.4f;
    public PetAnimationSet AnimationSet;
    public FurVariantDefinition[] FurVariants = new FurVariantDefinition[0];

    public string SpeciesLabel
    {
        get { return Species == IntroPetSpecies.Cat ? "Cat" : "Dog"; }
    }

    public string BreedLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(BreedName))
            {
                return BreedName;
            }

            return !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : SpeciesLabel;
        }
    }

    public FurVariantDefinition GetDefaultFurVariant()
    {
        if (FurVariants == null || FurVariants.Length == 0)
        {
            return null;
        }

        return FurVariants[0];
    }

    public FurVariantDefinition GetFurVariant(int index)
    {
        if (FurVariants == null || FurVariants.Length == 0)
        {
            return null;
        }

        int safeIndex = Mathf.Abs(index) % FurVariants.Length;
        return FurVariants[safeIndex];
    }

    public int GetFurVariantIndex(FurVariantDefinition variant)
    {
        if (variant == null || FurVariants == null)
        {
            return 0;
        }

        for (int i = 0; i < FurVariants.Length; i++)
        {
            if (FurVariants[i] == variant)
            {
                return i;
            }
        }

        return 0;
    }
}
