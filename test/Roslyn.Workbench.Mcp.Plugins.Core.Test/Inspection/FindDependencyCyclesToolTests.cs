namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDependencyCyclesToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnCycles()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindDependencyCyclesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-dependency-cycles", target, new FindDependencyCyclesRequest
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
        });

        result.Data!.Cycles.Should().Contain(static cycle => cycle.Nodes.Any(node => node.DisplayName.Contains("AlphaCycle", StringComparison.Ordinal)) && cycle.Nodes.Any(node => node.DisplayName.Contains("BetaCycle", StringComparison.Ordinal)));
    }
}
