using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

/// <summary>
/// Removes undisclosable identities and stack details while projecting a captured failure for external review.
/// </summary>
internal sealed class ExternalErrorReportProjector : IExternalErrorReportProjector
{
    private const string _externalPluginTool = "external-plugin-tool";

    /// <summary>
    /// Projects a captured error into its externally reportable form.
    /// </summary>
    /// <param name="record">The locally retained diagnostic record.</param>
    /// <param name="reportId">The opaque identifier assigned to the projected report.</param>
    /// <returns>A privacy-filtered report containing only externally eligible diagnostic fields.</returns>
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
            Exceptions = ProjectExceptions(record.Exceptions),
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

        switch (exceptions[0].Component)
        {
            case ErrorReportComponent.DotNet:
                return "DotNetException";
            case ErrorReportComponent.Roslyn:
                return "RoslynException";
            case ErrorReportComponent.RoslynWorkbench:
                return "RoslynWorkbenchException";
            default:
                return "ExternalComponentException";
        }
    }

    private static ImmutableArray<ExternalException> ProjectExceptions(
        ImmutableArray<CapturedException> exceptions)
    {
        if (exceptions.IsDefaultOrEmpty)
        {
            return [];
        }

        var projected = ImmutableArray.CreateBuilder<ExternalException>(exceptions.Length);
        foreach (var exception in exceptions)
        {
            projected.Add(new ExternalException
            {
                Type = ProjectExceptionType(exception),
                Message = exception.Message,
                StackFrames = ProjectStackFrames(exception.StackFrames),
            });
        }

        return projected.ToImmutable();
    }

    private static string ProjectExceptionType(CapturedException exception)
    {
        return exception.Component == ErrorReportComponent.Unknown
            ? "ExternalComponentException"
            : exception.Type;
    }

    private static ImmutableArray<ExternalStackFrame> ProjectStackFrames(
        ImmutableArray<CapturedStackFrame> frames)
    {
        var projected = ImmutableArray.CreateBuilder<ExternalStackFrame>(frames.Length);
        foreach (var frame in frames)
        {
            var component = frame.Component;
            if (component == ErrorReportComponent.Unknown)
            {
                continue;
            }

            projected.Add(ProjectStackFrame(frame, component));
        }

        return projected.ToImmutable();
    }

    private static ExternalStackFrame ProjectStackFrame(
        CapturedStackFrame frame,
        ErrorReportComponent component)
    {
        string? file = null;
        int? line = null;
        if (component == ErrorReportComponent.RoslynWorkbench)
        {
            file = GetSafeFileName(frame.File);
            line = frame.Line;
        }

        return new ExternalStackFrame
        {
            Component = component,
            Assembly = frame.Assembly,
            Type = frame.Type,
            Method = frame.Method,
            File = file,
            Line = line,
        };
    }

    private static string? GetSafeFileName(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // PDB paths can originate on a different operating system, so recognise both separator styles.
        var windowsSeparatorIndex = path.LastIndexOf('\\');
        var unixSeparatorIndex = path.LastIndexOf('/');
        var separatorIndex = Math.Max(windowsSeparatorIndex, unixSeparatorIndex);

        return separatorIndex < 0
            ? path
            : path[(separatorIndex + 1)..];
    }
}
