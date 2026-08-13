using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class CodeActionWorkflowIntegrationTests
{
    private const int _codeFixKind = 1;
    private const int _refactoringKind = 2;

    private static readonly string[] _builtInIdeDiagnosticIds = ["IDE0003"];
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
                    ["document"] = AcceptanceLocationSelectorFactory.CreateDocument("RawString.cs"),
                    ["range"] = new Dictionary<string, object?>
                    {
                        ["start"] = stringLiteralStart,
                        ["length"] = 0,
                    },
                    ["expectedSnapshot"] = snapshot,
                    ["kinds"] = _refactoringKind,
                },
                TestContext.Current.CancellationToken);

            startResult.IsError.Should().NotBeTrue();
            listResult.IsError.Should().NotBeTrue(
                listResult.IsError == true
                    ? AcceptanceProtocol.GetError(listResult).GetRawText()
                    : string.Empty);
            var actions = AcceptanceProtocol.GetSuccessData(listResult)
                .GetProperty("actions")
                .GetProperty("items")
                .EnumerateArray()
                .ToArray();
            var action = actions.Single(static candidate => candidate.GetProperty("title").GetString() == "Convert to raw string");

            var actionIdText = action.GetProperty("actionId").GetString();
            Guid.TryParse(actionIdText, out var actionId).Should().BeTrue();

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
            var broadRange = locations.CreateRange(
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
            await StartTransactionAsync(target, workspaceSelector);

            var actions = await ListNullableReturnActionsAsync(
                target,
                workspaceSelector,
                documentPath,
                broadRange,
                workspace.CreateSnapshot(transactionRevision: 0));

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
            var rediscoveredActions = await ListNullableReturnActionsAsync(
                target,
                workspaceSelector,
                documentPath,
                broadRange,
                workspace.CreateSnapshot(transactionRevision: 0));
            await AssertStagesExpectedCodeFixAsync(
                target,
                workspace,
                workspaceSelector,
                rediscoveredActions[1],
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

    [Fact]
    public async Task GIVEN_DocumentSelectionAndCaretRequests_WHEN_ListingActions_THEN_ShouldPublishConciseLocationAwareResults()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var documentPath = Path.Combine(target.WorkspaceRoot, "CandidateRefactorings.cs");
            var sourceText = await File.ReadAllTextAsync(documentPath, TestContext.Current.CancellationToken);
            var typeStart = sourceText.IndexOf("internal sealed class MethodPropertyCandidate", StringComparison.Ordinal);
            var methodStart = sourceText.IndexOf("public int GetValue()", typeStart, StringComparison.Ordinal);
            typeStart.Should().BeGreaterThanOrEqualTo(0);
            methodStart.Should().BeGreaterThanOrEqualTo(0);

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

            var documentResult = await ListRefactoringsAsync(
                target,
                workspaceSelector,
                "CandidateRefactorings.cs",
                workspace.CreateSnapshot(transactionRevision: null));
            var selectionResult = await ListRefactoringsAsync(
                target,
                workspaceSelector,
                "CandidateRefactorings.cs",
                workspace.CreateSnapshot(transactionRevision: null),
                new Dictionary<string, object?>
                {
                    ["start"] = typeStart,
                    ["length"] = "internal sealed class MethodPropertyCandidate".Length,
                });
            var caretResult = await ListRefactoringsAsync(
                target,
                workspaceSelector,
                "CandidateRefactorings.cs",
                workspace.CreateSnapshot(transactionRevision: null),
                new Dictionary<string, object?>
                {
                    ["start"] = methodStart,
                    ["length"] = 0,
                });

            AssertConciseActionList(documentResult, "CandidateRefactorings.cs");
            AssertConciseActionList(selectionResult, "CandidateRefactorings.cs");
            AssertConciseActionList(caretResult, "CandidateRefactorings.cs");
            AcceptanceProtocol.GetSuccessData(selectionResult)
                .GetProperty("actions")
                .GetProperty("items")
                .GetArrayLength()
                .Should()
                .BeGreaterThan(0);
            AcceptanceProtocol.GetSuccessData(caretResult)
                .GetProperty("actions")
                .GetProperty("items")
                .GetArrayLength()
                .Should()
                .BeGreaterThan(0);
            AcceptanceProtocol.GetSuccessData(caretResult)
                .GetProperty("actions")
                .GetProperty("items")
                .EnumerateArray()
                .Select(static action => action.GetProperty("title").GetString())
                .Should()
                .NotContain(title =>
                    title != null
                    && (title.Contains("Change signature", StringComparison.OrdinalIgnoreCase)
                        || title.Contains("Generate overrides", StringComparison.OrdinalIgnoreCase)));
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_BuiltInIdeCodeFix_WHEN_PreparingAndStagingFixAll_THEN_ShouldRemainReadOnlyUntilStandardStaging()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var documentPath = Path.Combine(target.WorkspaceRoot, "SimplifyThisOrMe.cs");
            var originalBytes = await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken);
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
            await StartTransactionAsync(target, workspaceSelector);

            var listResult = await target.CallToolAsync(
                "list-code-actions",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["document"] = AcceptanceLocationSelectorFactory.CreateDocument("SimplifyThisOrMe.cs"),
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                    ["kinds"] = _codeFixKind,
                    ["diagnosticIds"] = _builtInIdeDiagnosticIds,
                },
                TestContext.Current.CancellationToken);

            listResult.IsError.Should().NotBeTrue(
                listResult.IsError == true
                    ? AcceptanceProtocol.GetError(listResult).GetRawText()
                    : string.Empty);
            var action = AcceptanceProtocol.GetSuccessData(listResult)
                .GetProperty("actions")
                .GetProperty("items")
                .EnumerateArray()
                .Single(candidate =>
                    candidate.GetProperty("diagnostics")
                        .GetProperty("items")
                        .EnumerateArray()
                        .Any(diagnostic => diagnostic.GetProperty("id").GetString() == "IDE0003")
                    && candidate.TryGetProperty("fixAllScopes", out var scopes)
                    && scopes.EnumerateArray().Any(scope => scope.GetInt32() == 0));

            var prepareResult = await target.CallToolAsync(
                "prepare-fix-all",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = action.GetProperty("actionId").GetGuid(),
                    ["scope"] = 0,
                    ["maxChanges"] = 10,
                    ["affectedDocumentsLimit"] = 10,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            prepareResult.IsError.Should().NotBeTrue(
                prepareResult.IsError == true
                    ? AcceptanceProtocol.GetError(prepareResult).GetRawText()
                    : string.Empty);
            var prepared = AcceptanceProtocol.GetSuccessData(prepareResult);
            prepared.GetProperty("scope").GetInt32().Should().Be(0);
            prepared.GetProperty("affectedDocuments")
                .GetProperty("items")
                .EnumerateArray()
                .Should()
                .ContainSingle(document => document.GetProperty("path").GetString() == "SimplifyThisOrMe.cs");
            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);

            var previewBeforeStage = await target.CallToolAsync(
                "transaction-preview",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            AcceptanceProtocol.GetSuccessData(previewBeforeStage)
                .GetProperty("documents")
                .GetArrayLength()
                .Should()
                .Be(0);

            var stageResult = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = prepared.GetProperty("actionId").GetGuid(),
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            stageResult.IsError.Should().NotBeTrue(
                stageResult.IsError == true
                    ? AcceptanceProtocol.GetError(stageResult).GetRawText()
                    : string.Empty);
            AcceptanceProtocol.GetSuccessData(stageResult)
                .GetProperty("transaction")
                .GetProperty("revision")
                .GetInt32()
                .Should()
                .Be(1);

            var previewAfterStage = await target.CallToolAsync(
                "transaction-preview",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            AcceptanceProtocol.GetSuccessData(previewAfterStage)
                .GetProperty("documents")
                .EnumerateArray()
                .Should()
                .ContainSingle(change => change.GetProperty("document").GetProperty("path").GetString() == "SimplifyThisOrMe.cs");

            var rollbackResult = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            rollbackResult.IsError.Should().NotBeTrue();
            (await File.ReadAllBytesAsync(documentPath, TestContext.Current.CancellationToken)).Should().Equal(originalBytes);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_ExpiredAndStaleCodeActionReferences_WHEN_Staging_THEN_ShouldRequireRediscovery()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample,
            environmentVariables: new Dictionary<string, string?>
            {
                ["ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_LIFETIME"] = "00:00:05",
            });

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var sourceText = await File.ReadAllTextAsync(
                Path.Combine(target.WorkspaceRoot, "RawString.cs"),
                TestContext.Current.CancellationToken);
            var stringLiteralStart = sourceText.IndexOf("\"raw\"", StringComparison.Ordinal);
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
            await StartTransactionAsync(target, workspaceSelector);

            var firstList = await ListRawStringActionsAsync(
                target,
                workspaceSelector,
                stringLiteralStart,
                workspace.CreateSnapshot(transactionRevision: 0));
            var secondList = await ListRawStringActionsAsync(
                target,
                workspaceSelector,
                stringLiteralStart,
                workspace.CreateSnapshot(transactionRevision: 0));
            var firstActionId = GetRawStringActionId(firstList);
            var staleActionId = GetRawStringActionId(secondList);

            var firstStage = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = firstActionId,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            firstStage.IsError.Should().NotBeTrue();
            var staleList = await ListRawStringActionsAsync(
                target,
                workspaceSelector,
                stringLiteralStart,
                workspace.CreateSnapshot(transactionRevision: 0));

            staleList.IsError.Should().BeTrue();
            AcceptanceProtocol.GetError(staleList).GetProperty("code").GetString().Should().Be("SnapshotMismatch");
            var currentList = await ListRawStringActionsAsync(
                target,
                workspaceSelector,
                stringLiteralStart,
                workspace.CreateSnapshot(transactionRevision: 1));

            currentList.IsError.Should().NotBeTrue();
            var staleStage = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = staleActionId,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 1),
                },
                TestContext.Current.CancellationToken);

            AssertStaleAction(staleStage);
            var rollback = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            rollback.IsError.Should().NotBeTrue();
            await StartTransactionAsync(target, workspaceSelector);
            var expiringList = await ListRawStringActionsAsync(
                target,
                workspaceSelector,
                stringLiteralStart,
                workspace.CreateSnapshot(transactionRevision: 0));
            var expiringActionId = GetRawStringActionId(expiringList);
            await WaitForTimerAsync(TimeSpan.FromMilliseconds(5250), TestContext.Current.CancellationToken);

            var expiredStage = await target.CallToolAsync(
                "stage-code-action",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                    ["actionId"] = expiringActionId,
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
                },
                TestContext.Current.CancellationToken);

            AssertActionExpired(expiredStage);
            var finalRollback = await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            finalRollback.IsError.Should().NotBeTrue();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<IReadOnlyList<JsonElement>> ListNullableReturnActionsAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string documentPath,
        IReadOnlyDictionary<string, object?> range,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        var listResult = await target.CallToolAsync(
            "list-code-actions",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["document"] = AcceptanceLocationSelectorFactory.CreateDocument(documentPath),
                ["range"] = range,
                ["expectedSnapshot"] = expectedSnapshot,
                ["kinds"] = _codeFixKind,
                ["diagnosticIds"] = _nullableReturnDiagnosticIds,
            },
            TestContext.Current.CancellationToken);

        listResult.IsError.Should().NotBeTrue(
            listResult.IsError == true
                ? AcceptanceProtocol.GetError(listResult).GetRawText()
                : string.Empty);

        var actions = new List<JsonElement>();
        var listedActions = AcceptanceProtocol.GetSuccessData(listResult)
            .GetProperty("actions")
            .GetProperty("items");
        foreach (var action in listedActions.EnumerateArray())
        {
            if (action.GetProperty("title").GetString() == "Declare as nullable")
            {
                actions.Add(action);
            }
        }

        return actions;
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
            "stage-code-action",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["actionId"] = action.GetProperty("actionId").GetGuid(),
                ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
            },
            TestContext.Current.CancellationToken);

        stageResult.IsError.Should().NotBeTrue(
            stageResult.IsError == true
                ? AcceptanceProtocol.GetError(stageResult).GetRawText()
                : string.Empty);
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

    private static Task<CallToolResult> ListRefactoringsAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string documentPath,
        IReadOnlyDictionary<string, object?> expectedSnapshot,
        IReadOnlyDictionary<string, object?>? range = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspace"] = workspaceSelector,
            ["document"] = AcceptanceLocationSelectorFactory.CreateDocument(documentPath),
            ["expectedSnapshot"] = expectedSnapshot,
            ["kinds"] = _refactoringKind,
        };

        if (range is not null)
        {
            arguments["range"] = range;
        }

        return target.CallToolAsync(
            "list-code-actions",
            arguments,
            TestContext.Current.CancellationToken);
    }

    private static void AssertConciseActionList(CallToolResult result, string expectedDocumentPath)
    {
        result.IsError.Should().NotBeTrue(
            result.IsError == true
                ? AcceptanceProtocol.GetError(result).GetRawText()
                : string.Empty);

        var forbiddenProperties = new[]
        {
            "providerId",
            "providerType",
            "equivalenceKey",
            "actionPath",
            "executionMode",
            "executorTool",
            "expectedSnapshot",
            "expiry",
        };

        foreach (var action in AcceptanceProtocol.GetSuccessData(result)
            .GetProperty("actions")
            .GetProperty("items")
            .EnumerateArray())
        {
            action.GetProperty("actionId").GetGuid().Should().NotBe(Guid.Empty);
            action.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            action.GetProperty("location")
                .GetProperty("document")
                .GetProperty("path")
                .GetString()
                .Should()
                .Be(expectedDocumentPath);

            foreach (var propertyName in forbiddenProperties)
            {
                action.TryGetProperty(propertyName, out _).Should().BeFalse();
            }
        }
    }

    private static Task<CallToolResult> ListRawStringActionsAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        int stringLiteralStart,
        IReadOnlyDictionary<string, object?> expectedSnapshot)
    {
        return target.CallToolAsync(
            "list-code-actions",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["document"] = AcceptanceLocationSelectorFactory.CreateDocument("RawString.cs"),
                ["range"] = new Dictionary<string, object?>
                {
                    ["start"] = stringLiteralStart,
                    ["length"] = 0,
                },
                ["expectedSnapshot"] = expectedSnapshot,
                ["kinds"] = _refactoringKind,
            },
            TestContext.Current.CancellationToken);
    }

    private static Guid GetRawStringActionId(CallToolResult listResult)
    {
        listResult.IsError.Should().NotBeTrue(
            listResult.IsError == true
                ? AcceptanceProtocol.GetError(listResult).GetRawText()
                : string.Empty);

        return AcceptanceProtocol.GetSuccessData(listResult)
            .GetProperty("actions")
            .GetProperty("items")
            .EnumerateArray()
            .Single(static candidate => candidate.GetProperty("title").GetString() == "Convert to raw string")
            .GetProperty("actionId")
            .GetGuid();
    }

    private static void AssertActionExpired(CallToolResult result)
    {
        result.IsError.Should().BeTrue();
        var error = AcceptanceProtocol.GetError(result);
        error.GetProperty("code").GetString().Should().Be("ActionExpired");
        var continuation = AcceptanceProtocol.GetContinuation(result);
        continuation.GetProperty("kind").GetString().Should().Be("ReviseRequest");
        continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static void AssertStaleAction(CallToolResult result)
    {
        result.IsError.Should().BeTrue();
        var error = AcceptanceProtocol.GetError(result);
        error.GetProperty("code").GetString().Should().Be("SnapshotMismatch");
        var continuation = AcceptanceProtocol.GetContinuation(result);
        continuation.GetProperty("kind").GetString().Should().Be("ReviseRequest");
        continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task WaitForTimerAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timer = new Timer(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            completion,
            duration,
            Timeout.InfiniteTimeSpan);
        await completion.Task.WaitAsync(cancellationToken);
    }
}
