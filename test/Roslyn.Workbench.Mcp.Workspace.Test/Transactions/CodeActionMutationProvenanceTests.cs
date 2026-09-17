using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class CodeActionMutationProvenanceTests
{
    [Fact]
    public void GIVEN_InternalAuditFields_WHEN_SerializingProvenance_THEN_ShouldPublishOnlyConciseAttribution()
    {
        var provider = new MutationProviderIdentity
        {
            TypeName = "Provider.Type",
            AssemblyName = "Provider.Assembly",
            AssemblyVersion = "1.0.0.0",
        };
        var fixAllProvider = new MutationProviderIdentity
        {
            TypeName = "FixAll.Provider",
            AssemblyName = "FixAll.Assembly",
            AssemblyVersion = "2.0.0.0",
        };

        var provenance = new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.FixAll,
            Provider = provider,
            FixAllProvider = fixAllProvider,
            DiagnosticIds = ["A001"],
            EquivalenceKey = "EquivalenceKey",
            FixAllScope = "Solution",
        };

        var json = JsonSerializer.SerializeToElement(provenance);

        json.GetProperty(nameof(CodeActionMutationProvenance.Kind)).GetInt32().Should().Be(2);
        json.GetProperty(nameof(CodeActionMutationProvenance.Provider)).Should().NotBeNull();
        json.GetProperty(nameof(CodeActionMutationProvenance.FixAllProvider)).Should().NotBeNull();
        json.TryGetProperty("DiagnosticIds", out _).Should().BeFalse();
        json.TryGetProperty("EquivalenceKey", out _).Should().BeFalse();
        json.TryGetProperty("FixAllScope", out _).Should().BeFalse();
    }
}
