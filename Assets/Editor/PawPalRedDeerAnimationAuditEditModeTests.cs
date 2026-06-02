using System.Collections.Generic;
using NUnit.Framework;

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
}
