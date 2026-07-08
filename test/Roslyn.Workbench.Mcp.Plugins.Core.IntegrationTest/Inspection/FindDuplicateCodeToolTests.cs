namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class FindDuplicateCodeToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDuplicateGroups()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindDuplicateCodeTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-duplicate-code", target, new FindDuplicateCodeRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            },
            MinimumStatements = 3,
        });

        result.Data!.Groups.Items.Should().Contain(static group => group.Occurrences.Any(occurrence => occurrence.Symbol!.DisplayName.Contains("DuplicateCodeSamples.ComputeOne", StringComparison.Ordinal)) && group.Occurrences.Any(occurrence => occurrence.Symbol!.DisplayName.Contains("DuplicateCodeSamples.ComputeTwo", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GIVEN_InvalidThreshold_WHEN_ExecutingTool_THEN_ShouldRejectRequest()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindDuplicateCodeTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-duplicate-code", target, new FindDuplicateCodeRequest
        {
            MinimumStatements = 0,
        });

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }
}
