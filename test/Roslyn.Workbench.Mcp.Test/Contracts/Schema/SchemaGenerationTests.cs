using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

[Trait("Category", "Contract")]
public sealed class SchemaGenerationTests
{
    [Theory]
    [InlineData(nameof(ContractSchemaTestTools.WorkspaceOpen), "path")]
    [InlineData(nameof(ContractSchemaTestTools.TransactionStart), "workspace")]
    [InlineData(nameof(ContractSchemaTestTools.SearchSymbols), "query")]
    [InlineData(nameof(ContractSchemaTestTools.ResolveSymbol), "location")]
    [InlineData(nameof(ContractSchemaTestTools.GetTypeHierarchy), "symbol")]
    [InlineData(nameof(ContractSchemaTestTools.AnalyzeControlFlow), "location")]
    [InlineData(nameof(ContractSchemaTestTools.RenameSymbol), "newName")]
    public void GIVEN_RequestContract_WHEN_GeneratingToolSchema_THEN_ShouldPublishRepresentativeProperty(
        string methodName,
        string propertyName)
    {
        var tool = CreateTool(methodName);

        GetRequestProperties(tool).TryGetProperty(propertyName, out var property).Should().BeTrue();
        property.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void GIVEN_WorkspaceListRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishStructuredWorkspaceCollectionOutput()
    {
        var tool = CreateTool(nameof(ContractSchemaTestTools.WorkspaceList));

        tool.ProtocolTool.OutputSchema!.Value.GetRawText().Should().Contain("workspaces");
        tool.ProtocolTool.OutputSchema!.Value.GetRawText().Should().Contain("transactionOwnerWorkspaceId");
    }

    [Fact]
    public void GIVEN_SelectorProbeRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectorProperties()
    {
        var properties = GetRequestProperties(CreateTool(nameof(ContractSchemaTestTools.SchemaProbe)));

        properties.TryGetProperty("document", out _).Should().BeTrue();
        properties.TryGetProperty("location", out _).Should().BeTrue();
        properties.TryGetProperty("scope", out _).Should().BeTrue();
        properties.TryGetProperty("expectedSnapshot", out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ListCodeActionsRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionQueryProperties()
    {
        var tool = CreateTool(nameof(ContractSchemaTestTools.ListCodeActions));
        var requestSchema = GetRequestSchema(tool);
        var properties = requestSchema.GetProperty("properties");

        properties.TryGetProperty("document", out _).Should().BeTrue();
        properties.TryGetProperty("range", out _).Should().BeTrue();
        properties.TryGetProperty("kinds", out var kinds).Should().BeTrue();
        properties.TryGetProperty("diagnosticIds", out _).Should().BeTrue();
        properties.TryGetProperty("limit", out _).Should().BeTrue();
        properties.TryGetProperty("workspace", out _).Should().BeTrue();
        properties.TryGetProperty("expectedSnapshot", out _).Should().BeFalse();
        kinds.GetRawText().Should().Contain("CodeFixes");
        kinds.GetRawText().Should().Contain("Refactorings");
        kinds.GetRawText().Should().Contain("All");

        var limitDefault = typeof(ListCodeActionsRequest)
            .GetProperty(nameof(ListCodeActionsRequest.Limit))!
            .GetCustomAttribute<DefaultValueAttribute>();

        limitDefault.Should().NotBeNull();
        limitDefault!.Value.Should().Be(50);
        requestSchema.GetProperty("required").EnumerateArray()
            .Select(static item => item.GetString())
            .Should().Contain(["document", "kinds"]);
    }

    [Fact]
    public void GIVEN_ListCodeActionsOutput_WHEN_GeneratingToolSchema_THEN_ShouldPublishConciseBoundedActions()
    {
        var outputSchema = CreateTool(nameof(ContractSchemaTestTools.ListCodeActions)).ProtocolTool.OutputSchema!.Value;
        var dataSchema = outputSchema.GetProperty("properties").GetProperty("data");
        var actionsSchema = dataSchema.GetProperty("properties").GetProperty("actions");
        var actionSchema = actionsSchema.GetProperty("properties").GetProperty("items").GetProperty("items");
        var actionText = actionSchema.GetRawText();
        var diagnosticsText = actionSchema.GetProperty("properties").GetProperty("diagnostics").GetRawText();

        actionsSchema.GetRawText().Should().ContainAll("items", "hasMore", "totalCount");
        actionText.Should().ContainAll("actionId", "title", "kind", "location", "diagnostics", "fixAllScopes");
        diagnosticsText.Should().ContainAll("items", "hasMore", "totalCount");
        actionText.Should().NotContainAny(
            "providerId",
            "equivalenceKey",
            "actionPath",
            "executionMode",
            "executorTool",
            "describeTool",
            "unsupportedReasonCode",
            "requirements");
    }

    [Fact]
    public void GIVEN_PrepareFixAllOutput_WHEN_GeneratingToolSchema_THEN_ShouldPublishBoundedAffectedDocuments()
    {
        var outputSchema = CreateTool(nameof(ContractSchemaTestTools.PrepareFixAll)).ProtocolTool.OutputSchema!.Value;
        var dataSchema = outputSchema.GetProperty("properties").GetProperty("data");
        var dataText = dataSchema.GetRawText();
        var affectedDocumentsText = dataSchema.GetProperty("properties").GetProperty("affectedDocuments").GetRawText();

        dataText.Should().ContainAll("actionId", "scope", "affectedDiagnosticCount");
        dataText.Should().NotContainAny("affectedDocumentCount", "hasMoreAffectedDocuments");
        affectedDocumentsText.Should().ContainAll("items", "hasMore", "totalCount");
    }

    [Fact]
    public void GIVEN_StageCodeActionRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionReferenceAndSnapshotProperties()
    {
        var tool = CreateTool(nameof(ContractSchemaTestTools.StageCodeAction));
        var requestProperties = GetRequestProperties(tool);
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("actionId", out var actionId).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out _).Should().BeTrue();
        actionId.GetProperty("type").GetString().Should().Be("string");
        actionId.GetProperty("format").GetString().Should().Be("uuid");
        outputSchema.GetRawText().Should().ContainAll("transaction", "preview");
    }

    [Fact]
    public void GIVEN_SolutionStructureOutput_WHEN_GeneratingToolSchema_THEN_ShouldPublishStructuredCollectionPayload()
    {
        var outputSchema = CreateTool(nameof(ContractSchemaTestTools.GetSolutionStructure)).ProtocolTool.OutputSchema!.Value;

        outputSchema.GetProperty("type").GetString().Should().Be("object");
        outputSchema.GetRawText().Should().ContainAll("folders", "projects");
    }

    private static McpServerTool CreateTool(string methodName)
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        return McpServerTool.Create(method!);
    }

    private static JsonElement GetRequestSchema(McpServerTool tool)
    {
        return tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request");
    }

    private static JsonElement GetRequestProperties(McpServerTool tool)
    {
        return GetRequestSchema(tool).GetProperty("properties");
    }
}
