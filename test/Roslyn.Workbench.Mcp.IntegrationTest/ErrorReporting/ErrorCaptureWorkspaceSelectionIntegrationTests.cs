using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Capture;
using Roslyn.Workbench.Mcp.ErrorReporting.Configuration;
using Roslyn.Workbench.Mcp.ErrorReporting.Projection;
using Roslyn.Workbench.Mcp.PluginLoading;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Selection;
using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorCaptureWorkspaceSelectionIntegrationTests : IDisposable
{
    private readonly AdhocWorkspace _roslynWorkspace = new AdhocWorkspace();
    private readonly Guid _firstWorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _secondWorkspaceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly string _firstWorkspacePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ErrorCapture", "First.sln"));
    private readonly string _secondWorkspacePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ErrorCapture", "Second.sln"));

    [Theory]
    [InlineData("workspaceId")]
    [InlineData("alias")]
    [InlineData("path")]
    [InlineData("caseVariant")]
    public void GIVEN_MultipleWorkspacesAndValidSelector_WHEN_Capturing_THEN_ShouldUseNormalBindingAndSelection(
        string selectorKind)
    {
        var firstSession = CreateSession(_firstWorkspaceId, "First", _firstWorkspacePath, workspaceEpoch: 5);
        var secondSession = CreateSession(_secondWorkspaceId, "Second", _secondWorkspacePath, workspaceEpoch: 7);
        var target = CreateTarget(firstSession, secondSession);
        var arguments = CreateArguments(selectorKind);

        var result = target.Capture(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "unknown-tool",
            arguments,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            new InvalidOperationException("Failure message."));

        var workspace = result.Workspace;
        workspace.Should().NotBeNull();
        workspace?.WorkspaceId.Should().Be(_firstWorkspaceId);
        workspace?.WorkspaceEpoch.Should().Be(5);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("unknownAlias")]
    [InlineData("mismatched")]
    public void GIVEN_MultipleWorkspacesAndUnresolvableSelector_WHEN_Capturing_THEN_ShouldNotAttributeWorkspace(
        string selectorKind)
    {
        var firstSession = CreateSession(_firstWorkspaceId, "First", _firstWorkspacePath, workspaceEpoch: 5);
        var secondSession = CreateSession(_secondWorkspaceId, "Second", _secondWorkspacePath, workspaceEpoch: 7);
        var target = CreateTarget(firstSession, secondSession);
        var arguments = CreateArguments(selectorKind);

        var result = target.Capture(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "unknown-tool",
            arguments,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            new InvalidOperationException("Failure message."));

        result.Workspace.Should().BeNull();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void GIVEN_OmittedSelector_WHEN_Capturing_THEN_ShouldUseNormalImplicitSelection(
        bool includeSecondWorkspace,
        bool shouldCaptureWorkspace)
    {
        var firstSession = CreateSession(_firstWorkspaceId, "First", _firstWorkspacePath, workspaceEpoch: 5);
        var sessions = new List<WorkspaceSessionSnapshot> { firstSession };
        if (includeSecondWorkspace)
        {
            sessions.Add(CreateSession(_secondWorkspaceId, "Second", _secondWorkspacePath, workspaceEpoch: 7));
        }

        var target = CreateTarget([.. sessions]);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["unrelated"] = JsonSerializer.SerializeToElement("Value"),
        };

        var result = target.Capture(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "unknown-tool",
            arguments,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            new InvalidOperationException("Failure message."));

        if (shouldCaptureWorkspace)
        {
            var workspace = result.Workspace;
            workspace.Should().NotBeNull();
            workspace?.WorkspaceId.Should().Be(_firstWorkspaceId);
        }
        else
        {
            result.Workspace.Should().BeNull();
        }
    }

    [Fact]
    public void GIVEN_ExternalAssemblyWithRoslynName_WHEN_Capturing_THEN_ShouldNotExposePluginImplementationDetails()
    {
        var packageDirectory = Path.Combine(AppContext.BaseDirectory, "PluginFixtureAssets", "Lookalike");
        var entryAssemblyPath = Path.Combine(packageDirectory, "Microsoft.CodeAnalysis.LookalikePluginFixture.dll");
        var packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        var containedEntryAssemblyPath = entryAssemblyPath;
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, entryAssemblyPath, out containedEntryAssemblyPath))
            .Returns(true);

        var loadContextFactory = new PluginLoadContextFactory(packagePathPolicy.Object);
        var created = loadContextFactory.TryCreate(packageDirectory, entryAssemblyPath, out var loadContext);
        created.Should().BeTrue();
        var pluginLoadContext = loadContext
            ?? throw new InvalidOperationException("The plugin load context was not created.");
        var pluginAssembly = pluginLoadContext.LoadFromAssemblyPath(entryAssemblyPath);
        var pluginType = pluginAssembly.GetType(
            "Microsoft.CodeAnalysis.LookalikePluginFixture.LookalikePluginFailure",
            throwOnError: true);
        var throwMethod = pluginType?.GetMethod("Throw", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("The lookalike fixture method was not found.");

        var action = () => throwMethod.Invoke(null, null);
        var invocationException = action.Should().Throw<TargetInvocationException>().Which;
        var pluginException = invocationException.InnerException
            ?? throw new InvalidOperationException("The lookalike fixture exception was not retained.");
        var target = CreateTarget();

        var result = target.Capture(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "unknown-tool",
            arguments: null,
            TimeSpan.Zero,
            cancellationRequested: false,
            workspaceContext: null,
            pluginException);

        result.Exceptions[0].Component.Should().Be(ErrorReportComponent.Unknown);
        var pluginFrame = result.Exceptions[0].StackFrames.Single(
            frame => string.Equals(
                frame.Assembly,
                "Microsoft.CodeAnalysis.LookalikePluginFixture",
                StringComparison.Ordinal));
        pluginFrame.Component.Should().Be(ErrorReportComponent.Unknown);
        var projector = new ExternalErrorReportProjector();

        var externalReport = projector.Project(result, "ReportId");

        externalReport.ExceptionClassification.Should().Be("ExternalComponentException");
        externalReport.Exceptions[0].Type.Should().Be("ExternalComponentException");
    }

    public void Dispose()
    {
        _roslynWorkspace.Dispose();
    }

    private static ErrorCaptureService CreateTarget(params WorkspaceSessionSnapshot[] sessions)
    {
        var snapshot = new WorkspaceHostSnapshot
        {
            Workspaces = sessions.ToDictionary(session => session.Workspace.WorkspaceId),
        };
        var sessionStore = new Mock<IWorkspaceSessionStore>();
        sessionStore.Setup(item => item.ReadSnapshot()).Returns(snapshot);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider
            .Setup(item => item.GetUtcNow())
            .Returns(DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture));

        var pluginCatalogState = new Mock<IPluginCatalogState>();
        pluginCatalogState.SetupGet(static state => state.Current).Returns(new PluginRuntimeCatalogSnapshot
        {
            Catalog = new PluginCatalogSnapshot(),
        });

        var requestValidator = new RequestObjectGraphValidator();
        var requestBinder = new ToolRequestBinder(requestValidator);
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathNormalizer = new WorkspacePathNormalizer(fileSystem);
        var workspaceSelector = new WorkspaceSelectorService(pathComparison, pathNormalizer);

        return new ErrorCaptureService(
            Options.Create(new ErrorReportingOptions()),
            timeProvider.Object,
            sessionStore.Object,
            workspaceSelector,
            requestBinder,
            pluginCatalogState.Object,
            new CodeActionCatalogSnapshot());
    }

    private WorkspaceSessionSnapshot CreateSession(
        Guid workspaceId,
        string alias,
        string loadedPath,
        long workspaceEpoch)
    {
        var committedSnapshotId = new WorkspaceSnapshotId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
            Alias = alias,
            LoadedPath = loadedPath,
            WorkspaceRoot = Path.GetDirectoryName(loadedPath) ?? loadedPath,
        };
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = WorkspaceLifecycleState.Ready,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = _roslynWorkspace.CurrentSolution,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                transaction: null),
            ProjectCount = 1,
            DocumentCount = 2,
        };
    }

    private Dictionary<string, JsonElement> CreateArguments(string selectorKind)
    {
        return selectorKind switch
        {
            "workspaceId" => CreateArguments("workspace", new { workspaceId = _firstWorkspaceId }),
            "alias" => CreateArguments("workspace", new { alias = "First" }),
            "path" => CreateArguments("workspace", new { path = _firstWorkspacePath }),
            "caseVariant" => CreateArguments("Workspace", new { Alias = "First" }),
            "invalid" => CreateArguments("workspace", new { }),
            "unknownAlias" => CreateArguments("workspace", new { alias = "Missing" }),
            "mismatched" => CreateArguments("workspace", new { workspaceId = _firstWorkspaceId, alias = "Second" }),
            _ => throw new InvalidOperationException($"Unknown selector kind '{selectorKind}'."),
        };
    }

    private static Dictionary<string, JsonElement> CreateArguments(string propertyName, object workspace)
    {
        return new Dictionary<string, JsonElement>
        {
            [propertyName] = JsonSerializer.SerializeToElement(workspace),
        };
    }
}
