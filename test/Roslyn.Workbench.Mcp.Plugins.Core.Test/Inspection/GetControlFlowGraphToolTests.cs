using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetControlFlowGraphToolTests
{
    private const int _anonymousFunctionScope = 2;
    private const int _localFunctionScope = 1;
    private const int _methodScope = 0;

    [Fact]
    public void GIVEN_GraphLimitsAreNullOrZero_WHEN_GettingEffectiveValues_THEN_ShouldUseDefaultsOrZero()
    {
        var target = new GetControlFlowGraphRequest
        {
            MaxBlocks = null,
            MaxRegions = null,
            MaxOperationsPerBlock = null,
        };

        target.EffectiveMaxBlocks.Should().Be(64);
        target.EffectiveMaxRegions.Should().Be(32);
        target.EffectiveMaxOperationsPerBlock.Should().Be(32);

        target = target with
        {
            MaxBlocks = 0,
            MaxRegions = 0,
            MaxOperationsPerBlock = 0,
        };

        target.EffectiveMaxBlocks.Should().Be(0);
        target.EffectiveMaxRegions.Should().Be(0);
        target.EffectiveMaxOperationsPerBlock.Should().Be(0);
    }

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
        var expected = PluginExecutionResult.Rejected<ControlFlowGraphData>(new PluginExecutionError
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
            .ReturnsAsync(ToolResolutionResult.Rejected<ISymbol, ControlFlowGraphData>(expected));

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
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ControlFlowGraphData>(metadataSymbol));

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
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ControlFlowGraphData>(symbol));

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
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ControlFlowGraphData>(symbol));

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
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ControlFlowGraphData>(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
            MaxRegions = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Regions.Items.Should().ContainSingle();
        result.Data.Regions.HasMore.Should().BeTrue();
        result.Data.Regions.TotalCount.Should().BeNull();
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
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ControlFlowGraphData>(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector(),
            MaxRegions = 32,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Regions.Items.Should().HaveCountGreaterThan(1);
        result.Data.Regions.HasMore.Should().BeFalse();
        result.Data.Regions.TotalCount.Should().Be(result.Data.Regions.Items.Count);
    }

    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<ControlFlowGraphData>(new PluginExecutionError
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
            .ReturnsAsync(SelectorResolveResult.NotFound<Location>());

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns(new ResolvedLocation
            {
                Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                    Guid.Parse("11111111-1111-1111-1111-111111111111")),
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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item =>
            {
                if (item.Name == ownerSymbol.Name)
                {
                    return new SymbolReference
                    {
                        DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Kind = item.Kind.ToString(),
                        DocumentationCommentId = item.GetDocumentationCommentId(),
                    };
                }

                return SelectorTestFactory.CreateSymbolReference(item);
            });

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
            MaxBlocks = 1,
            MaxRegions = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Owner!.DisplayName.Should().Contain("Formatter");
        result.Data.Blocks.Items.Should().ContainSingle();
        result.Data.Blocks.HasMore.Should().BeTrue();
        result.Data.Blocks.TotalCount.Should().BeGreaterThan(1);
        result.Data.Regions.Items.Should().ContainSingle();
        result.Data.Regions.HasMore.Should().BeFalse();
        result.Data.Regions.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData(_methodScope)]
    [InlineData(_localFunctionScope)]
    [InlineData(_anonymousFunctionScope)]
    public async Task GIVEN_LocationResolvesInsideExecutableScope_WHEN_CallingExecuteAsync_THEN_ShouldReturnContainingControlFlowGraph(int executableScope)
    {
        const string source = """
            using System;

            class Formatter
            {
                void Run()
                {
                    int value = 0;
                    value++;

                    void Local()
                    {
                        int localValue = 0;
                        localValue++;
                    }

                    Action action = () =>
                    {
                        int lambdaValue = 0;
                        lambdaValue++;
                    };
                }
            }
            """;

        var (selectedText, expectedOwner) = executableScope switch
        {
            _methodScope => ("value++;", "Run"),
            _localFunctionScope => ("localValue++;", "Local"),
            _anonymousFunctionScope => ("lambdaValue++;", "AnonymousFunction"),
            _ => throw new InvalidOperationException($"Unsupported executable scope kind '{executableScope}'."),
        };

        using var document = RoslynTestFactory.CreateDocument(source);
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var sourceText = await document.Document.GetTextAsync(TestContext.Current.CancellationToken);
        var sourceTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        if (sourceTree is null)
        {
            throw new InvalidOperationException("The test document did not produce a syntax tree.");
        }

        var selectedStart = sourceText.ToString().IndexOf(selectedText, StringComparison.Ordinal);
        var selectedSpan = new TextSpan(selectedStart, selectedText.Length);
        var location = Location.Create(sourceTree, selectedSpan);

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => new SymbolReference
            {
                DisplayName = string.IsNullOrEmpty(item.Name) ? "AnonymousFunction" : item.Name,
                Kind = item.Kind.ToString(),
                DocumentationCommentId = item.GetDocumentationCommentId(),
            });

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Blocks.Items.Should().NotBeEmpty();
        result.Data.Owner.Should().NotBeNull();
        result.Data.Owner.DisplayName.Should().Be(expectedOwner);
    }

    [Fact]
    public async Task GIVEN_LocationDoesNotResolveInsideExecutableScope_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        const string source = "class Formatter { }";
        using var document = RoslynTestFactory.CreateDocument(source);
        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var sourceTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        if (sourceTree is null)
        {
            throw new InvalidOperationException("The test document did not produce a syntax tree.");
        }

        var selectedSpan = new TextSpan(source.IndexOf("Formatter", StringComparison.Ordinal), "Formatter".Length);
        var location = Location.Create(sourceTree, selectedSpan);

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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        var expectedError = new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The selected target does not support control-flow graph generation.",
        };

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(expectedError);
    }

    [Fact]
    public async Task GIVEN_BasicBlockOperationsExceedLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedOperationPointers()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Run()
                {
                    var first = 1;
                    var second = 2;
                    var third = 3;
                    return first + second + third;
                }
            }
            """);

        var target = new GetControlFlowGraphTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (MethodDeclarationSyntax item) => item.Identifier.ValueText == "Run",
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
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
            MaxBlocks = 64,
            MaxOperationsPerBlock = 1,
            MaxRegions = 32,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        var boundedBlock = result.Data!.Blocks.Items.Single(item => item.Operations.HasMore);
        boundedBlock.Operations.Items.Should().ContainSingle();
        boundedBlock.Operations.Items[0].Kind.Should().NotBeEmpty();
        boundedBlock.Operations.Items[0].Location.Should().NotBeNull();
        boundedBlock.Operations.TotalCount.Should().BeGreaterThan(1);

        var zeroOperationsResult = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = new LocationSelector(),
            MaxBlocks = 64,
            MaxOperationsPerBlock = 0,
            MaxRegions = 32,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        var zeroOperationsBlock = zeroOperationsResult.Data!.Blocks.Items.First(item => item.Operations.HasMore);
        zeroOperationsBlock.Operations.Items.Should().BeEmpty();
    }
}
