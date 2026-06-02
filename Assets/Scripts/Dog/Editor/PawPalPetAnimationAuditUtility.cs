using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PawPalPetAnimationAuditUtility
{
    private const string MenuPath = "PawFriends/Dogs/Audit Red Deer Pet Animation Wiring";

    public struct AuditIssue
    {
        public string CanonicalKey;
        public string Message;
    }

    [MenuItem(MenuPath)]
    public static void AuditFromMenu()
    {
        List<AuditIssue> issues = RunAudit();
        if (issues.Count == 0)
        {
            Debug.Log("PawPalPetAnimationAuditUtility: audit passed for all Red Deer registry entries.");
            return;
        }

        for (int i = 0; i < issues.Count; i++)
        {
            Debug.LogError("PawPalPetAnimationAuditUtility [" + issues[i].CanonicalKey + "]: " + issues[i].Message);
        }
    }

    public static List<AuditIssue> RunAudit()
    {
        List<AuditIssue> issues = new List<AuditIssue>();
        HashSet<string> canonicalKeys = new HashSet<string>();

        foreach (PawPalPetAnimationEntry entry in PawPalPetAnimationRegistry.GetAllEntries())
        {
            if (entry == null)
            {
                continue;
            }

            if (!canonicalKeys.Add(entry.CanonicalKey))
            {
                issues.Add(new AuditIssue { CanonicalKey = entry.CanonicalKey, Message = "Duplicate canonical key." });
            }

            if (!PawPalPetAnimationRegistry.HasEditorControllerSource(entry))
            {
                issues.Add(new AuditIssue { CanonicalKey = entry.CanonicalKey, Message = "Missing runtime controller source." });
            }

            if (!PawPalPetAnimationRegistry.HasEditorAnimationSource(entry))
            {
                issues.Add(new AuditIssue { CanonicalKey = entry.CanonicalKey, Message = "Missing imported animation asset." });
                continue;
            }

            string missingClipSuffix;
            if (!PawPalPetAnimationRegistry.HasRequiredGameplayClips(entry, out missingClipSuffix))
            {
                issues.Add(new AuditIssue
                {
                    CanonicalKey = entry.CanonicalKey,
                    Message = "Missing required gameplay clip '" + missingClipSuffix + "'."
                });
            }

            if (entry.CanonicalKey == "puppy_labrador" && string.IsNullOrWhiteSpace(entry.PettingAnimationAssetPath))
            {
                issues.Add(new AuditIssue
                {
                    CanonicalKey = entry.CanonicalKey,
                    Message = "Missing puppy petting animation path."
                });
            }
        }

        return issues;
    }
}
