using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpClientTimeoutProtocolIntegrationTests
{
    private static readonly TimeSpan _initializationTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("2025-11-25", -1, "initialize")]
    [InlineData("2026-07-28", -1, "server/discover")]
    [InlineData("2026-07-28", 120000, "server/discover")]
    [InlineData("2026-07-28", 180000, "server/discover")]
    [Trait("Category", "Integration")]
    public async Task GIVEN_UnresponsivePeer_WHEN_OverallDeadlineExpires_THEN_ShouldTimeOutWithoutAnEarlyProbeFallback(string protocolVersion, int probeMilliseconds, string firstMethod)
    {
        var clock = new FakeTimeProvider();
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var requests = new StreamReader(clientToServer.Reader.AsStream());
        await using var responses = serverToClient.Writer.AsStream();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var transport = new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance);
        var options = new McpClientOptions
        {
            ProtocolVersion = protocolVersion,
            InitializationTimeout = _initializationTimeout,
            DiscoverProbeTimeout = TimeSpan.FromMilliseconds(probeMilliseconds),
            TimeProvider = clock,
        };

        var connection = McpClient.CreateAsync(transport, options, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellation.Token);
        try
        {
            var request = await ReadRequestAsync(requests, cancellation.Token);
            var method = request["method"]?.GetValue<string>();
            method.Should().Be(firstMethod);
            clock.Advance(_initializationTimeout - TimeSpan.FromTicks(1));
            connection.IsCompleted.Should().BeFalse();

            clock.Advance(TimeSpan.FromTicks(1));

            var connect = async () => await connection;
            await connect.Should().ThrowAsync<TimeoutException>();
            await clientToServer.Writer.CompleteAsync();
            var remaining = await requests.ReadToEndAsync(cancellation.Token);
            var messages = remaining.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line));
            messages.Should().NotContain(message => message != null && message["id"] != null);
        }
        finally
        {
            await CancelAndDisposeConnectionAsync(connection, cancellation);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Integration")]
    public async Task GIVEN_SilentDiscoveryPeer_WHEN_ProbeExpires_THEN_ShouldFallBackWithinOriginalOverallBudget(bool completeHandshake)
    {
        var clock = new FakeTimeProvider();
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var requests = new StreamReader(clientToServer.Reader.AsStream());
        await using var responseStream = serverToClient.Writer.AsStream();
        await using var responses = new StreamWriter(responseStream) { AutoFlush = true };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var transport = new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance);
        var options = new McpClientOptions
        {
            InitializationTimeout = _initializationTimeout,
            DiscoverProbeTimeout = _probeTimeout,
            TimeProvider = clock,
        };

        var connection = McpClient.CreateAsync(transport, options, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellation.Token);
        try
        {
            var discovery = await ReadRequestAsync(requests, cancellation.Token);
            var discoveryMethod = discovery["method"]?.GetValue<string>();
            discoveryMethod.Should().Be("server/discover");
            clock.Advance(_probeTimeout - TimeSpan.FromTicks(1));
            connection.IsCompleted.Should().BeFalse();

            clock.Advance(TimeSpan.FromTicks(1));

            var initialize = await ReadRequestAsync(requests, cancellation.Token);
            var fallbackMethod = initialize["method"]?.GetValue<string>();
            fallbackMethod.Should().Be("initialize");
            if (completeHandshake)
            {
                var capabilities = new ServerCapabilities();
                var serverInfo = new Implementation { Name = "Name", Version = "Version" };
                var initialized = new InitializeResult { ProtocolVersion = "2025-11-25", Capabilities = capabilities, ServerInfo = serverInfo };
                await WriteResultAsync(responses, initialize, initialized, cancellation.Token);
                var client = await connection;
                client.NegotiatedProtocolVersion.Should().Be("2025-11-25");
            }
            else
            {
                clock.Advance(_initializationTimeout - _probeTimeout - TimeSpan.FromTicks(1));
                connection.IsCompleted.Should().BeFalse();
                clock.Advance(TimeSpan.FromTicks(1));
                var connect = async () => await connection;
                await connect.Should().ThrowAsync<TimeoutException>();
            }
        }
        finally
        {
            await CancelAndDisposeConnectionAsync(connection, cancellation);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_DisabledProbeDeadline_WHEN_DiscoveryRespondsAfterDefaultProbeBudget_THEN_ShouldConnectUsingModernProtocol()
    {
        var clock = new FakeTimeProvider();
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var requests = new StreamReader(clientToServer.Reader.AsStream());
        await using var responseStream = serverToClient.Writer.AsStream();
        await using var responses = new StreamWriter(responseStream) { AutoFlush = true };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var transport = new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance);
        var options = new McpClientOptions
        {
            InitializationTimeout = _initializationTimeout,
            DiscoverProbeTimeout = Timeout.InfiniteTimeSpan,
            TimeProvider = clock,
        };

        var connection = McpClient.CreateAsync(transport, options, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellation.Token);
        try
        {
            var discovery = await ReadRequestAsync(requests, cancellation.Token);
            var discoveryMethod = discovery["method"]?.GetValue<string>();
            discoveryMethod.Should().Be("server/discover");
            clock.Advance(TimeSpan.FromSeconds(30));
            connection.IsCompleted.Should().BeFalse();

            var capabilities = new ServerCapabilities();
            var discovered = new DiscoverResult
            {
                SupportedVersions = ["2026-07-28"],
                Capabilities = capabilities,
                TimeToLive = TimeSpan.Zero,
                CacheScope = CacheScope.Private,
            };

            await WriteResultAsync(responses, discovery, discovered, cancellation.Token);

            var client = await connection;
            client.NegotiatedProtocolVersion.Should().Be("2026-07-28");
        }
        finally
        {
            await CancelAndDisposeConnectionAsync(connection, cancellation);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public async Task GIVEN_PendingDiscovery_WHEN_CallerCancelsBeforeDeadline_THEN_ShouldPreserveCancellation(bool disableProbeTimeout)
    {
        var clock = new FakeTimeProvider();
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var requests = new StreamReader(clientToServer.Reader.AsStream());
        await using var responses = serverToClient.Writer.AsStream();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(10));
        var transport = new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance);
        var options = new McpClientOptions
        {
            InitializationTimeout = _initializationTimeout,
            DiscoverProbeTimeout = disableProbeTimeout ? Timeout.InfiniteTimeSpan : _probeTimeout,
            TimeProvider = clock,
        };

        var connection = McpClient.CreateAsync(transport, options, loggerFactory: NullLoggerFactory.Instance, cancellationToken: cancellation.Token);
        try
        {
            var discovery = await ReadRequestAsync(requests, cancellation.Token);
            var discoveryMethod = discovery["method"]?.GetValue<string>();
            discoveryMethod.Should().Be("server/discover");

            await cancellation.CancelAsync();

            var connect = async () => await connection;
            await connect.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            await CancelAndDisposeConnectionAsync(connection, cancellation);
        }
    }

    private static async Task<JsonObject> ReadRequestAsync(StreamReader requests, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await requests.ReadLineAsync(cancellationToken);
            line.Should().NotBeNull();
            var message = JsonNode.Parse(line ?? throw new InvalidOperationException("The client closed its request stream."));
            var request = message.Should().BeOfType<JsonObject>().Subject;
            if (request.ContainsKey("id"))
            {
                return request;
            }
        }
    }

    private static async Task WriteResultAsync<T>(StreamWriter responses, JsonObject request, T result, CancellationToken cancellationToken)
    {
        var resultNode = JsonSerializer.SerializeToNode(result);
        var response = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = request["id"]?.DeepClone(), ["result"] = resultNode };
        await responses.WriteLineAsync(response.ToJsonString().AsMemory(), cancellationToken);
    }

    private static async Task CancelAndDisposeConnectionAsync(Task<McpClient> connection, CancellationTokenSource cancellation)
    {
        await cancellation.CancelAsync();
        McpClient client;
        try
        {
            client = await connection;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (TimeoutException)
        {
            return;
        }

        await client.DisposeAsync();
    }
}
