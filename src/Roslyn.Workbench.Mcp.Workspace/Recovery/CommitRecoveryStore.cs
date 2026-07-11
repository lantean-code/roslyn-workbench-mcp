using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Provides durable storage for unfinished commit recovery records.
/// </summary>
internal sealed class CommitRecoveryStore : ICommitRecoveryStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly string _recoveryDirectory;

    public CommitRecoveryStore(
        IOptions<WorkspaceCoordinatorOptions> options,
        IFileSystem fileSystem,
        IAtomicFileWriter atomicFileWriter)
    {
        ArgumentNullException.ThrowIfNull(options);
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _atomicFileWriter = atomicFileWriter ?? throw new ArgumentNullException(nameof(atomicFileWriter));
        _recoveryDirectory = _fileSystem.Path.Combine(
            _fileSystem.Path.GetFullPath(options.Value.StateDirectory),
            "recovery");
    }

    public async ValueTask<IReadOnlyList<RecoveryStatus>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        if (!_fileSystem.Directory.Exists(_recoveryDirectory))
        {
            return [];
        }

        var statuses = new List<RecoveryStatus>();

        foreach (var filePath in _fileSystem.Directory.EnumerateFiles(_recoveryDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                var status = JsonSerializer.Deserialize<RecoveryStatus>(json, _serializerOptions);
                if (status is not null)
                {
                    statuses.Add(status);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (JsonException)
            {
            }
        }

        return statuses;
    }

    public async ValueTask WriteStatusAsync(RecoveryStatus status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(status);
        cancellationToken.ThrowIfCancellationRequested();

        _fileSystem.Directory.CreateDirectory(_recoveryDirectory);

        var filePath = _fileSystem.Path.Combine(_recoveryDirectory, $"{status.CommitId}.json");
        await _atomicFileWriter.WriteAllTextAsync(
            filePath,
            JsonSerializer.Serialize(status, _serializerOptions),
            _encoding,
            cancellationToken).ConfigureAwait(false);
    }

    public void DeleteStatus(string commitId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commitId);

        var filePath = _fileSystem.Path.Combine(_recoveryDirectory, $"{commitId}.json");
        if (_fileSystem.File.Exists(filePath))
        {
            _fileSystem.File.Delete(filePath);
        }
    }
}
