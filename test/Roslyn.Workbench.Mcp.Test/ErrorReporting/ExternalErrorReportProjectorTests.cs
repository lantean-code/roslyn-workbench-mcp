using System.Collections.Immutable;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ExternalErrorReportProjectorTests
{
    public static TheoryData<string?, string?> SourcePaths
    {
        get
        {
            return new TheoryData<string?, string?>
            {
                { "/source/ServerStatusTool.cs", "ServerStatusTool.cs" },
                { @"C:\source\ServerStatusTool.cs", "ServerStatusTool.cs" },
                { "ServerStatusTool.cs", "ServerStatusTool.cs" },
                { null, null },
            };
        }
    }

    [Fact]
    public void GIVEN_ExternalPluginFailureWithSensitiveLocalData_WHEN_Projecting_THEN_ShouldExcludeExternalImplementationDetails()
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            WorkspaceEpoch = 7,
            LoadedPath = "LoadedPath",
            WorkspaceRoot = "WorkspaceRoot",
        };
        var workspaceContext = new CapturedWorkspaceContext(
            workspaceIdentity,
            WorkspaceLifecycleState.Ready,
            projectCount: 3,
            documentCount: 20,
            transactionRevision: null);
        var record = new CapturedErrorRecord
        {
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T01:00:00Z", CultureInfo.InvariantCulture),
            ToolName = "private-company-tool",
            ExecutionFamily = "Query",
            PluginClassification = "External",
            DurationMilliseconds = 25,
            Exceptions =
            [
                new CapturedException
                {
                    Type = "Private.Company.SecretException",
                    Message = "token=secret /home/user/private/repository.cs",
                    StackFrames =
                    [
                        new CapturedStackFrame
                        {
                            Assembly = "Private.Company.Plugin",
                            Type = "Private.Company.CustomerType",
                            Method = "SecretMethod",
                            File = "/home/user/private/repository.cs",
                            Line = 42,
                        },
                    ],
                },
            ],
            Workspace = workspaceContext,
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };

        var target = new ExternalErrorReportProjector();

        var result = target.Project(record, "report-id");

        result.ReportId.Should().Be("report-id");
        result.Tool.Should().Be("external-plugin-tool");
        result.ExceptionClassification.Should().Be("ExternalComponentException");
        result.Exceptions.Should().ContainSingle();
        result.Exceptions[0].Type.Should().Be("ExternalComponentException");
        result.Exceptions[0].Message.Should().Be("token=secret /home/user/private/repository.cs");
        result.Exceptions[0].StackFrames.Should().BeEmpty();
        result.Workspace!.WorkspaceEpoch.Should().Be(7);
        var serialized = JsonSerializer.Serialize(result);
        serialized.Should().NotContain("11111111-1111-1111-1111-111111111111");
        serialized.Should().NotContain("cccccccc-cccc-cccc-cccc-cccccccccccc");
        serialized.Should().NotContain("private-company-tool");
        serialized.Should().Contain("token=secret");
        serialized.Should().NotContain("Private.Company");
    }

    [Theory]
    [MemberData(nameof(SourcePaths))]
    public void GIVEN_HostAndRoslynFrames_WHEN_Projecting_THEN_ShouldRetainOnlyPortableFileName(
        string? sourcePath,
        string? expectedFileName)
    {
        var record = CreateRecord(
            exceptions:
            [
                new CapturedException
                {
                    Type = "System.InvalidOperationException",
                    Message = "Message",
                    Component = ErrorReportComponent.DotNet,
                    StackFrames =
                    [
                        new CapturedStackFrame
                        {
                            Assembly = "Roslyn.Workbench.Mcp",
                            Type = "Roslyn.Workbench.Mcp.Tools.ServerStatusTool",
                            Method = "ExecuteAsync",
                            File = sourcePath,
                            Line = 10,
                            Component = ErrorReportComponent.RoslynWorkbench,
                        },
                        new CapturedStackFrame
                        {
                            Assembly = "Microsoft.CodeAnalysis.Workspaces",
                            Type = "Microsoft.CodeAnalysis.Workspace",
                            Method = "ApplyChanges",
                            Component = ErrorReportComponent.Roslyn,
                        },
                    ],
                },
            ]);

        var target = new ExternalErrorReportProjector();

        var result = target.Project(record, "report-id");

        result.ExceptionClassification.Should().Be("DotNetException");
        result.Exceptions.Should().ContainSingle();
        result.Exceptions[0].Message.Should().Be("Message");
        result.Exceptions[0].StackFrames.Select(item => item.Component).Should().Equal(
            ErrorReportComponent.RoslynWorkbench,
            ErrorReportComponent.Roslyn);
        result.Exceptions[0].StackFrames[0].File.Should().Be(expectedFileName);
        result.Exceptions[0].StackFrames[0].Line.Should().Be(10);
        result.Exceptions[0].StackFrames[1].File.Should().BeNull();
        result.Exceptions[0].StackFrames[1].Line.Should().BeNull();
        var serialized = JsonSerializer.Serialize(result);
        if (sourcePath is not null && !string.Equals(sourcePath, expectedFileName, StringComparison.Ordinal))
        {
            serialized.Should().NotContain(sourcePath);
        }

        serialized.Should().Contain("ServerStatusTool");
        serialized.Should().Contain("ExecuteAsync");
    }

    [Theory]
    [InlineData(null, (int)ErrorReportComponent.Unknown, "UnexpectedFailure")]
    [InlineData("System.InvalidOperationException", (int)ErrorReportComponent.DotNet, "DotNetException")]
    [InlineData("Microsoft.CodeAnalysis.WorkspaceException", (int)ErrorReportComponent.Roslyn, "RoslynException")]
    [InlineData("Microsoft.CodeAnalysis.LookalikeException", (int)ErrorReportComponent.Unknown, "ExternalComponentException")]
    [InlineData("Roslyn.Workbench.Mcp.WorkbenchException", (int)ErrorReportComponent.RoslynWorkbench, "RoslynWorkbenchException")]
    public void GIVEN_ExceptionCategory_WHEN_Projecting_THEN_ShouldPublishExpectedClassification(
        string? type,
        int componentValue,
        string expectedClassification)
    {
        var exceptions = type is null
            ? ImmutableArray<CapturedException>.Empty
            :
            [
                new CapturedException
                {
                    Type = type,
                    Message = "Message",
                    Component = (ErrorReportComponent)componentValue,
                },
            ];
        var target = new ExternalErrorReportProjector();

        var result = target.Project(CreateRecord(exceptions), "report-id");

        result.ExceptionClassification.Should().Be(expectedClassification);
        result.Exceptions.Should().HaveCount(exceptions.Length);
    }

    private static CapturedErrorRecord CreateRecord(
        ImmutableArray<CapturedException> exceptions)
    {
        return new CapturedErrorRecord
        {
            CorrelationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T01:00:00Z", CultureInfo.InvariantCulture),
            ToolName = "server-status",
            ExecutionFamily = "ServerOwned",
            PluginClassification = "Host",
            DurationMilliseconds = 25,
            Exceptions = exceptions,
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };
    }
}
