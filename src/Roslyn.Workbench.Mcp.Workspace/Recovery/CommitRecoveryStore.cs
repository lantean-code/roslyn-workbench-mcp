using System.Text.Json;

using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Provides durable storage for unfinished commit recovery records.
/// </summary>
internal static class CommitRecoveryStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<RecoveryStatus> GetStatuses(string stateDirectory)
    {
        if (string.IsNullOrWhiteSpace(stateDirectory))
        {
            return [];
        }

        var directoryPath = GetRecoveryDirectory(stateDirectory);
        if (!Directory.Exists(directoryPath))
        {
            return [];
        }

        var statuses = new List<RecoveryStatus>();

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var status = JsonSerializer.Deserialize<RecoveryStatus>(json, _serializerOptions);
                if (status is not null)
                {
                    statuses.Add(status);
                }
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
        }

        return statuses;
    }

    public static void WriteStatus(string stateDirectory, RecoveryStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
        ArgumentNullException.ThrowIfNull(status);

        var directoryPath = GetRecoveryDirectory(stateDirectory);
        Directory.CreateDirectory(directoryPath);

        var filePath = Path.Combine(directoryPath, $"{status.CommitId}.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(status, _serializerOptions));
    }

    public static void DeleteStatus(string stateDirectory, string commitId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitId);

        var filePath = Path.Combine(GetRecoveryDirectory(stateDirectory), $"{commitId}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private static string GetRecoveryDirectory(string stateDirectory)
    {
        return Path.Combine(stateDirectory, "recovery");
    }
}
