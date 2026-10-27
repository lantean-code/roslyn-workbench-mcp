using System.IO.Pipelines;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class CatalogueResponseCachingProtocolIntegrationTests
{
    [Theory]
    [InlineData("2025-11-25", false)]
    [InlineData("2026-07-28", true)]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ComposedHost_WHEN_ReadingCataloguesAndSkills_THEN_ShouldPublishVersionAppropriatePrivateHints(string protocolVersion, bool modern)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        var resourceOptions = new McpServerResourceCreateOptions { Name = "dynamic", UriTemplate = "probe://dynamic/resource" };
        var resource = McpServerResource.Create((Func<string>)(() => "Resource"), resourceOptions);
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        builder.Services.AddMcpServer()
            .WithResources([resource])
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream());

        await using var services = builder.Services.BuildServiceProvider(validateScopes: true);
        var server = services.GetRequiredService<McpServer>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var serverTask = server.RunAsync(cancellation.Token);
        var transport = new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance);
        var clientOptions = new McpClientOptions { ProtocolVersion = protocolVersion };
        await using var client = await McpClient.CreateAsync(transport, clientOptions, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellation.Token);

        try
        {
            foreach (var method in new[] { RequestMethods.ToolsList, RequestMethods.ResourcesList })
            {
                var result = await client.SendRequestAsync<JsonObject, JsonObject>(method, new JsonObject(), cancellationToken: cancellation.Token);
                AssertHints(result, modern, 60000);

                var page = new JsonObject { ["cursor"] = "exhausted" };
                var lastPage = await client.SendRequestAsync<JsonObject, JsonObject>(method, page, cancellationToken: cancellation.Token);
                AssertHints(lastPage, modern, 60000);

                if (modern)
                {
                    foreach (var continuation in new[] { "inputResponses", "requestState" })
                    {
                        var retry = new JsonObject { [continuation] = continuation == "inputResponses" ? new JsonObject() : JsonValue.Create("RequestState") };
                        var retried = await client.SendRequestAsync<JsonObject, JsonObject>(method, retry, cancellationToken: cancellation.Token);
                        AssertHints(retried, modern, 0);
                    }
                }
            }

            var listing = await client.ListResourcesAsync(cancellationToken: cancellation.Token);
            var skills = listing.Where(item => item.Uri.StartsWith("skill://roslyn-workbench/", StringComparison.Ordinal)).ToArray();
            skills.Should().HaveCount(12);
            foreach (var skill in skills)
            {
                var parameters = new JsonObject { ["uri"] = skill.Uri };
                var result = await client.SendRequestAsync<JsonObject, JsonObject>(RequestMethods.ResourcesRead, parameters, cancellationToken: cancellation.Token);
                AssertHints(result, modern, 300000);
                result["contents"].Should().NotBeNull();
            }

            var dynamicRequest = new JsonObject { ["uri"] = "probe://dynamic/resource" };
            var dynamicResult = await client.SendRequestAsync<JsonObject, JsonObject>(RequestMethods.ResourcesRead, dynamicRequest, cancellationToken: cancellation.Token);
            AssertHints(dynamicResult, modern, 0);

            var toolRequest = new JsonObject { ["name"] = "server-status" };
            var status = await client.SendRequestAsync<JsonObject, JsonObject>(RequestMethods.ToolsCall, toolRequest, cancellationToken: cancellation.Token);
            status.ContainsKey("ttlMs").Should().BeFalse();
            status.ContainsKey("cacheScope").Should().BeFalse();

            var unknown = async () => await client.ReadResourceAsync(new Uri("skill://roslyn-workbench/unknown.md"), cancellationToken: cancellation.Token);
            await unknown.Should().ThrowAsync<McpProtocolException>();

            if (modern)
            {
                var retry = new JsonObject { ["uri"] = "skill://roslyn-workbench/SKILL.md", ["inputResponses"] = new JsonObject() };
                var retried = await client.SendRequestAsync<JsonObject, JsonObject>(RequestMethods.ResourcesRead, retry, cancellationToken: cancellation.Token);
                AssertHints(retried, modern, 0);
            }
        }
        finally
        {
            await cancellation.CancelAsync();
            await clientToServer.Writer.CompleteAsync();
            await serverToClient.Writer.CompleteAsync();
            await serverTask;
        }
    }

    private static void AssertHints(JsonObject result, bool modern, int milliseconds)
    {
        if (modern)
        {
            var ttl = result["ttlMs"]?.GetValue<long>();
            ttl.Should().Be(milliseconds);
            var scope = result["cacheScope"]?.GetValue<string>();
            scope.Should().Be("private");
        }
        else
        {
            result.ContainsKey("ttlMs").Should().BeFalse();
            result.ContainsKey("cacheScope").Should().BeFalse();
        }
    }
}
