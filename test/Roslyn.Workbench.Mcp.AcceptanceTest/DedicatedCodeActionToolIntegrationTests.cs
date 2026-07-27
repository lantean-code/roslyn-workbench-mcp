namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class DedicatedCodeActionToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_PublishedDedicatedCodeActionTools_WHEN_ExecutingEveryAcceptanceCase_THEN_ShouldStagePreviewAndRollback()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            var testCases = CodeActionAcceptanceCases.Create(target.WorkspaceRoot);
            AssertCompleteCoverage(testCases);
            await AssertPublishedAsync(target, testCases);

            var projectPath = Path.Combine(target.WorkspaceRoot, "Sample.csproj");
            var workspace = await OpenWorkspaceAsync(target, projectPath);
            var workspaceSelector = workspace.CreateSelector();
            var originalDocuments = LoadOriginalDocuments(target.WorkspaceRoot, testCases);

            foreach (var testCase in testCases)
            {
                await StartTransactionAsync(target, workspaceSelector, testCase.DisplayName);

                var arguments = CreateArguments(testCase, workspace, workspaceSelector);
                var mutationResult = await target.CallToolAsync(
                    testCase.ToolName,
                    arguments,
                    TestContext.Current.CancellationToken);

                AssertSuccessfulMutation(mutationResult, testCase.DisplayName);

                var previewResult = await PreviewAsync(target, workspaceSelector);
                AssertExpectedPreview(previewResult, testCase);

                await RollbackAsync(target, workspaceSelector, testCase.DisplayName);
                await AssertDocumentsRestoredAsync(
                    target.WorkspaceRoot,
                    testCase,
                    originalDocuments);
            }
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static void AssertCompleteCoverage(IReadOnlyList<CodeActionAcceptanceCase> testCases)
    {
        var manifestToolNames = CodeActionAcceptanceManifest.LoadToolNames();
        var acceptanceCaseNames = testCases
            .Select(static testCase => testCase.DisplayName)
            .ToArray();

        var acceptanceToolNames = testCases
            .Select(static testCase => testCase.ToolName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static toolName => toolName, StringComparer.Ordinal)
            .ToArray();

        acceptanceCaseNames.Should().OnlyHaveUniqueItems();
        acceptanceToolNames.Should().Equal(manifestToolNames);
        AssertMultiDiagnosticCoverage(testCases);
    }

    private static void AssertMultiDiagnosticCoverage(
        IReadOnlyList<CodeActionAcceptanceCase> testCases)
    {
        var expectedDiagnosticIdsByTool = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["add-obsolete-attribute"] = ["CS0612", "CS0618", "CS0672", "CS1062", "CS1064"],
            ["add-yield"] = ["CS0029", "CS0266"],
            ["declare-as-nullable"] = ["CS8600", "CS8603", "CS8618", "CS8625"],
            ["fix-incorrect-constraint"] = ["CS9010", "CS9011"],
            ["fix-return-type"] = ["CS0127", "CS0201", "CS1997"],
        };

        foreach (var (toolName, expectedDiagnosticIds) in expectedDiagnosticIdsByTool)
        {
            var actualDiagnosticIds = testCases
                .Where(testCase => testCase.ToolName == toolName)
                .Select(static testCase => testCase.DiagnosticId)
                .ToArray();

            actualDiagnosticIds.Should().BeEquivalentTo(expectedDiagnosticIds, toolName);
        }
    }

    private static async Task AssertPublishedAsync(
        AcceptanceProcessFixture target,
        IReadOnlyList<CodeActionAcceptanceCase> testCases)
    {
        var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);

        var toolNames = testCases
            .Select(static testCase => testCase.ToolName)
            .Distinct(StringComparer.Ordinal);

        foreach (var toolName in toolNames)
        {
            tools.Should().ContainSingle(tool => tool.Name == toolName);
        }

        tools.Should().NotContain(static tool => tool.Name == "sort-usings");
    }

    private static async Task<AcceptanceWorkspaceIdentity> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string projectPath)
    {
        var openResult = await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["path"] = projectPath,
                ["workspaceRoot"] = target.WorkspaceRoot,
            },
            TestContext.Current.CancellationToken);

        openResult.IsError.Should().NotBeTrue();
        return AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
    }

    private static Dictionary<string, byte[]> LoadOriginalDocuments(
        string workspaceRoot,
        IReadOnlyList<CodeActionAcceptanceCase> testCases)
    {
        var originalDocuments = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var testCase in testCases)
        {
            foreach (var documentPath in testCase.ExpectedDocumentPaths)
            {
                var fullPath = Path.Combine(workspaceRoot, documentPath);
                if (originalDocuments.ContainsKey(documentPath) || !File.Exists(fullPath))
                {
                    continue;
                }

                originalDocuments.Add(documentPath, File.ReadAllBytes(fullPath));
            }
        }

        return originalDocuments;
    }

    private static async Task StartTransactionAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string toolName)
    {
        var startResult = await target.CallToolAsync(
            "transaction-start",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);

        startResult.IsError.Should().NotBeTrue(toolName);
    }

    private static Dictionary<string, object?> CreateArguments(
        CodeActionAcceptanceCase testCase,
        AcceptanceWorkspaceIdentity workspace,
        IReadOnlyDictionary<string, object?> workspaceSelector)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace"] = workspaceSelector,
            ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: 0),
        };

        foreach (var (name, value) in testCase.Arguments)
        {
            arguments.Add(name, value);
        }

        return arguments;
    }

    private static void AssertSuccessfulMutation(
        ModelContextProtocol.Protocol.CallToolResult mutationResult,
        string toolName)
    {
        string? error = null;
        if (mutationResult.IsError == true)
        {
            error = AcceptanceProtocol.GetError(mutationResult).GetRawText();
        }

        mutationResult.IsError.Should().NotBeTrue($"{toolName}: {error}");

        var mutation = AcceptanceProtocol.GetSuccessData(mutationResult);
        mutation.GetProperty("staged").GetBoolean().Should().BeTrue(toolName);
        mutation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace(toolName);
        mutation.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(1, toolName);
    }

    private static Task<ModelContextProtocol.Protocol.CallToolResult> PreviewAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector)
    {
        return target.CallToolAsync(
            "transaction-preview",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);
    }

    private static void AssertExpectedPreview(
        ModelContextProtocol.Protocol.CallToolResult previewResult,
        CodeActionAcceptanceCase testCase)
    {
        previewResult.IsError.Should().NotBeTrue(testCase.DisplayName);

        var actualDocumentPaths = AcceptanceProtocol.GetSuccessData(previewResult)
            .GetProperty("documents")
            .EnumerateArray()
            .Select(static change => change.GetProperty("document").GetProperty("path").GetString())
            .ToArray();

        actualDocumentPaths.Should().BeEquivalentTo(testCase.ExpectedDocumentPaths, testCase.DisplayName);
    }

    private static async Task RollbackAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        string toolName)
    {
        var rollbackResult = await target.CallToolAsync(
            "transaction-rollback",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
            },
            TestContext.Current.CancellationToken);

        rollbackResult.IsError.Should().NotBeTrue(toolName);
    }

    private static async Task AssertDocumentsRestoredAsync(
        string workspaceRoot,
        CodeActionAcceptanceCase testCase,
        Dictionary<string, byte[]> originalDocuments)
    {
        foreach (var documentPath in testCase.ExpectedDocumentPaths)
        {
            var fullPath = Path.Combine(workspaceRoot, documentPath);
            if (!originalDocuments.TryGetValue(documentPath, out var originalBytes))
            {
                File.Exists(fullPath).Should().BeFalse(testCase.DisplayName);
                continue;
            }

            var currentBytes = await File.ReadAllBytesAsync(
                fullPath,
                TestContext.Current.CancellationToken);

            currentBytes.Should().Equal(originalBytes, testCase.DisplayName);
        }
    }
}
