namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class WorkspaceQuerySelectorIntegrationTests
{
    [Fact]
    public async Task GIVEN_LinkedMultiTargetDocument_WHEN_UsingProjectDocumentSpanAndCopiedSelection_THEN_ShouldResolveDeterministically()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.MultiTargetLinked);

        try
        {
            var solutionPath = Path.Combine(target.WorkspaceRoot, "Sample.slnx");
            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = solutionPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            var workspaceSelector = AcceptanceWorkspaceIdentity.FromOpenResult(openResult).CreateSelector();
            var projectSelector = new Dictionary<string, object?>
            {
                ["path"] = "MultiTarget/MultiTarget.csproj",
                ["targetFramework"] = "net10.0",
            };
            var documentSelector = new Dictionary<string, object?>
            {
                ["project"] = projectSelector,
                ["path"] = "Shared/SharedFormatter.cs",
            };

            var projectResult = await target.CallToolAsync(
                "get-project-details",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["project"] = projectSelector,
                    ["includeDocuments"] = true,
                },
                TestContext.Current.CancellationToken);
            projectResult.IsError.Should().NotBeTrue();
            var project = AcceptanceProtocol.GetSuccessData(projectResult).GetProperty("project");
            project.GetProperty("name").GetString().Should().Contain("MultiTarget");
            project.GetProperty("targetFrameworks").EnumerateArray()
                .Select(static item => item.GetString())
                .Should()
                .Contain("net10.0");

            var ambiguousOutline = await target.CallToolAsync(
                "get-document-outline",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["document"] = new Dictionary<string, object?>
                    {
                        ["path"] = "Shared/SharedFormatter.cs",
                    },
                },
                TestContext.Current.CancellationToken);
            ambiguousOutline.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(ambiguousOutline).GetProperty("code").GetString().Should().Be("DocumentAmbiguous");

            var outlineResult = await target.CallToolAsync(
                "get-document-outline",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["document"] = documentSelector,
                },
                TestContext.Current.CancellationToken);
            outlineResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(outlineResult)
                .GetProperty("document")
                .GetProperty("path")
                .GetString()
                .Should()
                .Be("Shared/SharedFormatter.cs");

            var documentText = await File.ReadAllTextAsync(
                Path.Combine(target.WorkspaceRoot, "Shared", "SharedFormatter.cs"),
                TestContext.Current.CancellationToken);
            var symbolStart = documentText.IndexOf("SharedFormatter", StringComparison.Ordinal);

            var spanResult = await ResolveSymbolAsync(
                target,
                workspaceSelector,
                new Dictionary<string, object?>
                {
                    ["span"] = new Dictionary<string, object?>
                    {
                        ["document"] = documentSelector,
                        ["start"] = symbolStart,
                        ["length"] = "SharedFormatter".Length,
                    },
                });
            var copiedSelectionResult = await ResolveSymbolAsync(
                target,
                workspaceSelector,
                new Dictionary<string, object?>
                {
                    ["selection"] = new Dictionary<string, object?>
                    {
                        ["document"] = documentSelector,
                        ["selectedText"] = "SharedFormatter",
                        ["contextBefore"] = "public sealed class ",
                        ["contextAfter"] = "\r\n{",
                    },
                });

            spanResult.IsError.Should().NotBeTrue();
            copiedSelectionResult.IsError.Should().NotBeTrue();
            var spanSymbol = AcceptanceProtocol.GetSuccessData(spanResult).GetProperty("symbol");
            var selectionSymbol = AcceptanceProtocol.GetSuccessData(copiedSelectionResult).GetProperty("symbol");
            spanSymbol.GetProperty("documentationCommentId").GetString().Should().Be("T:Shared.SharedFormatter");
            selectionSymbol.GetProperty("documentationCommentId").GetString().Should().Be("T:Shared.SharedFormatter");

            var symbolInfoResult = await target.CallToolAsync(
                "get-symbol-info",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["symbol"] = new Dictionary<string, object?>
                    {
                        ["project"] = projectSelector,
                        ["documentationCommentId"] = "T:Shared.SharedFormatter",
                    },
                },
                TestContext.Current.CancellationToken);
            symbolInfoResult.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> ResolveSymbolAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> location)
    {
        return await target.CallToolAsync(
            "resolve-symbol",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["location"] = location,
            },
            TestContext.Current.CancellationToken);
    }
}
