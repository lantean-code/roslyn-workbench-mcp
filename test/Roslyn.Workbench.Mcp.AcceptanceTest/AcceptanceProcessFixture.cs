using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed class AcceptanceProcessFixture : IAsyncDisposable
{
    private const string RetainRootEnvironmentVariableName = "ROSLYN_WORKBENCH_MCP_ACCEPTANCE_RETAIN_ROOT";
    private const string PendingStateRootArgument = "{acceptance-state-root}";
    private static readonly TimeSpan _initializationTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan _invocationTimeout = TimeSpan.FromSeconds(30);

    // MCP C# SDK 1.4.1 waits before closing stdin, so keep its forced-cleanup fallback short; direct EOF coverage owns graceful Host shutdown evidence.
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(2);

    private readonly object _standardErrorLock = new();
    private readonly StringBuilder _standardError = new();
    private readonly string _command;
    private readonly IReadOnlyList<string> _arguments;
    private McpClient? _client;
    private Task<ClientCompletionDetails>? _completion;
    private ClientCompletionDetails? _completionDetails;
    private bool _retainRoot;
    private bool _disposed;

    private AcceptanceProcessFixture(string command, IReadOnlyList<string> arguments, string scenarioRoot)
    {
        _command = command;
        _arguments = arguments;
        ScenarioRoot = scenarioRoot;
        WorkspaceRoot = Path.Combine(scenarioRoot, "workspace");
        StateRoot = Path.Combine(scenarioRoot, "state");
    }

    public string ScenarioRoot { get; }

    public string WorkspaceRoot { get; }

    public string StateRoot { get; }

    public static Task<AcceptanceProcessFixture> StartPublishedHostAsync(CancellationToken cancellationToken)
    {
        var executablePath = PublishedHostExecutable.ResolveFromEnvironment();

        return StartAsync(
            executablePath,
            ["--state-directory", PendingStateRootArgument],
            copyWorkspaceAsset: true,
            cancellationToken);
    }

    internal static Task<AcceptanceProcessFixture> StartCommandAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return StartAsync(command, arguments, copyWorkspaceAsset: false, cancellationToken);
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
            if (!ShouldRetainRoot())
            {
                Directory.Delete(ScenarioRoot, recursive: true);
            }
        }
    }

    private static async Task<AcceptanceProcessFixture> StartAsync(
        string command,
        IReadOnlyList<string> arguments,
        bool copyWorkspaceAsset,
        CancellationToken cancellationToken)
    {
        var scenarioRoot = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-acceptance",
            Guid.NewGuid().ToString("N"));
        var stateRoot = Path.Combine(scenarioRoot, "state");
        var effectiveArguments = arguments
            .Select(argument => string.Equals(argument, PendingStateRootArgument, StringComparison.Ordinal)
                ? stateRoot
                : argument)
            .ToArray();
        var target = new AcceptanceProcessFixture(command, effectiveArguments, scenarioRoot);

        Directory.CreateDirectory(target.WorkspaceRoot);
        Directory.CreateDirectory(target.StateRoot);

        if (copyWorkspaceAsset)
        {
            CopyDirectory(
                Path.Combine(AppContext.BaseDirectory, "TestAssets", "Workspaces", "SdkProject"),
                target.WorkspaceRoot);
        }

        var transport = new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = "Roslyn Workbench acceptance Host",
                Command = command,
                Arguments = effectiveArguments,
                WorkingDirectory = target.WorkspaceRoot,
                InheritEnvironmentVariables = false,
                EnvironmentVariables = StdioClientTransportOptions.GetDefaultEnvironmentVariables(),
                ShutdownTimeout = _shutdownTimeout,
                StandardErrorLines = target.CaptureStandardError,
            },
            NullLoggerFactory.Instance);
        using var timeoutSource = CreateTimeoutSource(cancellationToken, _initializationTimeout);

        try
        {
            target._client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    InitializationTimeout = _initializationTimeout,
                },
                NullLoggerFactory.Instance,
                timeoutSource.Token);
            target._completion = target._client.Completion;

            return target;
        }
        catch (Exception exception)
        {
            if (exception is ClientTransportClosedException transportClosedException)
            {
                target._completionDetails = transportClosedException.Details;
            }

            target._retainRoot = true;
            var diagnosticException = target.CreateDiagnosticException("MCP initialization failed", exception);
            await target.DisposeAsync();
            throw diagnosticException;
        }
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
            .AppendLine((_completionDetails as StdioClientCompletionDetails)?.ExitCode?.ToString() ?? "unavailable")
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

        var configuredValue = Environment.GetEnvironmentVariable(RetainRootEnvironmentVariableName);
        return string.Equals(configuredValue, "1", StringComparison.Ordinal)
            || string.Equals(configuredValue, "true", StringComparison.OrdinalIgnoreCase);
    }
}
