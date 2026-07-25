namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class CodeActionWorkflowIntegrationTests
{
    private const int _maximumCodeActionTokenLength = 256 * 1024;

    [Fact]
    public async Task GIVEN_BuiltInCodeAction_WHEN_ListingStagingAndRollingBack_THEN_ShouldPreservePublicCodeActionBoundary()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var documentPath = Path.Combine(target.WorkspaceRoot, "RawString.cs");
            var originalBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);
            var sourceText = await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken);
            var stringLiteralStart = sourceText.IndexOf("\"raw\"", StringComparison.Ordinal);
            stringLiteralStart.Should().BeGreaterThanOrEqualTo(0);

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
            var snapshot = workspace.CreateSnapshot(transactionRevision: 0);

            var startResult = await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var listResult = await target.CallToolAsync(
                "list-code-actions",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["location"] = new Dictionary<string, object?>
                    {
                        ["span"] = new Dictionary<string, object?>
                        {
                            ["document"] = new Dictionary<string, object?>
                            {
                                ["path"] = "RawString.cs",
                            },
                            ["start"] = stringLiteralStart,
                            ["length"] = 0,
                        },
                    },
                    ["expectedSnapshot"] = snapshot,
                    ["includeRefactorings"] = true,
                    ["includeCodeFixes"] = false,
                },
                TestContext.Current.CancellationToken);

            startResult.IsError.Should().NotBeTrue();
            listResult.IsError.Should().NotBeTrue();
            var actions = AcceptanceProtocol.GetSuccessData(listResult).GetProperty("actions").EnumerateArray().ToArray();
            var action = actions.Single(static candidate => candidate.GetProperty("title").GetString() == "Convert to raw string");
            action.GetProperty("providerId").GetString().Should().Be(
                "Microsoft.CodeAnalysis.CSharp.ConvertToRawString.ConvertStringToRawStringCodeRefactoringProvider");

            action.GetProperty("actionId").GetString().Should().NotBeNullOrWhiteSpace();

            var stageResult = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = action.GetProperty("actionId").GetString(),
                    ["expectedSnapshot"] = snapshot,
                },
                TestContext.Current.CancellationToken);

            var previewResult = await target.CallToolAsync(
                "transaction-preview",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            stageResult.IsError.Should().NotBeTrue();
            var stage = AcceptanceProtocol.GetSuccessData(stageResult);
            stage.GetProperty("staged").GetBoolean().Should().BeTrue();
            stage.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
            stage.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1);
            previewResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(previewResult)
                .GetProperty("documents")
                .EnumerateArray()
                .Should()
                .ContainSingle(change => change.GetProperty("document").GetProperty("path").GetString() == "RawString.cs");

            var rollbackResult = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var currentBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);

            rollbackResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(rollbackResult).GetProperty("state").GetString().Should().Be("Ready");
            currentBytes.Should().Equal(originalBytes);

            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            var pluginIds = AcceptanceProtocol.GetSuccessData(statusResult)
                .GetProperty("plugins")
                .EnumerateArray()
                .Select(static plugin => plugin.GetProperty("pluginId").GetString())
                .ToArray();

            pluginIds.Should().Contain("roslyn.workbench.core");
            pluginIds.Should().NotContain("roslyn.workbench.codeactions");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_OversizedCodeActionToken_WHEN_Staging_THEN_ShouldRejectAndRemainResponsive()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
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
            var snapshot = workspace.CreateSnapshot(transactionRevision: 0);

            await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var stageResult = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = new string('A', _maximumCodeActionTokenLength + 1),
                    ["expectedSnapshot"] = snapshot,
                },
                TestContext.Current.CancellationToken);

            stageResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(stageResult)
                .GetProperty("code")
                .GetString()
                .Should()
                .Be("ActionExpired");

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
}
