using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class InputContractSchemaTransformer
{
    public static JsonNode Transform(AIJsonSchemaCreateContext context, JsonNode schema)
    {
        if (schema is not JsonObject schemaObject || context.TypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return schema;
        }

        var propertiesByMemberName = CreatePropertyMap(context.TypeInfo);
        var nullabilityContext = new NullabilityInfoContext();
        PublishTypeConstraints(schemaObject, context.TypeInfo.Type, propertiesByMemberName, context.TypeInfo.Options);
        PublishPropertyConstraints(schemaObject, context.TypeInfo, propertiesByMemberName, nullabilityContext);

        return schema;
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

    private static void PublishTypeConstraints(
        JsonObject schema,
        Type contractType,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties,
        JsonSerializerOptions serializerOptions)
    {
        var atLeastOneAttributes = contractType.GetCustomAttributes<RequiresAtLeastOneAttribute>(inherit: true);
        foreach (var attribute in atLeastOneAttributes)
        {
            var members = ResolveMembers(contractType, attribute.MemberNames, properties);
            var branches = members
                .Select(member => CreateRequiredPropertyBranch(member, serializerOptions, forbiddenMembers: []))
                .ToArray();

            AddConstraint(schema, CreateAlternativeConstraint("anyOf", branches));
        }

        var exactlyOneAttributes = contractType.GetCustomAttributes<RequiresExactlyOneAttribute>(inherit: true);
        foreach (var attribute in exactlyOneAttributes)
        {
            var members = ResolveMembers(contractType, attribute.MemberNames, properties);
            var branches = members
                .Select(member => CreateRequiredPropertyBranch(
                    member,
                    serializerOptions,
                    members.Where(candidate => !ReferenceEquals(candidate, member))))
                .ToArray();

            AddConstraint(schema, CreateAlternativeConstraint("oneOf", branches));
        }
    }

    private static void PublishPropertyConstraints(
        JsonObject schema,
        JsonTypeInfo contractTypeInfo,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties,
        NullabilityInfoContext nullabilityContext)
    {
        foreach (var property in contractTypeInfo.Properties)
        {
            PublishNullability(schema, property, nullabilityContext);
            PublishDefaultValue(schema, contractTypeInfo, property);
            PublishRequiredWhenConstraints(schema, contractTypeInfo, properties, property);
            PublishProhibitedUnlessConstraints(schema, contractTypeInfo, properties, property);
            PublishNonEmptyGuidConstraint(schema, contractTypeInfo, property);
        }
    }

    private static void PublishNullability(
        JsonObject schema,
        JsonPropertyInfo property,
        NullabilityInfoContext nullabilityContext)
    {
        if (property.AttributeProvider is not PropertyInfo reflectedProperty
            || nullabilityContext.Create(reflectedProperty).WriteState != NullabilityState.NotNull
            || schema["properties"] is not JsonObject schemaProperties
            || schemaProperties[property.Name] is not JsonObject propertySchema)
        {
            return;
        }

        RemoveNullType(propertySchema);
    }

    private static void PublishDefaultValue(
        JsonObject schema,
        JsonTypeInfo contractTypeInfo,
        JsonPropertyInfo property)
    {
        var attribute = GetAttributes<DefaultValueAttribute>(property).SingleOrDefault();
        if (attribute is null
            || schema["properties"] is not JsonObject schemaProperties
            || schemaProperties[property.Name] is not JsonObject propertySchema)
        {
            return;
        }

        propertySchema["default"] = JsonSerializer.SerializeToNode(
            attribute.Value,
            property.PropertyType,
            contractTypeInfo.Options);
    }

    private static void PublishRequiredWhenConstraints(
        JsonObject schema,
        JsonTypeInfo contractTypeInfo,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties,
        JsonPropertyInfo property)
    {
        foreach (var attribute in GetAttributes<RequiredWhenAttribute>(property))
        {
            var controllingProperty = ResolveMember(contractTypeInfo.Type, attribute.OtherProperty, properties);
            ValidateExpectedValue(contractTypeInfo.Type, controllingProperty, attribute.ExpectedValue);

            var condition = CreateEqualityCondition(controllingProperty, attribute.ExpectedValue, contractTypeInfo.Options);
            var constrainedProperties = new JsonObject
            {
                [property.Name] = CreateProvidedSchema(property, contractTypeInfo.Options),
            };
            var consequence = new JsonObject
            {
                ["properties"] = constrainedProperties,
            };
            RequireExplicitMemberWhenOmissionIsNotProvided(consequence, property);

            var constraint = new JsonObject
            {
                ["if"] = condition,
                ["then"] = consequence,
            };

            AddConstraint(schema, constraint);
        }
    }

    private static void PublishProhibitedUnlessConstraints(
        JsonObject schema,
        JsonTypeInfo contractTypeInfo,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties,
        JsonPropertyInfo property)
    {
        foreach (var attribute in GetAttributes<ProhibitedUnlessAttribute>(property))
        {
            var controllingProperty = ResolveMember(contractTypeInfo.Type, attribute.OtherProperty, properties);
            ValidateExpectedValue(contractTypeInfo.Type, controllingProperty, attribute.ExpectedValue);

            var allowedCondition = CreateEqualityCondition(controllingProperty, attribute.ExpectedValue, contractTypeInfo.Options);
            var prohibitedProperties = new JsonObject
            {
                [property.Name] = CreateAbsentSchema(property, contractTypeInfo.Options),
            };
            var consequence = new JsonObject
            {
                ["properties"] = prohibitedProperties,
            };
            RequireExplicitMemberWhenOmissionIsProvided(consequence, property);

            var constraint = new JsonObject
            {
                ["if"] = new JsonObject
                {
                    ["not"] = allowedCondition,
                },
                ["then"] = consequence,
            };

            AddConstraint(schema, constraint);
        }
    }

    private static void PublishNonEmptyGuidConstraint(
        JsonObject schema,
        JsonTypeInfo contractTypeInfo,
        JsonPropertyInfo property)
    {
        if (!GetAttributes<NonEmptyGuidAttribute>(property).Any())
        {
            return;
        }

        var emptyGuid = JsonSerializer.SerializeToNode(Guid.Empty, contractTypeInfo.Options);
        var propertyConstraint = new JsonObject
        {
            ["not"] = new JsonObject
            {
                ["const"] = emptyGuid,
            },
        };
        var properties = new JsonObject
        {
            [property.Name] = propertyConstraint,
        };

        AddConstraint(schema, new JsonObject { ["properties"] = properties });
    }

    private static JsonObject CreateRequiredPropertyBranch(
        JsonPropertyInfo requiredMember,
        JsonSerializerOptions serializerOptions,
        IEnumerable<JsonPropertyInfo> forbiddenMembers)
    {
        var properties = new JsonObject
        {
            [requiredMember.Name] = CreateProvidedSchema(requiredMember, serializerOptions),
        };
        var explicitlyRequiredMembers = new JsonArray();
        if (!HasProvidedDefault(requiredMember))
        {
            explicitlyRequiredMembers.Add(requiredMember.Name);
        }

        foreach (var forbiddenMember in forbiddenMembers)
        {
            properties[forbiddenMember.Name] = CreateAbsentSchema(forbiddenMember, serializerOptions);
            if (HasProvidedDefault(forbiddenMember))
            {
                explicitlyRequiredMembers.Add(forbiddenMember.Name);
            }
        }

        var branch = new JsonObject
        {
            ["properties"] = properties,
        };
        if (explicitlyRequiredMembers.Count > 0)
        {
            branch["required"] = explicitlyRequiredMembers;
        }

        return branch;
    }

    private static JsonObject CreateAlternativeConstraint(string keyword, JsonObject[] branches)
    {
        return new JsonObject
        {
            [keyword] = new JsonArray(branches),
        };
    }

    private static JsonObject CreateEqualityCondition(
        JsonPropertyInfo property,
        object expectedValue,
        JsonSerializerOptions serializerOptions)
    {
        var serializedValue = JsonSerializer.SerializeToNode(expectedValue, property.PropertyType, serializerOptions);
        var propertyConstraint = new JsonObject
        {
            ["const"] = serializedValue,
        };
        var properties = new JsonObject
        {
            [property.Name] = propertyConstraint,
        };

        var condition = new JsonObject
        {
            ["properties"] = properties,
        };
        if (!TryGetEffectiveDefaultValue(property, out var defaultValue)
            || !Equals(defaultValue, expectedValue))
        {
            condition["required"] = new JsonArray(property.Name);
        }

        return condition;
    }

    private static JsonObject CreateProvidedSchema(JsonPropertyInfo property, JsonSerializerOptions serializerOptions)
    {
        if (property.PropertyType == typeof(string))
        {
            return new JsonObject
            {
                ["type"] = "string",
                ["pattern"] = @"\S",
            };
        }

        var propertyTypeInfo = serializerOptions.GetTypeInfo(property.PropertyType);
        if (propertyTypeInfo.Kind == JsonTypeInfoKind.Enumerable)
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
            };
        }

        if (propertyTypeInfo.Kind == JsonTypeInfoKind.Dictionary)
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["minProperties"] = 1,
            };
        }

        return new JsonObject
        {
            ["not"] = new JsonObject
            {
                ["type"] = "null",
            },
        };
    }

    private static JsonObject CreateAbsentSchema(JsonPropertyInfo property, JsonSerializerOptions serializerOptions)
    {
        return new JsonObject
        {
            ["not"] = CreateProvidedSchema(property, serializerOptions),
        };
    }

    private static void RequireExplicitMemberWhenOmissionIsNotProvided(JsonObject schema, JsonPropertyInfo property)
    {
        if (!HasProvidedDefault(property))
        {
            schema["required"] = new JsonArray(property.Name);
        }
    }

    private static void RequireExplicitMemberWhenOmissionIsProvided(JsonObject schema, JsonPropertyInfo property)
    {
        if (HasProvidedDefault(property))
        {
            schema["required"] = new JsonArray(property.Name);
        }
    }

    private static bool HasProvidedDefault(JsonPropertyInfo property)
    {
        return TryGetEffectiveDefaultValue(property, out var defaultValue)
            && ValidationMemberAccess.IsProvided(defaultValue);
    }

    private static bool TryGetEffectiveDefaultValue(JsonPropertyInfo property, out object? defaultValue)
    {
        var defaultValueAttribute = GetAttributes<DefaultValueAttribute>(property).SingleOrDefault();
        if (defaultValueAttribute is not null)
        {
            defaultValue = defaultValueAttribute.Value;
            return true;
        }

        if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
        {
            defaultValue = Activator.CreateInstance(property.PropertyType);
            return true;
        }

        defaultValue = null;
        return false;
    }

    private static JsonPropertyInfo[] ResolveMembers(
        Type contractType,
        IReadOnlyList<string> memberNames,
        IReadOnlyDictionary<string, JsonPropertyInfo> properties)
    {
        var members = new JsonPropertyInfo[memberNames.Count];
        for (var index = 0; index < memberNames.Count; index++)
        {
            members[index] = ResolveMember(contractType, memberNames[index], properties);
        }

        return members;
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

    private static void ValidateExpectedValue(Type contractType, JsonPropertyInfo property, object expectedValue)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (!propertyType.IsInstanceOfType(expectedValue))
        {
            throw new InvalidOperationException(
                $"Validation value '{expectedValue}' is not compatible with '{contractType.FullName}.{property.Name}' of type '{propertyType.FullName}'.");
        }
    }

    private static IEnumerable<TAttribute> GetAttributes<TAttribute>(JsonPropertyInfo property)
        where TAttribute : Attribute
    {
        return property.AttributeProvider?
            .GetCustomAttributes(typeof(TAttribute), inherit: true)
            .OfType<TAttribute>()
            ?? [];
    }

    private static void AddConstraint(JsonObject schema, JsonObject constraint)
    {
        if (schema["allOf"] is not JsonArray constraints)
        {
            constraints = [];
            schema["allOf"] = constraints;
        }

        constraints.Add(constraint);
    }

    private static void RemoveNullType(JsonObject schema)
    {
        if (schema["type"] is not JsonArray types)
        {
            return;
        }

        var nullType = JsonValue.Create("null");
        for (var index = types.Count - 1; index >= 0; index--)
        {
            if (JsonNode.DeepEquals(types[index], nullType))
            {
                types.RemoveAt(index);
            }
        }
    }
}
