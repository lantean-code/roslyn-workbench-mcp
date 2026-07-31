using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed class ErrorCaptureService : IErrorCaptureService
{
    private const int _maximumExceptionDepth = 4;
    private const int _maximumFramesPerException = 8;
    private const int _maximumMessageLength = 512;
    private const int _maximumNameLength = 256;
    private const int _maximumPathLength = 1_024;
    private const string _bundledPluginId = "roslyn.workbench.core";

    private static readonly string _serverVersion =
        typeof(ErrorCaptureService).Assembly.GetName().Version?.ToString() ?? "unknown";
    private static readonly string _roslynVersion =
        typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString() ?? "unknown";

    private readonly ErrorReportingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkspaceSessionStore _workspaceSessionStore;
    private readonly PluginCatalogSnapshot _pluginCatalog;
    private readonly CodeActionCatalogSnapshot _codeActionCatalog;

    public ErrorCaptureService(
        IOptions<ErrorReportingOptions> options,
        TimeProvider timeProvider,
        IWorkspaceSessionStore workspaceSessionStore,
        PluginCatalogSnapshot pluginCatalog,
        CodeActionCatalogSnapshot codeActionCatalog)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _workspaceSessionStore = workspaceSessionStore;
        _pluginCatalog = pluginCatalog;
        _codeActionCatalog = codeActionCatalog;
    }

    public CapturedErrorRecord Capture(
        Guid correlationId,
        string toolName,
        IDictionary<string, JsonElement>? arguments,
        TimeSpan duration,
        bool cancellationRequested,
        Exception exception)
    {
        var failureTime = _timeProvider.GetUtcNow();
        var workspace = CaptureWorkspace(arguments);
        var (executionFamily, pluginClassification) = ClassifyTool(toolName);
        var exceptions = CaptureExceptionChain(exception);

        var record = new CapturedErrorRecord
        {
            CorrelationId = correlationId,
            FailureTime = failureTime,
            ExpiresAt = failureTime + _options.CapturedErrorLifetime,
            ToolName = Truncate(toolName, _maximumNameLength),
            ExecutionFamily = executionFamily,
            PluginClassification = pluginClassification,
            DurationMilliseconds = Math.Max(0, (long)duration.TotalMilliseconds),
            CancellationRequested = cancellationRequested,
            Exceptions = exceptions,
            Workspace = workspace,
            ServerVersion = _serverVersion,
            RoslynVersion = _roslynVersion,
            DotNetVersion = Environment.Version.ToString(),
            OperatingSystem = GetOperatingSystemFamily(),
            ProcessorArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
        };

        return EnforceTotalSize(record);
    }

    private CapturedWorkspaceContext? CaptureWorkspace(IDictionary<string, JsonElement>? arguments)
    {
        var workspaceId = TryGetWorkspaceId(arguments);
        WorkspaceSessionSnapshot? session = null;
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            session = _workspaceSessionStore.ReadSession(workspaceId);
        }
        else
        {
            var snapshot = _workspaceSessionStore.ReadSnapshot();
            if (snapshot.Workspaces.Count == 1)
            {
                session = snapshot.Workspaces.Values.Single();
            }
        }

        if (session is null)
        {
            return null;
        }

        return new CapturedWorkspaceContext
        {
            WorkspaceId = session.Workspace.WorkspaceId,
            WorkspaceEpoch = session.Workspace.WorkspaceEpoch,
            LifecycleState = session.State.ToString(),
            ProjectCount = session.ProjectCount,
            DocumentCount = session.DocumentCount,
            TransactionRevision = session.Transaction?.CurrentRevision,
        };
    }

    private (string ExecutionFamily, string PluginClassification) ClassifyTool(string toolName)
    {
        if (ServerOwnedToolRegistration.ToolNames.Contains(toolName))
        {
            return ("ServerOwned", "Host");
        }

        if (_codeActionCatalog.Tools.Any(tool =>
            string.Equals(tool.Metadata.Name, toolName, StringComparison.Ordinal)))
        {
            return ("CodeAction", "Bundled");
        }

        var pluginTool = _pluginCatalog.Tools.FirstOrDefault(tool =>
            string.Equals(tool.Tool.Metadata.Name, toolName, StringComparison.Ordinal));
        if (pluginTool is null)
        {
            return ("Unknown", "Unknown");
        }

        var classification = string.Equals(
            pluginTool.Tool.Plugin.PluginId,
            _bundledPluginId,
            StringComparison.Ordinal)
            ? "Bundled"
            : "External";

        return (pluginTool.Tool.Kind.ToString(), classification);
    }

    private CapturedErrorRecord EnforceTotalSize(CapturedErrorRecord record)
    {
        var serialized = JsonSerializer.SerializeToUtf8Bytes(record);
        if (serialized.Length <= _options.MaximumCapturedErrorBytes)
        {
            return record;
        }

        var exceptions = record.Exceptions
            .Select(static item => item with
            {
                Message = Truncate(item.Message, 128),
                StackFrames = item.StackFrames.Take(2).ToImmutableArray(),
            })
            .Take(2)
            .ToImmutableArray();

        var reduced = record with { Exceptions = exceptions };
        serialized = JsonSerializer.SerializeToUtf8Bytes(reduced);
        if (serialized.Length <= _options.MaximumCapturedErrorBytes)
        {
            return reduced;
        }

        var minimalException = exceptions.IsDefaultOrEmpty
            ? ImmutableArray<CapturedException>.Empty
            :
            [
                exceptions[0] with
                {
                    Message = Truncate(exceptions[0].Message, 64),
                    StackFrames = [],
                },
            ];

        return reduced with { Exceptions = minimalException };
    }

    private static ImmutableArray<CapturedException> CaptureExceptionChain(Exception exception)
    {
        var exceptions = ImmutableArray.CreateBuilder<CapturedException>();
        var current = exception;

        while (current is not null && exceptions.Count < _maximumExceptionDepth)
        {
            exceptions.Add(new CapturedException
            {
                Type = Truncate(current.GetType().FullName ?? current.GetType().Name, _maximumNameLength),
                Message = Truncate(current.Message, _maximumMessageLength),
                StackFrames = CaptureStackFrames(current),
            });

            current = current.InnerException;
        }

        return exceptions.ToImmutable();
    }

    private static ImmutableArray<CapturedStackFrame> CaptureStackFrames(Exception exception)
    {
        var frames = new StackTrace(exception, fNeedFileInfo: true).GetFrames();
        if (frames is null)
        {
            return [];
        }

        var captured = ImmutableArray.CreateBuilder<CapturedStackFrame>(
            Math.Min(frames.Length, _maximumFramesPerException));

        foreach (var frame in frames.Take(_maximumFramesPerException))
        {
            var method = frame.GetMethod();
            var assembly = method?.DeclaringType?.Assembly.GetName().Name;
            var line = frame.GetFileLineNumber();

            captured.Add(new CapturedStackFrame
            {
                Assembly = TruncateNullable(assembly, _maximumNameLength),
                Type = TruncateNullable(method?.DeclaringType?.FullName, _maximumNameLength),
                Method = TruncateNullable(method?.Name, _maximumNameLength),
                File = TruncateNullable(frame.GetFileName(), _maximumPathLength),
                Line = line > 0 ? line : null,
            });
        }

        return captured.MoveToImmutable();
    }

    private static string? TryGetWorkspaceId(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null
            || !arguments.TryGetValue("workspace", out var workspace)
            || workspace.ValueKind != JsonValueKind.Object
            || !workspace.TryGetProperty("workspaceId", out var workspaceId)
            || workspaceId.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return workspaceId.GetString();
    }

    private static string GetOperatingSystemFamily()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS";
        }

        return "Other";
    }

    private static string Truncate(string value, int maximumLength)
    {
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    private static string? TruncateNullable(string? value, int maximumLength)
    {
        return value is null ? null : Truncate(value, maximumLength);
    }
}
