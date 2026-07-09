namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetOperationTreeToolTests
{
    [Fact]
    public async Task GIVEN_LocationIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new GetOperationTreeTool();
        var context = new QueryContextBuilder().Build();

        var result = await target.ExecuteAsync(new GetOperationTreeRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ResolveLocationReturnsNotFound_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationRejection()
    {
        var resolver = new Mock<IWorkspaceResolver>();
        var context = new QueryContextBuilder()
            .WithResolver(resolver.Object)
            .Build();
        var target = new GetOperationTreeTool();

        resolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());
        resolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_SelectedNodeDoesNotResolveToOperation_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);
        var target = new GetOperationTreeTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = workspace.GetLocationSelector("namespace Sample;"),
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_OperationTreeExceedsMaxDepth_WHEN_CallingExecuteAsync_THEN_ShouldReturnTruncatedTree()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public string Format(string value)
                {
                    return value.Trim().ToUpperInvariant();
                }
            }
            """);
        var target = new GetOperationTreeTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = workspace.GetLocationSelector("value.Trim().ToUpperInvariant()"),
            MaxDepth = 0,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Root.Should().NotBeNull();
        result.Data.Truncated.Should().BeTrue();
        result.Data.Root!.Children.Should().BeEmpty();
    }
}

public sealed class GetControlFlowGraphToolTests
{
    [Fact]
    public async Task GIVEN_SymbolAndLocationAreBothNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new GetControlFlowGraphTool();
        var context = new QueryContextBuilder().Build();

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_SymbolHasNoSourceDeclaration_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFound()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace, requestResolver);
        var target = new GetControlFlowGraphTool();
        var compilation = await workspace.Solution.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);

        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<ControlFlowGraphData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ControlFlowGraphData>
            {
                Value = compilation!.GetSpecialType(SpecialType.System_String),
            });

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:System.String",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_LocationDoesNotSupportControlFlowGraph_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);
        var target = new GetControlFlowGraphTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Location = workspace.GetLocationSelector("Formatter"),
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ExecutableSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnProjectedControlFlowGraph()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public string Format(string value)
                {
                    if (value.Length == 0)
                    {
                        return string.Empty;
                    }

                    return value;
                }
            }
            """);
        var target = new GetControlFlowGraphTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new GetControlFlowGraphRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format(System.String)",
            },
            MaxBlocks = 1,
            MaxRegions = 1,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Owner!.DisplayName.Should().Contain("Format");
        result.Data.Blocks.Should().NotBeEmpty();
        result.Data.BlocksTruncated.Should().BeTrue();
    }
}

public sealed class GetSymbolDependenciesToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<SymbolDependenciesData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var target = new GetSymbolDependenciesTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(requestResolver: requestResolver);

        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, SymbolDependenciesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_MethodSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnDirectDependencies()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            using System;

            namespace Sample;

            public sealed class Formatter
            {
                public string Format(string value)
                {
                    return value.Trim().ToUpperInvariant();
                }
            }
            """);
        var target = new GetSymbolDependenciesTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format(System.String)",
            },
            IncludeAssemblies = false,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Dependencies.Items.Should().Contain(item => item.Symbol!.DisplayName.Contains("string", StringComparison.Ordinal));
        result.Data.Dependencies.Items.Should().OnlyContain(item => item.AssemblyName == null);
    }
}

public sealed class GetSymbolDependentsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample; public sealed class Formatter { }");
        var expected = PluginExecutionResult<SymbolDependentsData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var target = new GetSymbolDependentsTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace, requestResolver);

        var compilation = await workspace.Solution.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);
        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<SymbolDependentsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, SymbolDependentsData>
            {
                Value = compilation!.GetTypeByMetadataName("Sample.Formatter")!,
            });
        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<SymbolDependentsData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, SymbolDependentsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetSymbolDependentsRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ReferencedSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnDependentSymbols()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public string Format(string value)
                {
                    return value;
                }
            }

            public sealed class Caller
            {
                public string Call(Formatter formatter)
                {
                    return formatter.Format("hi");
                }
            }
            """);
        var target = new GetSymbolDependentsTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new GetSymbolDependentsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format(System.String)",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Dependents.Items.Should().Contain(symbol => symbol.DisplayName.Contains("Call", StringComparison.Ordinal));
    }
}

public sealed class FindDerivedTypesToolAdditionalTests
{
    [Fact]
    public async Task GIVEN_SymbolIsNotNamedType_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);
        var target = new FindDerivedTypesTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_BaseType_WHEN_CallingExecuteAsync_THEN_ShouldReturnDerivedTypes()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public class BaseType
            {
            }

            public sealed class DerivedType : BaseType
            {
            }
            """);
        var target = new FindDerivedTypesTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.BaseType",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.DerivedTypes.Items.Should().Contain(item => item.Type!.DisplayName.Contains("DerivedType", StringComparison.Ordinal));
    }
}

public sealed class FindDuplicateCodeToolTests
{
    [Fact]
    public async Task GIVEN_MinimumStatementsIsLessThanOne_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new FindDuplicateCodeTool();
        var context = new QueryContextBuilder().Build();

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 0,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<DuplicateCodeData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var target = new FindDuplicateCodeTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(requestResolver: requestResolver);

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DuplicateCodeData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DuplicateCodeData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DuplicateExecutableBlocks_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedGroups()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public int A(int value)
                {
                    var trimmed = value + 1;
                    var upper = trimmed + 1;
                    return upper;
                }

                public int B(int value)
                {
                    var trimmed = value + 1;
                    var upper = trimmed + 1;
                    return upper;
                }
            }
            """);
        var target = new FindDuplicateCodeTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace, defaultMaxResults: 1);

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 3,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Groups.Items.Should().HaveCount(1);
        result.Data.Groups.Items[0].Occurrences.Should().HaveCount(2);
    }
}

public sealed class GetCodeMetricsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<CodeMetricsData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var target = new GetCodeMetricsTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(requestResolver: requestResolver);

        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<CodeMetricsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CodeMetricsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest
        {
            Symbol = new SymbolSelector(),
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<CodeMetricsData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var target = new GetCodeMetricsTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(requestResolver: requestResolver);

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<CodeMetricsData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, CodeMetricsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_NamedTypeWithIncludeChildren_WHEN_CallingExecuteAsync_THEN_ShouldReturnMetricsForTypeAndChildren()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public string Format(string value)
                {
                    if (value.Length == 0)
                    {
                        return string.Empty;
                    }

                    return value;
                }
            }
            """);
        var target = new GetCodeMetricsTool();
        var context = AdvancedInspectionToolTestHelpers.CreateQueryContext(workspace);

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.Formatter",
            },
            IncludeChildren = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Metrics.Items.Should().HaveCountGreaterThan(1);
        result.Data.Metrics.Items.Should().Contain(item => item.Symbol!.DisplayName.Contains("Format", StringComparison.Ordinal));
    }
}

internal static class AdvancedInspectionToolTestHelpers
{
    public static IQueryContext CreateQueryContext(
        MiniWorkspace? workspace = null,
        Mock<IToolRequestResolver>? requestResolver = null,
        int defaultMaxResults = 100)
    {
        var currentWorkspace = workspace ?? MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = currentWorkspace.CreateWorkspaceIdentity();
        var servicesBuilder = new ToolExecutionServicesBuilder();
        if (requestResolver is not null)
        {
            servicesBuilder.WithRequestResolver(requestResolver.Object);
        }

        var services = servicesBuilder.Build();

        return new QueryContextBuilder()
            .WithCurrentSolution(currentWorkspace.Solution)
            .WithResolver(currentWorkspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithDefaultMaxResults(defaultMaxResults)
            .WithToolExecutionServices(services)
            .Build();
    }
}
