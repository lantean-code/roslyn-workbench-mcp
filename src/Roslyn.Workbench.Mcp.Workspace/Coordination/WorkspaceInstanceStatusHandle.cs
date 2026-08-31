using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

/// <summary>
/// Owns the live resources for workspace instance status.
/// </summary>
internal sealed class WorkspaceInstanceStatusHandle : IDisposable
{
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly Stream _stream;
    private WorkspaceInstanceStatus _status;

    /// <summary>
    /// Gets the path of the live instance record.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the workspace root advertised by the record.
    /// </summary>
    public string WorkspaceRoot => _status.WorkspaceRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceInstanceStatusHandle"/> class.
    /// </summary>
    /// <param name="path">The path of the live instance record.</param>
    /// <param name="stream">The locked stream used to publish the record.</param>
    /// <param name="status">The initial instance status.</param>
    /// <param name="serializerOptions">The options used to serialize the record.</param>
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

    /// <summary>
    /// Writes the current workspace ownership status to the instance record.
    /// </summary>
    /// <returns>A task that completes when the record has been written.</returns>
    public async ValueTask PublishAsync()
    {
        await WriteAsync();
    }

    /// <summary>
    /// Updates the in-memory workspace status and writes the revised instance record.
    /// </summary>
    /// <param name="state">The current workspace lifecycle state.</param>
    /// <param name="transactionRevision">The current transaction revision, when a transaction is active.</param>
    /// <param name="commitId">The commit identifier.</param>
    /// <param name="commitPhase">The commit phase in which the operation is running.</param>
    /// <returns>A task that completes when the revised record has been written.</returns>
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

    /// <summary>
    /// Releases resources held by this instance.
    /// </summary>
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
    }
}
