namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class FindUnusedSymbolsToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnUnusedCandidates()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindUnusedSymbolsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-unused-symbols", target, new FindUnusedSymbolsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "RemoveUnusedVariable.cs",
                },
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Should().Contain(static candidate => candidate.Symbol!.DisplayName.Contains("unused", StringComparison.Ordinal));
        result.Data.Candidates.Items.Should().Contain(static candidate => candidate.Reasons.Any(reason => reason.Contains("CS0219", StringComparison.Ordinal)));
    }
}
