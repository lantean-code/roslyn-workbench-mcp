using System.Collections.Immutable;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class ExternalErrorReportProjectorTests
{
    [Fact]
    public void GIVEN_ExternalPluginFailureWithSensitiveLocalData_WHEN_Projecting_THEN_ShouldIncludeOnlyAllowListedAnonymousData()
    {
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
            Workspace = new CapturedWorkspaceContext
            {
                WorkspaceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                WorkspaceEpoch = 7,
                LifecycleState = "Ready",
                ProjectCount = 3,
                DocumentCount = 20,
            },
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
        result.StackFrames.Should().BeEmpty();
        result.Workspace!.WorkspaceEpoch.Should().Be(7);
        var serialized = JsonSerializer.Serialize(result);
        serialized.Should().NotContain("11111111-1111-1111-1111-111111111111");
        serialized.Should().NotContain("cccccccc-cccc-cccc-cccc-cccccccccccc");
        serialized.Should().NotContain("private-company-tool");
        serialized.Should().NotContain("token=secret");
        serialized.Should().NotContain("/home/user");
        serialized.Should().NotContain("Private.Company");
    }

    [Fact]
    public void GIVEN_HostAndRoslynFrames_WHEN_Projecting_THEN_ShouldRetainApprovedFrameCategoriesWithoutPaths()
    {
        var record = CreateRecord(
            exceptions:
            [
                new CapturedException
                {
                    Type = "System.InvalidOperationException",
                    Message = "Message",
                    StackFrames =
                    [
                        new CapturedStackFrame
                        {
                            Assembly = "Roslyn.Workbench.Mcp",
                            Type = "Roslyn.Workbench.Mcp.Tools.ServerStatusTool",
                            Method = "ExecuteAsync",
                            File = "/source/ServerStatusTool.cs",
                            Line = 10,
                        },
                        new CapturedStackFrame
                        {
                            Assembly = "Microsoft.CodeAnalysis.Workspaces",
                            Type = "Microsoft.CodeAnalysis.Workspace",
                            Method = "ApplyChanges",
                        },
                    ],
                },
            ]);

        var target = new ExternalErrorReportProjector();

        var result = target.Project(record, "report-id");

        result.ExceptionClassification.Should().Be("DotNetException");
        result.StackFrames.Select(item => item.Component).Should().Equal("RoslynWorkbench", "Roslyn");
        var serialized = JsonSerializer.Serialize(result);
        serialized.Should().NotContain("/source/ServerStatusTool.cs");
        serialized.Should().NotContain("ServerStatusTool");
        serialized.Should().NotContain("ExecuteAsync");
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
