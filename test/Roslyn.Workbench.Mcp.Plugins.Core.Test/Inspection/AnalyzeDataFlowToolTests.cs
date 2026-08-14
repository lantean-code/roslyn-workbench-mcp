using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeDataFlowToolTests
{
    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsConflict_WHEN_CallingExecuteAsync_THEN_ShouldReturnConflictResult()
    {
        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Conflict<DataFlowAnalysisData>(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                WorkspaceEpoch = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.WorkspaceResolver.Verify(item => item.ResolveLocationAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolveLocationReturnsNotFound_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<DataFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.NotFound<Location>());

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not match any result.",
        });

        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolveLocationReturnsAmbiguous_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationAmbiguousResult()
    {
        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<DataFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Ambiguous<Location>());

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "LocationAmbiguous",
            Message = "The location selector matched multiple results.",
        });

        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationHasNoDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                string Format(string value)
                {
                    var trimmed = value.Trim();
                    Func<string> get = () => trimmed;
                    return get();
                }
            }
            """);

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<LocalDeclarationStatementSyntax>(item => item.ToString().Contains("Func<string> get", StringComparison.Ordinal));

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
            .Returns(new ResolvedLocation());

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not resolve to a source document.",
        });

        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_CurrentSolutionDoesNotContainResolvedDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                string Format(string value)
                {
                    var trimmed = value.Trim();
                    Func<string> get = () => trimmed;
                    return get();
                }
            }
            """);

        using var emptyWorkspace = new AdhocWorkspace();

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<LocalDeclarationStatementSyntax>(item => item.ToString().Contains("Func<string> get", StringComparison.Ordinal));

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(emptyWorkspace.CurrentSolution);

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
            .Returns(SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs"));

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not resolve to a source document.",
        });

        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_SelectedNodeHasNoStatementAncestor_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                string Format(string value)
                {
                    var trimmed = value.Trim();
                    Func<string> get = () => trimmed;
                    return get();
                }
            }
            """);

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<ClassDeclarationSyntax>(item => item.Identifier.ValueText == "Formatter");

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
            .Returns(SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs"));

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region must exactly match an expression, a complete statement, or a contiguous range of statements in one executable body.",
        });
    }

    [Fact]
    public async Task GIVEN_ExecutableStatementHasDataFlowOutputs_WHEN_CallingExecuteAsync_THEN_ShouldReturnDataFlowAnalysisResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                string Format(string value)
                {
                    var trimmed = value.Trim();
                    Func<string> get = () => trimmed;
                    return get();
                }
            }
            """);

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<LocalDeclarationStatementSyntax>(item => item.ToString().Contains("Func<string> get", StringComparison.Ordinal));
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

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data.Should().BeEquivalentTo(new DataFlowAnalysisData
        {
            Region = region,
            VariablesDeclared =
            [
                SelectorTestFactory.CreateSymbolReference("get", SymbolKind.Local),
            ],
            ReadInside =
            [
                SelectorTestFactory.CreateSymbolReference("trimmed", SymbolKind.Local),
            ],
            WrittenInside =
            [
                SelectorTestFactory.CreateSymbolReference("get", SymbolKind.Local),
            ],
            DataFlowsIn =
            [
                SelectorTestFactory.CreateSymbolReference("trimmed", SymbolKind.Local),
            ],
            DataFlowsOut =
            [
                SelectorTestFactory.CreateSymbolReference("get", SymbolKind.Local),
            ],
            Captured =
            [
                SelectorTestFactory.CreateSymbolReference("trimmed", SymbolKind.Local),
            ],
        });
    }

    [Fact]
    public async Task GIVEN_SelectionExactlyMatchesExpression_WHEN_CallingExecuteAsync_THEN_ShouldAnalyzeAndReturnExactRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                string Format(string value)
                {
                    return value.Trim();
                }
            }
            """);

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<InvocationExpressionSyntax>(item => item.ToString() == "value.Trim()");
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

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Region.Should().Be(region);
        result.Data.ReadInside.Should().ContainSingle(item => item.DisplayName == "value");
    }

    [Fact]
    public async Task GIVEN_SelectionExactlySpansContiguousStatements_WHEN_CallingExecuteAsync_THEN_ShouldAnalyzeAndReturnExactRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(int value)
                {
                    var first = value + 1;
                    var second = first + 1;
                    return second;
                }
            }
            """);

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var body = syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Single().Body!;
        var selectedSpan = TextSpan.FromBounds(body.Statements[0].SpanStart, body.Statements[1].Span.End);
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

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Region.Should().Be(region);
        result.Data.VariablesDeclared.Select(item => item.DisplayName).Should().Equal("first", "second");
    }

    [Fact]
    public async Task GIVEN_SelectionContainsOnlyPartOfStatement_WHEN_CallingExecuteAsync_THEN_ShouldRejectPartialRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(int value)
                {
                    var result = value + 1;
                    return result;
                }
            }
            """);

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var statement = syntaxRoot.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Single();
        var partialSpan = TextSpan.FromBounds(statement.SpanStart, statement.Span.End - 1);
        var selectedLocation = syntaxTree.GetLocation(partialSpan);
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

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region must exactly match an expression, a complete statement, or a contiguous range of statements in one executable body.",
        });
    }

    [Fact]
    public async Task GIVEN_ExactExpressionCannotBeAnalyzed_WHEN_CallingExecuteAsync_THEN_ShouldRejectUnsupportedAnalysis()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Format(int value)
                {
                    System.Console.WriteLine(value);
                }
            }
            """);

        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<MemberAccessExpressionSyntax>(item => item.ToString() == "System.Console");
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

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region does not support data-flow analysis.",
        });
    }

}
