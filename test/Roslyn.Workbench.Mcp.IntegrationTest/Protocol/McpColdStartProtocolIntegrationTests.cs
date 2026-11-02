using System.IO.Pipelines;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpColdStartProtocolIntegrationTests
{
    private static readonly TimeSpan _initializationTimeout = TimeSpan.FromMinutes(2);

    [Theory]
    [InlineData(false, "Start")]
    [InlineData(true, "Start")]
    [InlineData(true, "Timeout")]
    [InlineData(true, "Cancel")]
    [Trait("Category", "Integration")]
    public async Task GIVEN_HostWaitingForRecovery_WHEN_DiscoveryProbeBudgetElapses_THEN_ShouldExposeSdkFallbackConflictAndPreserveConfiguredClientDeadlines(bool disableProbeTimeout, string completion)
    {
        using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-cold-start-tests");
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        watchdog.CancelAfter(TimeSpan.FromSeconds(30));
        using var clientCancellation = CancellationTokenSource.CreateLinkedTokenSource(watchdog.Token);
        var clock = new FakeTimeProvider();
        var recoveryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recoveryRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = new Mock<IWorkspaceCommitRecoveryService>();
        recovery.Setup(item => item.RecoverAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken cancellationToken) =>
            {
                recoveryEntered.TrySetResult();
                var pendingRecovery = recoveryRelease.Task.WaitAsync(cancellationToken);
                return new ValueTask(pendingRecovery);
            });

        var clientToRelay = new Pipe();
        var relayToHost = new Pipe();
        var hostToClient = new Pipe();
        var observedMethods = Channel.CreateUnbounded<string>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", stateDirectory.DirectoryPath]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(recovery.Object);
        builder.Services.AddMcpServer().WithStreamServerTransport(relayToHost.Reader.AsStream(), hostToClient.Writer.AsStream());
        using var host = builder.Build();
        var startup = host.StartAsync(watchdog.Token);
        var relay = ForwardRequestsAsync(clientToRelay.Reader, relayToHost.Writer, observedMethods.Writer, watchdog.Token);
        var options = new McpClientOptions
        {
            InitializationTimeout = _initializationTimeout,
            TimeProvider = clock,
        };

        if (disableProbeTimeout)
        {
            options.DiscoverProbeTimeout = Timeout.InfiniteTimeSpan;
        }

        var transport = new StreamClientTransport(clientToRelay.Writer.AsStream(), hostToClient.Reader.AsStream(), NullLoggerFactory.Instance);
        var connection = McpClient.CreateAsync(transport, options, loggerFactory: NullLoggerFactory.Instance, cancellationToken: clientCancellation.Token);
        try
        {
            await recoveryEntered.Task.WaitAsync(watchdog.Token);
            var discoveryMethod = await observedMethods.Reader.ReadAsync(watchdog.Token);
            discoveryMethod.Should().Be("server/discover");
            startup.IsCompleted.Should().BeFalse();

            clock.Advance(TimeSpan.FromSeconds(6));

            if (!disableProbeTimeout)
            {
                var fallbackMethod = await observedMethods.Reader.ReadAsync(watchdog.Token);
                fallbackMethod.Should().Be("initialize");
            }

            await VerifyConnectionOutcomeAsync(connection, startup, recoveryRelease, clock, clientCancellation, disableProbeTimeout, completion, watchdog.Token);
        }
        finally
        {
            try
            {
                recoveryRelease.TrySetResult();
                await clientCancellation.CancelAsync();
                await DisposeConnectionAsync(connection);
                await startup;
                await host.StopAsync(watchdog.Token);
            }
            finally
            {
                await watchdog.CancelAsync();
                try
                {
                    await relay;
                }
                catch (OperationCanceledException) when (watchdog.IsCancellationRequested)
                {
                }
            }
        }
    }

    private static async Task VerifyConnectionOutcomeAsync(Task<McpClient> connection, Task startup, TaskCompletionSource recoveryRelease, FakeTimeProvider clock, CancellationTokenSource clientCancellation, bool disableProbeTimeout, string completion, CancellationToken watchdogToken)
    {
        if (completion == "Timeout")
        {
            clock.Advance(_initializationTimeout - TimeSpan.FromSeconds(6) - TimeSpan.FromTicks(1));
            connection.IsCompleted.Should().BeFalse();
            clock.Advance(TimeSpan.FromTicks(1));
            var connect = async () => await connection;
            await connect.Should().ThrowAsync<TimeoutException>();
            startup.IsCompleted.Should().BeFalse();
        }
        else if (completion == "Cancel")
        {
            await clientCancellation.CancelAsync();
            var connect = async () => await connection;
            await connect.Should().ThrowAsync<OperationCanceledException>();
            startup.IsCompleted.Should().BeFalse();
        }
        else
        {
            recoveryRelease.TrySetResult();
            await startup;
            if (disableProbeTimeout)
            {
                var client = await connection;
                client.NegotiatedProtocolVersion.Should().Be("2026-07-28");
                var tools = await client.ListToolsAsync(new ListToolsRequestParams(), watchdogToken);
                tools.Tools.Should().Contain(tool => tool.Name == "server-status");
                tools.TimeToLive.Should().Be(TimeSpan.FromMinutes(1));
                var status = await client.CallToolAsync("server-status", cancellationToken: watchdogToken);
                status.IsError.Should().NotBeTrue();
            }
            else
            {
                // SDK 2.3.0 commits the modern protocol before rejecting the already queued legacy fallback.
                // This row characterises the upstream race; it must change when an SDK fix is adopted.
                var connect = async () => await connection;
                var failure = await connect.Should().ThrowAsync<McpProtocolException>();
                failure.Which.ErrorCode.Should().Be(McpErrorCode.MethodNotFound);
                failure.Which.Message.Should().Contain("initialize");
            }
        }
    }

    private static async Task ForwardRequestsAsync(PipeReader source, PipeWriter destination, ChannelWriter<string> observedMethods, CancellationToken cancellationToken)
    {
        using var requests = new StreamReader(source.AsStream());
        await using var forwardedStream = destination.AsStream();
        await using var forwarded = new StreamWriter(forwardedStream) { AutoFlush = true };
        while (true)
        {
            var line = await requests.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return;
            }

            await forwarded.WriteLineAsync(line.AsMemory(), cancellationToken);
            var message = JsonNode.Parse(line);
            if (message?["id"] is not null && message["method"]?.GetValue<string>() is { } method)
            {
                await observedMethods.WriteAsync(method, cancellationToken);
            }
        }
    }

    private static async Task DisposeConnectionAsync(Task<McpClient> connection)
    {
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
        catch (McpProtocolException)
        {
            return;
        }

        await client.DisposeAsync();
    }
}
