using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Roslyn.Workbench.Mcp.ToolReferenceGenerator;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

[Trait("Category", "Contract")]
public sealed class XunitEvidenceMethodValidatorTests
{
    public static TheoryData<string, bool, string> MethodShapes
    {
        get
        {
            var data = new TheoryData<string, bool, string>();
            data.Add("public sealed class Evidence { [Xunit.Fact] public void Run() { } }", true, string.Empty);
            data.Add("public sealed class Evidence { [Xunit.Fact] public Task Run() { return Task.CompletedTask; } }", true, string.Empty);
            data.Add("public sealed class Evidence { [Xunit.Fact] public Task<int> Run() { return Task.FromResult(0); } }", true, string.Empty);
            data.Add("public sealed class Evidence { [Xunit.Fact] public ValueTask Run() { return ValueTask.CompletedTask; } }", true, string.Empty);
            data.Add("public sealed class Evidence { [Xunit.Fact] public ValueTask<int> Run() { return ValueTask.FromResult(0); } }", true, string.Empty);
            data.Add("public sealed class Evidence { [Xunit.Theory, Xunit.InlineData(1)] public void Run(int value) { } }", false, "*Theory methods are not accepted*");
            data.Add("public abstract class Evidence { [Xunit.Fact] public void Run() { } }", false, "*containing type*");
            data.Add("public sealed class Evidence<T> { [Xunit.Fact] public void Run() { } }", false, "*containing type*");
            data.Add("internal sealed class Evidence { [Xunit.Fact] public void Run() { } }", false, "*containing type*");
            data.Add("public sealed class Evidence { [Xunit.Fact] public static void Run() { } }", false, "*non-static*");
            data.Add("public sealed class Evidence { [Xunit.Fact] public void Run<T>() { } }", false, "*non-generic*");
            data.Add("public sealed class Evidence { [Xunit.Fact] public void Run(int value) { } }", false, "*Fact methods must not declare parameters*");
            data.Add("public sealed class Evidence { [Xunit.Theory] public void Run(int value) { } }", false, "*Theory methods are not accepted*");
            data.Add("public sealed class Evidence { [Xunit.Theory, Xunit.InlineData(1)] public void Run(ref int value) { } }", false, "*passed by value*");
            data.Add("public sealed class Evidence { [Xunit.Fact] public int Run() { return 0; } }", false, "*must return void, Task*");
            data.Add("public sealed class Evidence { [Xunit.Fact] public async void Run() { await Task.Yield(); } }", false, "*must return void, Task*");
            data.Add("public sealed class Evidence { [Xunit.Fact(Skip = \"conditional\")] public void Run() { } }", false, "*sets 'Skip'*");
            data.Add("public sealed class Evidence { [Xunit.Fact(Explicit = true)] public void Run() { } }", false, "*sets 'Explicit'*");
            data.Add("public sealed class Evidence { public void Run() { } }", false, "*resolved attributes were*");
            data.Add("public sealed class Evidence { [Xunit.Fact, Xunit.Theory] public void Run() { } }", false, "*resolved attributes were*");
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MethodShapes))]
    public void GIVEN_MethodShape_WHEN_ValidatingXunitEvidence_THEN_ShouldReportDiscoverability(
        string declaration,
        bool expectedResult,
        string expectedReason)
    {
        var compilation = CreateCompilation(declaration);
        var method = compilation.GetSymbolsWithName(
                "Run",
                SymbolFilter.Member,
                TestContext.Current.CancellationToken)
            .OfType<IMethodSymbol>()
            .Single();

        var result = XunitEvidenceMethodValidator.TryValidate(method, out var rejectionReason);

        result.Should().Be(expectedResult);
        if (expectedResult)
        {
            rejectionReason.Should().BeEmpty();
        }
        else
        {
            rejectionReason.Should().Match(expectedReason);
        }
    }

    [Fact]
    public void GIVEN_CompilationWithoutErrors_WHEN_ValidatingEvidenceCompilation_THEN_ShouldAcceptProject()
    {
        var compilation = CreateCompilation("public sealed class Evidence { public void Run() { } }");

        var action = () => SecurityEvidenceValidator.ValidateCompilation(
            compilation,
            "evidence.csproj",
            TestContext.Current.CancellationToken);

        action.Should().NotThrow();
    }

    [Fact]
    public void GIVEN_CompilationError_WHEN_ValidatingEvidenceCompilation_THEN_ShouldRejectProject()
    {
        var compilation = CreateCompilation("public sealed class Evidence { public MissingType Run() { } }");

        var action = () => SecurityEvidenceValidator.ValidateCompilation(
            compilation,
            "evidence.csproj",
            TestContext.Current.CancellationToken);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*evidence.csproj*contains compilation errors*MissingType*");
    }

    [Fact]
    public void GIVEN_ShadowXunitFactAttribute_WHEN_ValidatingEvidence_THEN_ShouldRejectMethod()
    {
        const string source = """
            using System;
            namespace Xunit { public sealed class FactAttribute : Attribute { } }
            public sealed class Evidence { [Xunit.Fact] public void Run() { } }
            """;
        var compilation = CreateCompilation(source, includeUsings: false);
        var method = compilation.GetSymbolsWithName(
                "Run",
                SymbolFilter.Member,
                TestContext.Current.CancellationToken)
            .OfType<IMethodSymbol>()
            .Single();

        var result = XunitEvidenceMethodValidator.TryValidate(method, out var rejectionReason);

        result.Should().BeFalse();
        rejectionReason.Should().Match("*resolved attributes were*");
    }

    private static CSharpCompilation CreateCompilation(string declaration, bool includeUsings = true)
    {
        var source = declaration;
        if (includeUsings)
        {
            source = $$"""
            using System;
            using System.Threading.Tasks;

            {{declaration}}
            """;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("Trusted platform assemblies were not available.");

        var references = trustedPlatformAssemblies.Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "Evidence",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }
}
