using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class MutationPipelineIntegrationTests
{
    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingBundledMutations_THEN_ShouldStageRevisionsAndPreviewResultingContent()
    {
        await using var fixture = await InspectionSampleFixture.CreateAsync();
        await using var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        var startResult = await coordinator.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        var registry = BundledPluginCatalogueFactory.CreateCatalogue();

        var rename = await PluginToolTestHarness.InvokeAsync<MutationData>(coordinator, TestContext.Current.CancellationToken, registry, "rename-symbol", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.StateHolder",
            }),
            ["newName"] = JsonSerializer.SerializeToElement("SessionState"),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(openResult, startResult.Data!.Transaction!.Revision)),
        });
        var sortUsings = await PluginToolTestHarness.InvokeAsync<MutationData>(coordinator, TestContext.Current.CancellationToken, registry, "sort-usings", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Usings.cs",
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(openResult, rename.Data!.Transaction!.Revision)),
        });
        var formatDocument = await PluginToolTestHarness.InvokeAsync<MutationData>(coordinator, TestContext.Current.CancellationToken, registry, "format-document", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Usings.cs",
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(openResult, sortUsings.Data!.Transaction!.Revision)),
        });
        var transactionPreview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), TestContext.Current.CancellationToken);
        var usingsPreview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest
        {
            Document = new DocumentSelector
            {
                Path = "Usings.cs",
            },
            IncludeDiff = true,
        }, TestContext.Current.CancellationToken);
        var renamePreview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest
        {
            Document = new DocumentSelector
            {
                Path = "Formatting.cs",
            },
            IncludeDiff = true,
        }, TestContext.Current.CancellationToken);

        rename.Data!.Transaction!.Revision.Should().Be(1);
        sortUsings.Data!.Transaction!.Revision.Should().Be(2);
        formatDocument.Data!.Transaction!.Revision.Should().Be(3);
        transactionPreview.Data!.Transaction!.Revision.Should().Be(3);
        transactionPreview.Data.Documents.Should().Contain(static change => change.Document!.Path == "Formatting.cs");
        transactionPreview.Data.Documents.Should().Contain(static change => change.Document!.Path == "Usings.cs");
        string.Join(Environment.NewLine, usingsPreview.Data!.Diff!.Hunks.SelectMany(static hunk => hunk.Lines)).Should().Contain("public static string BuildText()");
        string.Join(Environment.NewLine, usingsPreview.Data.Diff.Hunks.SelectMany(static hunk => hunk.Lines)).Should().Contain("StringBuilder builder = new();");
        string.Join(Environment.NewLine, renamePreview.Data!.Diff!.Hunks.SelectMany(static hunk => hunk.Lines)).Should().Contain("SessionState");
    }
}
