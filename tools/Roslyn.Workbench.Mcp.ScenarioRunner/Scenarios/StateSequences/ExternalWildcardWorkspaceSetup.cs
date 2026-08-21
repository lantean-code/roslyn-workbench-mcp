using System.Globalization;
using System.Security;
using System.Text;

using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed class ExternalWildcardWorkspaceSetup : IDisposable
{
    private readonly string _externalRoot;
    private readonly byte[] _originalProjectContents;
    private readonly string _projectPath;
    private readonly object _restoreLock = new();
    private bool _isRestored;

    private ExternalWildcardWorkspaceSetup(
        string projectPath,
        byte[] originalProjectContents,
        string externalRoot)
    {
        _projectPath = projectPath;
        _originalProjectContents = originalProjectContents;
        _externalRoot = externalRoot;
    }

    public void Dispose()
    {
        Restore();
    }

    public void Restore()
    {
        lock (_restoreLock)
        {
            if (_isRestored)
            {
                return;
            }

            File.WriteAllBytes(_projectPath, _originalProjectContents);
            if (Directory.Exists(_externalRoot))
            {
                Directory.Delete(_externalRoot, recursive: true);
            }

            _isRestored = true;
        }
    }

    public static ExternalWildcardWorkspaceSetup Apply(
        string repositoryRoot,
        ExternalWildcardStressDefinition definition,
        CancellationToken cancellationToken)
    {
        Validate(definition);
        var projectPath = Path.GetFullPath(definition.TargetProjectPath, repositoryRoot);
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException(
                "The external-wildcard stress target project does not exist.",
                projectPath);
        }

        var repositoryParent = Path.GetDirectoryName(repositoryRoot)
            ?? throw new InvalidOperationException("The repository root does not have a parent directory.");

        var externalRoot = Path.Combine(
            repositoryParent,
            $".roslyn-workbench-external-wildcard-{Guid.NewGuid():N}");

        var originalProjectContents = File.ReadAllBytes(projectPath);
        var setup = new ExternalWildcardWorkspaceSetup(
            projectPath,
            originalProjectContents,
            externalRoot);

        try
        {
            setup.CreateExternalFilesAndProjectItems(definition, cancellationToken);
            return setup;
        }
        catch
        {
            setup.Restore();
            throw;
        }
    }

    private void CreateExternalFilesAndProjectItems(
        ExternalWildcardStressDefinition definition,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_externalRoot);
        var projectText = Encoding.UTF8.GetString(_originalProjectContents);
        var insertionIndex = projectText.LastIndexOf("</Project>", StringComparison.Ordinal);
        if (insertionIndex < 0)
        {
            throw new InvalidDataException(
                $"Project '{_projectPath}' does not contain a closing Project element.");
        }

        var itemGroup = new StringBuilder()
            .AppendLine("  <ItemGroup>");

        for (var rootIndex = 0; rootIndex < definition.RootCount; rootIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rootPath = Path.Combine(
                _externalRoot,
                $"root-{rootIndex:D4}");

            Directory.CreateDirectory(rootPath);
            var escapedRoot = SecurityElement.Escape(
                rootPath.Replace(Path.DirectorySeparatorChar, '/'));

            for (var globIndex = 0; globIndex < definition.GlobsPerRoot; globIndex++)
            {
                var extension = $"rwmcp{globIndex:D2}.txt";
                itemGroup
                    .Append("    <AdditionalFiles Include=\"")
                    .Append(escapedRoot)
                    .Append("/**/*.")
                    .Append(extension)
                    .AppendLine("\" />");

                for (var fileIndex = 0; fileIndex < definition.FilesPerGlob; fileIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var filePath = Path.Combine(
                        rootPath,
                        $"file-{globIndex:D2}-{fileIndex:D5}.{extension}");

                    File.WriteAllText(
                        filePath,
                        fileIndex.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        itemGroup.AppendLine("  </ItemGroup>");
        var updatedProjectText = projectText.Insert(insertionIndex, itemGroup.ToString());
        File.WriteAllText(_projectPath, updatedProjectText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void Validate(ExternalWildcardStressDefinition definition)
    {
        if (definition.RootCount <= 0
            || definition.GlobsPerRoot <= 0
            || definition.FilesPerGlob < 0
            || string.IsNullOrWhiteSpace(definition.TargetProjectPath))
        {
            throw new InvalidDataException(
                "External-wildcard stress requires a target project, positive root and glob counts, and a non-negative file count.");
        }
    }
}
