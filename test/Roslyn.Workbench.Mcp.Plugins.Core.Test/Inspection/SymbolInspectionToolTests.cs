namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class ResolveSymbolToolTests
{
    [Fact]
    public async Task GIVEN_LocationIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new ResolveSymbolTool();
        var context = new QueryContextBuilder().Build();

        var result = await target.ExecuteAsync(new ResolveSymbolRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_LocationResolvesToSourceSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolSelectorAndDeclarations()
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
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new ResolveSymbolTool();

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = workspace.GetLocationSelector("Format"),
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Contain("Format");
        result.Data.Selector.Should().NotBeNull();
        result.Data.Declarations.Should().ContainSingle();
    }
}

public sealed class GoToDefinitionToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<DefinitionData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = CreateContext(requestResolver: requestResolver.Object);
        var target = new GoToDefinitionTool();

        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<DefinitionData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, DefinitionData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GoToDefinitionRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_SourceSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnSourceDefinitionLocations()
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
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GoToDefinitionTool();

        var result = await target.ExecuteAsync(new GoToDefinitionRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Definitions.Should().ContainSingle();
        result.Data.Definitions[0].Location!.Document!.Path.Should().Be("Sample.cs");
    }

    private static IQueryContext CreateContext(MiniWorkspace? workspace = null, IToolRequestResolver? requestResolver = null)
    {
        var currentWorkspace = workspace ?? MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = currentWorkspace.CreateWorkspaceIdentity();
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver ?? Mock.Of<IToolRequestResolver>())
            .Build();

        return new QueryContextBuilder()
            .WithCurrentSolution(currentWorkspace.Solution)
            .WithResolver(currentWorkspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
    }
}

public sealed class SearchSymbolsToolTests
{
    [Fact]
    public async Task GIVEN_QueryAndMetadataNameAreEmpty_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new SearchSymbolsTool();

        var result = await target.ExecuteAsync(new SearchSymbolsRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_QueryAndFiltersMatchSymbols_WHEN_CallingExecuteAsync_THEN_ShouldReturnFilteredSymbols()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }

                private void FormatInternal()
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new SearchSymbolsTool();

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            Query = "Format",
            Kinds = ["Method"],
            Accessibilities = ["Public"],
            Namespace = "Sample",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().ContainSingle(symbol => symbol.DisplayName.Contains("Format()", StringComparison.Ordinal));
    }
}

