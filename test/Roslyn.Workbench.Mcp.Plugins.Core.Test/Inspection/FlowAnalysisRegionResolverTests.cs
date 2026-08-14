using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FlowAnalysisRegionResolverTests
{
    [Fact]
    public async Task GIVEN_SelectionSpansSwitchSectionStatements_WHEN_ResolvingDataFlowRegion_THEN_ShouldReturnStatementRange()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(int value)
                {
                    switch (value)
                    {
                        case 0:
                            var first = value + 1;
                            var second = first + 1;
                            return second;
                        default:
                            return value;
                    }
                }
            }
            """);

        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var switchSection = syntaxRoot.DescendantNodes().OfType<SwitchSectionSyntax>().First();
        var selectedSpan = TextSpan.FromBounds(switchSection.Statements[0].SpanStart, switchSection.Statements[1].Span.End);
        var selectedLocation = syntaxTree.GetLocation(selectedSpan);
        var region = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<DataFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(selectedLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(region);

        var result = await FlowAnalysisRegionResolver.ResolveDataFlowRegionAsync<DataFlowAnalysisData>(
            new LocationSelector(),
            expectedSnapshot: null,
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeFalse();
        var statementRegion = result.Value.Should().BeOfType<ResolvedStatementFlowRegion>().Subject;
        statementRegion.FirstStatement.Should().BeSameAs(switchSection.Statements[0]);
        statementRegion.LastStatement.Should().BeSameAs(switchSection.Statements[1]);
        statementRegion.ResolvedLocation.Should().Be(region);
        var analysis = statementRegion.SemanticModel.AnalyzeDataFlow(
            statementRegion.FirstStatement,
            statementRegion.LastStatement);
        analysis.Should().NotBeNull();
        analysis!.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_SelectionSpansTopLevelStatements_WHEN_ResolvingDataFlowRegion_THEN_ShouldReturnStatementRange()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            var first = 1;
            var second = first + 1;
            System.Console.WriteLine(second);
            """);

        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var statements = syntaxRoot.Members.OfType<GlobalStatementSyntax>().Select(static item => item.Statement).ToArray();
        var selectedSpan = TextSpan.FromBounds(statements[1].SpanStart, statements[2].Span.End);
        var selectedLocation = syntaxTree.GetLocation(selectedSpan);
        var region = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<DataFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(selectedLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(region);

        var result = await FlowAnalysisRegionResolver.ResolveDataFlowRegionAsync<DataFlowAnalysisData>(
            new LocationSelector(),
            expectedSnapshot: null,
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeFalse();
        var statementRegion = result.Value.Should().BeOfType<ResolvedStatementFlowRegion>().Subject;
        statementRegion.FirstStatement.Should().BeSameAs(statements[1]);
        statementRegion.LastStatement.Should().BeSameAs(statements[2]);
        statementRegion.ResolvedLocation.Should().Be(region);
        var analysis = statementRegion.SemanticModel.AnalyzeDataFlow(
            statementRegion.FirstStatement,
            statementRegion.LastStatement);
        analysis.Should().NotBeNull();
        analysis!.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_TopLevelSelectionContainsNonStatementMember_WHEN_ResolvingDataFlowRegion_THEN_ShouldRejectRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            var first = 1;

            class Marker
            {
            }

            var second = first + 1;
            """);

        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var statements = syntaxRoot.Members.OfType<GlobalStatementSyntax>().Select(static item => item.Statement).ToArray();
        var selectedSpan = TextSpan.FromBounds(statements[0].SpanStart, statements[1].Span.End);
        var selectedLocation = syntaxTree.GetLocation(selectedSpan);
        var region = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<DataFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(selectedLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(region);

        var result = await FlowAnalysisRegionResolver.ResolveDataFlowRegionAsync<DataFlowAnalysisData>(
            new LocationSelector(),
            expectedSnapshot: null,
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeTrue();
        result.Rejection!.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region must exactly match an expression, a complete statement, or a contiguous range of statements in one executable body.",
        });
    }

    [Fact]
    public async Task GIVEN_SelectionCrossesNestedBodies_WHEN_ResolvingStatementRegion_THEN_ShouldRejectRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(bool flag, int value)
                {
                    if (flag)
                        value++;

                    if (!flag)
                        value--;

                    return value;
                }
            }
            """);

        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var statements = syntaxRoot.DescendantNodes().OfType<ExpressionStatementSyntax>().ToArray();
        var selectedSpan = TextSpan.FromBounds(statements[0].SpanStart, statements[1].Span.End);
        var selectedLocation = syntaxTree.GetLocation(selectedSpan);
        var region = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(selectedLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(region);

        var result = await FlowAnalysisRegionResolver.ResolveStatementRegionAsync<ControlFlowAnalysisData>(
            new LocationSelector(),
            expectedSnapshot: null,
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeTrue();
        result.Rejection!.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region must exactly match a complete statement or a contiguous range of statements in one executable body.",
        });
    }

    [Fact]
    public async Task GIVEN_SelectionIsEmpty_WHEN_ResolvingStatementRegion_THEN_ShouldRejectRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format()
                {
                    return 1;
                }
            }
            """);

        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var statement = syntaxRoot.DescendantNodes().OfType<ReturnStatementSyntax>().Single();
        var selectedLocation = syntaxTree.GetLocation(new TextSpan(statement.SpanStart, 0));
        var region = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(selectedLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(region);

        var result = await FlowAnalysisRegionResolver.ResolveStatementRegionAsync<ControlFlowAnalysisData>(
            new LocationSelector(),
            expectedSnapshot: null,
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeTrue();
        result.Rejection!.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region must exactly match a complete statement or a contiguous range of statements in one executable body.",
        });
    }
}
