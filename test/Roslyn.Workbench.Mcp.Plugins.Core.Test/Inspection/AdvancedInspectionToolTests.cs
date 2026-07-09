namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

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
