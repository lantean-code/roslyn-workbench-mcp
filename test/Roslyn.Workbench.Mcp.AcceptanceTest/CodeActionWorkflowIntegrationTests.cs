using System.Text.Json;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class CodeActionWorkflowIntegrationTests
{
    private const string _declareAsNullableProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider";

    private static readonly string[] _nullableReturnDiagnosticIds = ["CS8603"];

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

            var actionIdText = action.GetProperty("actionId").GetString();
            Guid.TryParse(actionIdText, out var actionId).Should().BeTrue();

            var describeResult = await target.CallToolAsync(
                "describe-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = actionId,
                    ["expectedSnapshot"] = snapshot,
                },
                TestContext.Current.CancellationToken);

            describeResult.IsError.Should().NotBeTrue();
            var describedAction = AcceptanceProtocol.GetSuccessData(describeResult).GetProperty("descriptor");
            describedAction.GetProperty("actionId").GetGuid().Should().Be(actionId);
            describedAction.GetProperty("expiresAt").GetString().Should().Be(action.GetProperty("expiresAt").GetString());
            var describedLocation = describedAction.GetProperty("location");
            var listedLocation = action.GetProperty("location");
            describedLocation.GetRawText().Should().Be(listedLocation.GetRawText());

            var stageResult = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = actionId,
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

            var repeatedStageResult = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = actionId,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 1),
                },
                TestContext.Current.CancellationToken);

            repeatedStageResult.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(repeatedStageResult)
                .GetProperty("code")
                .GetString()
                .Should()
                .Be("ActionExpired");

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
    public async Task GIVEN_BroadRangeContainsEquivalentCodeFixes_WHEN_ListingAndStagingByReference_THEN_ShouldTargetEachPreciseLocation()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            const string documentPath = "CandidateCodeFixes.cs";
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var sourceText = await File.ReadAllTextAsync(
                Path.Combine(target.WorkspaceRoot, documentPath),
                TestContext.Current.CancellationToken);

            var locations = new AcceptanceLocationSelectorFactory(target.WorkspaceRoot);
            var broadLocation = locations.CreateSelection(
                documentPath,
                "internal static string DeclareAsNullable()",
                "internal static void DeclareLocalAsNullable()");

            var firstMethodStart = sourceText.IndexOf("DeclareAsNullable", StringComparison.Ordinal);
            var firstNullStart = sourceText.IndexOf(
                "return null;",
                firstMethodStart,
                StringComparison.Ordinal) + "return ".Length;

            var secondMethodStart = sourceText.IndexOf("DeclareSecondAsNullable", StringComparison.Ordinal);
            var secondNullStart = sourceText.IndexOf(
                "return null;",
                secondMethodStart,
                StringComparison.Ordinal) + "return ".Length;

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
            await StartTransactionAsync(target, workspaceSelector);

            var listResult = await target.CallToolAsync(
                "list-code-actions",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["location"] = broadLocation,
                    ["expectedSnapshot"] = snapshot,
                    ["includeRefactorings"] = false,
                    ["includeCodeFixes"] = true,
                    ["diagnosticIds"] = _nullableReturnDiagnosticIds,
                },
                TestContext.Current.CancellationToken);

            listResult.IsError.Should().NotBeTrue();
            var actions = new List<JsonElement>();
            var listedActions = AcceptanceProtocol.GetSuccessData(listResult).GetProperty("actions");
            foreach (var action in listedActions.EnumerateArray())
            {
                var providerId = action.GetProperty("providerId").GetString();
                if (providerId == _declareAsNullableProviderId)
                {
                    actions.Add(action);
                }
            }

            actions.Should().HaveCount(2);
            var actionStarts = new List<int>();
            foreach (var action in actions)
            {
                var location = action.GetProperty("location");
                var span = location.GetProperty("span");
                actionStarts.Add(span.GetProperty("start").GetInt32());
            }

            actionStarts.Should().Equal(firstNullStart, secondNullStart);

            await AssertStagesExpectedCodeFixAsync(
                target,
                workspace,
                workspaceSelector,
                actions[0],
                documentPath,
                "internal static string? DeclareAsNullable()");

            await StartTransactionAsync(target, workspaceSelector);
            await AssertStagesExpectedCodeFixAsync(
                target,
                workspace,
                workspaceSelector,
                actions[1],
                documentPath,
                "internal static string? DeclareSecondAsNullable()");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_UnknownCodeActionReference_WHEN_Staging_THEN_ShouldRejectAndRemainResponsive()
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
                    ["actionId"] = "11111111-1111-1111-1111-111111111111",
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

    private static async Task AssertStagesExpectedCodeFixAsync(
        AcceptanceProcessFixture target,
        AcceptanceWorkspaceIdentity workspace,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        JsonElement action,
        string documentPath,
        string expectedChangedText)
    {
        var stageResult = await target.CallToolAsync(
            "stage-code-fix",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["actionId"] = action.GetProperty("actionId").GetGuid(),
                ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
            },
            TestContext.Current.CancellationToken);

        stageResult.IsError.Should().NotBeTrue();
        var previewResult = await target.CallToolAsync(
            "transaction-preview",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["document"] = AcceptanceLocationSelectorFactory.CreateDocument(documentPath),
                ["includeDiff"] = true,
            },
            TestContext.Current.CancellationToken);

        previewResult.IsError.Should().NotBeTrue();
        var diffLines = new List<string?>();
        var preview = AcceptanceProtocol.GetSuccessData(previewResult);
        var hunks = preview.GetProperty("diff").GetProperty("hunks");
        foreach (var hunk in hunks.EnumerateArray())
        {
            foreach (var line in hunk.GetProperty("lines").EnumerateArray())
            {
                diffLines.Add(line.GetString());
            }
        }

        diffLines.Should().Contain(line => line != null && line.Contains(expectedChangedText, StringComparison.Ordinal));
        var rollbackResult = await target.CallToolAsync(
            "transaction-rollback",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);

        rollbackResult.IsError.Should().NotBeTrue();
    }

    private static async Task StartTransactionAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector)
    {
        var startResult = await target.CallToolAsync(
            "transaction-start",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);

        startResult.IsError.Should().NotBeTrue();
    }
}
