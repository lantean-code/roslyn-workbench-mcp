using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.CodeActions;
using Roslyn.Workbench.Mcp.Plugins.AssemblyIdentity;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Builds size-bounded diagnostic records from unhandled tool exceptions without retaining tool arguments or source content.
/// </summary>
internal sealed class ErrorCaptureService : IErrorCaptureService
{
    private const int _maximumExceptionDepth = 4;
    private const int _maximumFramesPerException = 8;
    private const int _maximumMessageLength = 512;
    private const int _maximumNameLength = 256;
    private const int _maximumPathLength = 1_024;
    private const string _bundledPluginId = "roslyn.workbench.core";

    private static readonly ImmutableArray<Assembly> _firstPartyAssemblies =
    [
        typeof(HostAssemblyMarker).Assembly,
        typeof(AbstractionsAssemblyMarker).Assembly,
        typeof(WorkspaceAssemblyMarker).Assembly,
        typeof(CodeActionsAssemblyMarker).Assembly,
        typeof(PluginsAssemblyMarker).Assembly,
        typeof(PluginsCoreAssemblyMarker).Assembly,
    ];

    private static readonly string _serverVersion =
        typeof(ErrorCaptureService).Assembly.GetName().Version?.ToString() ?? "unknown";

    private static readonly string _roslynVersion =
        typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString() ?? "unknown";

    private readonly ErrorReportingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkspaceSessionStore _workspaceSessionStore;
    private readonly IWorkspaceSelector _workspaceSelector;
    private readonly IToolRequestBinder _requestBinder;
    private readonly IPluginCatalogState _pluginCatalogState;
    private readonly CodeActionCatalogSnapshot _codeActionCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCaptureService"/> class.
    /// </summary>
    /// <param name="options">The capture lifetime and maximum payload size.</param>
    /// <param name="timeProvider">The time source used for expiry and timestamp calculations.</param>
    /// <param name="workspaceSessionStore">The store containing the workspace state that may be associated with a failure.</param>
    /// <param name="workspaceSelector">The selector used to resolve a workspace supplied in the failed request.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="pluginCatalogState">The published plugin catalogue used to report or capture runtime state.</param>
    /// <param name="codeActionCatalog">The catalogue of host-published Code Action tools.</param>
    public ErrorCaptureService(
        IOptions<ErrorReportingOptions> options,
        TimeProvider timeProvider,
        IWorkspaceSessionStore workspaceSessionStore,
        IWorkspaceSelector workspaceSelector,
        IToolRequestBinder requestBinder,
        IPluginCatalogState pluginCatalogState,
        CodeActionCatalogSnapshot codeActionCatalog)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _workspaceSessionStore = workspaceSessionStore;
        _workspaceSelector = workspaceSelector;
        _requestBinder = requestBinder;
        _pluginCatalogState = pluginCatalogState;
        _codeActionCatalog = codeActionCatalog;
    }

    /// <summary>
    /// Captures a bounded diagnostic record for a failed tool invocation.
    /// </summary>
    /// <param name="correlationId">The identifier used to correlate the tool error with the retained record.</param>
    /// <param name="toolName">The published name of the tool associated with the captured error.</param>
    /// <param name="arguments">The arguments supplied to the tool invocation.</param>
    /// <param name="duration">The elapsed duration of the captured tool operation.</param>
    /// <param name="cancellationRequested">Whether cancellation had been requested when the error was captured.</param>
    /// <param name="workspaceContext">The workspace context already acquired by the invocation, when available.</param>
    /// <param name="exception">The unhandled exception raised by the tool invocation.</param>
    /// <returns>A size-bounded record containing diagnostic exception, environment and workspace metadata.</returns>
    public CapturedErrorRecord Capture(
        Guid correlationId,
        string toolName,
        IDictionary<string, JsonElement>? arguments,
        TimeSpan duration,
        bool cancellationRequested,
        CapturedWorkspaceContext? workspaceContext,
        Exception exception)
    {
        var failureTime = _timeProvider.GetUtcNow();
        var workspace = workspaceContext ?? CaptureWorkspace(arguments);
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
        WorkspaceSelector? selector = null;
        if (arguments is not null)
        {
            if (!_requestBinder.TryBind<ErrorCaptureWorkspaceRequest>(
                arguments,
                out var request,
                out _))
            {
                return null;
            }

            selector = request.Workspace;
        }

        var snapshot = _workspaceSessionStore.ReadSnapshot();
        var selectionResult = _workspaceSelector.Select(snapshot, selector);
        if (selectionResult.HasError)
        {
            return null;
        }

        var session = selectionResult.Selection.Session;
        return new CapturedWorkspaceContext(
            session.Workspace,
            session.State,
            session.ProjectCount,
            session.DocumentCount,
            session.Transaction?.CurrentRevision);
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

        var pluginCatalog = _pluginCatalogState.Current.Catalog;
        var pluginTool = pluginCatalog.Tools.FirstOrDefault(tool =>
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
            .Take(2)
            .Select(static item => item with
            {
                Message = Truncate(item.Message, 128),
                StackFrames = item.StackFrames.Take(2).ToImmutableArray(),
            })
            .ToImmutableArray();

        var reduced = record with { Exceptions = exceptions };
        serialized = JsonSerializer.SerializeToUtf8Bytes(reduced);
        if (serialized.Length <= _options.MaximumCapturedErrorBytes)
        {
            return reduced;
        }

        if (exceptions.IsDefaultOrEmpty)
        {
            return reduced;
        }

        ImmutableArray<CapturedException> minimalException =
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
                Component = ClassifyAssembly(current.GetType().Assembly),
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
            var declaringAssembly = method?.DeclaringType?.Assembly;
            var assembly = declaringAssembly?.GetName().Name;
            var line = frame.GetFileLineNumber();

            captured.Add(new CapturedStackFrame
            {
                Component = ClassifyAssembly(declaringAssembly),
                Assembly = TruncateNullable(assembly, _maximumNameLength),
                Type = TruncateNullable(method?.DeclaringType?.FullName, _maximumNameLength),
                Method = TruncateNullable(method?.Name, _maximumNameLength),
                File = TruncateNullable(frame.GetFileName(), _maximumPathLength),
                Line = line > 0 ? line : null,
            });
        }

        return captured.MoveToImmutable();
    }

    private static ErrorReportComponent ClassifyAssembly(Assembly? assembly)
    {
        if (assembly is null)
        {
            return ErrorReportComponent.Unknown;
        }

        foreach (var firstPartyAssembly in _firstPartyAssemblies)
        {
            if (ReferenceEquals(assembly, firstPartyAssembly))
            {
                return ErrorReportComponent.RoslynWorkbench;
            }
        }

        // External plugins can choose framework-like assembly names. Only assemblies resolved through the
        // Host's default load context are eligible for framework or Roslyn implementation detail disclosure.
        if (AssemblyLoadContext.GetLoadContext(assembly) != AssemblyLoadContext.Default)
        {
            return ErrorReportComponent.Unknown;
        }

        var assemblyName = assembly.GetName().Name;
        if (assemblyName?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true)
        {
            return ErrorReportComponent.Roslyn;
        }

        if (assemblyName?.StartsWith("System.", StringComparison.Ordinal) == true
            || string.Equals(assemblyName, "System.Private.CoreLib", StringComparison.Ordinal))
        {
            return ErrorReportComponent.DotNet;
        }

        return ErrorReportComponent.Unknown;
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
