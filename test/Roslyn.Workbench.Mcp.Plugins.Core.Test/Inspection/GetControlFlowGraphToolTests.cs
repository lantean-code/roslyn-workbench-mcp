using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetControlFlowGraphToolTests
{
    [Fact]
    public async Task GIVEN_SymbolAndLocationAreBothMissing_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "Specify exactly one of symbol or location.",
        });
    }

    [Fact]
    public async Task GIVEN_SymbolAndLocationAreBothProvided_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "Specify exactly one of symbol or location.",
        });
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ControlFlowGraphData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ControlFlowGraphData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, ControlFlowGraphData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_SymbolDoesNotHaveSourceDeclaration_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter {}");

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilation = await document.Solution.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);
        var metadataSymbol = compilation!.GetSpecialType(SpecialType.System_String);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ControlFlowGraphData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, ControlFlowGraphData>.Resolved(metadataSymbol));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_SymbolSourceTreeIsNotInCurrentSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ControlFlowGraphData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, ControlFlowGraphData>.Resolved(symbol));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_SymbolTargetDoesNotSupportControlFlowGraph_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter {}");

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ControlFlowGraphData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, ControlFlowGraphData>.Resolved(symbol));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_SymbolResolvesToExceptionalFlow_WHEN_CallingExecuteAsync_THEN_ShouldReturnTruncatedRegions()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run(string value)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        value = string.Empty;
                    }
                    finally
                    {
                        value = value.Trim();
                    }
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ControlFlowGraphData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, ControlFlowGraphData>.Resolved(symbol));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
            MaxRegions = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Regions.Should().ContainSingle();
        result.Data.RegionsTruncated.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_SymbolResolvesToExceptionalFlowWithinRegionLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnCompleteRegions()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run(string value)
                {
                    try
                    {
                        value = value.Trim();
                    }
                    catch (InvalidOperationException)
                    {
                        value = string.Empty;
                    }
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ControlFlowGraphData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, ControlFlowGraphData>.Resolved(symbol));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
            MaxRegions = 32,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Regions.Should().HaveCountGreaterThan(1);
        result.Data.RegionsTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ControlFlowGraphData>.Rejected(new PluginExecutionError
        {
            Code = "SnapshotConflict",
            Message = "SnapshotConflict",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowGraphData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_LocationSelectorDoesNotResolve_WHEN_CallingExecuteAsync_THEN_ShouldReturnStatusRejection()
    {
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowGraphData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowGraphData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationProjectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    int value = 0;
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (LocalDeclarationStatementSyntax item) => item.ToString().Contains("value", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowGraphData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowGraphData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns((ResolvedLocation?)null);

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationDoesNotContainDocumentPath_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    int value = 0;
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (LocalDeclarationStatementSyntax item) => item.ToString().Contains("value", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowGraphData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowGraphData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns(new ResolvedLocation
            {
                Document = new DocumentReference(),
            });

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationSourceTreeIsNotInCurrentSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    int value = 0;
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (LocalDeclarationStatementSyntax item) => item.ToString().Contains("value", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowGraphData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowGraphData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_LocationResolvesToExecutableRegion_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedControlFlowGraph()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run(int value)
                {
                    if (value > 0)
                    {
                        value--;
                    }
                    else
                    {
                        value++;
                    }
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (MethodDeclarationSyntax item) => item.Identifier.ValueText == "Run",
            TestContext.Current.CancellationToken);
        var ownerSymbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ControlFlowGraphData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<ControlFlowGraphData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => item.Name == ownerSymbol.Name
                ? new SymbolReference
                {
                    DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    Kind = item.Kind.ToString(),
                    DocumentationCommentId = item.GetDocumentationCommentId(),
                }
                : SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
            MaxBlocks = 1,
            MaxRegions = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Owner!.DisplayName.Should().Contain("Formatter");
        result.Data.Blocks.Should().ContainSingle();
        result.Data.BlocksTruncated.Should().BeTrue();
        result.Data.Regions.Should().ContainSingle();
        result.Data.RegionsTruncated.Should().BeFalse();
    }
}
