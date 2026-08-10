using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ErrorCaptureServiceTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture);
    private readonly Mock<TimeProvider> _timeProvider = new Mock<TimeProvider>();
    private readonly Mock<IWorkspaceSessionStore> _workspaceSessionStore = new Mock<IWorkspaceSessionStore>();

    public ErrorCaptureServiceTests()
    {
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_now);
        _workspaceSessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
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
        var arguments = new Dictionary<string, JsonElement>
        {
            ["workspace"] = JsonSerializer.SerializeToElement(new { workspaceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") }),
        };
        var exception = new InvalidOperationException(
            new string('A', 1_000),
            new ArgumentException(
                new string('B', 1_000),
                new FormatException(new string('C', 1_000))));

        var result = target.Capture(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unknown-tool",
            arguments,
            TimeSpan.FromMilliseconds(-1),
            cancellationRequested: true,
            exception);

        result.ExecutionFamily.Should().Be("Unknown");
        result.PluginClassification.Should().Be("Unknown");
        result.DurationMilliseconds.Should().Be(0);
        result.CancellationRequested.Should().BeTrue();
        result.Exceptions.Should().HaveCount(2);
        result.Exceptions.Should().OnlyContain(item => item.Message.Length <= 128);
        result.Exceptions.Should().OnlyContain(item => item.StackFrames.Length <= 2);
        _workspaceSessionStore.Verify(item => item.ReadSession(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), Times.Once);
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
            new InvalidOperationException("Failure message."));

        result.ExecutionFamily.Should().Be("Query");
        result.PluginClassification.Should().Be(expectedClassification);
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
            pluginCatalogState.Object,
            codeActionCatalog);
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
