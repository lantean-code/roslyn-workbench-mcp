namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetDependencyGraphToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDependencyGraph()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetDependencyGraphTool();

        var result = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "get-dependency-graph", target, new GetDependencyGraphRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            },
            Granularity = "Type",
            MaxDepth = 2,
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Nodes.Items.Should().Contain(static node => node.DisplayName.Contains("FormatterCaller", StringComparison.Ordinal));
        result.Data.Nodes.Items.Should().Contain(static node => node.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        result.Data.Edges.Items.Should().Contain(static edge => edge.FromDisplayName.Contains("FormatterCaller", StringComparison.Ordinal) && edge.ToDisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
    }
}
