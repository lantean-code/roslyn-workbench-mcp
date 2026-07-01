using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.TestSupport;
using Roslyn.Workbench.Mcp.Workspace;

using Xunit;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class ReplayRefactoringToolsTests
{
    public static TheoryData<string, Func<InspectionSampleFixture, ToolResult<WorkspaceOpenData>, Dictionary<string, JsonElement>>> ReplayMutationCases
    {
        get
        {
            var allCases = new Dictionary<string, Func<InspectionSampleFixture, ToolResult<WorkspaceOpenData>, Dictionary<string, JsonElement>>>(StringComparer.Ordinal)
            {
                { "convert-between-regular-and-verbatim-interpolated-string", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("$\"C:\\\\temp\\\\{value}\""), open) },
                { "convert-between-regular-and-verbatim-string", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("\"C:\\\\temp\\\\logs\""), open) },
                { "convert-foreach-to-for", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("foreach (var value in values)"), open) },
                { "convert-for-to-foreach", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0), open) },
                { "convert-anonymous-type-to-tuple", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"), open) },
                { "convert-anonymous-type-to-class", static (fixture, open) => CreateAnonymousTypeToClassRequest(fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"), open, ConvertAnonymousTypeToClassKind.Class) },
                { "convert-auto-property-to-full-property", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Goo"), open) },
                { "convert-to-record", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("ConvertibleToRecord"), open) },
                { "convert-direct-cast-to-try-cast", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("(object)1"), open) },
                { "convert-local-function-to-method", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Local", 0), open) },
                { "convert-primary-to-regular-constructor", static (fixture, open) => CreateLocationRequest(fixture.GetSelection("PrimaryConstructorSamples(int value)"), open) },
                { "convert-try-cast-to-direct-cast", static (fixture, open) => CreateLocationRequest(fixture.GetSelection("value as string"), open) },
                { "invert-conditional", static (fixture, open) => CreateLocationRequest(fixture.GetSelection("count == 0 ? \"zero\" : \"non-zero\""), open) },
                { "invert-if", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("if (left > 0)"), open) },
                { "make-local-function-static", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Local", 0), open) },
                { "move-declaration-near-reference", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("int moved;"), open) },
                { "name-tuple-element", static (fixture, open) => CreateLocationRequest(fixture.GetCursor("return (1 + 1, 2);", 0, "return (".Length), open) },
                { "replace-doc-comment-text-with-tag", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("System.IDisposable"), open) },
                { "reverse-for-statement", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0), open) },
                { "use-explicit-type", static (fixture, open) => CreateLocationRequest(fixture.GetCursor("var explicitBuilder", 0, "var".Length), open) },
                { "use-implicit-type", static (fixture, open) => CreateLocationRequest(fixture.GetCursor("StringBuilder implicitBuilder", 0, "StringBuilder".Length), open) },
                { "use-named-arguments", static (fixture, open) => CreateUseNamedArgumentsRequest(fixture.GetCursor("Sum(1, 2)", 0, 4), open, includeTrailingArguments: false) },
                { "use-recursive-patterns", static (fixture, open) => CreateLocationRequest(fixture.GetCursor("cf != null && cf.C != 0", 0, "cf != null ".Length), open) },
                { "add-await", static (fixture, open) => CreateAddAwaitRequest(fixture.GetCursorAfter("GetValueAsync()", 2), open, AddAwaitKind.Await) },
                { "add-debugger-display", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("GreetingFormatter", 0), open) },
                { "add-import", static (fixture, open) => CreateAddImportRequest(fixture.GetCursor("System.Net.Http.HttpClient"), open, simplifyAllOccurrences: false) },
                { "convert-if-to-switch", static (fixture, open) => CreateConvertIfToSwitchRequest(fixture.GetLocation("if (value == 0)"), open, ConvertIfToSwitchKind.Statement) },
                { "invert-logical", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("&&"), open) },
                { "introduce-using-statement", static (fixture, open) => CreateLocationRequest(fixture.GetSelection("var stream = new MemoryStream();"), open) },
                { "replace-conditional-with-statements", static (fixture, open) => CreateLocationRequest(fixture.GetLocation("value = enabled ? 1 : 2;"), open) },
            };
            var cases = new TheoryData<string, Func<InspectionSampleFixture, ToolResult<WorkspaceOpenData>, Dictionary<string, JsonElement>>>();

            foreach (var toolName in BuiltInCodeActionAuditCases.PromotedDraftValidationCandidates
                .Select(static auditCase => auditCase.ToolName)
                .Where(static toolName => !string.IsNullOrWhiteSpace(toolName)))
            {
                if (allCases.TryGetValue(toolName!, out var requestFactory))
                {
                    cases.Add(toolName!, requestFactory);
                }
            }

            foreach (var toolName in BuiltInCodeActionAuditCases.PendingPromotionCandidates
                .Select(static auditCase => auditCase.ToolName)
                .Where(static toolName => !string.IsNullOrWhiteSpace(toolName)))
            {
                if (allCases.TryGetValue(toolName!, out var requestFactory))
                {
                    cases.Add(toolName!, requestFactory);
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ReplayMutationCases))]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingReplayWrapper_THEN_ShouldStageStructuredMutation(
        string toolName,
        Func<InspectionSampleFixture, ToolResult<WorkspaceOpenData>, Dictionary<string, JsonElement>> requestFactory)
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        var result = await ExecuteAsync(coordinator, registry, toolName, requestFactory(fixture, openResult));
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        result.Data.Operation.Should().Be(toolName);
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
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

    private static SnapshotPrecondition CreateSnapshot(ToolResult<WorkspaceOpenData> openResult)
    {
        return new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            TransactionRevision = 0,
        };
    }

    private static async Task<PluginExecutionResult<MutationData>> ExecuteAsync(
        IWorkspaceCoordinator coordinator,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        var registeredTool = registry.RegisteredTools.Single(tool => tool.Metadata.Name == toolName);
        var request = DeserializeRequest(registeredTool.RequestType, arguments);
        await using var mutationLease = await coordinator.CreateMutationContextAsync(registeredTool, new object(), CancellationToken.None);
        var proposalResult = await registeredTool.Invoker.ExecuteAsync(request, mutationLease.Context!, CancellationToken.None);

        proposalResult.Outcome.Should().Be(ToolOutcome.Succeeded, proposalResult.Error?.Message);
        proposalResult.Data.Should().BeOfType<MutationProposal>();

        var stagedResult = await mutationLease.Context!.StageAsync(
            registeredTool,
            (MutationProposal)proposalResult.Data!,
            proposalResult.Diagnostics,
            proposalResult.Warnings,
            CancellationToken.None);

        return stagedResult;
    }

    private static object DeserializeRequest(Type requestType, IDictionary<string, JsonElement> arguments)
    {
        var requestNode = new JsonObject();

        foreach (var pair in arguments)
        {
            requestNode[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
        }

        return requestNode.Deserialize(requestType, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
