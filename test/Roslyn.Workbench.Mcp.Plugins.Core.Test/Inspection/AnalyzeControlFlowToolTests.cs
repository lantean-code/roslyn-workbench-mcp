using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeControlFlowToolTests
{
    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsConflict_WHEN_CallingExecuteAsync_THEN_ShouldReturnConflictResult()
    {
        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Conflict<ControlFlowAnalysisData>(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
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
        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.NotFound<Location>());

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
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
        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowAnalysisData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Ambiguous<Location>());

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
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
            class Formatter
            {
                int Format(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 2;
                }
            }
            """);

        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;", StringComparison.Ordinal));

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
            .Returns(new ResolvedLocation());

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
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
            class Formatter
            {
                int Format(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 2;
                }
            }
            """);

        using var emptyWorkspace = new AdhocWorkspace();

        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;", StringComparison.Ordinal));

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(emptyWorkspace.CurrentSolution);

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
            .Returns(SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs"));

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
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
            class Formatter
            {
                int Format(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 2;
                }
            }
            """);

        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<ClassDeclarationSyntax>(item => item.Identifier.ValueText == "Formatter");

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
            .Returns(SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs"));

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region must exactly match a complete statement or a contiguous range of statements in one executable body.",
        });
    }

    [Fact]
    public async Task GIVEN_ExecutableStatementHasProjectedReturns_WHEN_CallingExecuteAsync_THEN_ShouldReturnControlFlowAnalysisResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 2;
                }
            }
            """);

        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;", StringComparison.Ordinal));
        var returnLocation = document.GetSingleNodeLocation<ReturnStatementSyntax>(item => item.ToString() == "return 1;");
        var region = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");
        var projectedReturn = SelectorTestFactory.CreateResolvedLocation(returnLocation, "Code.cs");

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

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item.SourceSpan == returnLocation.SourceSpan)))
            .Returns(projectedReturn);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data.Should().BeEquivalentTo(new ControlFlowAnalysisData
        {
            Region = region,
            EntryReachable = true,
            ExitReachable = true,
            Exits =
            [
                new ControlFlowExit
                {
                    Kind = nameof(SyntaxKind.ReturnStatement),
                    Location = projectedReturn,
                },
            ],
            Returns =
            [
                projectedReturn,
            ],
        });

        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GIVEN_ReturnLocationProjectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldExcludeReturnFromResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 2;
                }
            }
            """);

        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;", StringComparison.Ordinal));
        var returnLocation = document.GetSingleNodeLocation<ReturnStatementSyntax>(item => item.ToString() == "return 1;");
        var region = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");
        var projectedExit = SelectorTestFactory.CreateResolvedLocation(returnLocation, "Code.cs");

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

        queryContextMocks.WorkspaceResolver
            .SetupSequence(item => item.CreateResolvedLocation(It.Is<Location>(item => item.SourceSpan == returnLocation.SourceSpan)))
            .Returns(projectedExit)
            .Returns((ResolvedLocation?)null);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data.Should().BeEquivalentTo(new ControlFlowAnalysisData
        {
            Region = region,
            EntryReachable = true,
            ExitReachable = true,
            Exits =
            [
                new ControlFlowExit
                {
                    Kind = nameof(SyntaxKind.ReturnStatement),
                    Location = projectedExit,
                },
            ],
            Returns = [],
        });

        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(
            It.Is<Location>(item => item.SourceSpan == returnLocation.SourceSpan)), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_SelectionIsOnlyAStatementHeader_WHEN_CallingExecuteAsync_THEN_ShouldRejectPartialRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(bool flag)
                {
                    if (flag)
                    {
                        return 1;
                    }

                    return 2;
                }
            }
            """);

        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var syntaxRoot = (CompilationUnitSyntax)(await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken))!;
        var syntaxTree = syntaxRoot.SyntaxTree;
        var statement = syntaxRoot.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var headerSpan = TextSpan.FromBounds(statement.SpanStart, statement.CloseParenToken.Span.End);
        var selectedLocation = syntaxTree.GetLocation(headerSpan);
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

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected region must exactly match a complete statement or a contiguous range of statements in one executable body.",
        });
    }

    [Fact]
    public async Task GIVEN_SelectionExactlySpansContiguousStatements_WHEN_CallingExecuteAsync_THEN_ShouldAnalyzeAndReturnExactRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(bool flag)
                {
                    var value = 0;
                    if (flag)
                    {
                        value++;
                    }

                    return value;
                }
            }
            """);

        var target = new AnalyzeControlFlowTool();
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

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Region.Should().Be(region);
        result.Data.EntryReachable.Should().BeTrue();
        result.Data.ExitReachable.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_SelectionSpansSwitchSectionStatements_WHEN_CallingExecuteAsync_THEN_ShouldAnalyzeAndReturnExactRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format(int value)
                {
                    switch (value)
                    {
                        case 0:
                            value++;
                            break;
                        default:
                            return value;
                    }

                    return value;
                }
            }
            """);

        var target = new AnalyzeControlFlowTool();
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

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Region.Should().Be(region);
        result.Data.EntryReachable.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_SelectionSpansTopLevelStatements_WHEN_CallingExecuteAsync_THEN_ShouldAnalyzeAndReturnExactRegion()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            var first = 1;
            var second = first + 1;
            System.Console.WriteLine(second);
            """);

        var target = new AnalyzeControlFlowTool();
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

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Region.Should().Be(region);
        result.Data.EntryReachable.Should().BeTrue();
    }

}
