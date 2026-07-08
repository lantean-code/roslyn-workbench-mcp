namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetDiagnosticsToolTests
{
    [Fact]
    public async Task GIVEN_FilteredRequest_WHEN_ExecutingTool_THEN_ShouldReturnDiagnostics()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetDiagnosticsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-diagnostics", target, new GetDiagnosticsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            },
            Severities = ["Warning"],
            Ids = ["CS0219"],
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().ContainSingle(static diagnostic => diagnostic.Id == "CS0219");
    }
}
