using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Composition;

public sealed class CodeActionAssemblyResolverTests
{
    [Fact]
    public void GIVEN_BuiltInAssembliesEnabled_WHEN_Resolving_THEN_ShouldIncludePinnedFeatureAssembliesOnce()
    {
        var result = CodeActionAssemblyResolver.Resolve(new CodeActionCompositionOptions());

        result.Should().ContainSingle(assembly =>
            assembly.GetName().Name == "Microsoft.CodeAnalysis.Features");

        result.Should().ContainSingle(assembly =>
            assembly.GetName().Name == "Microsoft.CodeAnalysis.CSharp.Features");
    }

    [Fact]
    public void GIVEN_BuiltInAssembliesDisabled_WHEN_Resolving_THEN_ShouldRetainMefDefaultsAndAdditionalAssemblies()
    {
        var additionalAssembly = typeof(CodeActionAssemblyResolverTests).Assembly;
        var result = CodeActionAssemblyResolver.Resolve(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                additionalAssembly,
                additionalAssembly,
            ],
        });

        result.Should().Contain(additionalAssembly);
        result.Should().Contain(typeof(Microsoft.CodeAnalysis.Workspace).Assembly);
        result.Distinct(AssemblyIdentityComparer.Instance).Should().HaveSameCount(result);
    }

    private sealed class AssemblyIdentityComparer : IEqualityComparer<Assembly>
    {
        public static AssemblyIdentityComparer Instance { get; } = new();

        public bool Equals(Assembly? left, Assembly? right)
        {
            return string.Equals(
                left?.FullName,
                right?.FullName,
                StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(Assembly value)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(value.FullName ?? string.Empty);
        }
    }
}
