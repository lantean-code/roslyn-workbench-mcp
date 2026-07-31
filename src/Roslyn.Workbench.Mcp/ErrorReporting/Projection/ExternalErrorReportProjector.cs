using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal sealed class ExternalErrorReportProjector : IExternalErrorReportProjector
{
    private const string _externalPluginTool = "external-plugin-tool";

    public ExternalErrorReport Project(CapturedErrorRecord record, string reportId)
    {
        var workspace = record.Workspace is null
            ? null
            : new ExternalWorkspaceContext
            {
                LifecycleState = record.Workspace.LifecycleState,
                WorkspaceEpoch = record.Workspace.WorkspaceEpoch,
                ProjectCount = record.Workspace.ProjectCount,
                DocumentCount = record.Workspace.DocumentCount,
                TransactionRevision = record.Workspace.TransactionRevision,
            };

        return new ExternalErrorReport
        {
            ReportId = reportId,
            FailureTime = record.FailureTime,
            Tool = record.PluginClassification == "External"
                ? _externalPluginTool
                : record.ToolName,
            ExecutionFamily = record.ExecutionFamily,
            PluginClassification = record.PluginClassification,
            DurationMilliseconds = record.DurationMilliseconds,
            CancellationRequested = record.CancellationRequested,
            ExceptionClassification = ClassifyException(record.Exceptions),
            StackFrames = ProjectStackFrames(record.Exceptions),
            Workspace = workspace,
            ServerVersion = record.ServerVersion,
            RoslynVersion = record.RoslynVersion,
            DotNetVersion = record.DotNetVersion,
            OperatingSystem = record.OperatingSystem,
            ProcessorArchitecture = record.ProcessorArchitecture,
        };
    }

    private static string ClassifyException(ImmutableArray<CapturedException> exceptions)
    {
        if (exceptions.IsDefaultOrEmpty)
        {
            return "UnexpectedFailure";
        }

        var type = exceptions[0].Type;
        if (type.StartsWith("System.", StringComparison.Ordinal))
        {
            return "DotNetException";
        }

        if (type.StartsWith("Microsoft.CodeAnalysis.", StringComparison.Ordinal))
        {
            return "RoslynException";
        }

        if (type.StartsWith("Roslyn.Workbench.", StringComparison.Ordinal))
        {
            return "RoslynWorkbenchException";
        }

        return "ExternalComponentException";
    }

    private static ImmutableArray<ExternalStackFrame> ProjectStackFrames(
        ImmutableArray<CapturedException> exceptions)
    {
        if (exceptions.IsDefaultOrEmpty)
        {
            return [];
        }

        var frames = ImmutableArray.CreateBuilder<ExternalStackFrame>();
        foreach (var frame in exceptions[0].StackFrames)
        {
            var component = ClassifyComponent(frame.Assembly);
            if (component is null)
            {
                continue;
            }

            frames.Add(new ExternalStackFrame
            {
                Component = component,
            });
        }

        return frames.ToImmutable();
    }

    private static string? ClassifyComponent(string? assembly)
    {
        if (assembly is null)
        {
            return null;
        }

        if (assembly.StartsWith("Roslyn.Workbench.", StringComparison.Ordinal))
        {
            return "RoslynWorkbench";
        }

        if (assembly.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
        {
            return "Roslyn";
        }

        if (assembly.StartsWith("System.", StringComparison.Ordinal)
            || string.Equals(assembly, "System.Private.CoreLib", StringComparison.Ordinal))
        {
            return "DotNet";
        }

        return null;
    }
}
