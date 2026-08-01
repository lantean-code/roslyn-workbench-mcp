using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedValidationSchemaIntegrationTests
{
    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_ListingTools_THEN_ShouldPublishAttributeDerivedValidationSchemas()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);

            AssertRequiresAtLeastOneSchema(tools);
            AssertRequiresExactlyOneSchema(tools);
            AssertRequiredWhenSchema(tools);
            AssertProhibitedUnlessSchema(tools);
            AssertNonEmptyGuidSchema(tools);

            var completion = await target.StopAsync();

            completion.ProcessId.Should().NotBeNull();
            completion.ExitCode.Should().NotBeNull();
            completion.Exception.Should().BeNull();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static void AssertRequiresAtLeastOneSchema(IList<McpClientTool> tools)
    {
        var schema = GetSelectorSchema(tools, "get-project-details", "project");
        var constraint = schema
            .GetProperty("allOf")
            .EnumerateArray()
            .Single(static candidate => candidate.TryGetProperty("anyOf", out _));
        var alternatives = constraint.GetProperty("anyOf");
        var requiredMembers = alternatives
            .EnumerateArray()
            .Select(static alternative => alternative.GetProperty("required")[0].GetString())
            .ToArray();

        requiredMembers.Should().BeEquivalentTo("projectId", "name", "path", "targetFramework");
        foreach (var alternative in alternatives.EnumerateArray())
        {
            var memberName = alternative.GetProperty("required")[0].GetString();
            memberName.Should().NotBeNull();
            alternative
                .GetProperty("properties")
                .GetProperty(memberName)
                .GetProperty("pattern")
                .GetString()
                .Should()
                .Be(@"\S");
        }
    }

    private static void AssertRequiresExactlyOneSchema(IList<McpClientTool> tools)
    {
        var schema = GetSelectorSchema(tools, "get-document-outline", "document");
        var constraint = schema
            .GetProperty("allOf")
            .EnumerateArray()
            .Single(static candidate => candidate.TryGetProperty("oneOf", out _));
        var alternatives = constraint.GetProperty("oneOf");
        var pathAlternative = FindAlternative(alternatives, "path");
        var documentIdAlternative = FindAlternative(alternatives, "documentId");

        AssertMeaningfulString(pathAlternative, "path");
        AssertProhibitedMeaningfulString(pathAlternative, "documentId");
        AssertMeaningfulString(documentIdAlternative, "documentId");
        AssertProhibitedMeaningfulString(documentIdAlternative, "path");
    }

    private static void AssertRequiredWhenSchema(IList<McpClientTool> tools)
    {
        var schema = GetSelectorSchema(tools, "get-diagnostics", "scope");
        var constraint = schema
            .GetProperty("allOf")
            .EnumerateArray()
            .Single(static candidate => IsRequiredWhenConstraint(candidate, "Project", "project"));

        constraint
            .GetProperty("then")
            .GetProperty("properties")
            .GetProperty("project")
            .GetProperty("not")
            .GetProperty("type")
            .GetString()
            .Should()
            .Be("null");
    }

    private static void AssertProhibitedUnlessSchema(IList<McpClientTool> tools)
    {
        var schema = GetSelectorSchema(tools, "get-api-surface", "scope");
        var constraint = schema
            .GetProperty("allOf")
            .EnumerateArray()
            .Single(static candidate => IsProhibitedUnlessConstraint(candidate, "Document", "document"));

        constraint
            .GetProperty("then")
            .GetProperty("properties")
            .GetProperty("document")
            .GetProperty("not")
            .GetProperty("not")
            .GetProperty("type")
            .GetString()
            .Should()
            .Be("null");
    }

    private static void AssertNonEmptyGuidSchema(IList<McpClientTool> tools)
    {
        var schema = GetSelectorSchema(tools, "search-symbols", "workspace");
        var constraint = schema
            .GetProperty("allOf")
            .EnumerateArray()
            .Single(static candidate => candidate.TryGetProperty("properties", out var properties)
                && properties.TryGetProperty("workspaceId", out _));

        constraint
            .GetProperty("properties")
            .GetProperty("workspaceId")
            .GetProperty("not")
            .GetProperty("const")
            .GetGuid()
            .Should()
            .Be(Guid.Empty);
    }

    private static JsonElement GetSelectorSchema(
        IList<McpClientTool> tools,
        string toolName,
        string propertyName)
    {
        var tool = tools.Single(item => item.Name == toolName);
        var root = tool.ProtocolTool.InputSchema;
        var propertySchema = root.GetProperty("properties").GetProperty(propertyName);
        return ResolveObjectSchema(root, propertySchema);
    }

    private static JsonElement FindAlternative(JsonElement alternatives, string requiredMember)
    {
        return alternatives
            .EnumerateArray()
            .Single(alternative => string.Equals(
                alternative.GetProperty("required")[0].GetString(),
                requiredMember,
                StringComparison.Ordinal));
    }

    private static void AssertMeaningfulString(JsonElement alternative, string propertyName)
    {
        alternative
            .GetProperty("properties")
            .GetProperty(propertyName)
            .GetProperty("pattern")
            .GetString()
            .Should()
            .Be(@"\S");
    }

    private static void AssertProhibitedMeaningfulString(JsonElement alternative, string propertyName)
    {
        alternative
            .GetProperty("properties")
            .GetProperty(propertyName)
            .GetProperty("not")
            .GetProperty("pattern")
            .GetString()
            .Should()
            .Be(@"\S");
    }

    private static bool IsRequiredWhenConstraint(
        JsonElement candidate,
        string expectedValue,
        string requiredProperty)
    {
        if (!candidate.TryGetProperty("if", out var condition)
            || !candidate.TryGetProperty("then", out var consequence)
            || !consequence.TryGetProperty("required", out var required))
        {
            return false;
        }

        return string.Equals(
                condition.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString(),
                expectedValue,
                StringComparison.Ordinal)
            && required.EnumerateArray().Any(item => string.Equals(item.GetString(), requiredProperty, StringComparison.Ordinal));
    }

    private static bool IsProhibitedUnlessConstraint(
        JsonElement candidate,
        string expectedValue,
        string prohibitedProperty)
    {
        if (!candidate.TryGetProperty("if", out var condition)
            || !condition.TryGetProperty("not", out var negatedCondition)
            || !candidate.TryGetProperty("then", out var consequence)
            || !consequence.TryGetProperty("properties", out var properties)
            || !properties.TryGetProperty(prohibitedProperty, out _))
        {
            return false;
        }

        return string.Equals(
            negatedCondition.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString(),
            expectedValue,
            StringComparison.Ordinal);
    }

    private static JsonElement ResolveObjectSchema(JsonElement root, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            return ResolveReference(root, reference.GetString());
        }

        if (schema.TryGetProperty("anyOf", out var alternatives))
        {
            foreach (var alternative in alternatives.EnumerateArray())
            {
                if (alternative.TryGetProperty("$ref", out reference))
                {
                    return ResolveReference(root, reference.GetString());
                }

                if (alternative.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() != "null")
                {
                    return alternative;
                }
            }
        }

        return schema;
    }

    private static JsonElement ResolveReference(JsonElement root, [NotNull] string? reference)
    {
        reference.Should().StartWith("#/$defs/");
        var definitionName = reference["#/$defs/".Length..]
            .Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

        return root.GetProperty("$defs").GetProperty(definitionName);
    }
}
