namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class AnalyzeNullabilityToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnNullabilityFindings()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new AnalyzeNullabilityTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "analyze-nullability", target, new AnalyzeNullabilityRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "EnableNullable.cs",
                },
            },
        });

        result.Data!.Findings.Items.Select(static finding => finding.Diagnostic!.Id).Should().Contain("CS8602");
    }
}
