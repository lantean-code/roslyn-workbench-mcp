using Microsoft.CodeAnalysis.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Composition;

public sealed class CodeActionProviderIdentityTests
{
    [Fact]
    public void GIVEN_ProviderType_WHEN_CreatingProvenance_THEN_ShouldUseRuntimeTypeAndAssemblyIdentity()
    {
        var providerType = typeof(CodeFixProvider);
        var assemblyName = providerType.Assembly.GetName();

        var result = CodeActionProviderIdentity.CreateProvenance(providerType);

        result.TypeName.Should().Be(providerType.FullName);
        result.AssemblyName.Should().Be(assemblyName.Name);
        result.AssemblyVersion.Should().Be(assemblyName.Version!.ToString());
    }
}
