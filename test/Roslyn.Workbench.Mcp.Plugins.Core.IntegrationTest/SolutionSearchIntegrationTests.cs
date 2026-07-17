namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SolutionSearchIntegrationTests
{
    [Fact]
    public async Task GIVEN_CrossProjectSolution_WHEN_SearchingRelationships_THEN_ShouldResolveAcrossProjectBoundary()
    {
        await using var fixture = await SolutionHierarchyFixture.CreateAsync();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.SolutionPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var implementations = await session.ExecuteQueryAsync<FindImplementationsRequest, ImplementationSearchData>(
            "find-implementations",
            new FindImplementationsRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "T:Sample.IMessageFormatter",
                },
            }, TestContext.Current.CancellationToken);
        var references = await session.ExecuteQueryAsync<FindReferencesRequest, ReferenceSearchData>(
            "find-references",
            new FindReferencesRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "T:Sample.IMessageFormatter",
                },
                IncludeDefinitions = false,
            }, TestContext.Current.CancellationToken);
        var callers = await session.ExecuteQueryAsync<FindCallersRequest, CallerSearchData>(
            "find-callers",
            new FindCallersRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "M:Sample.AppFormatter.Format(System.String)",
                },
            }, TestContext.Current.CancellationToken);
        var derivedTypes = await session.ExecuteQueryAsync<FindDerivedTypesRequest, DerivedTypesData>(
            "find-derived-types",
            new FindDerivedTypesRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "T:Sample.IMessageFormatter",
                },
            }, TestContext.Current.CancellationToken);
        var dependencies = await session.ExecuteQueryAsync<GetSymbolDependenciesRequest, SymbolDependenciesData>(
            "get-symbol-dependencies",
            new GetSymbolDependenciesRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "T:Sample.AppFormatter",
                },
            }, TestContext.Current.CancellationToken);
        var graph = await session.ExecuteQueryAsync<GetDependencyGraphRequest, DependencyGraphData>(
            "get-dependency-graph",
            new GetDependencyGraphRequest
            {
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Project,
                    Project = new ProjectSelector
                    {
                        Path = "App/App.csproj",
                    },
                },
                Granularity = "Type",
                MaxDepth = 2,
            }, TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        implementations.Data!.Implementations.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("AppFormatter", StringComparison.Ordinal));
        references.Data!.References.Items.Should().Contain(static reference => reference.Location != null && reference.Location.Document != null && reference.Location.Document.Path.EndsWith("AppFormatter.cs", StringComparison.Ordinal));
        callers.Data!.Callers.Items.Should().Contain(static caller => caller.Caller!.DisplayName.Contains("AppCaller.Call", StringComparison.Ordinal));
        derivedTypes.Data!.DerivedTypes.Items.Should().Contain(static node => node.Type!.DisplayName.Contains("AppFormatter", StringComparison.Ordinal));
        dependencies.Data!.Dependencies.Items.Should().Contain(static dependency => dependency.Symbol!.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
        graph.Data!.Edges.Items.Should().Contain(static edge => edge.FromDisplayName.Contains("AppCaller", StringComparison.Ordinal) && edge.ToDisplayName.Contains("AppFormatter", StringComparison.Ordinal));
    }
}
