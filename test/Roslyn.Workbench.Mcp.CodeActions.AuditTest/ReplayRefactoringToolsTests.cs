using System.Text.Json;
using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;
namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class ReplayRefactoringToolsTests
{
    public static TheoryData<string> ReplayMutationCaseNames
    {
        get
        {
            var cases = new TheoryData<string>();

            foreach (var toolName in BuiltInCodeActionAuditCases.SupportedCompatibilityCases
                .Select(static auditCase => auditCase.ToolName)
                .Where(static toolName => !string.IsNullOrWhiteSpace(toolName))
                .Distinct(StringComparer.Ordinal))
            {
                if (ReplayMutationCases.ContainsKey(toolName!))
                {
                    cases.Add(toolName!);
                }
            }

            return cases;
        }
    }

    private static IReadOnlyDictionary<string, ReplayMutationCaseDefinition> ReplayMutationCases
    {
        get
        {
            return new Dictionary<string, ReplayMutationCaseDefinition>(StringComparer.Ordinal)
            {
                { "convert-between-regular-and-verbatim-interpolated-string", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("$\"C:\\\\temp\\\\{value}\""), open), "Formatting.cs") },
                { "convert-between-regular-and-verbatim-string", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("\"C:\\\\temp\\\\logs\""), open), "Formatting.cs") },
                { "convert-foreach-to-for", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("foreach (var value in values)"), open), "Formatting.cs") },
                { "convert-for-to-foreach", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0), open), "Formatting.cs") },
                { "convert-anonymous-type-to-tuple", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"), open), "Formatting.cs") },
                { "convert-anonymous-type-to-class", new(static (fixture, open) => CreateAnonymousTypeToClassRequest(fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"), open, ConvertAnonymousTypeToClassKind.Class), "Formatting.cs") },
                { "convert-auto-property-to-full-property", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Goo"), open), "Formatting.cs") },
                { "convert-to-record", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("ConvertibleToRecord"), open), "Formatting.cs") },
                { "convert-direct-cast-to-try-cast", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("(object)1"), open), "Formatting.cs") },
                { "convert-expression-body", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Square"), open), "Formatting.cs") },
                { "convert-local-function-to-method", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Local", 0), open), "Formatting.cs") },
                { "convert-primary-to-regular-constructor", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("PrimaryConstructorSamples(int value)"), open), "Formatting.cs") },
                { "convert-try-cast-to-direct-cast", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("value as string"), open), "Formatting.cs") },
                { "invert-conditional", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("count == 0 ? \"zero\" : \"non-zero\""), open), "Formatting.cs") },
                { "invert-if", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("if (left > 0)"), open), "Formatting.cs") },
                { "make-local-function-static", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Local", 0), open), "Formatting.cs") },
                { "move-declaration-near-reference", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("int moved;"), open), "Formatting.cs") },
                { "name-tuple-element", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("return (1 + 1, 2);", 0, "return (".Length), open), "Formatting.cs") },
                { "replace-doc-comment-text-with-tag", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("System.IDisposable"), open), "Formatting.cs") },
                { "reverse-for-statement", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0), open), "Formatting.cs") },
                { "use-explicit-type", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("var explicitBuilder", 0, "var".Length), open), "Formatting.cs") },
                { "use-implicit-type", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("StringBuilder implicitBuilder", 0, "StringBuilder".Length), open), "Formatting.cs") },
                { "use-named-arguments", new(static (fixture, open) => CreateUseNamedArgumentsRequest(fixture.GetCursor("Sum(1, 2)", 0, 4), open, includeTrailingArguments: false), "Formatting.cs") },
                { "use-recursive-patterns", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("cf != null && cf.C != 0", 0, "cf != null ".Length), open), "Formatting.cs") },
                { "add-await", new(static (fixture, open) => CreateAddAwaitRequest(fixture.GetCursorAfter("GetValueAsync()", 2), open, AddAwaitKind.Await), "Formatting.cs") },
                { "add-debugger-display", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("GreetingFormatter", 0), open), "Formatting.cs") },
                { "add-import", new(static (fixture, open) => CreateAddImportRequest(fixture.GetCursor("System.Net.Http.HttpClient"), open, simplifyAllOccurrences: false), "Formatting.cs") },
                { "add-null-checks", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursorInDocument("AddParameterCheck.cs", "object value"), open), "AddParameterCheck.cs") },
                { "convert-if-to-switch", new(static (fixture, open) => CreateConvertIfToSwitchRequest(fixture.GetLocation("if (value == 0)"), open, ConvertIfToSwitchKind.Statement), "Formatting.cs") },
                { "invert-logical", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("&&"), open), "Formatting.cs") },
                { "introduce-using-statement", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("var stream = new MemoryStream();"), open), "Formatting.cs") },
                { "replace-conditional-with-statements", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("value = enabled ? 1 : 2;"), open), "Formatting.cs") },
            };
        }
    }

    [Theory]
    [MemberData(nameof(ReplayMutationCaseNames))]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingReplayWrapper_THEN_ShouldStageStructuredMutation(
        string toolName)
    {
        var testCase = ReplayMutationCases[toolName];
        using var fixture = await (testCase.FixtureFactory?.Invoke() ?? InspectionSampleFixture.CreateAsync());
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var result = await ExecuteAsync(coordinator, toolName, testCase.RequestFactory(fixture, openResult));
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        result.Data.Operation.Should().Be(toolName);
        preview.Data!.Documents.Should().ContainSingle(change => change.Document!.Path == testCase.ExpectedDocumentPath);
        var documentPreview = preview.Data.Documents.Single(change => change.Document!.Path == testCase.ExpectedDocumentPath).Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    public sealed record ReplayMutationCaseDefinition(
        Func<InspectionSampleFixture, ToolResult<WorkspaceOpenData>, Dictionary<string, JsonElement>> RequestFactory,
        string ExpectedDocumentPath,
        Func<Task<InspectionSampleFixture>>? FixtureFactory = null);

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingMoveTypeToFile_THEN_ShouldStageNewDocumentAndSourceUpdate()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var result = await ExecuteAsync(coordinator, "move-type-to-file", CreateMoveTypeToFileRequest(fixture.GetLocation("AutoPropertySamples"), openResult));
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Operation.Should().Be("move-type-to-file");
        preview.Data!.Documents.Should().Contain(change => change.Document!.Path == "Formatting.cs");
        preview.Data.Documents.Should().Contain(change => change.Document!.Path == "AutoPropertySamples.cs");
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingConvertPropertyToFull_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var result = await ExecuteAsync(coordinator, "convert-property", CreateConvertPropertyRequest(fixture.GetLocation("Goo"), openResult, ConvertPropertyDirection.ToFull));
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Operation.Should().Be("convert-property");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingConvertPropertyToAutoWhenSafe_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync(new InspectionSampleFixtureOptions
        {
            AdditionalEditorConfigText = "dotnet_style_prefer_auto_properties = true:suggestion",
        });
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var result = await ExecuteAsync(coordinator, "convert-property", CreateConvertPropertyRequest(fixture.GetLocation("Score"), openResult, ConvertPropertyDirection.ToAutoWhenSafe));
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Operation.Should().Be("convert-property");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
    }

    private static Dictionary<string, JsonElement> CreateLocationRequest(LocationSelector selection, ToolResult<WorkspaceOpenData> openResult)
    {
        return new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(selection),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static Dictionary<string, JsonElement> CreateAnonymousTypeToClassRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        ConvertAnonymousTypeToClassKind kind)
    {
        return new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(selection),
            ["kind"] = JsonSerializer.SerializeToElement(kind),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static Dictionary<string, JsonElement> CreateAddAwaitRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        AddAwaitKind kind)
    {
        return new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(selection),
            ["kind"] = JsonSerializer.SerializeToElement(kind),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static Dictionary<string, JsonElement> CreateAddImportRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        bool simplifyAllOccurrences)
    {
        return new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(selection),
            ["simplifyAllOccurrences"] = JsonSerializer.SerializeToElement(simplifyAllOccurrences),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static Dictionary<string, JsonElement> CreateConvertIfToSwitchRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        ConvertIfToSwitchKind kind)
    {
        return new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(selection),
            ["kind"] = JsonSerializer.SerializeToElement(kind),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static Dictionary<string, JsonElement> CreateUseNamedArgumentsRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        bool includeTrailingArguments)
    {
        return new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(selection),
            ["includeTrailingArguments"] = JsonSerializer.SerializeToElement(includeTrailingArguments),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static Dictionary<string, JsonElement> CreateMoveTypeToFileRequest(LocationSelector selection, ToolResult<WorkspaceOpenData> openResult)
    {
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                Location = selection,
            }),
            ["preserveNamespace"] = JsonSerializer.SerializeToElement(true),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static Dictionary<string, JsonElement> CreateConvertPropertyRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        ConvertPropertyDirection direction)
    {
        return new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(selection),
            ["direction"] = JsonSerializer.SerializeToElement(direction),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(CreateSnapshot(openResult)),
        };
    }

    private static SnapshotPrecondition CreateSnapshot(ToolResult<WorkspaceOpenData> openResult)
    {
        return new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            TransactionRevision = 0,
        };
    }

    private static IWorkspaceRuntime CreateBuiltInCoordinator()
    {
        return BundledCoreToolTestHarness.CreateBuiltInCodeActionCoordinator();
    }

    private static async Task<ToolResult<MutationData>> ExecuteAsync(
        IWorkspaceRuntime coordinator,
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        var result = await CodeActionToolTestHarness.InvokeAsync<MutationData>(coordinator, toolName, arguments);

        result.Outcome.Should().Be(ToolOutcome.Succeeded, result.Error?.Message);
        return result;
    }
}
