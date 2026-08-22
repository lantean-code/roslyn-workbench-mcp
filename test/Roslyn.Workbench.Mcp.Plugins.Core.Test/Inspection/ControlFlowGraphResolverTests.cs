using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class ControlFlowGraphResolverTests
{
    public static TheoryData<string> NestedParameterInitializerCases { get; } = new()
    {
        { "class Formatter { void Run() { void Local(int value = 1) { } } }" },
        { "using System; class Formatter { void Run() { Func<int, int> value = (int input = 1) => input; } }" },
    };

    public static TheoryData<string, string, Type> SupportedRootCases { get; } = new()
    {
        { "[System.Obsolete] class Formatter { }", "Obsolete", typeof(IAttributeOperation) },
        { "class Formatter { Formatter() { int value = 0; } }", "int value", typeof(IConstructorBodyOperation) },
        { "class Formatter { int value = 1; }", "= 1", typeof(IFieldInitializerOperation) },
        { "class Formatter { void Run() { int value = 0; } }", "int value", typeof(IMethodBodyOperation) },
        { "class Formatter { void Run(int value = 1) { } }", "= 1", typeof(IParameterInitializerOperation) },
        { "class Formatter { int Value { get; } = 1; }", "= 1", typeof(IPropertyInitializerOperation) },
    };

    [Theory]
    [MemberData(nameof(SupportedRootCases))]
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "xUnit requires public test methods and supplies the non-null values declared by SupportedRootCases.")]
    public async Task GIVEN_SupportedExecutableRoot_WHEN_Resolving_THEN_ShouldReturnGraphCreatedFromExpectedOperation(string source, string selectedText, Type expectedOperationType)
    {
        var graph = await ResolveAsync(source, selectedText);

        graph.Should().NotBeNull();
        graph!.OriginalOperation.Should().BeAssignableTo(expectedOperationType);
    }

    [Theory]
    [MemberData(nameof(NestedParameterInitializerCases))]
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "xUnit requires public test methods and supplies the non-null values declared by NestedParameterInitializerCases.")]
    public async Task GIVEN_ParameterInitializerOwnedByNestedFunction_WHEN_Resolving_THEN_ShouldReturnStandaloneInitializerGraph(string source)
    {
        var graph = await ResolveAsync(source, "= 1");

        graph.Should().NotBeNull();
        graph!.OriginalOperation.Should().BeAssignableTo<IParameterInitializerOperation>();
        graph.Parent.Should().BeNull();
    }

    [Theory]
    [InlineData("methodValue++;", 0)]
    [InlineData("localValue++;", 1)]
    [InlineData("lambdaValue++;", 2)]
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "xUnit requires public test methods and supplies the non-null strings declared by the InlineData attributes.")]
    public async Task GIVEN_NestedExecutableScope_WHEN_Resolving_THEN_ShouldReturnGraphAtExpectedDepth(string selectedText, int expectedParentCount)
    {
        const string source = """
            using System;

            class Formatter
            {
                void Run()
                {
                    int methodValue = 0;
                    methodValue++;

                    void Local()
                    {
                        int localValue = 0;
                        localValue++;

                        Action action = () =>
                        {
                            int lambdaValue = 0;
                            lambdaValue++;
                        };
                    }
                }
            }
            """;

        var graph = await ResolveAsync(source, selectedText);

        graph.Should().NotBeNull();
        GetParentCount(graph!).Should().Be(expectedParentCount);
    }

    [Fact]
    public async Task GIVEN_LambdaInBranchValue_WHEN_Resolving_THEN_ShouldReturnNestedGraph()
    {
        const string source = """
            using System;

            class Formatter
            {
                bool Run()
                {
                    bool Predicate(Func<bool> candidate)
                    {
                        return candidate();
                    }

                    if (Predicate(() => true))
                    {
                        return true;
                    }

                    return false;
                }
            }
            """;

        var graph = await ResolveAsync(source, "true");

        graph.Should().NotBeNull();
        GetParentCount(graph!).Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_UnsupportedOperationRoot_WHEN_Resolving_THEN_ShouldReturnNull()
    {
        const string source = "class Formatter { int value; }";

        var graph = await ResolveAsync(source, "value");

        graph.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_NodeWithoutOperation_WHEN_Resolving_THEN_ShouldReturnNull()
    {
        const string source = "class Formatter { }";

        var graph = await ResolveAsync(source, "Formatter");

        graph.Should().BeNull();
    }

    private static async ValueTask<ControlFlowGraph?> ResolveAsync(string source, string selectedText)
    {
        using var document = RoslynTestFactory.CreateDocument(source);
        var sourceText = await document.Document.GetTextAsync(TestContext.Current.CancellationToken);
        var selectedStart = sourceText.ToString().IndexOf(selectedText, StringComparison.Ordinal);
        if (selectedStart < 0)
        {
            throw new InvalidOperationException($"The selected text '{selectedText}' was not found in the test source.");
        }

        var syntaxRoot = await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var semanticModel = await document.Document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            throw new InvalidOperationException("The test document could not be analysed.");
        }

        var selectedSpan = new TextSpan(selectedStart, selectedText.Length);
        var node = syntaxRoot.FindNode(selectedSpan, getInnermostNodeForTie: true);
        return ControlFlowGraphResolver.Resolve(node, semanticModel, TestContext.Current.CancellationToken);
    }

    private static int GetParentCount(ControlFlowGraph graph)
    {
        var count = 0;
        for (var current = graph.Parent; current is not null; current = current.Parent)
        {
            count++;
        }

        return count;
    }
}
