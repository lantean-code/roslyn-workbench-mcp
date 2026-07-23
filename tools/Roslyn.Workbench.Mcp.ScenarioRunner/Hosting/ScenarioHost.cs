using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

internal sealed class ScenarioHost : IAsyncDisposable
{
    private static readonly TimeSpan _initializationTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(5);
    private readonly Process _process;
    private readonly StringBuilder _standardError = new();
    private readonly object _terminationLock = new();
    private McpClient? _client;
    private int? _exitCode;
    private long _lastCancellationRequestId;
    private bool _forcedTermination;
    private bool _isDisposed;

    private ScenarioHost(Process process)
    {
        _process = process;
    }

    public int ProcessId => _process.Id;

    public static async Task<ScenarioHost> StartAsync(
        string hostPath,
        string workingDirectory,
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stateDirectory);
        var startInfo = CreateStartInfo(hostPath, workingDirectory, stateDirectory);
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        var target = new ScenarioHost(process);
        process.ErrorDataReceived += target.CaptureStandardError;

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Unable to start Host '{hostPath}'.");
        }

        process.BeginErrorReadLine();
        var transport = new StreamClientTransport(
            process.StandardInput.BaseStream,
            process.StandardOutput.BaseStream,
            NullLoggerFactory.Instance);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_initializationTimeout);

        try
        {
            target._client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions { InitializationTimeout = _initializationTimeout },
                NullLoggerFactory.Instance,
                timeoutSource.Token);
            return target;
        }
        catch
        {
            await target.DisposeAsync();
            throw;
        }
    }

    public ValueTask<CallToolResult> CallToolAsync(
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var client = _client ?? throw new InvalidOperationException("The MCP client is not connected.");
        return client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);
    }

    public (RequestId RequestId, Task<CallToolResult> Completion) StartCancellableToolCall(
        string tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var client = _client ?? throw new InvalidOperationException("The MCP client is not connected.");
        var requestId = new RequestId($"scenario-cancellation-{Interlocked.Increment(ref _lastCancellationRequestId)}");
        var requestArguments = new Dictionary<string, JsonElement>(arguments.Count);
        foreach (var (name, value) in arguments)
        {
            requestArguments.Add(name, JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object)));
        }

        var request = new CallToolRequestParams
        {
            Name = tool,
            Arguments = requestArguments,
        };

        var completion = client.SendRequestAsync<CallToolRequestParams, CallToolResult>(
            RequestMethods.ToolsCall,
            request,
            requestId: requestId,
            cancellationToken: cancellationToken).AsTask();

        return (requestId, completion);
    }

    public Task CancelToolCallAsync(RequestId requestId, CancellationToken cancellationToken)
    {
        var client = _client ?? throw new InvalidOperationException("The MCP client is not connected.");
        var parameters = new CancelledNotificationParams
        {
            RequestId = requestId,
        };

        return client.SendNotificationAsync(
            NotificationMethods.CancelledNotification,
            parameters,
            cancellationToken: cancellationToken);
    }

    public HostSnapshot CaptureSnapshot()
    {
        _process.Refresh();
        return new HostSnapshot
        {
            CpuTime = _process.TotalProcessorTime,
            WorkingSetBytes = _process.WorkingSet64,
            PeakWorkingSetBytes = _process.PeakWorkingSet64,
        };
    }

    public string GetStandardError()
    {
        lock (_standardError)
        {
            return _standardError.ToString();
        }
    }

    public HostShutdownResult GetShutdownResult()
    {
        return new HostShutdownResult
        {
            ExitCode = _exitCode,
            ForcedTermination = _forcedTermination,
            StandardError = GetStandardError(),
        };
    }

    public async ValueTask TerminateAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _ = TryTerminate();
        if (!_process.HasExited)
        {
            await _process.WaitForExitAsync(CancellationToken.None);
        }

        await DisposeResourcesAsync();
    }

    public bool TryTerminate()
    {
        lock (_terminationLock)
        {
            if (_isDisposed || _process.HasExited)
            {
                return false;
            }

            _process.Kill(entireProcessTree: true);
            _forcedTermination = true;
            return true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        _process.StandardInput.Close();
        if (!_process.HasExited)
        {
            using var timeoutSource = new CancellationTokenSource(_shutdownTimeout);
            try
            {
                await _process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                _forcedTermination = true;
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }

        await DisposeResourcesAsync();
    }

    private async ValueTask DisposeResourcesAsync()
    {
        try
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }
        }
        finally
        {
            _exitCode = _process.ExitCode;
            _process.Dispose();
            _isDisposed = true;
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string hostPath,
        string workingDirectory,
        string stateDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (string.Equals(Path.GetExtension(hostPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(hostPath);
        }
        else
        {
            startInfo.FileName = hostPath;
        }

        startInfo.ArgumentList.Add("--state-directory");
        startInfo.ArgumentList.Add(stateDirectory);
        startInfo.Environment["NUGET_PACKAGES"] = RepositoryManager.GetNuGetPackagesDirectory(workingDirectory);
        return startInfo;
    }

    private void CaptureStandardError(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is null)
        {
            return;
        }

        lock (_standardError)
        {
            _standardError.AppendLine(eventArgs.Data);
        }
    }
}
