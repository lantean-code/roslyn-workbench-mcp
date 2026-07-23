using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal enum AcceptanceWorkspaceAsset
{
    SdkProject,
    InspectionSample,
    SolutionHierarchy,
    MixedSolution,
    MultiTargetLinked,
}

internal enum AcceptancePluginAsset
{
    HostQuery,
    HostMutation,
}

internal sealed record AcceptanceToolInvocation(RequestId RequestId, Task<CallToolResult> Completion);

internal sealed class AcceptanceProcessFixture : IAsyncDisposable
{
    private const string _pendingStateRootArgument = "{acceptance-state-root}";
    private const string _pendingPluginRootArgument = "{acceptance-plugin-root}";
    private static readonly TimeSpan _initializationTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan _invocationTimeout = TimeSpan.FromSeconds(30);

    // MCP C# SDK 1.4.1 waits before closing stdin, so keep its forced-cleanup fallback short; direct EOF coverage owns graceful Host shutdown evidence.
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(2);

    private readonly object _standardErrorLock = new();
    private readonly StringBuilder _standardError = new();
    private readonly string _command;
    private readonly IReadOnlyList<string> _arguments;
    private readonly bool _retainInitializationFailure;
    private McpClient? _client;
    private Task<ClientCompletionDetails>? _completion;
    private ClientCompletionDetails? _completionDetails;
    private long _lastRequestId;
    private bool _retainRoot;
    private bool _disposed;

    private AcceptanceProcessFixture(
        string command,
        IReadOnlyList<string> arguments,
        string scenarioRoot,
        bool retainInitializationFailure)
    {
        _command = command;
        _arguments = arguments;
        _retainInitializationFailure = retainInitializationFailure;
        ScenarioRoot = scenarioRoot;
        WorkspaceRoot = Path.Combine(scenarioRoot, "workspace");
        StateRoot = Path.Combine(scenarioRoot, "state");
    }

    public string ScenarioRoot { get; }

    public string WorkspaceRoot { get; }

    public string StateRoot { get; }

    public static Task<AcceptanceProcessFixture> StartPublishedHostAsync(
        CancellationToken cancellationToken,
        AcceptanceWorkspaceAsset workspaceAsset = AcceptanceWorkspaceAsset.SdkProject,
        IReadOnlyList<string>? additionalArguments = null,
        IReadOnlyList<AcceptancePluginAsset>? pluginAssets = null)
    {
        var executablePath = PublishedHostExecutable.ResolveFromEnvironment();
        var arguments = new List<string>
        {
            "--state-directory",
            _pendingStateRootArgument,
        };

        arguments.AddRange(additionalArguments ?? []);
        if (pluginAssets is { Count: > 0 })
        {
            arguments.Add("--plugin-directory");
            arguments.Add(_pendingPluginRootArgument);
        }

        return StartAsync(
            executablePath,
            arguments,
            workspaceAsset,
            pluginAssets,
            retainInitializationFailure: true,
            cancellationToken);
    }

    internal static Task<AcceptanceProcessFixture> StartCommandAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return StartAsync(
            command,
            arguments,
            workspaceAsset: null,
            pluginAssets: null,
            retainInitializationFailure: false,
            cancellationToken);
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        using var timeoutSource = CreateTimeoutSource(_invocationTimeout, cancellationToken);

        try
        {
            return await GetClient().ListToolsAsync(cancellationToken: timeoutSource.Token);
        }
        catch (Exception exception)
        {
            _retainRoot = true;
            throw CreateDiagnosticException("tools/list failed", exception);
        }
    }

    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CreateTimeoutSource(_invocationTimeout, cancellationToken);

        try
        {
            return await GetClient().CallToolAsync(
                toolName,
                arguments,
                cancellationToken: timeoutSource.Token);
        }
        catch (Exception exception)
        {
            _retainRoot = true;
            throw CreateDiagnosticException($"MCP tool '{toolName}' failed", exception);
        }
    }

    public AcceptanceToolInvocation StartCancellableToolCall(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var requestId = new RequestId($"acceptance-{Interlocked.Increment(ref _lastRequestId)}");
        var requestArguments = new Dictionary<string, JsonElement>(arguments.Count);
        foreach (var (name, value) in arguments)
        {
            requestArguments.Add(
                name,
                JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object)));
        }

        var request = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = requestArguments,
        };

        var completion = GetClient().SendRequestAsync<CallToolRequestParams, CallToolResult>(
            RequestMethods.ToolsCall,
            request,
            requestId: requestId,
            cancellationToken: cancellationToken).AsTask();

        return new AcceptanceToolInvocation(requestId, completion);
    }

    public Task CancelToolCallAsync(RequestId requestId, CancellationToken cancellationToken)
    {
        var parameters = new CancelledNotificationParams
        {
            RequestId = requestId,
        };

        return GetClient().SendNotificationAsync(
            NotificationMethods.CancelledNotification,
            parameters,
            cancellationToken: cancellationToken);
    }

    public void RetainRootOnFailure()
    {
        _retainRoot = true;
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await StopAsync();
        _completion = null;
        _completionDetails = null;
        await ConnectAsync(cancellationToken);
    }

    public async Task<StdioClientCompletionDetails> StopAsync()
    {
        if (_completionDetails is StdioClientCompletionDetails existingDetails)
        {
            return existingDetails;
        }

        var client = Interlocked.Exchange(ref _client, null);

        if (client is not null)
        {
            await client.DisposeAsync();
        }

        if (_completion is null)
        {
            throw new InvalidOperationException("The MCP client did not expose process completion details.");
        }

        try
        {
            _completionDetails = await _completion.WaitAsync(_shutdownTimeout + TimeSpan.FromSeconds(2));
        }
        catch (Exception exception)
        {
            _retainRoot = true;
            throw CreateDiagnosticException("The published Host did not stop within the shutdown timeout", exception);
        }

        if (_completionDetails is not StdioClientCompletionDetails stdioDetails)
        {
            _retainRoot = true;
            throw CreateDiagnosticException("The MCP client returned non-stdio completion details");
        }

        return stdioDetails;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_client is not null)
            {
                await StopAsync();
            }
        }
        finally
        {
            if (ShouldRetainRoot())
            {
                await AcceptanceFailureDiagnostics.WriteAsync(
                    ScenarioRoot,
                    FormatCommand(),
                    (_completionDetails as StdioClientCompletionDetails)?.ExitCode,
                    GetStandardError());
            }
            else
            {
                await AcceptanceScenarioCleanup.DeleteAsync(ScenarioRoot);
            }
        }
    }

    private static async Task<AcceptanceProcessFixture> StartAsync(
        string command,
        IReadOnlyList<string> arguments,
        AcceptanceWorkspaceAsset? workspaceAsset,
        IReadOnlyList<AcceptancePluginAsset>? pluginAssets,
        bool retainInitializationFailure,
        CancellationToken cancellationToken)
    {
        var scenarioRoot = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-acceptance",
            Guid.NewGuid().ToString("N"));

        var stateRoot = Path.Combine(scenarioRoot, "state");
        var pluginRoot = Path.Combine(scenarioRoot, "plugins");
        var effectiveArguments = arguments
            .Select(argument => argument switch
            {
                _pendingStateRootArgument => stateRoot,
                _pendingPluginRootArgument => pluginRoot,
                _ => argument,
            })
            .ToArray();

        var target = new AcceptanceProcessFixture(
            command,
            effectiveArguments,
            scenarioRoot,
            retainInitializationFailure);

        Directory.CreateDirectory(target.WorkspaceRoot);
        Directory.CreateDirectory(target.StateRoot);

        if (workspaceAsset is not null)
        {
            CopyDirectory(
                GetWorkspaceAssetPath(workspaceAsset.Value),
                target.WorkspaceRoot);
        }

        if (pluginAssets is not null)
        {
            foreach (var pluginAsset in pluginAssets)
            {
                CopyDirectory(
                    GetPluginAssetPath(pluginAsset),
                    Path.Combine(pluginRoot, GetPluginPackageDirectoryName(pluginAsset)));
            }
        }

        await target.ConnectAsync(cancellationToken);
        return target;
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "Roslyn Workbench acceptance Host",
                Command = _command,
                Arguments = _arguments.ToArray(),
                WorkingDirectory = WorkspaceRoot,
                InheritEnvironmentVariables = false,
                EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
                ShutdownTimeout = _shutdownTimeout,
                StandardErrorLines = CaptureStandardError,
            },
            NullLoggerFactory.Instance);

        using var timeoutSource = CreateTimeoutSource(_initializationTimeout, cancellationToken);

        try
        {
            _client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    InitializationTimeout = _initializationTimeout,
                },
                NullLoggerFactory.Instance,
                timeoutSource.Token);

            _completion = _client.Completion;
        }
        catch (Exception exception)
        {
            if (exception is ClientTransportClosedException transportClosedException)
            {
                _completionDetails = transportClosedException.Details;
            }

            _retainRoot = _retainInitializationFailure;
            var diagnosticException = CreateDiagnosticException("MCP initialization failed", exception);
            await DisposeAsync();
            throw diagnosticException;
        }
    }

    private static string GetWorkspaceAssetPath(AcceptanceWorkspaceAsset workspaceAsset)
    {
        var assetDirectory = workspaceAsset switch
        {
            AcceptanceWorkspaceAsset.SdkProject => "SdkProject",
            AcceptanceWorkspaceAsset.InspectionSample => Path.Combine("InspectionSample", "Base"),
            AcceptanceWorkspaceAsset.SolutionHierarchy => "SolutionHierarchy",
            AcceptanceWorkspaceAsset.MixedSolution => Path.Combine("CompatibilitySamples", "MixedSolution"),
            AcceptanceWorkspaceAsset.MultiTargetLinked => "MultiTargetLinked",
            _ => throw new ArgumentOutOfRangeException(nameof(workspaceAsset), workspaceAsset, "Unknown acceptance workspace asset."),
        };

        return Path.Combine(AppContext.BaseDirectory, "TestAssets", "Workspaces", assetDirectory);
    }

    private static string GetPluginAssetPath(AcceptancePluginAsset pluginAsset)
    {
        var assetDirectory = pluginAsset switch
        {
            AcceptancePluginAsset.HostQuery => "HostQuery",
            AcceptancePluginAsset.HostMutation => "HostMutation",
            _ => throw new ArgumentOutOfRangeException(nameof(pluginAsset), pluginAsset, "Unknown acceptance plugin asset."),
        };

        return Path.Combine(AppContext.BaseDirectory, "TestAssets", "Plugins", assetDirectory);
    }

    private static string GetPluginPackageDirectoryName(AcceptancePluginAsset pluginAsset)
    {
        return pluginAsset switch
        {
            AcceptancePluginAsset.HostQuery => "host-query",
            AcceptancePluginAsset.HostMutation => "host-mutation",
            _ => throw new ArgumentOutOfRangeException(nameof(pluginAsset), pluginAsset, "Unknown acceptance plugin asset."),
        };
    }

    private static CancellationTokenSource CreateTimeoutSource(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        return timeoutSource;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
        }
    }

    private McpClient GetClient()
    {
        return _client ?? throw new InvalidOperationException("The MCP client is not running.");
    }

    private void CaptureStandardError(string line)
    {
        lock (_standardErrorLock)
        {
            _standardError.AppendLine(line);
        }
    }

    private InvalidOperationException CreateDiagnosticException(string message, Exception? innerException = null)
    {
        var diagnosticMessage = new StringBuilder()
            .AppendLine(message)
            .Append("Command: ")
            .AppendLine(FormatCommand())
            .Append("Exit code: ")
            .AppendLine((_completionDetails as StdioClientCompletionDetails)?.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable")
            .AppendLine("Standard error:")
            .Append(GetStandardError())
            .ToString();

        return new InvalidOperationException(diagnosticMessage, innerException);
    }

    private string FormatCommand()
    {
        return string.Join(' ', new[] { _command }.Concat(_arguments).Select(QuoteArgument));
    }

    private static string QuoteArgument(string argument)
    {
        return argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
    }

    private string GetStandardError()
    {
        lock (_standardErrorLock)
        {
            if (_standardError.Length > 0)
            {
                return _standardError.ToString();
            }
        }

        if (_completionDetails is StdioClientCompletionDetails completionDetails
            && completionDetails.StandardErrorTail is { Count: > 0 })
        {
            return string.Join(Environment.NewLine, completionDetails.StandardErrorTail);
        }

        return "<none>";
    }

    private bool ShouldRetainRoot()
    {
        if (!_retainRoot)
        {
            return false;
        }

        return AcceptanceFailureDiagnostics.IsRetentionEnabled();
    }

}
