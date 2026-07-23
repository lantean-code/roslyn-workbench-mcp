using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal static class RecoveryEvidenceReader
{
    public static async Task<RecoveryEvidence> ReadAsync(
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(stateDirectory))
        {
            return new RecoveryEvidence();
        }

        var manifestPaths = Directory
            .EnumerateFiles(stateDirectory, "manifest.json", SearchOption.AllDirectories)
            .ToArray();
        if (manifestPaths.Length == 0)
        {
            return new RecoveryEvidence();
        }

        if (manifestPaths.Length != 1)
        {
            throw new InvalidDataException(
                $"Expected one recovery manifest but found {manifestPaths.Length}.");
        }

        await using var stream = File.OpenRead(manifestPaths[0]);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        var state = document.RootElement
            .GetProperty("state")
            .GetString();
        var commitDirectory = Path.GetDirectoryName(manifestPaths[0])
            ?? throw new InvalidDataException(
                $"Recovery manifest '{manifestPaths[0]}' has no parent directory.");
        var artifactCount = Directory
            .EnumerateFiles(commitDirectory, "*.bin", SearchOption.AllDirectories)
            .Count();

        return new RecoveryEvidence
        {
            State = state,
            ArtifactCount = artifactCount,
        };
    }
}
