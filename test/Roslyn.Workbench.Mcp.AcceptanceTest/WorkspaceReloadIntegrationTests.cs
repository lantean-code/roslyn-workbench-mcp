namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class WorkspaceReloadIntegrationTests
{
    [Fact]
    public async Task GIVEN_WarmedQueryAndExternalEdit_WHEN_ReloadingWorkspace_THEN_ShouldRejectStaleWorkAndRefreshEpochAndResults()
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

            var initialWorkspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var workspaceSelector = initialWorkspace.CreateSelector();
            var initialSearch = await SearchAsync(target, workspaceSelector, "Class1");
            initialSearch.IsError.Should().NotBeTrue();

            var updatedText = "namespace Sample;\r\n\r\npublic sealed class Class2\r\n{\r\n}\r\n";
            await File.WriteAllTextAsync(documentPath, updatedText, TestContext.Current.CancellationToken);

            var staleSearch = await SearchAsync(target, workspaceSelector, "Class1");
            staleSearch.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(staleSearch).GetProperty("code").GetString().Should().Be("WorkspaceOutOfDate");
            staleSearch.StructuredContent!.Value.GetProperty("next").GetString().Should().Be("ReloadWorkspace");

            var statusResult = await target.CallToolAsync(
                "workspace-status",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var statusData = AcceptanceProtocol.GetSuccessData(statusResult);
            statusData.GetProperty("state").GetString().Should().Be("WorkspaceOutOfDate");

            var externalChange = statusData.GetProperty("externalChange");
            var detectionSource = externalChange.GetProperty("detectionSource").GetString();
            externalChange.GetProperty("path").GetString().Should().Be(documentPath);

            if (detectionSource == "FileSystemWatcher")
            {
                externalChange.GetProperty("kind").GetString().Should().Be("Changed");
            }
            else
            {
                detectionSource.Should().Be("MetadataPolling");
                externalChange.GetProperty("kind").GetString().Should().Be("MetadataChanged");
            }

            var reloadResult = await target.CallToolAsync(
                "workspace-reload",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            reloadResult.IsError.Should().NotBeTrue();
            var reloadedWorkspace = AcceptanceProtocol.GetSuccessData(reloadResult).GetProperty("workspace");
            reloadedWorkspace.GetProperty("workspaceEpoch").GetInt64().Should().BeGreaterThan(initialWorkspace.WorkspaceEpoch);

            var refreshedSearch = await SearchAsync(target, workspaceSelector, "Class2");
            refreshedSearch.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(refreshedSearch)
                .GetProperty("symbols")
                .GetProperty("items")[0]
                .GetProperty("displayName")
                .GetString()
                .Should()
                .Be("Sample.Class2");

            var staleSnapshotResult = await target.CallToolAsync(
                "get-code-context",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["location"] = new Dictionary<string, object?>
                    {
                        ["span"] = new Dictionary<string, object?>
                        {
                            ["document"] = new Dictionary<string, object?>
                            {
                                ["path"] = "Class1.cs",
                            },
                            ["start"] = updatedText.IndexOf("Class2", StringComparison.Ordinal),
                            ["length"] = "Class2".Length,
                        },
                    },
                    ["expectedSnapshot"] = initialWorkspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            staleSnapshotResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(staleSnapshotResult).GetProperty("code").GetString().Should().Be("SnapshotMismatch");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_GeneratedOutputChanges_WHEN_QueryingWorkspace_THEN_ShouldRemainReady()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var generatedDirectory = Path.Combine(target.WorkspaceRoot, "obj", "Debug", "net10.0");
            var generatedDocumentPath = Path.Combine(generatedDirectory, "Sample.AssemblyInfo.cs");
            Directory.CreateDirectory(generatedDirectory);
            await File.WriteAllTextAsync(
                generatedDocumentPath,
                "// Initial generated output.\r\n",
                TestContext.Current.CancellationToken);

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
            var initialSearch = await SearchAsync(target, workspaceSelector, "Class1");
            initialSearch.IsError.Should().NotBeTrue();

            await File.WriteAllTextAsync(
                generatedDocumentPath,
                "// Updated generated output.\r\n",
                TestContext.Current.CancellationToken);

            var refreshedSearch = await SearchAsync(target, workspaceSelector, "Class1");
            refreshedSearch.IsError.Should().NotBeTrue();

            var statusResult = await target.CallToolAsync(
                "workspace-status",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var statusData = AcceptanceProtocol.GetSuccessData(statusResult);
            statusData.GetProperty("state").GetString().Should().Be("Ready");
            statusData.TryGetProperty("externalChange", out _).Should().BeFalse();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> SearchAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string query)
    {
        return await target.CallToolAsync(
            "search-symbols",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["query"] = query,
            },
            TestContext.Current.CancellationToken);
    }
}
