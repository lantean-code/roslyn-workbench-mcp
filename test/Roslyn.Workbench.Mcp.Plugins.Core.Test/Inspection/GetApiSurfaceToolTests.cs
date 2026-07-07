namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetApiSurfaceToolTests
{
    [Fact]
    public async Task GIVEN_UnableToResolveDocument_WHEN_CallingExecute_THEN_ShouldReturnRejection()
    {
        var expected = PluginExecutionResult<ApiSurfaceData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        }, RequiredAction.ResolveTargetAgain);
        var requestResolver = new Mock<IToolRequestResolver>();
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver.Object)
            .Build();
        var context = new QueryContextBuilder()
            .WithToolExecutionServices(services)
            .Build();
        var target = new GetApiSurfaceTool();

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Missing.cs",
                },
            },
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_UnableToParseAccessibility_WHEN_CallingExecute_THEN_ShouldReturnInvalidRequest()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver.Object)
            .Build();
        var context = new QueryContextBuilder()
            .WithToolExecutionServices(services)
            .Build();
        var target = new GetApiSurfaceTool();

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>
            {
                Value = Array.Empty<Document>(),
            });

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            MinimumAccessibility = "Private",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_LowDefaultMaxResults_WHEN_CallingExecute_THEN_ShouldBoundResults()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public interface IMessageFormatter
            {
            }

            public sealed class GreetingFormatter
            {
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithDefaultMaxResults(1)
            .Build();
        var target = new GetApiSurfaceTool();

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(1);
        result.Data.Symbols.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ProjectScopeWithPublicType_WHEN_CallingExecute_THEN_ShouldReturnExportedSymbols()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public interface IMessageFormatter
            {
            }

            public sealed class GreetingFormatter : IMessageFormatter
            {
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GetApiSurfaceTool();

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().Contain(static symbol => symbol.Symbol!.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        result.Data.Symbols.Items.Should().Contain(static symbol => symbol.Symbol!.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
    }
}
