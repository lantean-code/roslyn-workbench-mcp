using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class InputContractSchemaTransformer
{
    private static readonly JsonNode _nullType = JsonValue.Create("null");

    public static JsonNode Transform(AIJsonSchemaCreateContext context, JsonNode schema)
    {
        if (schema is not JsonObject schemaObject || context.TypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return schema;
        }

        var properties = CreatePropertyMap(context.TypeInfo);
        ValidateContractRules(context.TypeInfo, properties);

        var nullabilityContext = new NullabilityInfoContext();
        PublishPropertyMetadata(schemaObject, context.TypeInfo, nullabilityContext);
        return schemaObject;
    }

    private static Dictionary<string, JsonPropertyInfo> CreatePropertyMap(JsonTypeInfo typeInfo)
    {
        var properties = new Dictionary<string, JsonPropertyInfo>(StringComparer.Ordinal);
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is MemberInfo member)
            {
                properties.Add(member.Name, property);
            }
        }

        return properties;
    }

    private static void ValidateContractRules(
        JsonTypeInfo contractTypeInfo,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties)
    {
        var contractType = contractTypeInfo.Type;
        foreach (var attribute in contractType.GetCustomAttributes<RequiresAtLeastOneAttribute>(inherit: true))
        {
            ValidateMembers(contractType, attribute.MemberNames, properties);
        }

        foreach (var attribute in contractType.GetCustomAttributes<RequiresExactlyOneAttribute>(inherit: true))
        {
            ValidateMembers(contractType, attribute.MemberNames, properties);
        }

        foreach (var property in contractTypeInfo.Properties)
        {
            ValidateConditionalAttributes(contractType, property, properties);
        }
    }

    private static void ValidateConditionalAttributes(
        Type contractType,
        JsonPropertyInfo property,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties)
    {
        if (property.AttributeProvider is null)
        {
            return;
        }

        var attributes = property.AttributeProvider.GetCustomAttributes(inherit: true);
        foreach (var attribute in attributes)
        {
            if (attribute is RequiredWhenAttribute requiredWhen)
            {
                ValidateExpectedValue(contractType, requiredWhen.OtherProperty, requiredWhen.ExpectedValue, properties);
            }
            else if (attribute is ProhibitedUnlessAttribute prohibitedUnless)
            {
                ValidateExpectedValue(contractType, prohibitedUnless.OtherProperty, prohibitedUnless.ExpectedValue, properties);
            }
        }
    }

    private static void ValidateMembers(
        Type contractType,
        IReadOnlyList<string> memberNames,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties)
    {
        foreach (var memberName in memberNames)
        {
            ResolveMember(contractType, memberName, properties);
        }
    }

    private static void ValidateExpectedValue(
        Type contractType,
        string memberName,
        object expectedValue,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties)
    {
        var property = ResolveMember(contractType, memberName, properties);
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (!propertyType.IsInstanceOfType(expectedValue))
        {
            throw new InvalidOperationException(
                $"Validation value '{expectedValue}' is not compatible with '{contractType.FullName}.{property.Name}' of type '{propertyType.FullName}'.");
        }
    }

    private static JsonPropertyInfo ResolveMember(
        Type contractType,
        string memberName,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties)
    {
        if (!properties.TryGetValue(memberName, out var property))
        {
            throw new InvalidOperationException(
                $"Validation member '{contractType.FullName}.{memberName}' is not included in the JSON contract.");
        }

        return property;
    }

    private static void PublishPropertyMetadata(
        JsonObject schema,
        JsonTypeInfo contractTypeInfo,
        NullabilityInfoContext nullabilityContext)
    {
        foreach (var property in contractTypeInfo.Properties)
        {
            PublishNullability(schema, property, nullabilityContext);
            PublishDefaultValue(schema, contractTypeInfo, property);
        }
    }

    private static void PublishNullability(
        JsonObject schema,
        JsonPropertyInfo property,
        NullabilityInfoContext nullabilityContext)
    {
        if (property.AttributeProvider is not PropertyInfo reflectedProperty)
        {
            return;
        }

        var nullability = nullabilityContext.Create(reflectedProperty);
        if (nullability.WriteState != NullabilityState.NotNull)
        {
            return;
        }

        if (TryGetPropertySchema(schema, property, out var propertySchema))
        {
            RemoveNullType(propertySchema);
        }
    }

    private static void PublishDefaultValue(
        JsonObject schema,
        JsonTypeInfo contractTypeInfo,
        JsonPropertyInfo property)
    {
        var defaultValue = GetDefaultValue(property);
        if (defaultValue is null || !TryGetPropertySchema(schema, property, out var propertySchema))
        {
            return;
        }

        propertySchema["default"] = JsonSerializer.SerializeToNode(
            defaultValue.Value,
            property.PropertyType,
            contractTypeInfo.Options);
    }

    private static DefaultValueAttribute? GetDefaultValue(JsonPropertyInfo property)
    {
        if (property.AttributeProvider is null)
        {
            return null;
        }

        var attributes = property.AttributeProvider.GetCustomAttributes(typeof(DefaultValueAttribute), inherit: true);
        if (attributes.Length == 0)
        {
            return null;
        }

        return (DefaultValueAttribute)attributes[0];
    }

    private static bool TryGetPropertySchema(
        JsonObject schema,
        JsonPropertyInfo property,
        [NotNullWhen(true)] out JsonObject? propertySchema)
    {
        propertySchema = null;
        if (schema["properties"] is not JsonObject schemaProperties)
        {
            return false;
        }

        propertySchema = schemaProperties[property.Name] as JsonObject;
        return propertySchema is not null;
    }

    private static void RemoveNullType(JsonObject schema)
    {
        if (schema["type"] is not JsonArray types)
        {
            return;
        }

        for (var index = types.Count - 1; index >= 0; index--)
        {
            if (JsonNode.DeepEquals(types[index], _nullType))
            {
                types.RemoveAt(index);
            }
        }

        if (types.Count != 1)
        {
            return;
        }

        var remainingType = types[0];
        if (remainingType is not null)
        {
            schema["type"] = remainingType.DeepClone();
        }
    }
}
