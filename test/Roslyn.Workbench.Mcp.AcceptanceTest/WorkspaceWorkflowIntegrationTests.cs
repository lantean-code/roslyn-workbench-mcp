using System.Text.Json;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class WorkspaceWorkflowIntegrationTests
{
    [Fact]
    public async Task GIVEN_CopiedWorkspace_WHEN_UsingLifecycleAndQueryTools_THEN_ShouldReturnSemanticResultsAndCloseWorkspace()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["alias"] = "acceptance-query",
                    ["path"] = projectPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            openResult.IsError.Should().NotBeTrue();
            var open = AcceptanceProtocol.GetSuccessData(openResult);
            var workspaceIdentity = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var workspace = open.GetProperty("workspace");

            workspaceIdentity.WorkspaceId.Should().NotBe(Guid.Empty);
            workspace.GetProperty("loadedPath").GetString().Should().Be(projectPath);
            open.GetProperty("projectCount").GetInt32().Should().Be(1);
            open.GetProperty("documentCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);

            var workspaceSelector = workspaceIdentity.CreateSelector();

            var listResult = await target.CallToolAsync(
                "workspace-list",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken);

            var statusResult = await target.CallToolAsync(
                "workspace-status",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            var searchResult = await target.CallToolAsync(
                "search-symbols",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["query"] = "Class1",
                    ["symbolsLimit"] = 10,
                },
                TestContext.Current.CancellationToken);

            listResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(listResult).GetProperty("workspaces").GetArrayLength().Should().Be(1);
            statusResult.IsError.Should().NotBeTrue();
            var status = AcceptanceProtocol.GetSuccessData(statusResult);
            status.GetProperty("state").GetString().Should().Be("Ready");
            status.GetProperty("loadDiagnostics").ValueKind.Should().Be(JsonValueKind.Array);
            searchResult.IsError.Should().NotBeTrue();

            var symbols = AcceptanceProtocol.GetSuccessData(searchResult).GetProperty("symbols").GetProperty("items");
            symbols.GetArrayLength().Should().Be(1);
            symbols[0].GetProperty("displayName").GetString().Should().Be("Sample.Class1");
            symbols[0].GetProperty("kind").GetString().Should().Be("NamedType");

            var closeResult = await target.CallToolAsync(
                "workspace-close",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var closedListResult = await target.CallToolAsync(
                "workspace-list",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken);

            var closedStatusResult = await target.CallToolAsync(
                "workspace-status",
                new Dictionary<string, object?>(),
                TestContext.Current.CancellationToken);

            closeResult.IsError.Should().NotBeTrue();
            closeResult.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
            AcceptanceProtocol.GetSuccessData(closedListResult).GetProperty("workspaces").GetArrayLength().Should().Be(0);
            closedStatusResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(closedStatusResult).GetProperty("code").GetString().Should().Be("WorkspaceNotOpen");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_RenamingAndCommitting_THEN_ShouldUpdateDiskAndPromoteWorkspaceState()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = projectPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var workspaceSelector = workspace.CreateSelector();
            var initialSnapshot = workspace.CreateSnapshot(transactionRevision: 0);

            var startResult = await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var renameResult = await target.CallToolAsync(
                "rename-symbol",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["symbol"] = new Dictionary<string, object?>
                    {
                        ["documentationCommentId"] = "T:Sample.Class1",
                    },
                    ["newName"] = "RenamedClass",
                    ["expectedSnapshot"] = initialSnapshot,
                },
                TestContext.Current.CancellationToken);

            var previewResult = await target.CallToolAsync(
                "transaction-preview",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            startResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(startResult).GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(0);
            renameResult.IsError.Should().NotBeTrue();
            var rename = AcceptanceProtocol.GetSuccessData(renameResult);
            rename.GetProperty("staged").GetBoolean().Should().BeTrue();
            rename.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
            rename.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
            previewResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(previewResult).GetProperty("documents").GetArrayLength().Should().Be(1);

            var commitResult = await target.CallToolAsync(
                "transaction-commit",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 1),
                },
                TestContext.Current.CancellationToken);

            commitResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(commitResult).GetProperty("committed").GetBoolean().Should().BeTrue();
            var committedText = await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken);
            committedText.Should().Be(
                "namespace Sample;\r\n\r\npublic sealed class RenamedClass\r\n{\r\n}");

            var searchResult = await target.CallToolAsync(
                "search-symbols",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["query"] = "RenamedClass",
                },
                TestContext.Current.CancellationToken);

            searchResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(searchResult)
                .GetProperty("symbols")
                .GetProperty("items")[0]
                .GetProperty("displayName")
                .GetString()
                .Should()
                .Be("Sample.RenamedClass");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }
}
