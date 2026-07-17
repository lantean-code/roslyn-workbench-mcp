using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SolutionSearchIntegrationTests
{
    [Fact]
    public async Task GIVEN_CrossProjectSolution_WHEN_SearchingRelationships_THEN_ShouldResolveAcrossProjectBoundary()
    {
        await using var fixture = await SolutionHierarchyFixture.CreateAsync();
        await using var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.SolutionPath,
        }, TestContext.Current.CancellationToken);
        var registry = BundledPluginCatalogueFactory.CreateCatalogue();

        var implementations = await PluginToolTestHarness.InvokeAsync<ImplementationSearchData>(coordinator, TestContext.Current.CancellationToken, registry, "find-implementations", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.IMessageFormatter",
            }),
        });
        var references = await PluginToolTestHarness.InvokeAsync<ReferenceSearchData>(coordinator, TestContext.Current.CancellationToken, registry, "find-references", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.IMessageFormatter",
            }),
            ["includeDefinitions"] = JsonSerializer.SerializeToElement(false),
        });
        var callers = await PluginToolTestHarness.InvokeAsync<CallerSearchData>(coordinator, TestContext.Current.CancellationToken, registry, "find-callers", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.AppFormatter.Format(System.String)",
            }),
        });
        var derivedTypes = await PluginToolTestHarness.InvokeAsync<DerivedTypesData>(coordinator, TestContext.Current.CancellationToken, registry, "find-derived-types", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.IMessageFormatter",
            }),
        });
        var dependencies = await PluginToolTestHarness.InvokeAsync<SymbolDependenciesData>(coordinator, TestContext.Current.CancellationToken, registry, "get-symbol-dependencies", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.AppFormatter",
            }),
        });
        var graph = await PluginToolTestHarness.InvokeAsync<DependencyGraphData>(coordinator, TestContext.Current.CancellationToken, registry, "get-dependency-graph", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "App/App.csproj",
                },
            }),
            ["granularity"] = JsonSerializer.SerializeToElement("Type"),
            ["maxDepth"] = JsonSerializer.SerializeToElement(2),
        });

        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);
        implementations.Data!.Implementations.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("AppFormatter", StringComparison.Ordinal));
        references.Data!.References.Items.Should().Contain(static reference => reference.Location != null && reference.Location.Document != null && reference.Location.Document.Path.EndsWith("AppFormatter.cs", StringComparison.Ordinal));
        callers.Data!.Callers.Items.Should().Contain(static caller => caller.Caller!.DisplayName.Contains("AppCaller.Call", StringComparison.Ordinal));
        derivedTypes.Data!.DerivedTypes.Items.Should().Contain(static node => node.Type!.DisplayName.Contains("AppFormatter", StringComparison.Ordinal));
        dependencies.Data!.Dependencies.Items.Should().Contain(static dependency => dependency.Symbol!.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
        graph.Data!.Edges.Items.Should().Contain(static edge => edge.FromDisplayName.Contains("AppCaller", StringComparison.Ordinal) && edge.ToDisplayName.Contains("AppFormatter", StringComparison.Ordinal));
    }
}
