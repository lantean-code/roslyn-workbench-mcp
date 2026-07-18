using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal enum AcceptanceWorkspaceAsset
{
    SdkProject,
    InspectionSample,
}

internal enum AcceptancePluginAsset
{
    HostQuery,
}

internal sealed class AcceptanceProcessFixture : IAsyncDisposable
{
    private const string PendingStateRootArgument = "{acceptance-state-root}";
    private const string PendingPluginRootArgument = "{acceptance-plugin-root}";
    private static readonly TimeSpan _initializationTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan _invocationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _cleanupTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _cleanupRetryInterval = TimeSpan.FromMilliseconds(50);

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
        AcceptancePluginAsset? pluginAsset = null)
    {
        var executablePath = PublishedHostExecutable.ResolveFromEnvironment();
        var arguments = new List<string>
        {
            "--state-directory",
            PendingStateRootArgument,
        };
        arguments.AddRange(additionalArguments ?? []);
        if (pluginAsset is not null)
        {
            arguments.Add("--plugin-directory");
            arguments.Add(PendingPluginRootArgument);
        }

        return StartAsync(
            executablePath,
            arguments,
            workspaceAsset,
            pluginAsset,
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
            pluginAsset: null,
            retainInitializationFailure: false,
            cancellationToken);
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        using var timeoutSource = CreateTimeoutSource(cancellationToken, _invocationTimeout);

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
        using var timeoutSource = CreateTimeoutSource(cancellationToken, _invocationTimeout);

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
                await DeleteScenarioRootAsync();
            }
        }
    }

    private static async Task<AcceptanceProcessFixture> StartAsync(
        string command,
        IReadOnlyList<string> arguments,
        AcceptanceWorkspaceAsset? workspaceAsset,
        AcceptancePluginAsset? pluginAsset,
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
                PendingStateRootArgument => stateRoot,
                PendingPluginRootArgument => pluginRoot,
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

        if (pluginAsset is not null)
        {
            CopyDirectory(
                GetPluginAssetPath(pluginAsset.Value),
                Path.Combine(pluginRoot, "host-query"));
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
        using var timeoutSource = CreateTimeoutSource(cancellationToken, _initializationTimeout);

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
            _ => throw new ArgumentOutOfRangeException(nameof(workspaceAsset), workspaceAsset, "Unknown acceptance workspace asset."),
        };

        return Path.Combine(AppContext.BaseDirectory, "TestAssets", "Workspaces", assetDirectory);
    }

    private static string GetPluginAssetPath(AcceptancePluginAsset pluginAsset)
    {
        var assetDirectory = pluginAsset switch
        {
            AcceptancePluginAsset.HostQuery => "HostQuery",
            _ => throw new ArgumentOutOfRangeException(nameof(pluginAsset), pluginAsset, "Unknown acceptance plugin asset."),
        };

        return Path.Combine(AppContext.BaseDirectory, "TestAssets", "Plugins", assetDirectory);
    }

    private static CancellationTokenSource CreateTimeoutSource(
        CancellationToken cancellationToken,
        TimeSpan timeout)
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

    private async Task DeleteScenarioRootAsync()
    {
        using var retryTimer = new PeriodicTimer(_cleanupRetryInterval);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastException = null;

        do
        {
            try
            {
                if (!Directory.Exists(ScenarioRoot))
                {
                    return;
                }

                Directory.Delete(ScenarioRoot, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastException = exception;
            }
        }
        while (elapsed.Elapsed < _cleanupTimeout
            && await retryTimer.WaitForNextTickAsync());

        throw new IOException($"The acceptance scenario root '{ScenarioRoot}' could not be removed.", lastException);
    }
}
