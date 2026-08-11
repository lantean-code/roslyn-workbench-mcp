using System.Text.Json;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedDistributionIntegrationTests
{
    [Fact]
    public async Task GIVEN_EnvironmentAndRepeatedArguments_WHEN_ReadingFullStatus_THEN_ShouldUseLastArgumentAndOmitSensitivePaths()
    {
        var environmentVariables = new Dictionary<string, string?>
        {
            ["ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS"] = "5",
        };
        var arguments = new[]
        {
            "--default-max-results=7",
            "--default-max-results=9",
        };

        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: arguments,
            pluginAssets: [AcceptancePluginAsset.HostQuery],
            environmentVariables: environmentVariables);

        try
        {
            var statusResult = await target.CallToolAsync(
                "server-status",
                new Dictionary<string, object?>
                {
                    ["detail"] = "Full",
                },
                TestContext.Current.CancellationToken);

            statusResult.IsError.Should().NotBeTrue();
            var status = AcceptanceProtocol.GetSuccessData(statusResult);
            var configuration = status.GetProperty("configuration");
            configuration.GetProperty("defaultMaxResults").GetInt32().Should().Be(9);
            configuration.GetProperty("toolOutputSchemaMode").GetString().Should().Be("Omit");

            var serializedStatus = status.GetRawText();
            serializedStatus.Should().NotContain(target.StateRoot);
            serializedStatus.Should().NotContain(target.PluginRoot);
            serializedStatus.Should().NotContain(Path.GetFileName(target.ScenarioRoot));

            var msBuild = status.GetProperty("msBuild");
            if (msBuild.GetProperty("isAvailable").GetBoolean())
            {
                msBuild.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
            }
            else
            {
                msBuild.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
            }
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_DefaultAndFullSchemaModes_WHEN_ListingTools_THEN_ShouldPublishSchemasOnlyInFullMode()
    {
        await using var defaultTarget = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
            ]);
        await using var fullTarget = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            additionalArguments: ["--tool-output-schema-mode=Full"],
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
            ]);

        try
        {
            var defaultTools = await defaultTarget.ListToolsAsync(TestContext.Current.CancellationToken);
            var fullTools = await fullTarget.ListToolsAsync(TestContext.Current.CancellationToken);
            var representativeToolNames = new[]
            {
                "workspace-list",
                "search-symbols",
                "rename-symbol",
                "list-code-actions",
                "host-valid-query",
                "host-valid-mutation",
            };

            foreach (var toolName in representativeToolNames)
            {
                defaultTools.Single(tool => tool.Name == toolName).ProtocolTool.OutputSchema.Should().BeNull();
                var fullTool = fullTools.Single(tool => tool.Name == toolName);
                fullTool.ProtocolTool.OutputSchema.Should().NotBeNull();
                var outputSchema = fullTool.ProtocolTool.OutputSchema!.Value;
                outputSchema.GetProperty("type").GetString().Should().Be("object");
                AssertContinuationSchema(outputSchema);
            }

            var nullableDataToolNames = new[]
            {
                "workspace-list",
                "search-symbols",
                "list-code-actions",
                "host-valid-query",
            };

            foreach (var toolName in nullableDataToolNames)
            {
                var outputSchema = fullTools.Single(tool => tool.Name == toolName).ProtocolTool.OutputSchema!.Value;
                AllowsNull(GetSuccessDataSchema(outputSchema)).Should().BeTrue();
            }

            foreach (var toolName in new[] { "rename-symbol", "host-valid-mutation" })
            {
                var outputSchema = fullTools.Single(tool => tool.Name == toolName).ProtocolTool.OutputSchema!.Value;
                AllowsNull(GetSuccessDataSchema(outputSchema)).Should().BeFalse();
            }
        }
        catch
        {
            defaultTarget.RetainRootOnFailure();
            fullTarget.RetainRootOnFailure();
            throw;
        }
    }

    private static JsonElement GetSuccessDataSchema(JsonElement outputSchema)
    {
        var successSchema = outputSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Single(static candidate => candidate.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        successSchema.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .Should()
            .Contain("data");

        return successSchema.GetProperty("properties").GetProperty("data");
    }

    private static void AssertContinuationSchema(JsonElement outputSchema)
    {
        var failureSchema = outputSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Single(static candidate => !candidate.GetProperty("properties").GetProperty("ok").GetProperty("const").GetBoolean());

        var continuationSchema = failureSchema.GetProperty("properties").GetProperty("continuation");
        var kinds = continuationSchema.GetProperty("oneOf")
            .EnumerateArray()
            .Select(static variant => variant.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString())
            .ToArray();

        kinds.Should().Equal("CallTool", "ChooseTool", "RetryRequest", "ReviseRequest", "ResolveExternally");
    }

    private static bool AllowsNull(JsonElement schema)
    {
        var type = schema.GetProperty("type");
        return type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(static item => string.Equals(item.GetString(), "null", StringComparison.Ordinal));
    }
}
