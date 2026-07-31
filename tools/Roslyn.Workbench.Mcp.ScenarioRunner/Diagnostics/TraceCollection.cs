using System.Diagnostics.Tracing;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

internal sealed class TraceCollection : IAsyncDisposable
{
    private const string _performanceProvider = "Roslyn-Workbench-Mcp";

    private readonly EventPipeSession _session;
    private readonly FileStream _output;
    private readonly Task _copyTask;
    private bool _stopped;

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
        if (_stopped)
        {
            return;
        }

        _session.Stop();
        await _copyTask.WaitAsync(cancellationToken);
        await _output.FlushAsync(cancellationToken);
        _stopped = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopped)
        {
            _session.Stop();
            await _copyTask;
        }

        await _output.DisposeAsync();
        _session.Dispose();
    }
}
