using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PawPalLeashEditModeTests
{
    [Test]
    public void LeashSocketResolvesExplicitSocketChild()
    {
        GameObject root = new GameObject("Collar");
        GameObject child = new GameObject("LeashSocket");
        child.transform.SetParent(root.transform, false);

        PawPalLeashSocket leashSocket = root.AddComponent<PawPalLeashSocket>();

        Assert.AreEqual(child.transform, leashSocket.Socket);

        Object.DestroyImmediate(root);
    }

    [Test]
    public void DogSceneBridgeReturnsSocketFromRuntimeCollarInstance()
    {
        GameObject bridgeObject = new GameObject("Bridge");
        PawPalDogSceneBridge bridge = bridgeObject.AddComponent<PawPalDogSceneBridge>();

        GameObject collar = new GameObject("RuntimeCollar");
        GameObject socketChild = new GameObject("LeashSocket");
        socketChild.transform.SetParent(collar.transform, false);
        PawPalLeashSocket leashSocket = collar.AddComponent<PawPalLeashSocket>();
        leashSocket.SetSocket(socketChild.transform);

        SetRuntimeCollarInstance(bridge, "dog-1", collar);

        Assert.IsTrue(bridge.TryGetLeashSocket("dog-1", out Transform resolvedSocket));
        Assert.AreEqual(socketChild.transform, resolvedSocket);

        Object.DestroyImmediate(collar);
        Object.DestroyImmediate(bridgeObject);
    }

    [Test]
    public void DogSceneBridgeFallsBackToCollarRootWhenSocketComponentMissing()
    {
        GameObject bridgeObject = new GameObject("Bridge");
        PawPalDogSceneBridge bridge = bridgeObject.AddComponent<PawPalDogSceneBridge>();
        GameObject collar = new GameObject("RuntimeCollar");

        SetRuntimeCollarInstance(bridge, "dog-2", collar);
        LogAssert.Expect(LogType.Warning, "PawPalDogSceneBridge: Runtime collar instance for dog 'dog-2' has no PawPalLeashSocket. Falling back to the collar root transform.");

        Assert.IsTrue(bridge.TryGetLeashSocket("dog-2", out Transform resolvedSocket));
        Assert.AreEqual(collar.transform, resolvedSocket);

        Object.DestroyImmediate(collar);
        Object.DestroyImmediate(bridgeObject);
    }

    private static void SetRuntimeCollarInstance(PawPalDogSceneBridge bridge, string dogId, GameObject collar)
    {
        FieldInfo field = typeof(PawPalDogSceneBridge).GetField(
            "collarInstancesByDogId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        Dictionary<string, GameObject> instances =
            field.GetValue(bridge) as Dictionary<string, GameObject>;
        Assert.NotNull(instances);

        instances[dogId] = collar;
    }
}
