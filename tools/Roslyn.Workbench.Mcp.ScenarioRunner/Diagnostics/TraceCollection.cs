using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Runtime.ExceptionServices;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

internal sealed class TraceCollection : IAsyncDisposable
{
    private const string _performanceProvider = "Roslyn-Workbench-Mcp";

    private readonly EventPipeSession _session;
    private readonly FileStream _output;
    private readonly Task _copyTask;
    private bool _disposed;
    private bool _stopRequested;

    private TraceCollection(
        EventPipeSession session,
        FileStream output)
    {
        _session = session;
        _output = output;
        _copyTask = session.EventStream.CopyToAsync(output);
    }

    public static async Task<TraceCollection> StartAsync(
        int processId,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var runtimeProvider = new EventPipeProvider(
            ClrTraceEventParser.ProviderName,
            EventLevel.Informational,
            (long)ClrTraceEventParser.Keywords.Default);

        var sampleProfilerProvider = new EventPipeProvider(
            "Microsoft-DotNETCore-SampleProfiler",
            EventLevel.Informational);

        var workbenchProvider = new EventPipeProvider(
            _performanceProvider,
            EventLevel.Informational,
            unchecked((long)ulong.MaxValue));

        EventPipeProvider[] providers =
        [
            runtimeProvider,
            sampleProfilerProvider,
            workbenchProvider,
        ];

        var client = new DiagnosticsClient(processId);
        var session = await client.StartEventPipeSessionAsync(
            providers,
            requestRundown: true,
            circularBufferMB: 256,
            cancellationToken);

        try
        {
            var output = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 81_920,
                useAsync: true);

            var collection = new TraceCollection(session, output);
            return collection;
        }
        catch
        {
            session.Stop();
            session.Dispose();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        RequestStop();
        await _copyTask.WaitAsync(cancellationToken);
        await _output.FlushAsync(cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Trace disposal must attempt stream and EventPipe-session cleanup while retaining every finalisation failure.")]
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? failure = null;
        try
        {
            RequestStop();
            await _copyTask;
        }
        catch (Exception exception)
        {
            failure = CaptureFailure(failure, exception);
        }

        try
        {
            await _output.DisposeAsync();
        }
        catch (Exception exception)
        {
            failure = CaptureFailure(failure, exception);
        }

        try
        {
            _session.Dispose();
        }
        catch (Exception exception)
        {
            failure = CaptureFailure(failure, exception);
        }

        failure?.Throw();
    }

    private void RequestStop()
    {
        if (_stopRequested)
        {
            return;
        }

        _stopRequested = true;
        _session.Stop();
    }

    private static ExceptionDispatchInfo CaptureFailure(ExceptionDispatchInfo? current, Exception next)
    {
        return current is null
            ? ExceptionDispatchInfo.Capture(next)
            : ExceptionDispatchInfo.Capture(new AggregateException(current.SourceException, next));
    }
}
