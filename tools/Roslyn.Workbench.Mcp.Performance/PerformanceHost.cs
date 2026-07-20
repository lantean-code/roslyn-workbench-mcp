using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class PerformanceHost : IAsyncDisposable
{
    private static readonly TimeSpan _initializationTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(5);
    private readonly Process _process;
    private readonly StringBuilder _standardError = new();
    private McpClient? _client;
    private int? _exitCode;
    private bool _forcedTermination;
    private bool _isDisposed;

    private PerformanceHost(Process process)
    {
        _process = process;
    }

    public int ProcessId => _process.Id;

    public static async Task<PerformanceHost> StartAsync(
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
        var target = new PerformanceHost(process);
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

        _exitCode = _process.ExitCode;
        _process.Dispose();
        _isDisposed = true;
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
