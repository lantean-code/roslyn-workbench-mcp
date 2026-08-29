using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.ErrorReporting.Capture;
using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Workspace.Results;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpSdkSchemaProviderIntegrationTests
{
    private readonly McpSdkSchemaProvider _target;

    public McpSdkSchemaProviderIntegrationTests()
    {
        _target = new McpSdkSchemaProvider();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_RequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishRequestProperties()
    {
        var result = _target.GetInputSchema<TestRequest>();

        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_EmptyRequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishClosedObject()
    {
        var result = _target.GetInputSchema<WorkspaceListRequest>();

        result.GetProperty("type").GetString().Should().Be("object");
        result.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_DataAnnotations_WHEN_ExportingInputSchema_THEN_ShouldPublishValidationKeywords()
    {
        var result = _target.GetInputSchema<AnnotatedRequest>();

        var properties = result.GetProperty("properties");
        var range = properties.GetProperty("range");
        var text = properties.GetProperty("text");
        var choice = properties.GetProperty("choice");

        range.GetProperty("minimum").GetInt32().Should().Be(1);
        range.GetProperty("maximum").GetInt32().Should().Be(10);
        text.GetProperty("minLength").GetInt32().Should().Be(2);
        text.GetProperty("maxLength").GetInt32().Should().Be(10);
        choice.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Equal("First", "Second");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_UnattributedEnum_WHEN_ExportingSchemas_THEN_ShouldPublishExactStringValues()
    {
        var inputSchema = _target.GetInputSchema<EnumRequest>();
        var valueSchema = _target.GetValueSchema<EnumResponse>();

        AssertStringEnum(
            inputSchema.GetProperty("properties").GetProperty("value"),
            "First",
            "Second");

        AssertStringEnum(
            valueSchema.GetProperty("properties").GetProperty("value"),
            "First",
            "Second");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CodeActionEnums_WHEN_ExportingSchemas_THEN_ShouldPublishExactStringValues()
    {
        var listRequestSchema = _target.GetInputSchema<ListCodeActionsRequest>();
        var prepareRequestSchema = _target.GetInputSchema<PrepareFixAllRequest>();
        var itemSchema = _target.GetValueSchema<CodeActionListItem>();
        var prepareDataSchema = _target.GetValueSchema<PrepareFixAllData>();

        AssertStringEnum(
            listRequestSchema.GetProperty("properties").GetProperty("kinds"),
            "CodeFixes",
            "Refactorings",
            "All");

        AssertStringEnum(
            prepareRequestSchema.GetProperty("properties").GetProperty("scope"),
            "Document",
            "Project",
            "Solution");

        AssertStringEnum(
            itemSchema.GetProperty("properties").GetProperty("kind"),
            "CodeFix",
            "Refactoring");

        var fixAllScopes = itemSchema.GetProperty("properties").GetProperty("fixAllScopes");
        AssertStringEnum(
            fixAllScopes.GetProperty("items"),
            "Document",
            "Project",
            "Solution");

        AssertStringEnum(
            prepareDataSchema.GetProperty("properties").GetProperty("scope"),
            "Document",
            "Project",
            "Solution");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PresenceAndNullabilityContracts_WHEN_ExportingInputSchema_THEN_ShouldPublishPresenceAndNullabilityState()
    {
        var result = _target.GetInputSchema<PresenceRequest>();

        var required = result.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();

        required.Should().Contain("dataAnnotatedRequired");
        required.Should().Contain("requiredNonNullable");
        required.Should().Contain("requiredNullable");
        required.Should().NotContain("notNullAnnotated");

        var properties = result.GetProperty("properties");
        AllowsNull(properties.GetProperty("dataAnnotatedRequired")).Should().BeTrue();
        AllowsNull(properties.GetProperty("requiredNonNullable")).Should().BeFalse();
        AllowsNull(properties.GetProperty("requiredNullable")).Should().BeTrue();
        AllowsNull(properties.GetProperty("notNullAnnotated")).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PropertyDefaultValueAttribute_WHEN_ExportingInputSchema_THEN_ShouldPublishPropertyDefault()
    {
        var result = _target.GetInputSchema<DefaultedRequest>();

        result.GetProperty("properties")
            .GetProperty("limit")
            .GetProperty("default")
            .GetInt32()
            .Should()
            .Be(25);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ReferenceHeavyRequest_WHEN_ExportingInputSchema_THEN_ShouldRebaseReferencesToPublishedRoot()
    {
        var result = _target.GetInputSchema<FindReferencesRequest>();
        var json = result.GetRawText();

        json.Should().Contain("\"$ref\":\"#/properties/");
        json.Should().NotContain("#/properties/request/");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ObjectContract_WHEN_ExportingValueSchema_THEN_ShouldPublishProperties()
    {
        var result = _target.GetValueSchema<TestResponse>();

        result.GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ServerRequestContract_WHEN_ExportingInputSchema_THEN_ShouldPublishPropertyDescription()
    {
        var result = _target.GetInputSchema<WorkspaceOpenRequest>();

        result.GetProperty("properties")
            .GetProperty("path")
            .GetProperty("description")
            .GetString()
            .Should()
            .Be("The absolute solution or project path to load.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ServerResponseContract_WHEN_ExportingValueSchema_THEN_ShouldPublishPropertyDescription()
    {
        var result = _target.GetValueSchema<WorkspaceOpenData>();

        result.GetProperty("properties")
            .GetProperty("projectCount")
            .GetProperty("description")
            .GetString()
            .Should()
            .Be("The loaded project count.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ResponseContainingSharedShape_WHEN_ExportingValueSchema_THEN_ShouldPublishNestedPropertyDescription()
    {
        var result = _target.GetValueSchema<WorkspaceOpenData>();

        result.GetRawText().Should().Contain("\"description\":\"The stable server-generated workspace identifier.\"");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_WorkspaceOwnedContractGraph_WHEN_ExportingSchemas_THEN_ShouldPublishNestedPropertyDescriptions()
    {
        var inputSchema = _target.GetInputSchema<WorkspaceOpenRequest>();
        var outputSchema = _target.GetValueSchema<TransactionPreviewData>();

        inputSchema.GetProperty("properties")
            .GetProperty("msBuildProperties")
            .GetProperty("properties")
            .GetProperty("artifactsPath")
            .GetProperty("description")
            .GetString()
            .Should()
            .Be("Existing absolute directory used for MSBuild intermediate and output artifacts.");
        outputSchema.GetProperty("properties")
            .GetProperty("diff")
            .GetProperty("properties")
            .GetProperty("hunks")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("lines")
            .GetProperty("description")
            .GetString()
            .Should()
            .Be("Unified-diff content lines prefixed with space, +, or -.");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_TransactionPreviewRequest_WHEN_ExportingInputSchema_THEN_ShouldExplainDetailedDiffFields()
    {
        var schema = _target.GetInputSchema<TransactionPreviewRequest>();
        var properties = schema.GetProperty("properties");

        properties.GetProperty("document").GetProperty("description").GetString()
            .Should()
            .Be("Document whose detailed diff should be returned; required when includeDiff is true.");
        properties.GetProperty("contextLines").GetProperty("description").GetString()
            .Should()
            .Be("Number of unchanged context lines around each diff hunk when includeDiff is true.");

        var documentProperty = typeof(TransactionPreviewRequest).GetProperty(nameof(TransactionPreviewRequest.Document))
            ?? throw new InvalidOperationException("The transaction-preview document property was not found.");
        var requiredWhen = documentProperty.GetCustomAttribute<RequiredWhenAttribute>()
            ?? throw new InvalidOperationException("The transaction-preview document requirement was not found.");

        requiredWhen.OtherProperty.Should().Be(nameof(TransactionPreviewRequest.IncludeDiff));
        requiredWhen.ExpectedValue.Should().Be(true);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PublishedServerAndSharedContracts_WHEN_AuditingSerializedProperties_THEN_ShouldFindDescriptionAttributes()
    {
        var serverProperties = GetSerializedPublicProperties(
            typeof(WorkspaceOpenRequest).Assembly,
            static type => type.Namespace is not null
                && (type.Namespace.StartsWith("Roslyn.Workbench.Mcp.Contracts.", StringComparison.Ordinal)
                    || type.Namespace.StartsWith("Roslyn.Workbench.Mcp.ErrorReporting.Contracts", StringComparison.Ordinal)
                    || type.Namespace.StartsWith("Roslyn.Workbench.Mcp.Protocol.Results", StringComparison.Ordinal))
                && !type.Namespace.StartsWith("Roslyn.Workbench.Mcp.Contracts.Validation", StringComparison.Ordinal));
        var capturedErrorProperties = GetSerializedPublicProperties(
            typeof(CapturedErrorRecord),
            typeof(CapturedException),
            typeof(CapturedStackFrame),
            typeof(CapturedWorkspaceContext));
        var sharedProperties = GetSerializedPublicProperties(
            typeof(WorkspaceIdentity).Assembly,
            static type => type.Namespace is "Roslyn.Workbench.Mcp.Workspace.Selectors" or "Roslyn.Workbench.Mcp.Workspace.Results");
        var publishedRootPropertyTypes = serverProperties
            .Concat(capturedErrorProperties)
            .Concat(sharedProperties)
            .Select(static property => property.PropertyType);
        var workspaceProperties = GetReachableSerializedPublicProperties(
            publishedRootPropertyTypes,
            typeof(TransactionInfo).Assembly);

        serverProperties.Concat(capturedErrorProperties).Concat(sharedProperties).Concat(workspaceProperties)
            .Where(static property => string.IsNullOrWhiteSpace(property.GetCustomAttribute<DescriptionAttribute>()?.Description))
            .Select(static property => $"{property.DeclaringType!.FullName}.{property.Name}")
            .Should()
            .BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PrimitiveBoundedCollection_WHEN_ExportingValueSchema_THEN_ShouldPublishBoundedCollectionProperties()
    {
        var result = _target.GetValueSchema<BoundedCollection<string>>();

        result.GetProperty("properties").TryGetProperty("items", out _).Should().BeTrue();
        result.GetProperty("properties").TryGetProperty("hasMore", out _).Should().BeTrue();
        result.GetProperty("properties").TryGetProperty("totalCount", out _).Should().BeTrue();
        result.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).Should().NotContain("totalCount");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ObjectBoundedCollection_WHEN_ExportingValueSchema_THEN_ShouldPreserveItemProperties()
    {
        var result = _target.GetValueSchema<BoundedCollection<TestResponse>>();

        result.GetRawText().Should().Contain("value");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_NullableValueContract_WHEN_ExportingValueSchema_THEN_ShouldNormalizeObjectType()
    {
        var result = _target.GetValueSchema<TestStruct?>();

        result.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PreviouslyExportedContract_WHEN_ExportingAgain_THEN_ShouldReturnCachedSchema()
    {
        var first = _target.GetValueSchema<TestResponse>();

        var second = _target.GetValueSchema<TestResponse>();

        second.GetRawText().Should().Be(first.GetRawText());
    }

    private static bool AllowsNull(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => string.Equals(type.GetString(), "null", StringComparison.Ordinal),
            JsonValueKind.Array => type.EnumerateArray().Any(static item => string.Equals(item.GetString(), "null", StringComparison.Ordinal)),
            _ => false,
        };
    }

    private static IEnumerable<PropertyInfo> GetSerializedPublicProperties(Assembly assembly, Func<Type, bool> includesType)
    {
        return assembly.GetTypes()
            .Where(includesType)
            .Where(static type => type.IsClass)
            .SelectMany(static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(IsSerializedPublicProperty);
    }

    private static IEnumerable<PropertyInfo> GetSerializedPublicProperties(params Type[] types)
    {
        return types
            .SelectMany(static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(IsSerializedPublicProperty);
    }

    private static List<PropertyInfo> GetReachableSerializedPublicProperties(
        IEnumerable<Type> rootTypes,
        Assembly contractAssembly)
    {
        var pendingTypes = new Queue<Type>(rootTypes);
        var visitedTypes = new HashSet<Type>();
        var properties = new List<PropertyInfo>();

        while (pendingTypes.TryDequeue(out var type))
        {
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType is not null)
            {
                pendingTypes.Enqueue(nullableType);
                continue;
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType is not null)
                {
                    pendingTypes.Enqueue(elementType);
                }

                continue;
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    pendingTypes.Enqueue(argument);
                }
            }

            if (type.Assembly != contractAssembly || !type.IsClass || !visitedTypes.Add(type))
            {
                continue;
            }

            var serializedProperties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(IsSerializedPublicProperty)
                .ToArray();
            properties.AddRange(serializedProperties);

            foreach (var property in serializedProperties)
            {
                pendingTypes.Enqueue(property.PropertyType);
            }
        }

        return properties;
    }

    private static bool IsSerializedPublicProperty(PropertyInfo property)
    {
        return property.GetIndexParameters().Length == 0
            && property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always;
    }

    private static void AssertStringEnum(JsonElement schema, params string[] expectedValues)
    {
        schema.GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Equal(expectedValues);
    }

#pragma warning disable CA1812 // Schema fixtures are consumed through type metadata without construction.
    private sealed record TestRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed record EnumRequest
    {
        public required TestEnum Value { get; init; }
    }

    private sealed record EnumResponse
    {
        public required TestEnum Value { get; init; }
    }

    private sealed record AnnotatedRequest
    {
        [Range(1, 10)]
        public int Range { get; init; } = 1;

        [StringLength(10, MinimumLength = 2)]
        public string? Text { get; init; }

        [AllowedValues("First", "Second")]
        public string? Choice { get; init; }
    }

    private sealed record PresenceRequest
    {
        [Required]
        public string? DataAnnotatedRequired { get; init; }

        public required string RequiredNonNullable { get; init; }

        public required string? RequiredNullable { get; init; }

        [NotNull]
        public string? NotNullAnnotated { get; init; }
    }

    private sealed record DefaultedRequest
    {
        [DefaultValue(25)]
        public int? Limit { get; init; } = 25;
    }

#pragma warning restore CA1812

    private readonly record struct TestStruct
    {
        public string Value { get; init; }
    }

    private enum TestEnum
    {
        First,
        Second,
    }
}
