using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;

public sealed class PawPalGameRuntimeEditModeTests
{
    [Test]
    public void GetCatalogItemReturnsNullForNullOrWhitespaceKeys()
    {
        PawPalGameRuntime runtime = CreateRuntimeForManagedTests();

        Assert.IsNull(runtime.GetCatalogItem(null));
        Assert.IsNull(runtime.GetCatalogItem(string.Empty));
        Assert.IsNull(runtime.GetCatalogItem("   "));
    }

    [Test]
    public void GetEquippedCollarItemIdNormalizesNullSavedValueToEmptyString()
    {
        PawPalGameRuntime runtime = CreateRuntimeForManagedTests();
        Dictionary<string, PawPalDogEquipmentState> dogEquipmentByDogId =
            GetField<Dictionary<string, PawPalDogEquipmentState>>(runtime, "dogEquipmentByDogId");

        dogEquipmentByDogId["pepper"] = new PawPalDogEquipmentState
        {
            DogId = "pepper",
            EquippedCollarItemId = null
        };

        Assert.AreEqual(string.Empty, runtime.GetEquippedCollarItemId("pepper"));
    }

    private static PawPalGameRuntime CreateRuntimeForManagedTests()
    {
        PawPalGameRuntime runtime =
            (PawPalGameRuntime)FormatterServices.GetUninitializedObject(typeof(PawPalGameRuntime));

        SetField(runtime, "catalogItems", new List<PawPalCatalogItemDefinition>());
        SetField(runtime, "catalogById",
            new Dictionary<string, PawPalCatalogItemDefinition>(System.StringComparer.Ordinal));
        SetField(runtime, "ownedItemById",
            new Dictionary<string, PawPalOwnedItemState>(System.StringComparer.Ordinal));
        SetField(runtime, "dogEquipmentByDogId",
            new Dictionary<string, PawPalDogEquipmentState>(System.StringComparer.Ordinal));

        return runtime;
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = typeof(PawPalGameRuntime).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(PawPalGameRuntime).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }
}
