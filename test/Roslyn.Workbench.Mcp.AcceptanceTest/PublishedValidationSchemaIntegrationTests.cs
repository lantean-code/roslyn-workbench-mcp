using System.Text.Json;
using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedValidationSchemaIntegrationTests
{
    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_ListingTools_THEN_ShouldPublishCompleteConstructionGuidanceSchemas()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(TestContext.Current.CancellationToken);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);

            AssertProjectSelectorSchema(tools);
            AssertDocumentSelectorSchema(tools);
            AssertScopeSelectorSchema(tools);
            AssertWorkspaceSelectorSchema(tools);

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

    private static void AssertProjectSelectorSchema(IList<McpClientTool> tools)
    {
        var root = GetToolInputSchema(tools, "get-project-details");
        var project = ResolvePropertySchema(root, "project");

        AssertCompleteObject(project, "projectId", "name", "path", "targetFramework");
    }

    private static void AssertDocumentSelectorSchema(IList<McpClientTool> tools)
    {
        var root = GetToolInputSchema(tools, "get-document-outline");
        var document = ResolvePropertySchema(root, "document");

        AssertCompleteObject(document, "documentId", "path", "project");

        var project = ResolvePropertySchema(root, document, "project");
        AssertCompleteObject(project, "projectId", "name", "path", "targetFramework");
    }

    private static void AssertScopeSelectorSchema(IList<McpClientTool> tools)
    {
        var root = GetToolInputSchema(tools, "get-diagnostics");
        var scope = ResolvePropertySchema(root, "scope");

        AssertCompleteObject(scope, "document", "kind", "project", "projects");
        scope
            .GetProperty("properties")
            .GetProperty("kind")
            .GetProperty("default")
            .GetString()
            .Should()
            .Be("Solution");
    }

    private static void AssertWorkspaceSelectorSchema(IList<McpClientTool> tools)
    {
        var root = GetToolInputSchema(tools, "search-symbols");
        var workspace = ResolvePropertySchema(root, "workspace");

        AssertCompleteObject(workspace, "alias", "path", "workspaceId");

        var workspaceId = workspace.GetProperty("properties").GetProperty("workspaceId");
        workspaceId.GetProperty("format").GetString().Should().Be("uuid");
        workspaceId.TryGetProperty("not", out _).Should().BeFalse();
    }

    private static void AssertCompleteObject(JsonElement schema, params string[] expectedPropertyNames)
    {
        var actualPropertyNames = new List<string>();
        foreach (var property in schema.GetProperty("properties").EnumerateObject())
        {
            actualPropertyNames.Add(property.Name);
        }

        actualPropertyNames.Should().BeEquivalentTo(expectedPropertyNames);
        schema.TryGetProperty("allOf", out _).Should().BeFalse();
        schema.TryGetProperty("anyOf", out _).Should().BeFalse();
        schema.TryGetProperty("oneOf", out _).Should().BeFalse();
        schema.TryGetProperty("if", out _).Should().BeFalse();
    }

    private static JsonElement GetToolInputSchema(IList<McpClientTool> tools, string toolName)
    {
        foreach (var tool in tools)
        {
            if (string.Equals(tool.Name, toolName, StringComparison.Ordinal))
            {
                return tool.ProtocolTool.InputSchema;
            }
        }

        throw new InvalidOperationException($"Published tool '{toolName}' was not found.");
    }

    private static JsonElement ResolvePropertySchema(JsonElement root, string propertyName)
    {
        return ResolvePropertySchema(root, root, propertyName);
    }

    private static JsonElement ResolvePropertySchema(JsonElement root, JsonElement owner, string propertyName)
    {
        var property = owner.GetProperty("properties").GetProperty(propertyName);
        return ResolveObjectSchema(root, property);
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

    private static JsonElement ResolveReference(JsonElement root, string? reference)
    {
        if (reference is null)
        {
            throw new InvalidOperationException("The published schema reference was null.");
        }

        reference.Should().StartWith("#/");

        var current = root;
        foreach (var encodedToken in reference[2..].Split('/'))
        {
            var token = encodedToken
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            current = current.GetProperty(token);
        }

        return current;
    }
}
