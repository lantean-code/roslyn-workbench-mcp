using System.Text.Json;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class BuiltInCodeActionStagingIntegrationTests
{
    [Fact]
    public async Task GIVEN_BuiltInCodeFixProvider_WHEN_RemovingUnusedUsings_THEN_ShouldStageRepresentativeBuiltInMutation()
    {
        await using var fixture = await InspectionSampleFixture.CreateAsync();
        await using var coordinator = BundledCoreToolTestHarness.CreateBuiltInCodeActionCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        var result = await CodeActionToolTestHarness.InvokeAsync<MutationData>(coordinator, TestContext.Current.CancellationToken, "remove-unused-usings", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Usings.cs",
                },
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(open, 0)),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest
        {
            IncludeDiff = true,
            Document = new DocumentSelector
            {
                Path = "Usings.cs",
            },
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Usings.cs");
        preview.Data.Diff.Should().NotBeNull();
        preview.Data.Diff!.Hunks.Should().NotBeEmpty();
    }
}
