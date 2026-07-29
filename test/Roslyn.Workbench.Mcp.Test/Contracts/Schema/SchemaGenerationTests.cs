using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

[Trait("Category", "Contract")]
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
        requestProperties.TryGetProperty("alias", out var aliasProperty).Should().BeTrue();
        pathProperty.GetProperty("type").GetString().Should().Be("string");
        aliasProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void GIVEN_TransactionStartRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishWorkspaceSelectorProperty()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.TransactionStart), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");

        requestProperties.TryGetProperty("workspace", out var workspaceProperty).Should().BeTrue();
        workspaceProperty.GetRawText().Should().Contain("workspaceId");
        workspaceProperty.GetRawText().Should().Contain("alias");
        workspaceProperty.GetRawText().Should().Contain("path");
    }

    [Fact]
    public void GIVEN_WorkspaceListRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishStructuredWorkspaceCollectionOutput()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.WorkspaceList), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        outputSchema.GetRawText().Should().Contain("workspaces");
        outputSchema.GetRawText().Should().Contain("transactionOwnerWorkspaceId");
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
        requestProperties.TryGetProperty("symbolsLimit", out var limitProperty).Should().BeTrue();

        queryProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        metadataNameProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        requestProperties.TryGetProperty("workspace", out var workspaceProperty).Should().BeTrue();
        scopeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        kindsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        accessibilitiesProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        @namespaceProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        limitProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        workspaceProperty.GetRawText().Should().Contain("workspaceId");
    }

    [Fact]
    public void GIVEN_ResolveSymbolRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishLocationAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ResolveSymbol), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");

        requestProperties.TryGetProperty("location", out var locationProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();
        requestProperties.TryGetProperty("workspace", out var workspaceProperty).Should().BeTrue();

        locationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        workspaceProperty.GetRawText().Should().Contain("workspaceId");
    }

    [Fact]
    public void GIVEN_SolutionStructureOutput_WHEN_GeneratingToolSchema_THEN_ShouldPublishStructuredCollectionPayload()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.GetSolutionStructure), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        outputSchema.GetProperty("type").GetString().Should().Be("object");
        outputSchema.GetRawText().Should().Contain("folders");
        outputSchema.GetRawText().Should().Contain("projects");
    }

    [Fact]
    public void GIVEN_ListCodeActionsRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionQueryProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ListCodeActions), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestSchema = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request");
        var requestProperties = requestSchema.GetProperty("properties");

        requestProperties.TryGetProperty("document", out var documentProperty).Should().BeTrue();
        requestProperties.TryGetProperty("range", out var rangeProperty).Should().BeTrue();
        requestProperties.TryGetProperty("kinds", out var kindsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("diagnosticIds", out var diagnosticIdsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("limit", out var limitProperty).Should().BeTrue();
        requestProperties.TryGetProperty("workspace", out var workspaceProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out _).Should().BeFalse();
        requestProperties.TryGetProperty("location", out _).Should().BeFalse();
        requestProperties.TryGetProperty("includeRefactorings", out _).Should().BeFalse();
        requestProperties.TryGetProperty("includeCodeFixes", out _).Should().BeFalse();
        documentProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        rangeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        kindsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        kindsProperty.GetRawText().Should().Contain("CodeFixes");
        kindsProperty.GetRawText().Should().Contain("Refactorings");
        kindsProperty.GetRawText().Should().Contain("All");
        diagnosticIdsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        var limitDefault = typeof(ListCodeActionsRequest)
            .GetProperty(nameof(ListCodeActionsRequest.Limit))!
            .GetCustomAttribute<DefaultValueAttribute>();

        limitDefault.Should().NotBeNull();
        limitDefault!.Value.Should().Be(50);
        workspaceProperty.GetRawText().Should().Contain("workspaceId");
        requestSchema.GetProperty("required").EnumerateArray()
            .Select(static item => item.GetString())
            .Should().Contain(["document", "kinds"]);
    }

    [Fact]
    public void GIVEN_ListCodeActionsOutput_WHEN_GeneratingToolSchema_THEN_ShouldPublishConciseBoundedActions()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ListCodeActions), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        var dataSchema = outputSchema.GetProperty("properties").GetProperty("data");
        var dataText = dataSchema.GetRawText();
        var actionsSchema = dataSchema
            .GetProperty("properties")
            .GetProperty("actions");
        var actionsText = actionsSchema.GetRawText();
        var actionText = actionsSchema
            .GetProperty("properties")
            .GetProperty("items")
            .GetProperty("items")
            .GetRawText();

        dataText.Should().NotContain("returnedCount");
        actionsText.Should().Contain("hasMore");
        actionsText.Should().Contain("totalCount");
        actionText.Should().Contain("actionId");
        actionText.Should().Contain("title");
        actionText.Should().Contain("kind");
        actionText.Should().Contain("location");
        actionText.Should().Contain("diagnostics");
        actionText.Should().Contain("fixAllScopes");
        actionText.Should().NotContain("providerId");
        actionText.Should().NotContain("equivalenceKey");
        actionText.Should().NotContain("actionPath");
        actionText.Should().NotContain("workspaceId");
        actionText.Should().NotContain("workspaceEpoch");
        actionText.Should().NotContain("transactionRevision");
        actionText.Should().NotContain("expiresAt");
        actionText.Should().NotContain("executionMode");
        actionText.Should().NotContain("executorTool");
        actionText.Should().NotContain("describeTool");
        actionText.Should().NotContain("unsupportedReasonCode");
        actionText.Should().NotContain("requirements");
    }

    [Fact]
    public void GIVEN_PrepareFixAllOutput_WHEN_GeneratingToolSchema_THEN_ShouldPublishBoundedAffectedDocuments()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.PrepareFixAll), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        var dataSchema = outputSchema.GetProperty("properties").GetProperty("data");
        var dataText = dataSchema.GetRawText();
        var affectedDocumentsText = dataSchema
            .GetProperty("properties")
            .GetProperty("affectedDocuments")
            .GetRawText();

        dataText.Should().Contain("actionId");
        dataText.Should().Contain("scope");
        dataText.Should().Contain("affectedDiagnosticCount");
        dataText.Should().NotContain("affectedDocumentCount");
        dataText.Should().NotContain("hasMoreAffectedDocuments");
        affectedDocumentsText.Should().Contain("items");
        affectedDocumentsText.Should().Contain("hasMore");
        affectedDocumentsText.Should().Contain("totalCount");
    }

    [Fact]
    public void GIVEN_StageCodeActionRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionReferenceAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.StageCodeAction), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("actionId", out var actionIdProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        actionIdProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        actionIdProperty.GetProperty("type").GetString().Should().Be("string");
        actionIdProperty.GetProperty("format").GetString().Should().Be("uuid");
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_DescribeCodeActionRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishActionDescriptorProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod("DescribeCodeAction", BindingFlags.Public | BindingFlags.Static);

        method.Should().NotBeNull();

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("actionId", out var actionIdProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        actionIdProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("descriptor");
        outputSchema.GetRawText().Should().Contain("context");
        outputSchema.GetRawText().Should().Contain("kind");
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
    public void GIVEN_WorkspaceExecutedRequestContracts_WHEN_InspectingRequestTypes_THEN_ShouldExposeWorkspaceSelectorProperty()
    {
        var contractAssemblies = new[]
        {
            typeof(SearchSymbolsRequest).Assembly,
            typeof(ListCodeActionsRequest).Assembly,
            typeof(TransactionStartRequest).Assembly,
        };

        var requestTypes = contractAssemblies
            .Distinct()
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass)
            .Where(static type => type.Name.EndsWith("Request", StringComparison.Ordinal))
            .Where(static type =>
                type.Namespace is "Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection"
                or "Roslyn.Workbench.Mcp.CodeActions.Contracts"
                or "Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes"
                or "Roslyn.Workbench.Mcp.CodeActions.Contracts.Conversions"
                or "Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings"
                or "Roslyn.Workbench.Mcp.Transaction.Contracts")
            .ToArray();

        requestTypes.Should().NotBeEmpty();
        requestTypes
            .Select(static type => type.GetProperty("Workspace"))
            .Should()
            .OnlyContain(static property => property != null);
    }

    [Fact]
    public void GIVEN_AddMissingUsingsRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishScopeOptionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.AddMissingUsings), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("scope", out var scopeProperty).Should().BeTrue();
        requestProperties.TryGetProperty("preferGlobalUsings", out var preferGlobalUsingsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        scopeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        preferGlobalUsingsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_FixedCompilerCodeFixRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishLocationAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.AddExplicitCast), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("location", out var locationProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        locationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_LocationRefactoringRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.AddDebuggerDisplay), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_AddImportRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionOptionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.AddImport), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("simplifyAllOccurrences", out var simplifyAllOccurrencesProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        simplifyAllOccurrencesProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_InlineVariableRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSymbolOptionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.InlineVariable), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("symbol", out var symbolProperty).Should().BeTrue();
        requestProperties.TryGetProperty("removeDeclaration", out var removeDeclarationProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        symbolProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        removeDeclarationProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_ConvertToInterpolatedStringRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ConvertToInterpolatedString), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_ConvertAnonymousTypeToClassRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionKindAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ConvertAnonymousTypeToClass), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("kind", out var kindProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        kindProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_ConvertAutoPropertyToFullPropertyRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ConvertAutoPropertyToFullProperty), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_ExtractMethodRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionKindAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ExtractMethod), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("targetKind", out var targetKindProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        targetKindProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_MoveTypeToFileRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishTypeAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.MoveTypeToFile), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("type", out var typeProperty).Should().BeTrue();
        requestProperties.TryGetProperty("preserveNamespace", out var preserveNamespaceProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        typeProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        preserveNamespaceProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_ConvertPropertyRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionDirectionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ConvertProperty), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("direction", out var directionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        directionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_IntroduceParameterRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionStrategyAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.IntroduceParameter), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("allOccurrences", out var allOccurrencesProperty).Should().BeTrue();
        requestProperties.TryGetProperty("strategy", out var strategyProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        allOccurrencesProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        strategyProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_EncapsulateFieldRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishFieldOptionAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.EncapsulateField), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("field", out var fieldProperty).Should().BeTrue();
        requestProperties.TryGetProperty("updateReferences", out var updateReferencesProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        fieldProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        updateReferencesProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_ConvertForeachLinqRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionKindAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.ConvertForeachLinq), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("conversionKind", out var conversionKindProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        conversionKindProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }

    [Fact]
    public void GIVEN_IntroduceVariableRequest_WHEN_GeneratingToolSchema_THEN_ShouldPublishSelectionKindAndSnapshotProperties()
    {
        var method = typeof(ContractSchemaTestTools).GetMethod(nameof(ContractSchemaTestTools.IntroduceVariable), BindingFlags.Public | BindingFlags.Static);

        var tool = McpServerTool.Create(method!);
        var requestProperties = tool.ProtocolTool.InputSchema.GetProperty("properties").GetProperty("request").GetProperty("properties");
        var outputSchema = tool.ProtocolTool.OutputSchema!.Value;

        requestProperties.TryGetProperty("selection", out var selectionProperty).Should().BeTrue();
        requestProperties.TryGetProperty("kind", out var kindProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        selectionProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        kindProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
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
        requestProperties.TryGetProperty("baseTypesLimit", out var baseTypesLimitProperty).Should().BeTrue();
        requestProperties.TryGetProperty("interfacesLimit", out var interfacesLimitProperty).Should().BeTrue();
        requestProperties.TryGetProperty("derivedTypesLimit", out var derivedTypesLimitProperty).Should().BeTrue();

        symbolProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        includeDerivedProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        maxDepthProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        baseTypesLimitProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        interfacesLimitProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        derivedTypesLimitProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
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
        requestProperties.TryGetProperty("renameInStrings", out var renameInStringsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("renameInComments", out var renameInCommentsProperty).Should().BeTrue();
        requestProperties.TryGetProperty("renameFile", out var renameFileProperty).Should().BeTrue();
        requestProperties.TryGetProperty("expectedSnapshot", out var snapshotProperty).Should().BeTrue();

        symbolProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        newNameProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        renameOverloadsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        renameInStringsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        renameInCommentsProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        renameFileProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        snapshotProperty.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        outputSchema.GetRawText().Should().Contain("transaction");
        outputSchema.GetRawText().Should().Contain("preview");
    }
}
