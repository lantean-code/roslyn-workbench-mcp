using System.Reflection;
using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Composition;

public sealed class CodeActionAssemblyIdentityComparerTests
{
    private readonly CodeActionAssemblyIdentityComparer _target;

    public CodeActionAssemblyIdentityComparerTests()
    {
        _target = CodeActionAssemblyIdentityComparer.Instance;
    }

    [Fact]
    public void GIVEN_SameAssemblyReference_WHEN_Comparing_THEN_ShouldReturnTrue()
    {
        var assembly = typeof(CodeActionAssemblyIdentityComparerTests).Assembly;

        var result = _target.Equals(assembly, assembly);

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_BothAssembliesAreNull_WHEN_Comparing_THEN_ShouldReturnTrue()
    {
        var result = _target.Equals(null, null);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_OneAssemblyIsNull_WHEN_Comparing_THEN_ShouldReturnFalse(bool firstIsNull)
    {
        var assembly = typeof(CodeActionAssemblyIdentityComparerTests).Assembly;
        var first = firstIsNull ? null : assembly;
        var second = firstIsNull ? assembly : null;

        var result = _target.Equals(first, second);

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_AssemblyIdentitiesDifferOnlyByCase_WHEN_Comparing_THEN_ShouldReturnTrue()
    {
        var first = CreateAssembly("AssemblyName, Version=1.0.0.0");
        var second = CreateAssembly("assemblyname, version=1.0.0.0");

        var result = _target.Equals(first.Object, second.Object);

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_DistinctAssemblyIdentities_WHEN_Comparing_THEN_ShouldReturnFalse()
    {
        var first = CreateAssembly("FirstAssembly, Version=1.0.0.0");
        var second = CreateAssembly("SecondAssembly, Version=1.0.0.0");

        var result = _target.Equals(first.Object, second.Object);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_OneAssemblyIdentityIsMissing_WHEN_Comparing_THEN_ShouldReturnFalse(bool firstIdentityIsMissing)
    {
        var first = CreateAssembly(firstIdentityIsMissing ? null : "AssemblyName, Version=1.0.0.0");
        var second = CreateAssembly(firstIdentityIsMissing ? "AssemblyName, Version=1.0.0.0" : null);

        var result = _target.Equals(first.Object, second.Object);

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_AssemblyHasIdentity_WHEN_GettingHashCode_THEN_ShouldUseCaseInsensitiveIdentityHash()
    {
        var assembly = CreateAssembly("AssemblyName, Version=1.0.0.0");

        var result = _target.GetHashCode(assembly.Object);

        result.Should().Be(StringComparer.OrdinalIgnoreCase.GetHashCode("assemblyname, version=1.0.0.0"));
    }

    [Fact]
    public void GIVEN_AssemblyHasNoIdentity_WHEN_GettingHashCode_THEN_ShouldUseReferenceHash()
    {
        var assembly = CreateAssembly(null);

        var result = _target.GetHashCode(assembly.Object);

        result.Should().Be(RuntimeHelpers.GetHashCode(assembly.Object));
    }

    private static Mock<Assembly> CreateAssembly(string? identity)
    {
        var assembly = new Mock<Assembly>();
        assembly.SetupGet(item => item.FullName).Returns(identity);
        return assembly;
    }
}
