namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class WorkspacePhysicalContainmentIntegrationTests
{
    [Fact]
    public async Task GIVEN_SourceDocumentLinkEscapesWorkspace_WHEN_QueryingAndMutating_THEN_ShouldRemainReadOnly()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken);

        try
        {
            var externalDirectory = Path.Combine(target.ScenarioRoot, "external-sources");
            var externalDocumentPath = Path.Combine(externalDirectory, "Escaped.cs");
            var linkedDirectory = Path.Combine(target.WorkspaceRoot, "Linked");
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            Directory.CreateDirectory(externalDirectory);
            await File.WriteAllTextAsync(
                externalDocumentPath,
                "public sealed class EscapedType { }",
                TestContext.Current.CancellationToken);

            await WriteProjectAsync(projectPath, "Linked/Escaped.cs");
            await AcceptanceDirectoryLink.CreateAsync(
                linkedDirectory,
                externalDirectory,
                TestContext.Current.CancellationToken);

            var openResult = await OpenWorkspaceAsync(target, projectPath);

            openResult.IsError.Should().NotBeTrue();
            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var workspaceSelector = workspace.CreateSelector();
            var searchResult = await target.CallToolAsync(
                "search-symbols",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["query"] = "EscapedType",
                },
                TestContext.Current.CancellationToken);

            searchResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(searchResult)
                .GetProperty("symbols")
                .GetProperty("items")
                .GetArrayLength()
                .Should()
                .Be(1);

            var startResult = await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            startResult.IsError.Should().NotBeTrue();
            var renameResult = await target.CallToolAsync(
                "rename-symbol",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["symbol"] = new Dictionary<string, object?>
                    {
                        ["documentationCommentId"] = "T:EscapedType",
                    },
                    ["newName"] = "RenamedEscapedType",
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            renameResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(renameResult)
                .GetProperty("code")
                .GetString()
                .Should()
                .Be("UnsupportedChange");

            var externalDocumentContent = await File.ReadAllTextAsync(
                externalDocumentPath,
                TestContext.Current.CancellationToken);

            externalDocumentContent.Should().Contain("EscapedType").And.NotContain("RenamedEscapedType");

            var rollbackResult = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            rollbackResult.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_SourceDocumentLinkRemainsInsideWorkspace_WHEN_Opening_THEN_ShouldLoadLinkedDocument()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken);

        try
        {
            var sourceDirectory = Path.Combine(target.WorkspaceRoot, "Sources");
            var linkedDirectory = Path.Combine(target.WorkspaceRoot, "Linked");
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            Directory.CreateDirectory(sourceDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(sourceDirectory, "Contained.cs"),
                "public sealed class ContainedType { }",
                TestContext.Current.CancellationToken);

            await WriteProjectAsync(projectPath, "Linked/Contained.cs");
            await AcceptanceDirectoryLink.CreateAsync(
                linkedDirectory,
                sourceDirectory,
                TestContext.Current.CancellationToken);

            var openResult = await OpenWorkspaceAsync(target, projectPath);

            openResult.IsError.Should().NotBeTrue();
            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var searchResult = await target.CallToolAsync(
                "search-symbols",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace.CreateSelector(),
                    ["query"] = "ContainedType",
                },
                TestContext.Current.CancellationToken);

            searchResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(searchResult)
                .GetProperty("symbols")
                .GetProperty("items")
                .GetArrayLength()
                .Should()
                .Be(1);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_CoordinationDirectoryLinkEscapesWorkspace_WHEN_Committing_THEN_ShouldRejectWithoutExternalWrites()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken);

        try
        {
            var externalDirectory = Path.Combine(target.ScenarioRoot, "external-coordination");
            var coordinationDirectory = Path.Combine(target.WorkspaceRoot, ".vs");
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var documentPath = Path.Combine(target.WorkspaceRoot, "Class1.cs");
            Directory.CreateDirectory(externalDirectory);
            await AcceptanceDirectoryLink.CreateAsync(
                coordinationDirectory,
                externalDirectory,
                TestContext.Current.CancellationToken);

            var openResult = await OpenWorkspaceAsync(target, projectPath);

            openResult.IsError.Should().NotBeTrue();
            Directory.EnumerateFileSystemEntries(externalDirectory).Should().BeEmpty();
            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var workspaceSelector = workspace.CreateSelector();
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
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            startResult.IsError.Should().NotBeTrue();
            renameResult.IsError.Should().NotBeTrue();
            var commitResult = await target.CallToolAsync(
                "transaction-commit",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 1),
                },
                TestContext.Current.CancellationToken);

            commitResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(commitResult)
                .GetProperty("code")
                .GetString()
                .Should()
                .Be("CommitLockFailed");

            Directory.EnumerateFileSystemEntries(externalDirectory).Should().BeEmpty();
            (await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken))
                .Should()
                .Contain("Class1")
                .And
                .NotContain("RenamedClass");

            var rollbackResult = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            rollbackResult.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string projectPath)
    {
        return await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["path"] = projectPath,
                ["workspaceRoot"] = target.WorkspaceRoot,
            },
            TestContext.Current.CancellationToken);
    }

    private static Task WriteProjectAsync(string projectPath, string compilePath)
    {
        var lines = new[]
        {
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "  <PropertyGroup>",
            "    <TargetFramework>net10.0</TargetFramework>",
            "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
            "  </PropertyGroup>",
            "  <ItemGroup>",
            $"    <Compile Include=\"{compilePath}\" />",
            "  </ItemGroup>",
            "</Project>",
            string.Empty,
        };

        return File.WriteAllTextAsync(
            projectPath,
            string.Join(Environment.NewLine, lines),
            TestContext.Current.CancellationToken);
    }
}
