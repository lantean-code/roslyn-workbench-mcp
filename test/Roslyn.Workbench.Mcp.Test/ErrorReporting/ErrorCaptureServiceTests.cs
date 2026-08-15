using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorCaptureServiceTests : IDisposable
{
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture);
    private readonly Mock<TimeProvider> _timeProvider = new Mock<TimeProvider>();
    private readonly Mock<IWorkspaceSessionStore> _workspaceSessionStore = new Mock<IWorkspaceSessionStore>();
    private readonly Mock<IWorkspaceSelector> _workspaceSelector = new Mock<IWorkspaceSelector>();
    private readonly Mock<IToolRequestBinder> _requestBinder = new Mock<IToolRequestBinder>();
    private readonly AdhocWorkspace _roslynWorkspace = new AdhocWorkspace();

    public ErrorCaptureServiceTests()
    {
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_now);
        _workspaceSessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());

        var selectionError = new WorkspaceOperationError
        {
            Code = "Code",
            Message = "Message",
        };

        _workspaceSelector
            .Setup(item => item.Select(It.IsAny<WorkspaceHostSnapshot>(), It.IsAny<WorkspaceSelector?>()))
            .Returns(WorkspaceSelectionResult.Failure(selectionError));
    }

    [Fact]
    public void GIVEN_ServerOwnedFailure_WHEN_Capturing_THEN_ShouldCreateBoundedImmutableDiagnosticRecord()
    {
        var target = CreateTarget(new ErrorReportingOptions());
        var correlationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var exception = CreateThrownException();

        var result = target.Capture(
            correlationId,
            ServerOwnedToolRegistration.ServerStatusName,
            arguments: null,
            TimeSpan.FromMilliseconds(25),
            cancellationRequested: false,
            workspaceContext: null,
            exception);

        result.CorrelationId.Should().Be(correlationId);
        result.FailureTime.Should().Be(_now);
        result.ExpiresAt.Should().Be(_now.AddHours(1));
        result.ExecutionFamily.Should().Be("ServerOwned");
        result.PluginClassification.Should().Be("Host");
        result.DurationMilliseconds.Should().Be(25);
        result.Exceptions.Should().ContainSingle();
        result.Exceptions[0].Type.Should().Be(typeof(InvalidOperationException).FullName);
        result.Exceptions[0].Message.Should().Be("Failure message.");
        result.Exceptions[0].StackFrames.Should().NotBeEmpty();
        result.Workspace.Should().BeNull();
        result.ServerVersion.Should().NotBeNullOrWhiteSpace();
        result.RoslynVersion.Should().NotBeNullOrWhiteSpace();
        result.DotNetVersion.Should().NotBeNullOrWhiteSpace();
        result.OperatingSystem.Should().NotBeNullOrWhiteSpace();
        result.ProcessorArchitecture.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GIVEN_UnknownToolAndOversizedExceptionChain_WHEN_Capturing_THEN_ShouldReduceRetainedDetail()
    {
        var options = new ErrorReportingOptions
        {
            MaximumCapturedErrorBytes = 1_000,
        };
        var target = CreateTarget(options);
        var exception = new InvalidOperationException(
            new string('A', 1_000),
            new ArgumentException(
                new string('B', 1_000),
                new FormatException(new string('C', 1_000))));

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unknown-tool",
            arguments: null,
            TimeSpan.FromMilliseconds(-1),
            cancellationRequested: true,
            workspaceContext: null,
            exception);

        result.ExecutionFamily.Should().Be("Unknown");
        result.PluginClassification.Should().Be("Unknown");
        result.DurationMilliseconds.Should().Be(0);
        result.CancellationRequested.Should().BeTrue();
        result.Exceptions.Should().HaveCount(2);
        result.Exceptions.Should().OnlyContain(item => item.Message.Length <= 128);
        result.Exceptions.Should().OnlyContain(item => item.StackFrames.Length <= 2);
    }

    [Fact]
    public void GIVEN_RecordStillExceedsLimitAfterReduction_WHEN_Capturing_THEN_ShouldRetainMinimalException()
    {
        var options = new ErrorReportingOptions
        {
            MaximumCapturedErrorBytes = 1,
        };
        var target = CreateTarget(options);
        var exception = new InvalidOperationException(
            new string('A', 1_000),
            new ArgumentException(new string('B', 1_000)));

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unknown-tool",
            arguments: null,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            exception);

        result.Exceptions.Should().ContainSingle();
        result.Exceptions[0].Message.Length.Should().BeLessThanOrEqualTo(64);
        result.Exceptions[0].StackFrames.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_CodeActionFailure_WHEN_Capturing_THEN_ShouldClassifyBundledCodeAction()
    {
        var registeredTool = new Mock<IRegisteredCodeActionTool>();
        registeredTool.SetupGet(item => item.Metadata).Returns(new CodeActionToolMetadata
        {
            Name = "code-action-tool",
        });
        var codeActionCatalog = new CodeActionCatalogSnapshot
        {
            Tools = [registeredTool.Object],
        };
        var target = CreateTarget(
            new ErrorReportingOptions(),
            new PluginCatalogSnapshot(),
            codeActionCatalog);

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "code-action-tool",
            arguments: null,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            new InvalidOperationException("Failure message."));

        result.ExecutionFamily.Should().Be("CodeAction");
        result.PluginClassification.Should().Be("Bundled");
    }

    [Theory]
    [InlineData("roslyn.workbench.core", "Bundled")]
    [InlineData("external.plugin", "External")]
    public void GIVEN_PluginFailure_WHEN_Capturing_THEN_ShouldClassifyPlugin(
        string pluginId,
        string expectedClassification)
    {
        var registeredTool = new Mock<IRegisteredPluginTool>();
        registeredTool.SetupGet(item => item.Tool).Returns(new RegisteredTool
        {
            Plugin = new PluginMetadata
            {
                PluginId = pluginId,
            },
            Metadata = new ToolRegistrationMetadata
            {
                Name = "plugin-tool",
            },
            Kind = ToolKind.Query,
        });
        var pluginCatalog = new PluginCatalogSnapshot
        {
            Tools = [registeredTool.Object],
        };
        var target = CreateTarget(
            new ErrorReportingOptions(),
            pluginCatalog,
            new CodeActionCatalogSnapshot());

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "plugin-tool",
            arguments: null,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            new InvalidOperationException("Failure message."));

        result.ExecutionFamily.Should().Be("Query");
        result.PluginClassification.Should().Be(expectedClassification);
    }

    [Fact]
    public void GIVEN_ValidWorkspaceArguments_WHEN_Capturing_THEN_ShouldCaptureResolvedWorkspaceContext()
    {
        var target = CreateTarget(new ErrorReportingOptions());
        var arguments = new Dictionary<string, JsonElement>
        {
            ["workspace"] = JsonSerializer.SerializeToElement(new { alias = "Alias" }),
        };
        var selector = new WorkspaceSelector { Alias = "Alias" };
        var request = new ErrorCaptureWorkspaceRequest { Workspace = selector };
        ErrorCaptureWorkspaceRequest? boundRequest = request;
        string? bindingError = null;

        _requestBinder
            .Setup(item => item.TryBind<ErrorCaptureWorkspaceRequest>(arguments, out boundRequest, out bindingError))
            .Returns(true);

        var session = CreateSession();
        var snapshot = new WorkspaceHostSnapshot
        {
            Workspaces = new Dictionary<Guid, WorkspaceSessionSnapshot>
            {
                [session.Workspace.WorkspaceId] = session,
            },
        };
        var selection = new WorkspaceSelection
        {
            WorkspaceId = session.Workspace.WorkspaceId,
            Session = session,
        };

        _workspaceSessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);
        _workspaceSelector
            .Setup(item => item.Select(snapshot, selector))
            .Returns(WorkspaceSelectionResult.Success(selection));

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unknown-tool",
            arguments,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            new InvalidOperationException("Failure message."));

        result.Workspace.Should().NotBeNull();
        result.Workspace.WorkspaceId.Should().Be(session.Workspace.WorkspaceId);
        result.Workspace.WorkspaceEpoch.Should().Be(5);
        result.Workspace.LifecycleState.Should().Be(nameof(WorkspaceLifecycleState.TransactionActive));
        result.Workspace.ProjectCount.Should().Be(3);
        result.Workspace.DocumentCount.Should().Be(10);
        result.Workspace.TransactionRevision.Should().BeNull();
    }

    [Fact]
    public void GIVEN_InvalidWorkspaceArguments_WHEN_Capturing_THEN_ShouldNotAttemptWorkspaceSelection()
    {
        var target = CreateTarget(new ErrorReportingOptions());
        var arguments = new Dictionary<string, JsonElement>
        {
            ["workspace"] = JsonSerializer.SerializeToElement(new { alias = "Alias" }),
        };
        ErrorCaptureWorkspaceRequest? boundRequest = null;
        string? bindingError = "Binding error.";

        _requestBinder
            .Setup(item => item.TryBind<ErrorCaptureWorkspaceRequest>(arguments, out boundRequest, out bindingError))
            .Returns(false);

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unknown-tool",
            arguments,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            new InvalidOperationException("Failure message."));

        result.Workspace.Should().BeNull();
        _workspaceSessionStore.Verify(item => item.ReadSnapshot(), Times.Never);
        _workspaceSelector.Verify(
            item => item.Select(It.IsAny<WorkspaceHostSnapshot>(), It.IsAny<WorkspaceSelector?>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_AuthoritativeWorkspaceContext_WHEN_Capturing_THEN_ShouldUseItWithoutBindingOrSelectingWorkspace()
    {
        var target = CreateTarget(new ErrorReportingOptions());
        var arguments = new Dictionary<string, JsonElement>
        {
            ["workspace"] = JsonSerializer.SerializeToElement(new { alias = "Replacement" }),
        };
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            WorkspaceEpoch = 5,
        };
        var workspaceContext = new CapturedWorkspaceContext(
            workspaceIdentity,
            WorkspaceLifecycleState.Ready,
            projectCount: 3,
            documentCount: 10,
            transactionRevision: null);

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unknown-tool",
            arguments,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext,
            new InvalidOperationException("Failure message."));

        result.Workspace.Should().BeSameAs(workspaceContext);
        _requestBinder.Verify(item => item.TryBind<ErrorCaptureWorkspaceRequest>(
            It.IsAny<IDictionary<string, JsonElement>>(),
            out It.Ref<ErrorCaptureWorkspaceRequest?>.IsAny,
            out It.Ref<string?>.IsAny), Times.Never);
        _workspaceSessionStore.Verify(item => item.ReadSnapshot(), Times.Never);
        _workspaceSelector.Verify(
            item => item.Select(It.IsAny<WorkspaceHostSnapshot>(), It.IsAny<WorkspaceSelector?>()),
            Times.Never);
    }

    public void Dispose()
    {
        _roslynWorkspace.Dispose();
    }

    private ErrorCaptureService CreateTarget(ErrorReportingOptions options)
    {
        return CreateTarget(
            options,
            new PluginCatalogSnapshot(),
            new CodeActionCatalogSnapshot());
    }

    private ErrorCaptureService CreateTarget(
        ErrorReportingOptions options,
        PluginCatalogSnapshot pluginCatalog,
        CodeActionCatalogSnapshot codeActionCatalog)
    {
        var pluginRuntimeCatalog = new PluginRuntimeCatalogSnapshot
        {
            Catalog = pluginCatalog,
        };
        var pluginCatalogState = new Mock<IPluginCatalogState>();
        pluginCatalogState.SetupGet(static state => state.Current).Returns(pluginRuntimeCatalog);

        return new ErrorCaptureService(
            Options.Create(options),
            _timeProvider.Object,
            _workspaceSessionStore.Object,
            _workspaceSelector.Object,
            _requestBinder.Object,
            pluginCatalogState.Object,
            codeActionCatalog);
    }

    private WorkspaceSessionSnapshot CreateSession()
    {
        var workspaceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var committedSnapshotId = new WorkspaceSnapshotId(1);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = 5,
            Alias = "Alias",
            LoadedPath = "/Source/Workspace.sln",
            WorkspaceRoot = "/Source",
        };
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = _roslynWorkspace.CurrentSolution,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                transaction: null),
            ProjectCount = 3,
            DocumentCount = 10,
        };
    }

    private static InvalidOperationException CreateThrownException()
    {
        try
        {
            throw new InvalidOperationException("Failure message.");
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
