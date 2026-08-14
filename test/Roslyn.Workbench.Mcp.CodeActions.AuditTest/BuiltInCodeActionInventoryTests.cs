using System.Security.Cryptography;
using System.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionInventoryTests
{
    private const string _expectedProviderIdentitySha256 = "215A5BE5A0E994F1288BFE1090C1F43142D39F60A5A6B4FB80C68D5F6092B385";

    [Fact]
    public void GIVEN_PinnedBuiltInComposition_WHEN_InspectingProviders_THEN_ShouldRetainExactProviderCountAndUniqueIdentities()
    {
        var target = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        target.Status.IsAvailable.Should().BeTrue(target.Status.Message);

        var providerIds = target.RefactoringProviders
            .Select(CodeActionProviderIdentity.GetId)
            .Concat(target.CodeFixProviders.Select(CodeActionProviderIdentity.GetId))
            .ToArray();

        providerIds.Should().HaveCount(250);
        providerIds.Should().OnlyHaveUniqueItems();
        var sortedProviderSnapshot = string.Join(
            '\n',
            providerIds.Order(StringComparer.Ordinal)) + "\n";
        var providerIdentitySha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sortedProviderSnapshot)));

        providerIdentitySha256.Should().Be(_expectedProviderIdentitySha256);
        target.RefactoringProviders.Should().NotBeEmpty();
        target.CodeFixProviders.Should().NotBeEmpty();
    }

    [Fact]
    public void GIVEN_ProviderAssessments_WHEN_InspectingDispositions_THEN_ShouldNotRequireDedicatedMcpTools()
    {
        BuiltInCodeActionProviderAssessment.Entries
            .Select(static entry => entry.ProviderId)
            .Should()
            .OnlyHaveUniqueItems();

        Enum.GetNames<BuiltInCodeActionAuditStatus>()
            .Should()
            .NotContain(static name => name.Contains("Dedicated", StringComparison.Ordinal));
    }

    [Fact]
    public void GIVEN_SupportedCompatibilityCases_WHEN_InspectingReplayCoverage_THEN_ShouldRetainNestedAndDiagnosticBackedCases()
    {
        BuiltInCodeActionAuditCases.SupportedCompatibilityCases.Should().Contain(static auditCase =>
            auditCase.Kind == BuiltInCodeActionAuditKind.Refactoring
            && auditCase.ActionPath.Count > 1);
        BuiltInCodeActionAuditCases.SupportedCompatibilityCases.Should().Contain(static auditCase =>
            auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix
            && auditCase.ExpectedDiagnosticId != null);
    }
}
