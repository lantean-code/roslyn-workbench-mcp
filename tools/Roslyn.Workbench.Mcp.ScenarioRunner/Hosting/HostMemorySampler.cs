using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

internal sealed class HostMemorySampler : IAsyncDisposable
{
    private static readonly TimeSpan _samplingInterval = TimeSpan.FromMilliseconds(10);
    private readonly CancellationTokenSource _stopSource = new();
    private readonly Process _process;
    private readonly Task _samplingTask;
    private readonly long _baselinePrivateMemoryBytes;
    private readonly long _baselineWorkingSetBytes;
    private long _peakPrivateMemoryBytes;
    private long _peakWorkingSetBytes;
    private int _sampleCount;
    private bool _isComplete;

    public HostMemorySampler(Process process)
    {
        _process = process;

        var baseline = CaptureSnapshot();
        _baselineWorkingSetBytes = baseline.WorkingSetBytes;
        _baselinePrivateMemoryBytes = baseline.PrivateMemoryBytes;
        _peakWorkingSetBytes = baseline.WorkingSetBytes;
        _peakPrivateMemoryBytes = baseline.PrivateMemoryBytes;
        _sampleCount = 1;
        _samplingTask = SampleUntilStoppedAsync(_stopSource.Token);
    }

    public async ValueTask<HostMemoryMeasurement> CompleteAsync()
    {
        if (_isComplete)
        {
            throw new InvalidOperationException("Host memory sampling has already completed.");
        }

        _isComplete = true;
        await _stopSource.CancelAsync();
        await _samplingTask;

        var final = CaptureSnapshot();
        RecordPeak(final);
        _sampleCount++;

        return new HostMemoryMeasurement
        {
            SamplingIntervalMilliseconds = _samplingInterval.TotalMilliseconds,
            SampleCount = _sampleCount,
            BaselineWorkingSetBytes = _baselineWorkingSetBytes,
            FinalWorkingSetBytes = final.WorkingSetBytes,
            PeakWorkingSetBytes = _peakWorkingSetBytes,
            BaselinePrivateMemoryBytes = _baselinePrivateMemoryBytes,
            FinalPrivateMemoryBytes = final.PrivateMemoryBytes,
            PeakPrivateMemoryBytes = _peakPrivateMemoryBytes,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isComplete)
        {
            _isComplete = true;
            await _stopSource.CancelAsync();
            await _samplingTask;
        }

        _stopSource.Dispose();
    }

    private async Task SampleUntilStoppedAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_samplingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var snapshot = CaptureSnapshot();
                RecordPeak(snapshot);
                _sampleCount++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private HostMemorySnapshot CaptureSnapshot()
    {
        _process.Refresh();
        return new HostMemorySnapshot
        {
            WorkingSetBytes = _process.WorkingSet64,
            PrivateMemoryBytes = _process.PrivateMemorySize64,
        };
    }

    private void RecordPeak(HostMemorySnapshot snapshot)
    {
        _peakWorkingSetBytes = Math.Max(
            _peakWorkingSetBytes,
            snapshot.WorkingSetBytes);
        _peakPrivateMemoryBytes = Math.Max(
            _peakPrivateMemoryBytes,
            snapshot.PrivateMemoryBytes);
    }

    private readonly struct HostMemorySnapshot
    {
        public required long WorkingSetBytes { get; init; }

        public required long PrivateMemoryBytes { get; init; }
    }
}
