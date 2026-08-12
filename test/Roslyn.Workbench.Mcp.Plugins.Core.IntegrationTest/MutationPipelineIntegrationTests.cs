namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class MutationPipelineIntegrationTests
{
    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingBundledMutations_THEN_ShouldStageRevisionsAndPreviewResultingContent()
    {
        using var fixture = InspectionSampleFixture.Create();
        var usingsPath = Path.Combine(fixture.WorkspaceRoot, "Usings.cs");
        var unformattedSource = """
            using Sample;
            using System.Text;
            using System;

            namespace Sample;

            public static class UsingSamples
            {
            public static string BuildText()
            {
            StringBuilder builder = new();
            builder.Append(nameof(FormatterBase));
            return builder.ToString();
            }
            }
            """.ReplaceLineEndings("\r\n");

        await File.WriteAllTextAsync(
            usingsPath,
            unformattedSource,
            TestContext.Current.CancellationToken);

        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var startResult = await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var rename = await session.ExecuteMutationAsync(
            "rename-symbol",
            new RenameSymbolRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "T:Sample.StateHolder",
                },
                NewName = "SessionState",
                ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(openResult, startResult.Data!.Transaction.Revision),
            }, TestContext.Current.CancellationToken);

        var formatDocument = await session.ExecuteMutationAsync(
            "format-document",
            new FormatDocumentRequest
            {
                Document = new DocumentSelector
                {
                    Path = "Usings.cs",
                },
                ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(openResult, rename.Data!.Transaction!.Revision),
            }, TestContext.Current.CancellationToken);

        var transactionPreview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);
        var usingsPreview = await coordinator.PreviewTransactionAsync(
            TestContext.Current.CancellationToken,
            document: new DocumentSelector
            {
                Path = "Usings.cs",
            },
            includeDiff: true);

        var renamePreview = await coordinator.PreviewTransactionAsync(
            TestContext.Current.CancellationToken,
            document: new DocumentSelector
            {
                Path = "Formatting.cs",
            },
            includeDiff: true);

        rename.Data!.Transaction!.Revision.Should().Be(1);
        formatDocument.Data!.Transaction!.Revision.Should().Be(2);
        transactionPreview.Data!.Transaction!.Revision.Should().Be(2);
        transactionPreview.Data.Documents.Should().Contain(static change => change.Document!.Path == "Formatting.cs");
        transactionPreview.Data.Documents.Should().Contain(static change => change.Document!.Path == "Usings.cs");
        string.Join(Environment.NewLine, usingsPreview.Data!.Diff!.Hunks.SelectMany(static hunk => hunk.Lines)).Should().Contain("public static string BuildText()");
        string.Join(Environment.NewLine, usingsPreview.Data.Diff.Hunks.SelectMany(static hunk => hunk.Lines)).Should().Contain("StringBuilder builder = new();");
        string.Join(Environment.NewLine, renamePreview.Data!.Diff!.Hunks.SelectMany(static hunk => hunk.Lines)).Should().Contain("SessionState");
    }
}
