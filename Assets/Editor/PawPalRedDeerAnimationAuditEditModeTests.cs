using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PawPalRedDeerAnimationAuditEditModeTests
{
    [Test]
    public void RedDeerAnimationRegistryAuditHasNoIssues()
    {
        List<PawPalPetAnimationAuditUtility.AuditIssue> issues = PawPalPetAnimationAuditUtility.RunAudit();
        if (issues.Count == 0)
        {
            Assert.Pass();
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < issues.Count; i++)
        {
            builder.AppendLine(issues[i].CanonicalKey + ": " + issues[i].Message);
        }

        Assert.Fail(builder.ToString());
    }

    [Test]
    public void RedDeerRegistryProvidesForwardLocomotionClipsForEveryPace()
    {
        MethodInfo suffixMethod = typeof(DogRoomAgent).GetMethod(
            "GetImportedForwardLocomotionClipSuffixes",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(suffixMethod);

        DogMovementPace[] paces =
        {
            DogMovementPace.Walk,
            DogMovementPace.Trot,
            DogMovementPace.Run
        };

        foreach (PawPalPetAnimationEntry entry in PawPalPetAnimationRegistry.GetAllEntries())
        {
            Assert.NotNull(entry);
            Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
            Assert.NotNull(clips, entry.CanonicalKey);
            Assert.IsNotEmpty(clips, entry.CanonicalKey);

            for (int paceIndex = 0; paceIndex < paces.Length; paceIndex++)
            {
                DogMovementPace pace = paces[paceIndex];
                string[] suffixes = suffixMethod.Invoke(null, new object[] { pace }) as string[];
                Assert.NotNull(suffixes, entry.CanonicalKey + " " + pace);
                Assert.IsTrue(
                    ContainsAnyClipSuffix(clips, suffixes),
                    entry.CanonicalKey + " is missing a forward " + pace + " clip.");
            }
        }
    }

    [Test]
    public void RedDeerRegistryProvidesTurnClipsForEveryPet()
    {
        PawPalPetTurnClipKind[] turnKinds =
        {
            PawPalPetTurnClipKind.Left,
            PawPalPetTurnClipKind.Right,
            PawPalPetTurnClipKind.Left180,
            PawPalPetTurnClipKind.Right180
        };

        foreach (PawPalPetAnimationEntry entry in PawPalPetAnimationRegistry.GetAllEntries())
        {
            Assert.NotNull(entry);
            Dictionary<string, AnimationClip> clips = PawPalPetAnimationRegistry.GetEditorImportedClips(entry);
            Assert.NotNull(clips, entry.CanonicalKey);
            Assert.IsNotEmpty(clips, entry.CanonicalKey);

            for (int turnIndex = 0; turnIndex < turnKinds.Length; turnIndex++)
            {
                PawPalPetTurnClipKind turnKind = turnKinds[turnIndex];
                string[] suffixes = PawPalPetTurnAnimationUtility.GetTurnClipSuffixes(entry, turnKind);
                Assert.IsTrue(
                    ContainsAnyClipSuffix(clips, suffixes),
                    entry.CanonicalKey + " is missing a " + turnKind + " turn clip.");
            }
        }
    }

    [TestCase(4f, PawPalPetTurnClipKind.None)]
    [TestCase(35f, PawPalPetTurnClipKind.Right)]
    [TestCase(-35f, PawPalPetTurnClipKind.Left)]
    [TestCase(150f, PawPalPetTurnClipKind.Right180)]
    [TestCase(-150f, PawPalPetTurnClipKind.Left180)]
    public void TurnSelectionUsesSignedAngleAndHalfTurnThreshold(float signedAngle, PawPalPetTurnClipKind expectedKind)
    {
        PawPalPetTurnClipKind kind = PawPalPetTurnAnimationUtility.ResolveTurnClipKind(
            signedAngle,
            8f,
            135f);

        Assert.AreEqual(expectedKind, kind);
    }

    private static bool ContainsAnyClipSuffix(Dictionary<string, AnimationClip> clips, string[] suffixes)
    {
        if (clips == null || suffixes == null)
        {
            return false;
        }

        for (int suffixIndex = 0; suffixIndex < suffixes.Length; suffixIndex++)
        {
            string suffix = suffixes[suffixIndex];
            if (string.IsNullOrEmpty(suffix))
            {
                continue;
            }

            foreach (KeyValuePair<string, AnimationClip> clip in clips)
            {
                if (clip.Value == null || string.IsNullOrEmpty(clip.Key))
                {
                    continue;
                }

                if (clip.Key.EndsWith("|" + suffix, System.StringComparison.OrdinalIgnoreCase)
                    || clip.Key.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
