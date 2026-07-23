using System.Text;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed class ExternalMemberInsertion : IAsyncDisposable
{
    private static readonly Encoding _encoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private readonly byte[] _originalContents;
    private readonly string _path;
    private bool _isRestored;

    private ExternalMemberInsertion(string path, byte[] originalContents)
    {
        _path = path;
        _originalContents = originalContents;
    }

    public static async Task<ExternalMemberInsertion> ApplyAsync(
        string repositoryRoot,
        ExternalMemberInsertionDefinition definition,
        CancellationToken cancellationToken)
    {
        var path = ResolveRepositoryPath(repositoryRoot, definition.Path);
        var originalContents = await File.ReadAllBytesAsync(path, cancellationToken);
        var originalText = _encoding.GetString(originalContents);
        var declarationIndex = originalText.IndexOf(
            definition.TypeDeclaration,
            StringComparison.Ordinal);
        if (declarationIndex < 0)
        {
            throw new InvalidDataException(
                $"External mutation declaration '{definition.TypeDeclaration}' was not found in '{definition.Path}'.");
        }

        var duplicateIndex = originalText.IndexOf(
            definition.TypeDeclaration,
            declarationIndex + definition.TypeDeclaration.Length,
            StringComparison.Ordinal);
        if (duplicateIndex >= 0)
        {
            throw new InvalidDataException(
                $"External mutation declaration '{definition.TypeDeclaration}' was not unique in '{definition.Path}'.");
        }

        var openingBraceIndex = originalText.IndexOf(
            '{',
            declarationIndex + definition.TypeDeclaration.Length);
        if (openingBraceIndex < 0)
        {
            throw new InvalidDataException(
                $"External mutation declaration '{definition.TypeDeclaration}' did not have an opening brace.");
        }

        var newline = originalText.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
        var insertion = $"{newline}    {definition.MemberDeclaration}";
        var changedText = originalText.Insert(openingBraceIndex + 1, insertion);
        var target = new ExternalMemberInsertion(path, originalContents);

        try
        {
            await File.WriteAllBytesAsync(
                path,
                _encoding.GetBytes(changedText),
                cancellationToken);

            return target;
        }
        catch
        {
            await target.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isRestored)
        {
            return;
        }

        await File.WriteAllBytesAsync(
            _path,
            _originalContents,
            CancellationToken.None);

        _isRestored = true;
    }

    private static string ResolveRepositoryPath(
        string repositoryRoot,
        string relativePath)
    {
        var fullPath = Path.GetFullPath(
            Path.Combine(repositoryRoot, relativePath));

        var relativeToRoot = Path.GetRelativePath(repositoryRoot, fullPath);
        if (relativeToRoot == ".."
            || relativeToRoot.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToRoot))
        {
            throw new InvalidDataException(
                $"External mutation path '{relativePath}' resolves outside '{repositoryRoot}'.");
        }

        return fullPath;
    }
}
