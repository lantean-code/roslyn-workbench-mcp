using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeControlFlowToolTests
{
    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsConflict_WHEN_CallingExecuteAsync_THEN_ShouldReturnConflictResult()
    {
        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ControlFlowAnalysisData>.Conflict(new ToolError
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
                WorkspaceEpoch = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.RequestResolver.Verify(item => item.ValidateSnapshot<ControlFlowAnalysisData>(
            queryContextMocks.QueryContext.Object,
            It.IsAny<SnapshotPrecondition?>()), Times.Once);
        queryContextMocks.WorkspaceResolver.Verify(item => item.ResolveLocationAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationSelectorIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new AnalyzeControlFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowAnalysisData>?)null);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "A location selector is required.",
        });
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
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
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
            .ReturnsAsync(SelectorResolveResult<Location>.Ambiguous());

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
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
        using var document = InMemoryRoslynFactory.CreateDocument("""
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
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;"));

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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location => location.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(new ResolvedLocation());

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not resolve to a source document.",
        });
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_CurrentSolutionDoesNotContainResolvedDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = InMemoryRoslynFactory.CreateDocument("""
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
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;"));
        var resolvedLocation = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "DocumentId",
                ProjectId = "ProjectId",
                Path = "Code.cs",
            },
        };

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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location => location.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(resolvedLocation);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not resolve to a source document.",
        });
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_SelectedNodeHasNoStatementAncestor_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = InMemoryRoslynFactory.CreateDocument("""
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
        var resolvedLocation = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "DocumentId",
                ProjectId = "ProjectId",
                Path = "Code.cs",
            },
        };

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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location => location.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(resolvedLocation);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "The selected region must resolve to an executable statement.",
        });
    }

    [Fact]
    public async Task GIVEN_ExecutableStatementHasProjectedReturns_WHEN_CallingExecuteAsync_THEN_ShouldReturnControlFlowAnalysisResult()
    {
        using var document = InMemoryRoslynFactory.CreateDocument("""
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
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;"));
        var returnLocation = document.GetSingleNodeLocation<ReturnStatementSyntax>(item => item.ToString() == "return 1;");
        var region = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "DocumentId",
                ProjectId = "ProjectId",
                Path = "Code.cs",
            },
            Span = new TextSpanRange
            {
                Start = selectedLocation.SourceSpan.Start,
                Length = selectedLocation.SourceSpan.Length,
            },
        };
        var projectedReturn = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "ReturnDocumentId",
                ProjectId = "ProjectId",
                Path = "Code.cs",
            },
            Span = new TextSpanRange
            {
                Start = returnLocation.SourceSpan.Start,
                Length = returnLocation.SourceSpan.Length,
            },
        };

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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location => location.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(region);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location => location.SourceSpan == returnLocation.SourceSpan)))
            .Returns(projectedReturn);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
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
        using var document = InMemoryRoslynFactory.CreateDocument("""
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
        var selectedLocation = document.GetSingleNodeLocation<IfStatementSyntax>(item => item.ToString().Contains("return 1;"));
        var returnLocation = document.GetSingleNodeLocation<ReturnStatementSyntax>(item => item.ToString() == "return 1;");
        var region = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "DocumentId",
                ProjectId = "ProjectId",
                Path = "Code.cs",
            },
            Span = new TextSpanRange
            {
                Start = selectedLocation.SourceSpan.Start,
                Length = selectedLocation.SourceSpan.Length,
            },
        };
        var projectedExit = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "ExitDocumentId",
                ProjectId = "ProjectId",
                Path = "Code.cs",
            },
            Span = new TextSpanRange
            {
                Start = returnLocation.SourceSpan.Start,
                Length = returnLocation.SourceSpan.Length,
            },
        };

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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location => location.SourceSpan == selectedLocation.SourceSpan)))
            .Returns(region);
        queryContextMocks.WorkspaceResolver
            .SetupSequence(item => item.CreateResolvedLocation(It.Is<Location>(location => location.SourceSpan == returnLocation.SourceSpan)))
            .Returns(projectedExit)
            .Returns((ResolvedLocation?)null);

        var result = await target.ExecuteAsync(new AnalyzeControlFlowRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
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
            It.Is<Location>(location => location.SourceSpan == returnLocation.SourceSpan)), Times.Exactly(2));
    }
}
