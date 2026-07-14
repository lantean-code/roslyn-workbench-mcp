using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeDataFlowToolTests
{
    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsConflict_WHEN_CallingExecuteAsync_THEN_ShouldReturnConflictResult()
    {
        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DataFlowAnalysisData>.Conflict(new PluginExecutionError
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
                WorkspaceEpoch = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.WorkspaceResolver.Verify(item => item.ResolveLocationAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationSelectorIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<DataFlowAnalysisData>?)null);

        var result = await target.ExecuteAsync(new AnalyzeDataFlowRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
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
        var target = new AnalyzeDataFlowTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<DataFlowAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<DataFlowAnalysisData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

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
            .ReturnsAsync(SelectorResolveResult<Location>.Ambiguous());

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
        var selectedLocation = document.GetSingleNodeLocation<LocalDeclarationStatementSyntax>(item => item.ToString().Contains("Func<string> get"));

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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
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
        var selectedLocation = document.GetSingleNodeLocation<LocalDeclarationStatementSyntax>(item => item.ToString().Contains("Func<string> get"));

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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
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
            Message = "The selected region must resolve to an executable statement.",
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
        var selectedLocation = document.GetSingleNodeLocation<LocalDeclarationStatementSyntax>(item => item.ToString().Contains("Func<string> get"));
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
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
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

}
