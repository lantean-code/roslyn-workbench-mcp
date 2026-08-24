using System.IO.Pipelines;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using ModelContextProtocol;
using ModelContextProtocol.Client;

using Roslyn.Workbench.Mcp.ErrorReporting.Capture;
using Roslyn.Workbench.Mcp.Plugins.Registration;
using Roslyn.Workbench.Mcp.ToolExecution;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;
using Roslyn.Workbench.Mcp.Tools;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Operations;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class UnhandledToolExceptionFilterProtocolIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ActiveToolRequest_WHEN_ClientCancels_THEN_ShouldPropagateSameRequestTokenWithoutCapturingFailure()
    {
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider();
        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();
        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var filterTokenObserved = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerTokenObserved = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var filterCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = McpServerTool.Create(
            async (CancellationToken cancellationToken) =>
            {
                handlerTokenObserved.TrySetResult(cancellationToken);
                try
                {
                    await handlerRelease.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    handlerCancellationObserved.TrySetResult();
                    throw;
                }

                return "Completed";
            },
            new McpServerToolCreateOptions
            {
                Name = "cancellation-probe",
            });
        var healthTool = McpServerTool.Create(
            () => "Ready",
            new McpServerToolCreateOptions
            {
                Name = "health-probe",
            });
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices.AddSingleton(filter);
        protocolServices
            .AddMcpServer()
            .WithTools([tool, healthTool])
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddCallToolFilter(next =>
                {
                    var filteredNext = installedFilter(next);
                    return async (context, cancellationToken) =>
                    {
                        filterTokenObserved.TrySetResult(cancellationToken);
                        try
                        {
                            return await filteredNext(context, cancellationToken);
                        }
                        finally
                        {
                            filterCompleted.TrySetResult();
                        }
                    };
                });
            })
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var protocolServiceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = protocolServiceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);
        var requestId = new RequestId("cancellation-probe-request");
        var invocation = client.SendRequestAsync<CallToolRequestParams, CallToolResult>(
            RequestMethods.ToolsCall,
            new CallToolRequestParams
            {
                Name = "cancellation-probe",
            },
            requestId: requestId,
            cancellationToken: invocationCancellation.Token).AsTask();

        var filterToken = await filterTokenObserved.Task.WaitAsync(timeoutCancellation.Token);
        var handlerToken = await handlerTokenObserved.Task.WaitAsync(timeoutCancellation.Token);

        filterToken.Should().Be(handlerToken);
        filterToken.IsCancellationRequested.Should().BeFalse();

        await client.SendNotificationAsync(
            NotificationMethods.CancelledNotification,
            new CancelledNotificationParams
            {
                RequestId = requestId,
            },
            cancellationToken: timeoutCancellation.Token);

        await handlerCancellationObserved.Task.WaitAsync(timeoutCancellation.Token);
        await filterCompleted.Task.WaitAsync(timeoutCancellation.Token);
        filterToken.IsCancellationRequested.Should().BeTrue();
        handlerToken.IsCancellationRequested.Should().BeTrue();
        capturedErrorStore.Verify(item => item.Add(It.IsAny<CapturedErrorRecord>()), Times.Never);

        var healthResult = await client.CallToolAsync(
            "health-probe",
            cancellationToken: timeoutCancellation.Token);

        healthResult.IsError.Should().NotBeTrue();

        await invocationCancellation.CancelAsync();
        var invocationAction = async () => await invocation;
        await invocationAction.Should().ThrowAsync<OperationCanceledException>();

        await serverCancellation.CancelAsync();
        await clientToServerPipe.Writer.CompleteAsync();
        await serverToClientPipe.Writer.CompleteAsync();
        await serverTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ActiveToolRequest_WHEN_ToolThrowsIndependentCancellation_THEN_ShouldReturnAndCaptureCorrelatedFailure()
    {
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider();
        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();
        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        using var unrelatedCancellation = new CancellationTokenSource();
        await unrelatedCancellation.CancelAsync();
        var exception = new OperationCanceledException(
            "Sensitive independent cancellation",
            innerException: null,
            unrelatedCancellation.Token);
        var requestTokenObserved = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = McpServerTool.Create(
            async (CancellationToken cancellationToken) =>
            {
                requestTokenObserved.TrySetResult(cancellationToken);
                return await Task.FromException<string>(exception);
            },
            new McpServerToolCreateOptions
            {
                Name = "independent-cancellation-probe",
            });
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices.AddSingleton(filter);
        protocolServices
            .AddMcpServer()
            .WithTools([tool])
            .WithRequestFilters(requestFilters => requestFilters.AddCallToolFilter(installedFilter))
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var protocolServiceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = protocolServiceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        var result = await client.CallToolAsync(
            "independent-cancellation-probe",
            cancellationToken: timeoutCancellation.Token);

        var requestToken = await requestTokenObserved.Task.WaitAsync(timeoutCancellation.Token);
        requestToken.IsCancellationRequested.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().NotBeNull();
        var structuredContent = result.StructuredContent.GetValueOrDefault();
        var error = structuredContent.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("UnhandledException");
        structuredContent.GetRawText().Should().NotContain("Sensitive independent cancellation");
        var correlationId = error.GetProperty("correlationId").GetGuid();
        capturedErrorStore.Verify(item => item.Add(It.Is<CapturedErrorRecord>(record =>
            record.CorrelationId == correlationId
            && !record.CancellationRequested
            && record.Exceptions.Length == 1
            && record.Exceptions[0].Type == typeof(OperationCanceledException).FullName)), Times.Once);

        await serverCancellation.CancelAsync();
        await clientToServerPipe.Writer.CompleteAsync();
        await serverToClientPipe.Writer.CompleteAsync();
        await serverTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_WorkspaceAttributedFailure_WHEN_RoutedThroughInstalledFilter_THEN_ShouldCaptureOriginalExecutionWorkspace()
    {
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider();
        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();
        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        using var roslynWorkspace = new AdhocWorkspace();
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 5,
            LoadedPath = "C:\\Workspace\\Solution.sln",
            WorkspaceRoot = "C:\\Workspace",
        };
        var exception = new InvalidOperationException("Sensitive attributed failure");
        var workspaceContext = new CapturedWorkspaceContext(
            workspaceIdentity,
            roslynWorkspace.CurrentSolution,
            transactionRevision: 2);
        var attributedException = new WorkspaceAttributedToolException(
            workspaceContext,
            exception);
        var tool = McpServerTool.Create(
            async (CancellationToken _) => await Task.FromException<string>(attributedException),
            new McpServerToolCreateOptions
            {
                Name = "workspace-attribution-probe",
            });
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices.AddSingleton(filter);
        protocolServices
            .AddMcpServer()
            .WithTools([tool])
            .WithRequestFilters(requestFilters => requestFilters.AddCallToolFilter(installedFilter))
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var protocolServiceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = protocolServiceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        var result = await client.CallToolAsync(
            "workspace-attribution-probe",
            cancellationToken: timeoutCancellation.Token);

        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().NotBeNull();
        var structuredContent = result.StructuredContent.GetValueOrDefault();
        var correlationId = structuredContent.GetProperty("error").GetProperty("correlationId").GetGuid();
        capturedErrorStore.Verify(item => item.Add(It.Is<CapturedErrorRecord>(record =>
            record.CorrelationId == correlationId
            && record.Workspace != null
            && record.Workspace.WorkspaceId == workspaceIdentity.WorkspaceId
            && record.Workspace.WorkspaceEpoch == workspaceIdentity.WorkspaceEpoch
            && record.Workspace.TransactionRevision == 2
            && record.Exceptions.Length == 1
            && record.Exceptions[0].Type == typeof(InvalidOperationException).FullName)), Times.Once);

        await serverCancellation.CancelAsync();
        await clientToServerPipe.Writer.CompleteAsync();
        await serverToClientPipe.Writer.CompleteAsync();
        await serverTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_WorkspaceStatusCleanupFailsAfterClose_WHEN_CallingTool_THEN_ShouldCaptureRemovedWorkspaceContext()
    {
        using var fixture = TestWorkspaceFixture.Create();
        using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-lifecycle-failure-tests");
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        var cleanupFailure = new InvalidOperationException("Sensitive cleanup failure");
        instanceStatusPublisher
            .Setup(item => item.OpenAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WorkspaceLifecycleState>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceInstanceStatusResult.Empty);

        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", stateDirectory.DirectoryPath]);
        builder.Logging.ClearProviders();
        builder.Services.RemoveAll<IWorkspaceInstanceStatusPublisher>();
        builder.Services.AddSingleton(instanceStatusPublisher.Object);
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider(validateScopes: true);
        var lifecycleService = workbenchServices.GetRequiredService<IWorkspaceLifecycleService>();
        var openResult = await lifecycleService.OpenAsync(
            fixture.ProjectPath,
            "LifecycleFailure",
            fixture.WorkspaceRoot,
            msBuildProperties: null,
            TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        if (!openResult.HasData)
        {
            Assert.Fail("Workspace opening did not return its successful outcome.");
        }

        var openOutcome = openResult.Data;
        var workspace = openOutcome.Workspace;
        instanceStatusPublisher
            .Setup(item => item.CloseAsync(workspace.WorkspaceId))
            .Returns(() => ValueTask.FromException(cleanupFailure));

        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();

        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        var tool = new WorkspaceCloseTool(
            workbenchServices.GetRequiredService<IOptions<StartupOptions>>(),
            workbenchServices.GetRequiredService<IMcpToolProtocolFactory>(),
            workbenchServices.GetRequiredService<IToolRequestBinder>(),
            lifecycleService);

        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices.AddSingleton(filter);
        protocolServices
            .AddMcpServer()
            .WithTools([tool])
            .WithRequestFilters(requestFilters => requestFilters.AddCallToolFilter(installedFilter))
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var protocolServiceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = protocolServiceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        try
        {
            var result = await client.CallToolAsync(
                "workspace-close",
                new Dictionary<string, object?>
                {
                    ["workspace"] = new Dictionary<string, object?>
                    {
                        ["workspaceId"] = workspace.WorkspaceId,
                    },
                },
                cancellationToken: timeoutCancellation.Token);

            result.IsError.Should().BeTrue();
            result.StructuredContent.Should().NotBeNull();
            var structuredContent = result.StructuredContent.GetValueOrDefault();
            AssertTextContentMatchesStructuredContent(result);
            var error = structuredContent.GetProperty("error");
            error.GetProperty("code").GetString().Should().Be("UnhandledException");
            var correlationId = error.GetProperty("correlationId").GetGuid();
            capturedErrorStore.Verify(item => item.Add(It.Is<CapturedErrorRecord>(record =>
                record.CorrelationId == correlationId
                && record.Workspace != null
                && record.Workspace.WorkspaceId == workspace.WorkspaceId
                && record.Workspace.WorkspaceEpoch == workspace.WorkspaceEpoch
                && record.Workspace.LifecycleState == nameof(WorkspaceLifecycleState.Ready)
                && record.Workspace.ProjectCount == openOutcome.ProjectCount
                && record.Workspace.DocumentCount == openOutcome.DocumentCount
                && record.Exceptions.Length == 1
                && record.Exceptions[0].Type == typeof(InvalidOperationException).FullName)), Times.Once);
        }
        finally
        {
            await serverCancellation.CancelAsync();
            await clientToServerPipe.Writer.CompleteAsync();
            await serverToClientPipe.Writer.CompleteAsync();
            await serverTask;
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_PluginQueryHandlerFailure_WHEN_RoutedThroughAdapterAndInstalledFilter_THEN_ShouldCaptureOriginalFailureAndExecutionWorkspace()
    {
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider();
        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();
        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        using var roslynWorkspace = new AdhocWorkspace();
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 5,
            LoadedPath = "C:\\Workspace\\Solution.sln",
            WorkspaceRoot = "C:\\Workspace",
        };
        var context = new Mock<IQueryContext>();
        context.SetupGet(item => item.WorkspaceIdentity).Returns(workspaceIdentity);
        context.SetupGet(item => item.CurrentSolution).Returns(roslynWorkspace.CurrentSolution);
        context.SetupGet(item => item.TransactionRevision).Returns(2);
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        contextFactory
            .Setup(item => item.CreateQueryContext(
                It.IsAny<AdapterFailureRequest>(),
                "plugin.test",
                "adapter-failure-probe",
                It.IsAny<CancellationToken>()))
            .Returns(ToolExecutionContextLease.Acquired(context.Object));
        var handler = new Mock<IQueryToolHandler<AdapterFailureRequest, AdapterFailureResponse>>();
        handler
            .Setup(item => item.ExecuteAsync(
                It.IsAny<AdapterFailureRequest>(),
                context.Object,
                It.IsAny<CancellationToken>()))
            .Returns(ThrowAdapterFailure);
        var registeredTool = new RegisteredTool
        {
            Plugin = new PluginMetadata
            {
                PluginId = "plugin.test",
                DisplayName = "Plugin Test",
                Version = "1.0.0",
                SupportedApiVersion = PluginApiVersions.V1,
            },
            Metadata = new ToolRegistrationMetadata
            {
                Name = "adapter-failure-probe",
                Title = "Adapter Failure Probe",
                Description = "Exercises plugin failure attribution.",
            },
            Kind = ToolKind.Query,
            RequestType = typeof(AdapterFailureRequest),
            ResponseType = typeof(AdapterFailureResponse),
        };
        var registration = new PluginQueryRegistration<AdapterFailureRequest, AdapterFailureResponse>(
            registeredTool,
            handler.Object);
        var protocolFactory = workbenchServices.GetRequiredService<IMcpToolProtocolFactory>();
        var requestBinder = workbenchServices.GetRequiredService<IToolRequestBinder>();
        var startupOptions = workbenchServices.GetRequiredService<IOptions<StartupOptions>>();
        var tool = new PluginQueryMcpServerTool<AdapterFailureRequest, AdapterFailureResponse>(
            registration,
            contextFactory.Object,
            protocolFactory,
            requestBinder,
            startupOptions);
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices.AddSingleton(filter);
        protocolServices
            .AddMcpServer()
            .WithTools([tool])
            .WithRequestFilters(requestFilters => requestFilters.AddCallToolFilter(installedFilter))
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var protocolServiceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = protocolServiceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        try
        {
            var result = await client.CallToolAsync(
                "adapter-failure-probe",
                cancellationToken: timeoutCancellation.Token);

            result.IsError.Should().BeTrue();
            result.StructuredContent.Should().NotBeNull();
            var structuredContent = result.StructuredContent.GetValueOrDefault();
            var error = structuredContent.GetProperty("error");
            error.GetProperty("code").GetString().Should().Be("UnhandledException");
            var correlationId = error.GetProperty("correlationId").GetGuid();
            capturedErrorStore.Verify(item => item.Add(It.Is<CapturedErrorRecord>(record =>
                record.CorrelationId == correlationId
                && record.Workspace != null
                && record.Workspace.WorkspaceId == workspaceIdentity.WorkspaceId
                && record.Workspace.WorkspaceEpoch == workspaceIdentity.WorkspaceEpoch
                && record.Workspace.LifecycleState == nameof(WorkspaceLifecycleState.TransactionActive)
                && record.Workspace.TransactionRevision == 2
                && record.Exceptions.Length == 1
                && record.Exceptions[0].Type == typeof(InvalidOperationException).FullName
                && record.Exceptions[0].StackFrames.Any(frame => frame.Method == nameof(ThrowAdapterFailure)))), Times.Once);
            handler.Verify(item => item.ExecuteAsync(
                It.IsAny<AdapterFailureRequest>(),
                context.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            contextFactory.Verify(item => item.DetectUnexpectedWorkspaceChange(context.Object), Times.Once);
        }
        finally
        {
            await serverCancellation.CancelAsync();
            await clientToServerPipe.Writer.CompleteAsync();
            await serverToClientPipe.Writer.CompleteAsync();
            await serverTask;
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ToolThrowsProtocolException_WHEN_RoutedThroughInstalledFilter_THEN_ShouldReturnAndCaptureCorrelatedFailure()
    {
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider();
        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();
        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        var exception = new McpProtocolException(
            "Sensitive tool-controlled protocol failure",
            McpErrorCode.InvalidParams);
        var tool = McpServerTool.Create(
            async (CancellationToken _) => await Task.FromException<string>(exception),
            new McpServerToolCreateOptions
            {
                Name = "protocol-exception-probe",
            });
        var healthTool = McpServerTool.Create(
            () => "Ready",
            new McpServerToolCreateOptions
            {
                Name = "health-probe",
            });
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices.AddSingleton(filter);
        protocolServices
            .AddMcpServer()
            .WithTools([tool, healthTool])
            .WithRequestFilters(requestFilters => requestFilters.AddCallToolFilter(installedFilter))
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var protocolServiceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = protocolServiceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        var result = await client.CallToolAsync(
            "protocol-exception-probe",
            cancellationToken: timeoutCancellation.Token);

        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().NotBeNull();
        var structuredContent = result.StructuredContent.GetValueOrDefault();
        var error = structuredContent.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("UnhandledException");
        structuredContent.GetRawText().Should().NotContain("Sensitive tool-controlled protocol failure");
        var correlationId = error.GetProperty("correlationId").GetGuid();
        capturedErrorStore.Verify(item => item.Add(It.Is<CapturedErrorRecord>(record =>
            record.CorrelationId == correlationId
            && record.Exceptions.Length == 1
            && record.Exceptions[0].Type == typeof(McpProtocolException).FullName)), Times.Once);

        var healthResult = await client.CallToolAsync(
            "health-probe",
            cancellationToken: timeoutCancellation.Token);

        healthResult.IsError.Should().NotBeTrue();

        await serverCancellation.CancelAsync();
        await clientToServerPipe.Writer.CompleteAsync();
        await serverToClientPipe.Writer.CompleteAsync();
        await serverTask;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_CancelledToolRequest_WHEN_ToolThrowsIndependentCancellation_THEN_ShouldLetRequestCancellationWinWithoutCapturingFailure()
    {
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(capturedErrorStore.Object);

        await using var workbenchServices = builder.Services.BuildServiceProvider();
        var installedFilter = workbenchServices
            .GetRequiredService<IOptions<McpServerOptions>>()
            .Value
            .Filters
            .Request
            .CallToolFilters
            .Single();
        var filter = workbenchServices.GetRequiredService<UnhandledToolExceptionFilter>();
        using var unrelatedCancellation = new CancellationTokenSource();
        await unrelatedCancellation.CancelAsync();
        var exception = new OperationCanceledException(
            "Sensitive racing cancellation",
            innerException: null,
            unrelatedCancellation.Token);
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var filterCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = McpServerTool.Create(
            async (CancellationToken cancellationToken) =>
            {
                handlerStarted.TrySetResult();
                try
                {
                    await handlerRelease.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw exception;
                }

                return "Completed";
            },
            new McpServerToolCreateOptions
            {
                Name = "racing-cancellation-probe",
            });
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolServices = new ServiceCollection();
        protocolServices.AddLogging();
        protocolServices.AddSingleton(filter);
        protocolServices
            .AddMcpServer()
            .WithTools([tool])
            .WithRequestFilters(requestFilters =>
            {
                requestFilters.AddCallToolFilter(next =>
                {
                    var filteredNext = installedFilter(next);
                    return async (context, cancellationToken) =>
                    {
                        try
                        {
                            return await filteredNext(context, cancellationToken);
                        }
                        finally
                        {
                            filterCompleted.TrySetResult();
                        }
                    };
                });
            })
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var protocolServiceProvider = protocolServices.BuildServiceProvider(validateScopes: true);
        var server = protocolServiceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServerPipe.Writer.AsStream(),
                serverToClientPipe.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);
        var requestId = new RequestId("racing-cancellation-probe-request");
        var invocation = client.SendRequestAsync<CallToolRequestParams, CallToolResult>(
            RequestMethods.ToolsCall,
            new CallToolRequestParams
            {
                Name = "racing-cancellation-probe",
            },
            requestId: requestId,
            cancellationToken: invocationCancellation.Token).AsTask();

        await handlerStarted.Task.WaitAsync(timeoutCancellation.Token);

        await client.SendNotificationAsync(
            NotificationMethods.CancelledNotification,
            new CancelledNotificationParams
            {
                RequestId = requestId,
            },
            cancellationToken: timeoutCancellation.Token);

        await filterCompleted.Task.WaitAsync(timeoutCancellation.Token);
        capturedErrorStore.Verify(item => item.Add(It.IsAny<CapturedErrorRecord>()), Times.Never);

        await invocationCancellation.CancelAsync();
        var invocationAction = async () => await invocation;
        await invocationAction.Should().ThrowAsync<OperationCanceledException>();

        await serverCancellation.CancelAsync();
        await clientToServerPipe.Writer.CompleteAsync();
        await serverToClientPipe.Writer.CompleteAsync();
        await serverTask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ValueTask<PluginExecutionResult<AdapterFailureResponse>> ThrowAdapterFailure()
    {
        throw new InvalidOperationException("Sensitive adapter failure");
    }

    private static void AssertTextContentMatchesStructuredContent(CallToolResult result)
    {
        result.StructuredContent.Should().NotBeNull();
        result.Content.Should().ContainSingle();
        var textContent = result.Content[0].Should().BeOfType<TextContentBlock>().Subject;
        var structuredContent = result.StructuredContent.GetValueOrDefault();

        textContent.Text.Should().Be(structuredContent.GetRawText());
    }

#pragma warning disable CA1515 // Moq's dynamic proxy must access these closed-generic handler contracts.
    public sealed record AdapterFailureRequest : WorkspaceBoundRequest
    {
    }

    public sealed class AdapterFailureResponse : IQueryResponse
    {
    }
#pragma warning restore CA1515
}
