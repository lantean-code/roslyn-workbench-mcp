using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using ModelContextProtocol.Server;

using Xunit;

namespace Roslyn.Workbench.Mcp.Contracts.Test.Schema;

public sealed class SchemaGenerationTests
{
    [Fact]
    public void GIVEN_WorkspaceOpenRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishNestedPathProperty()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.WorkspaceOpen), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);

        var requestProperty = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request");
        var requestProperties = requestProperty.GetProperty("properties");

        requestProperties.TryGetProperty("path", out var pathProperty).Should().BeTrue();
        pathProperty.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void GIVEN_ToolResultOfWorkspaceStatusData_WHEN_GeneratingToolSchema_THEN_ShouldPublishOutputSchema()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.WorkspaceOpen), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);

        tool.ProtocolTool.OutputSchema.Should().NotBeNull();
        tool.ProtocolTool.OutputSchema!.Value.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void GIVEN_SelectorProbeRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectorProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.SchemaProbe), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);

        var requestProperty = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request");
        var requestProperties = requestProperty.GetProperty("properties");

        requestProperties.TryGetProperty("document", out var documentProperty).Should().BeTrue();
        requestProperties.TryGetProperty("location", out var locationProperty).Should().BeTrue();
        requestProperties.TryGetProperty("scope", out var scopeProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        documentProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        locationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        scopeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void GIVEN_SearchSymbolsRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishInspectionFilters()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.SearchSymbols), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");

        requestProperties.TryGetProperty("query", out var queryProperty).Should().BeTrue();
        requestProperties.TryGetProperty("metadataName", out var metadataNameProperty).Should().BeTrue();
        requestProperties.TryGetProperty("scope", out var scopeProperty).Should().BeTrue();
        requestProperties.TryGetProperty("kinds", out var kindsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("accessibilities", out var accessibilitiesProperty).Should().BeTrue();
        requestProperties.TryGetProperty("namespace", out var @namespaceProperty).Should().BeTrue();
        requestProperties.TryGetProperty("limit", out var limitProperty).Should().BeTrue();

        queryProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        metadataNameProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        scopeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        kindsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        accessibilitiesProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        @namespaceProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        limitProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void GIVEN_ResolveSymbolRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishLocationAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ResolveSymbol), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");

        requestProperties.TryGetProperty("location", out var locationProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        locationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void GIVEN_SolutionStructureOutput_WHEN_GeneratingToolSchema_THEN_ShouldPublishStructuredCollectionPayload()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.GetSolutionStructure), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        outputSchema.GetProperty("type").GetString().Should().Be("object");
        outputSchema.GetRawText().Should().Contain("returnedCount");
        outputSchema.GetRawText().Should().Contain("hasMore");
        outputSchema.GetRawText().Should().Contain("folders");
        outputSchema.GetRawText().Should().Contain("projects");
    }

    [Fact]
    public void GIVEN_ListCodeActionsRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionQueryProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ListCodeActions), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");

        requestProperties.TryGetProperty("location", out var locationProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();
        requestProperties.TryGetProperty("includeRefactorings", out var refactoringsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("includeCodeFixes", out var codeFixesProperty).Should().BeTrue();
        requestProperties.TryGetProperty("diagnosticIds", out var diagnosticIdsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("limit", out var limitProperty).Should().BeTrue();

        locationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        refactoringsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        codeFixesProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        diagnosticIdsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        limitProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void GIVEN_StageCodeActionRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionTokenAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.StageCodeAction), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("actionId", out var actionIdProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        actionIdProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_StageCodeFixRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionTokenAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.StageCodeFix), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("actionId", out var actionIdProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        actionIdProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_StageFixAllRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionScopeAndLimitProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.StageFixAll), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("actionId", out var actionIdProperty).Should().BeTrue();
        requestProperties.TryGetProperty("scope", out var scopeProperty).Should().BeTrue();
        requestProperties.TryGetProperty("maxChanges", out var maxChangesProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        actionIdProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        scopeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        maxChangesProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_RemoveUnusedUsingsRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishScopeAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.RemoveUnusedUsings), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("scope", out var scopeProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        scopeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_GetTypeHierarchyRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishHierarchyProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.GetTypeHierarchy), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("symbol", out var symbolProperty).Should().BeTrue();
        requestProperties.TryGetProperty("includeDerived", out var includeDerivedProperty).Should().BeTrue();
        requestProperties.TryGetProperty("maxDepth", out var maxDepthProperty).Should().BeTrue();
        requestProperties.TryGetProperty("limit", out var limitProperty).Should().BeTrue();

        symbolProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        includeDerivedProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        maxDepthProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        limitProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("baseTypes");
        outputSchema.GetRawText().Should().Contain("interfaces");
    }

    [Fact]
    public void GIVEN_AnalyzeControlFlowRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishLocationProperty()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.AnalyzeControlFlow), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("location", out var locationProperty).Should().BeTrue();

        locationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("entryReachable");
        outputSchema.GetRawText().Should().Contain("exitReachable");
    }

    [Fact]
    public void GIVEN_RenameSymbolRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishRenameProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.RenameSymbol), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("symbol", out var symbolProperty).Should().BeTrue();
        requestProperties.TryGetProperty("newName", out var newNameProperty).Should().BeTrue();
        requestProperties.TryGetProperty("renameOverloads", out var renameOverloadsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("renameImplementations", out var renameImplementationsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("renameFile", out var renameFileProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        symbolProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        newNameProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        renameOverloadsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        renameImplementationsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        renameFileProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }
}
