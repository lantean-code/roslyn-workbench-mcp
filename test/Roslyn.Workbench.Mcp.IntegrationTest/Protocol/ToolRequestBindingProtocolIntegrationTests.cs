using System.IO.Pipelines;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Client;

using Roslyn.Workbench.Mcp.Tools;
using Roslyn.Workbench.Mcp.Workspace.Operations;
using Roslyn.Workbench.Mcp.Workspace.Transactions;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class ToolRequestBindingProtocolIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ImplicitWorkspaceAndMisspelledWorkspaceMember_WHEN_CallingTransactionStart_THEN_ShouldRejectWithoutStartingTransaction()
    {
        var transactionService = new Mock<ITransactionService>();
        var implicitWorkspaceResult = WorkspaceOperationResult.Succeeded(new TransactionStartOutcome
        {
            Transaction = new TransactionInfo
            {
                Revision = 1,
            },
        });
        transactionService
            .Setup(item => item.StartAsync(
                workspaceId: null,
                alias: null,
                path: null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(implicitWorkspaceResult);

        var protocolTool = new Tool
        {
            Name = ServerOwnedToolRegistration.TransactionStartName,
        };
        var protocolFactory = new Mock<IMcpToolProtocolFactory>();
        protocolFactory
            .Setup(item => item.CreateServerOwnedTool<TransactionStartRequest, TransactionStartData>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<ToolOutputSchemaMode>()))
            .Returns(protocolTool);

        var requestValidator = new RequestObjectGraphValidator();
        var requestBinder = new ToolRequestBinder(requestValidator);
        var startupOptions = Options.Create(new StartupOptions());
        var target = new TransactionStartTool(
            startupOptions,
            protocolFactory.Object,
            requestBinder,
            transactionService.Object);

        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices
            .AddMcpServer(options => options.Handlers.CallToolHandler = target.InvokeAsync)
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var serviceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = serviceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        var clientTransport = new StreamClientTransport(
            clientToServerPipe.Writer.AsStream(),
            serverToClientPipe.Reader.AsStream(),
            NullLoggerFactory.Instance);

        await using var client = await McpClient.CreateAsync(
            clientTransport,
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        try
        {
            var request = new CallToolRequestParams
            {
                Name = ServerOwnedToolRegistration.TransactionStartName,
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["workspcae"] = JsonSerializer.SerializeToElement(new { alias = "WorkspaceAlias" }),
                },
            };

            var result = await client.SendRequestAsync<CallToolRequestParams, CallToolResult>(
                RequestMethods.ToolsCall,
                request,
                cancellationToken: timeoutCancellation.Token);

            result.IsError.Should().BeTrue();
            result.StructuredContent.Should().NotBeNull();
            var structuredContent = result.StructuredContent.GetValueOrDefault();
            structuredContent.GetProperty("error").GetProperty("code").GetString().Should().Be("InvalidRequest");
            transactionService.Verify(item => item.StartAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await serverCancellation.CancelAsync();
            await clientToServerPipe.Writer.CompleteAsync();
            await serverToClientPipe.Writer.CompleteAsync();
            await serverTask;
        }
    }
}
