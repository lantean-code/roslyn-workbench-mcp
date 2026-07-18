using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed class WorkspaceInstanceStatusHandle : IDisposable
{
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly Stream _stream;
    private WorkspaceInstanceStatus _status;

    public string Path { get; }

    public WorkspaceInstanceStatusHandle(
        string path,
        Stream stream,
        WorkspaceInstanceStatus status,
        JsonSerializerOptions serializerOptions)
    {
        Path = path;
        _stream = stream;
        _status = status;
        _serializerOptions = serializerOptions;
    }

    public async ValueTask PublishAsync()
    {
        await WriteAsync();
    }

    public async ValueTask UpdateAsync(
        WorkspaceLifecycleState state,
        long? transactionRevision,
        string? commitId,
        string? commitPhase)
    {
        _status = _status with
        {
            WorkspaceState = state,
            TransactionRevision = transactionRevision,
            CommitId = commitId,
            CommitPhase = commitPhase,
        };
        await WriteAsync();
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    private async ValueTask WriteAsync()
    {
        _stream.Position = 0;
        _stream.SetLength(0);
        await JsonSerializer.SerializeAsync(
            _stream,
            _status,
            _serializerOptions,
            CancellationToken.None);
        await _stream.FlushAsync(CancellationToken.None);
        _stream.Flush();
    }
}
