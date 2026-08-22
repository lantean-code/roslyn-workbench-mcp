using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.ErrorReporting.Contracts;
using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class ToolSchemaFactoryIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_FixedContractLimits_WHEN_ExportingInputSchemas_THEN_ShouldPublishDeclaredDefaults()
    {
        var target = CreateTarget();

        var calleesSchema = target.CreateInputSchema<FindCalleesRequest>();
        var operationTreeSchema = target.CreateInputSchema<GetOperationTreeRequest>();
        var controlFlowGraphSchema = target.CreateInputSchema<GetControlFlowGraphRequest>();
        var duplicateCodeSchema = target.CreateInputSchema<FindDuplicateCodeRequest>();
        var derivedTypesSchema = target.CreateInputSchema<FindDerivedTypesRequest>();
        var typeHierarchySchema = target.CreateInputSchema<GetTypeHierarchyRequest>();
        var codeContextSchema = target.CreateInputSchema<GetCodeContextRequest>();
        var transactionPreviewSchema = target.CreateInputSchema<TransactionPreviewRequest>();
        var prepareFixAllSchema = target.CreateInputSchema<PrepareFixAllRequest>();
        var dependencyCyclesSchema = target.CreateInputSchema<FindDependencyCyclesRequest>();

        GetProperty(calleesSchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(operationTreeSchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(8);
        GetProperty(controlFlowGraphSchema, "maxBlocks").GetProperty("default").GetInt32().Should().Be(64);
        GetProperty(controlFlowGraphSchema, "maxRegions").GetProperty("default").GetInt32().Should().Be(32);
        GetProperty(duplicateCodeSchema, "minimumStatements").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(derivedTypesSchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(typeHierarchySchema, "maxDepth").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(codeContextSchema, "beforeLines").GetProperty("default").GetInt32().Should().Be(10);
        GetProperty(codeContextSchema, "afterLines").GetProperty("default").GetInt32().Should().Be(10);
        GetProperty(codeContextSchema, "beforeLines").GetProperty("maximum").GetInt32().Should().Be(100);
        GetProperty(codeContextSchema, "afterLines").GetProperty("maximum").GetInt32().Should().Be(100);
        GetProperty(transactionPreviewSchema, "contextLines").GetProperty("default").GetInt32().Should().Be(3);
        GetProperty(prepareFixAllSchema, "maxChanges").GetProperty("default").GetInt32().Should().Be(50);
        GetProperty(prepareFixAllSchema, "affectedDocumentsLimit").GetProperty("default").GetInt32().Should().Be(20);
        GetProperty(dependencyCyclesSchema, "nodesLimit").GetProperty("default").GetInt32().Should().Be(25_000);
        GetProperty(dependencyCyclesSchema, "nodesLimit").GetProperty("maximum").GetInt32().Should().Be(100_000);
        GetProperty(dependencyCyclesSchema, "edgesLimit").GetProperty("default").GetInt32().Should().Be(100_000);
        GetProperty(dependencyCyclesSchema, "edgesLimit").GetProperty("maximum").GetInt32().Should().Be(500_000);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_CuratedResultLimit_WHEN_ExportingInputSchema_THEN_ShouldPublishIntegerDefault()
    {
        var target = CreateTarget();

        var result = target.CreateInputSchema<FindCalleesRequest>();

        var limitProperty = GetProperty(result, "calleesLimit");
        limitProperty.GetProperty("default").GetInt32().Should().Be(100);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ClosedStringOptions_WHEN_ExportingInputSchemas_THEN_ShouldPublishAllowedValuesAndDefaults()
    {
        var target = CreateTarget();

        var cycleGranularity = GetProperty(target.CreateInputSchema<FindDependencyCyclesRequest>(), "granularity");
        var graphGranularity = GetProperty(target.CreateInputSchema<GetDependencyGraphRequest>(), "granularity");
        var minimumAccessibility = GetProperty(target.CreateInputSchema<GetApiSurfaceRequest>(), "minimumAccessibility");

        cycleGranularity.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Equal("Project", "Namespace", "Type");

        graphGranularity.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Equal("Project", "Namespace", "Type", "Symbol");

        minimumAccessibility.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Equal("Public", "Protected", "Internal");

        cycleGranularity.GetProperty("default").GetString().Should().Be("Type");
        graphGranularity.GetProperty("default").GetString().Should().Be("Type");
        minimumAccessibility.GetProperty("default").GetString().Should().Be("Public");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_WorkspaceOpenPath_WHEN_ExportingInputSchema_THEN_ShouldPublishRequiredNonNullableProperty()
    {
        var target = CreateTarget();

        var schema = target.CreateInputSchema<WorkspaceOpenRequest>();
        var requiredProperties = schema.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();

        requiredProperties.Should().Contain("path");
        AllowsNull(GetProperty(schema, "path")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_WorkspaceMsBuildProperties_WHEN_ExportingInputSchema_THEN_ShouldPublishClosedOptionalAllowlist()
    {
        var target = CreateTarget();

        var schema = target.CreateInputSchema<WorkspaceOpenRequest>();
        var propertiesSchema = GetProperty(schema, "msBuildProperties");
        var publishedPropertyNames = propertiesSchema.GetProperty("properties")
            .EnumerateObject()
            .Select(static property => property.Name)
            .ToArray();

        publishedPropertyNames.Should().BeEquivalentTo(
            "artifactsPath",
            "configuration",
            "platform",
            "runtimeIdentifier",
            "targetFramework");

        propertiesSchema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        schema.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .NotContain("msBuildProperties");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ErrorReportingRequests_WHEN_ExportingInputSchemas_THEN_ShouldPublishRequiredTypedIdentifiers()
    {
        var target = CreateTarget();

        var getDetailsSchema = target.CreateInputSchema<GetErrorDetailsRequest>();
        var prepareSchema = target.CreateInputSchema<PrepareErrorReportRequest>();
        var submitSchema = target.CreateInputSchema<SubmitErrorReportRequest>();

        AssertRequiredStringProperty(getDetailsSchema, "correlationId", expectedFormat: "uuid");
        AssertRequiredStringProperty(prepareSchema, "correlationId", expectedFormat: "uuid");
        AssertRequiredStringProperty(submitSchema, "submissionHandle");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_DocumentRequest_WHEN_ExportingInputSchema_THEN_ShouldPublishProjectQualifier()
    {
        var target = CreateTarget();

        var result = target.CreateInputSchema<FormatDocumentRequest>();

        var documentProperty = GetProperty(result, "document");
        var projectProperty = GetProperty(documentProperty, "project");
        GetProperty(projectProperty, "projectId").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        GetProperty(projectProperty, "name").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        GetProperty(projectProperty, "path").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        GetProperty(projectProperty, "targetFramework").ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_FormatDocumentRange_WHEN_ExportingInputSchema_THEN_ShouldPublishDocumentlessRange()
    {
        var target = CreateTarget();

        var result = target.CreateInputSchema<FormatDocumentRequest>();
        var rangeProperties = GetProperty(result, "range").GetProperty("properties");

        rangeProperties.TryGetProperty("start", out _).Should().BeTrue();
        rangeProperties.TryGetProperty("length", out _).Should().BeTrue();
        rangeProperties.TryGetProperty("document", out _).Should().BeFalse();
        rangeProperties.GetProperty("start").GetProperty("minimum").GetInt32().Should().Be(0);
        rangeProperties.GetProperty("length").GetProperty("minimum").GetInt32().Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_DocumentBoundLocationSelectors_WHEN_ExportingInputSchema_THEN_ShouldPublishRequiredDocumentAndRangeConstraints()
    {
        var target = CreateTarget();

        var result = target.CreateInputSchema<GetCodeContextRequest>();
        var location = GetProperty(result, "location");
        var span = GetProperty(location, "span");
        var spanProperties = span.GetProperty("properties");
        var spanRequiredProperties = span.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();
        var range = spanProperties.GetProperty("range");
        var rangeProperties = range.GetProperty("properties");
        var selection = GetProperty(location, "selection");
        var selectionProperties = selection.GetProperty("properties");
        var selectionRequiredProperties = selection.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();

        spanRequiredProperties.Should().Contain("document");
        spanRequiredProperties.Should().Contain("range");
        AllowsNull(spanProperties.GetProperty("document")).Should().BeFalse();
        AllowsNull(range).Should().BeFalse();
        rangeProperties.GetProperty("start").GetProperty("minimum").GetInt32().Should().Be(0);
        rangeProperties.GetProperty("length").GetProperty("minimum").GetInt32().Should().Be(0);
        selectionRequiredProperties.Should().Contain("document");
        AllowsNull(selectionProperties.GetProperty("document")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_GetSymbolInfoRequest_WHEN_ExportingInputSchema_THEN_ShouldNotPublishUnsupportedMemberExpansion()
    {
        var target = CreateTarget();

        var result = target.CreateInputSchema<GetSymbolInfoRequest>();
        var properties = result.GetProperty("properties");

        properties.TryGetProperty("includeMembers", out _).Should().BeFalse();
        properties.TryGetProperty("includeDocumentation", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BundledQueryRequestsWithSoleTargets_WHEN_ExportingInputSchemas_THEN_ShouldPublishRequiredNonNullableProperties()
    {
        var target = CreateTarget();
        var targetSelectorProperties = new[]
        {
            GetRequiredProperty<AnalyzeControlFlowRequest>(nameof(AnalyzeControlFlowRequest.Location)),
            GetRequiredProperty<AnalyzeDataFlowRequest>(nameof(AnalyzeDataFlowRequest.Location)),
            GetRequiredProperty<FindCallersRequest>(nameof(FindCallersRequest.Symbol)),
            GetRequiredProperty<FindDerivedTypesRequest>(nameof(FindDerivedTypesRequest.Symbol)),
            GetRequiredProperty<FindImplementationsRequest>(nameof(FindImplementationsRequest.Symbol)),
            GetRequiredProperty<FindOverloadsRequest>(nameof(FindOverloadsRequest.Symbol)),
            GetRequiredProperty<FindOverridesRequest>(nameof(FindOverridesRequest.Symbol)),
            GetRequiredProperty<FindReferencesRequest>(nameof(FindReferencesRequest.Symbol)),
            GetRequiredProperty<GetChangeImpactRequest>(nameof(GetChangeImpactRequest.Symbol)),
            GetRequiredProperty<GetCodeContextRequest>(nameof(GetCodeContextRequest.Location)),
            GetRequiredProperty<GetDocumentOptionsRequest>(nameof(GetDocumentOptionsRequest.Document)),
            GetRequiredProperty<GetDocumentOutlineRequest>(nameof(GetDocumentOutlineRequest.Document)),
            GetRequiredProperty<GetOperationTreeRequest>(nameof(GetOperationTreeRequest.Location)),
            GetRequiredProperty<GetPartialDeclarationsRequest>(nameof(GetPartialDeclarationsRequest.Symbol)),
            GetRequiredProperty<GetProjectDetailsRequest>(nameof(GetProjectDetailsRequest.Project)),
            GetRequiredProperty<GetSymbolAttributesRequest>(nameof(GetSymbolAttributesRequest.Symbol)),
            GetRequiredProperty<GetSymbolDependenciesRequest>(nameof(GetSymbolDependenciesRequest.Symbol)),
            GetRequiredProperty<GetSymbolDependentsRequest>(nameof(GetSymbolDependentsRequest.Symbol)),
            GetRequiredProperty<GetSymbolInfoRequest>(nameof(GetSymbolInfoRequest.Symbol)),
            GetRequiredProperty<GetSymbolMembersRequest>(nameof(GetSymbolMembersRequest.Symbol)),
            GetRequiredProperty<GetTestImpactRequest>(nameof(GetTestImpactRequest.Symbol)),
            GetRequiredProperty<GetTypeHierarchyRequest>(nameof(GetTypeHierarchyRequest.Symbol)),
            GetRequiredProperty<GoToDefinitionRequest>(nameof(GoToDefinitionRequest.Symbol)),
            GetRequiredProperty<ResolveSymbolRequest>(nameof(ResolveSymbolRequest.Location)),
        };

        targetSelectorProperties.Should().HaveCount(24);
        AssertRequiredNonNullableProperties(target, targetSelectorProperties);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BuiltInMutationRequiredArguments_WHEN_ExportingInputSchemas_THEN_ShouldPublishRequiredNonNullableProperties()
    {
        var target = CreateTarget();
        var requiredProperties = new[]
        {
            GetRequiredProperty<FormatDocumentRequest>(nameof(FormatDocumentRequest.Document)),
            GetRequiredProperty<RenameSymbolRequest>(nameof(RenameSymbolRequest.Symbol)),
            GetRequiredProperty<RenameSymbolRequest>(nameof(RenameSymbolRequest.NewName)),
            GetRequiredProperty<StageCodeActionRequest>(nameof(StageCodeActionRequest.ActionId)),
            GetRequiredProperty<PrepareFixAllRequest>(nameof(PrepareFixAllRequest.ActionId)),
            GetRequiredProperty<PrepareFixAllRequest>(nameof(PrepareFixAllRequest.Scope)),
            GetRequiredProperty<TransactionHistoryRequest>(nameof(TransactionHistoryRequest.Direction)),
        };

        requiredProperties.Should().HaveCount(7);
        AssertRequiredNonNullableProperties(target, requiredProperties);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BuiltInMutationRequests_WHEN_ExportingInputSchemas_THEN_ShouldInheritRequiredSnapshotPrecondition()
    {
        var target = CreateTarget();
        var schemaMethod = typeof(ToolSchemaFactory).GetMethod(nameof(ToolSchemaFactory.CreateInputSchema))
            ?? throw new InvalidOperationException("The input-schema factory method was not found.");

        var requestAssemblies = new[]
        {
            typeof(TransactionCommitRequest).Assembly,
            typeof(StageCodeActionRequest).Assembly,
            typeof(FormatDocumentRequest).Assembly,
        };

        var requestTypes = new List<Type>();
        var visitedAssemblies = new HashSet<Assembly>();
        foreach (var requestAssembly in requestAssemblies)
        {
            if (!visitedAssemblies.Add(requestAssembly))
            {
                continue;
            }

            foreach (var requestType in requestAssembly.GetTypes())
            {
                if (!requestType.IsAbstract
                    && requestType.IsAssignableTo(typeof(WorkspaceMutationRequest)))
                {
                    requestTypes.Add(requestType);
                }
            }
        }

        requestTypes.Should().NotBeEmpty();
        foreach (var requestType in requestTypes)
        {
            var closedSchemaMethod = schemaMethod.MakeGenericMethod(requestType);
            var invocationResult = closedSchemaMethod.Invoke(target, null);
            if (invocationResult is not JsonElement publishedSchema)
            {
                throw new InvalidOperationException("The input-schema factory did not return a JSON element.");
            }

            var requiredProperties = publishedSchema.GetProperty("required")
                .EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray();

            requiredProperties.Should().Contain("expectedSnapshot");
            AllowsNull(GetProperty(publishedSchema, "expectedSnapshot")).Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PrepareFixAllRequest_WHEN_ExportingInputSchema_THEN_ShouldRequireSnapshotPrecondition()
    {
        var target = CreateTarget();

        var schema = target.CreateInputSchema<PrepareFixAllRequest>();
        var requiredProperties = schema.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();

        requiredProperties.Should().Contain("expectedSnapshot");
        AllowsNull(GetProperty(schema, "expectedSnapshot")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ListCodeActionsRequest_WHEN_ExportingInputSchema_THEN_ShouldRequireSnapshotPrecondition()
    {
        var target = CreateTarget();

        var schema = target.CreateInputSchema<ListCodeActionsRequest>();
        var requiredProperties = schema.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();

        requiredProperties.Should().Contain("expectedSnapshot");
        AllowsNull(GetProperty(schema, "expectedSnapshot")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BuiltInToolRequests_WHEN_AuditingLimitProperties_THEN_EveryLimitShouldDeclareAndPublishItsDefault()
    {
        var target = CreateTarget();
        var schemaMethod = typeof(ToolSchemaFactory).GetMethod(nameof(ToolSchemaFactory.CreateInputSchema))
            ?? throw new InvalidOperationException("The input-schema factory method was not found.");

        var requestAssemblies = new[]
        {
            typeof(TransactionPreviewRequest).Assembly,
            typeof(StageCodeActionRequest).Assembly,
            typeof(FindCalleesRequest).Assembly,
        };

        var requestTypes = requestAssemblies
            .Distinct()
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.Name.EndsWith("Request", StringComparison.Ordinal)
                && type.Namespace?.Contains(".Contracts", StringComparison.Ordinal) == true)
            .ToArray();

        var limitProperties = requestTypes
            .SelectMany(static type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(IsLimitProperty)
            .ToArray();

        limitProperties.Should().NotBeEmpty();
        foreach (var limitProperty in limitProperties)
        {
            var declaringType = limitProperty.DeclaringType
                ?? throw new InvalidOperationException("The limit property did not have a declaring type.");

            var closedSchemaMethod = schemaMethod.MakeGenericMethod(declaringType);
            var invocationResult = closedSchemaMethod.Invoke(target, null);
            if (invocationResult is not JsonElement publishedSchema)
            {
                throw new InvalidOperationException("The input-schema factory did not return a JSON element.");
            }

            var jsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(limitProperty.Name);
            var publishedDefault = GetProperty(publishedSchema, jsonPropertyName).GetProperty("default");

            var fixedDefault = limitProperty.GetCustomAttribute<DefaultValueAttribute>()
                ?? throw new InvalidOperationException($"{declaringType.Name}.{limitProperty.Name} must declare its fixed default.");

            publishedDefault.GetInt32().Should().Be(Convert.ToInt32(fixedDefault.Value, System.Globalization.CultureInfo.InvariantCulture));
            var defaultRequest = Activator.CreateInstance(declaringType, nonPublic: true)
                ?? throw new InvalidOperationException($"{declaringType.Name} could not be constructed for its default-value audit.");

            limitProperty.GetValue(defaultRequest).Should().Be(fixedDefault.Value);

            var range = limitProperty.GetCustomAttribute<RangeAttribute>()
                ?? throw new InvalidOperationException($"{declaringType.Name}.{limitProperty.Name} must declare its valid range.");

            var publishedLimit = GetProperty(publishedSchema, jsonPropertyName);
            publishedLimit.GetProperty("minimum").GetInt32()
                .Should().Be(Convert.ToInt32(range.Minimum, System.Globalization.CultureInfo.InvariantCulture));

            publishedLimit.GetProperty("maximum").GetInt32()
                .Should().Be(Convert.ToInt32(range.Maximum, System.Globalization.CultureInfo.InvariantCulture));

            if (IsResponseCollectionLimitProperty(limitProperty))
            {
                Convert.ToInt32(fixedDefault.Value, System.Globalization.CultureInfo.InvariantCulture).Should().BePositive();
                Convert.ToInt32(range.Minimum, System.Globalization.CultureInfo.InvariantCulture).Should().Be(0);
                Convert.ToInt32(range.Maximum, System.Globalization.CultureInfo.InvariantCulture).Should().Be(int.MaxValue);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_FullOutputSchemas_WHEN_ExportingResponseContracts_THEN_ShouldMatchSupportedDataNullability()
    {
        var target = CreateTarget();

        var directSchema = target.CreateDirectOutputSchema(typeof(SolutionStructureData));
        var querySchema = target.CreateOutputSchema(PublishedToolKind.Query, typeof(SolutionStructureData));
        var mutationSchema = target.CreateOutputSchema(PublishedToolKind.Mutation, typeof(MutationData));

        var directData = GetSuccessDataSchema(directSchema);
        var queryData = GetSuccessDataSchema(querySchema);
        var mutationData = GetSuccessDataSchema(mutationSchema);

        AllowsNull(directData).Should().BeTrue();
        AllowsNull(queryData).Should().BeTrue();
        AllowsNull(mutationData).Should().BeFalse();
        directData.GetRawText().Should().ContainAll("folders", "projects");
        queryData.GetRawText().Should().ContainAll("folders", "projects");
        mutationData.GetProperty("properties").TryGetProperty("staged", out _).Should().BeTrue();
    }

    private static ToolSchemaFactory CreateTarget()
    {
        var schemaProvider = new McpSdkSchemaProvider();

        return new ToolSchemaFactory(schemaProvider);
    }

    private static void AssertRequiredNonNullableProperties(ToolSchemaFactory target, IReadOnlyList<PropertyInfo> targetSelectorProperties)
    {
        var schemaMethod = typeof(ToolSchemaFactory).GetMethod(nameof(ToolSchemaFactory.CreateInputSchema))
            ?? throw new InvalidOperationException("The input-schema factory method was not found.");

        foreach (var targetSelectorProperty in targetSelectorProperties)
        {
            var declaringType = targetSelectorProperty.DeclaringType
                ?? throw new InvalidOperationException("The target selector property did not have a declaring type.");

            var closedSchemaMethod = schemaMethod.MakeGenericMethod(declaringType);
            var invocationResult = closedSchemaMethod.Invoke(target, null);
            if (invocationResult is not JsonElement publishedSchema)
            {
                throw new InvalidOperationException("The input-schema factory did not return a JSON element.");
            }

            var jsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(targetSelectorProperty.Name);
            var requiredProperties = publishedSchema.GetProperty("required")
                .EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray();

            requiredProperties.Should().Contain(jsonPropertyName);
            AllowsNull(GetProperty(publishedSchema, jsonPropertyName)).Should().BeFalse();
        }
    }

    private static PropertyInfo GetRequiredProperty<TRequest>(string propertyName)
    {
        return typeof(TRequest).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{typeof(TRequest).Name}.{propertyName} was not found.");
    }

    private static void AssertRequiredStringProperty(
        JsonElement schema,
        string propertyName,
        string? expectedFormat = null)
    {
        schema.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Contain(propertyName);

        var property = GetProperty(schema, propertyName);
        var propertyType = property.GetProperty("type");
        var publishedTypes = propertyType.ValueKind == JsonValueKind.Array
            ? propertyType.EnumerateArray().Select(static item => item.GetString()).ToArray()
            : [propertyType.GetString()];
        publishedTypes.Should().Equal("string");
        AllowsNull(property).Should().BeFalse();

        if (expectedFormat is not null)
        {
            property.GetProperty("format").GetString().Should().Be(expectedFormat);
        }
    }

    private static bool IsLimitProperty(PropertyInfo property)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return propertyType == typeof(int)
            && (property.Name.StartsWith("Max", StringComparison.Ordinal)
                || property.Name.StartsWith("Minimum", StringComparison.Ordinal)
                || property.Name.EndsWith("Lines", StringComparison.Ordinal)
                || property.Name.EndsWith("Limit", StringComparison.Ordinal));
    }

    private static bool IsResponseCollectionLimitProperty(PropertyInfo property)
    {
        if (property.DeclaringType == typeof(FindDependencyCyclesRequest)
            && property.Name is nameof(FindDependencyCyclesRequest.NodesLimit) or nameof(FindDependencyCyclesRequest.EdgesLimit))
        {
            return false;
        }

        if (property.Name == nameof(GetOperationTreeRequest.NodesLimit)
            && property.DeclaringType is not null
            && (property.DeclaringType == typeof(GetOperationTreeRequest)
                || property.DeclaringType == typeof(GetDocumentOutlineRequest)))
        {
            return false;
        }

        return property.Name.EndsWith("Limit", StringComparison.Ordinal)
            || string.Equals(property.Name, "MaxChanges", StringComparison.Ordinal);
    }

    private static bool AllowsNull(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return string.Equals(type.GetString(), "null", StringComparison.Ordinal);
        }

        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(static item => string.Equals(item.GetString(), "null", StringComparison.Ordinal));
    }

    private static JsonElement GetProperty(JsonElement schema, string propertyName)
    {
        return schema.GetProperty("properties").GetProperty(propertyName);
    }

    private static JsonElement GetSuccessDataSchema(JsonElement schema)
    {
        var successSchema = schema.GetProperty("oneOf")
            .EnumerateArray()
            .Single(static candidate => candidate.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        successSchema.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Contain("data");

        return successSchema.GetProperty("properties").GetProperty("data");
    }
}
