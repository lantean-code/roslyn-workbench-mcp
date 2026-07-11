using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceCommitWriter : IWorkspaceCommitWriter
{
    private readonly IFileSystem _fileSystem;

    public WorkspaceCommitWriter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async ValueTask ApplyAsync(
        Solution baselineSolution,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        var solutionChanges = currentSolution.GetChanges(baselineSolution);

        foreach (var projectChange in solutionChanges.GetProjectChanges())
        {
            foreach (var documentId in projectChange.GetChangedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = currentSolution.GetDocument(documentId)!;
                if (document.FilePath is null)
                {
                    continue;
                }

                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(document.FilePath)!);
                await WriteDocumentTextAsync(document, cancellationToken);
            }

            foreach (var documentId in projectChange.GetAddedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = currentSolution.GetDocument(documentId)!;
                if (document.FilePath is null)
                {
                    continue;
                }

                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(document.FilePath)!);
                await WriteDocumentTextAsync(document, cancellationToken);
            }

            foreach (var documentId in projectChange.GetRemovedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = baselineSolution.GetDocument(documentId)!;
                if (!string.IsNullOrWhiteSpace(document.FilePath) && _fileSystem.File.Exists(document.FilePath))
                {
                    _fileSystem.File.Delete(document.FilePath);
                }
            }
        }
    }

    private async ValueTask WriteDocumentTextAsync(Document document, CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        await _fileSystem.File.WriteAllTextAsync(
            document.FilePath!,
            sourceText.ToString(),
            sourceText.Encoding ?? Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);
    }
}
