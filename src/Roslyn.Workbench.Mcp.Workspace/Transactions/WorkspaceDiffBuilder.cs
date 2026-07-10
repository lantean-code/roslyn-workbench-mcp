using System.Text.RegularExpressions;

using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal static class WorkspaceDiffBuilder
{
    private static readonly IDiffer _differ = new Differ();

    private static readonly Regex _hunkHeaderPattern = new(
        "^@@ -(?<originalStart>\\d+)(?:,(?<originalCount>\\d+))? \\+(?<updatedStart>\\d+)(?:,(?<updatedCount>\\d+))? @@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async ValueTask<ChangeSummary> CreateChangeSummaryAsync(
        Solution baselineSolution,
        Solution currentSolution,
        IWorkspaceResolver resolver,
        CancellationToken cancellationToken)
    {
        var solutionChanges = currentSolution.GetChanges(baselineSolution);
        var added = new List<DocumentChange>();
        var modified = new List<DocumentChange>();
        var deleted = new List<DocumentChange>();

        foreach (var projectChange in solutionChanges.GetProjectChanges())
        {
            foreach (var documentId in projectChange.GetAddedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = currentSolution.GetDocument(documentId);
                if (document is null)
                {
                    continue;
                }

                added.Add(new DocumentChange
                {
                    Document = resolver.CreateDocumentReference(document),
                    ChangeKind = DocumentChangeKind.Added,
                    Preview = CreateDiffSummary(string.Empty, await GetDocumentTextAsync(document, cancellationToken)),
                });
            }

            foreach (var documentId in projectChange.GetChangedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var oldDocument = baselineSolution.GetDocument(documentId);
                var newDocument = currentSolution.GetDocument(documentId);
                if (oldDocument is null || newDocument is null)
                {
                    continue;
                }

                modified.Add(new DocumentChange
                {
                    Document = resolver.CreateDocumentReference(newDocument),
                    ChangeKind = DocumentChangeKind.Modified,
                    Preview = CreateDiffSummary(
                        await GetDocumentTextAsync(oldDocument, cancellationToken),
                        await GetDocumentTextAsync(newDocument, cancellationToken)),
                });
            }

            foreach (var documentId in projectChange.GetRemovedDocuments())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = baselineSolution.GetDocument(documentId);
                if (document is null)
                {
                    continue;
                }

                deleted.Add(new DocumentChange
                {
                    Document = resolver.CreateDocumentReference(document),
                    ChangeKind = DocumentChangeKind.Deleted,
                    Preview = CreateDiffSummary(await GetDocumentTextAsync(document, cancellationToken), string.Empty),
                });
            }
        }

        return new ChangeSummary
        {
            Added = added,
            Modified = modified,
            Deleted = deleted,
        };
    }

    public static async ValueTask<DocumentDiff?> CreateDocumentDiffAsync(
        Solution baselineSolution,
        Solution currentSolution,
        DocumentReference documentReference,
        IWorkspaceResolver resolver,
        int contextLines,
        CancellationToken cancellationToken)
    {
        var currentDocumentResolution = resolver.ResolveDocument(new DocumentSelector
        {
            DocumentId = documentReference.DocumentId,
        });

        var currentDocument = currentDocumentResolution.Value;
        var baselineDocument = baselineSolution.Projects
            .SelectMany(static project => project.Documents)
            .FirstOrDefault(document => document.Id.Id.ToString() == documentReference.DocumentId);

        if (currentDocument is null && baselineDocument is null)
        {
            return null;
        }

        var oldText = await GetDocumentTextAsync(baselineDocument, cancellationToken);
        var newText = await GetDocumentTextAsync(currentDocument, cancellationToken);

        return new DocumentDiff
        {
            Document = currentDocument is not null
                ? resolver.CreateDocumentReference(currentDocument)
                : resolver.CreateDocumentReference(baselineDocument!),
            Hunks = CreateHunks(oldText, newText, contextLines),
            Truncated = false,
        };
    }

    private static DiffSummary CreateDiffSummary(string oldText, string newText)
    {
        var diffResult = _differ.CreateDiffs(oldText, newText, false, false, LineChunker.Instance);
        var addedLines = 0;
        var removedLines = 0;
        var changedLines = 0;

        foreach (var diffBlock in diffResult.DiffBlocks)
        {
            changedLines += Math.Min(diffBlock.DeleteCountA, diffBlock.InsertCountB);
            removedLines += Math.Max(0, diffBlock.DeleteCountA - diffBlock.InsertCountB);
            addedLines += Math.Max(0, diffBlock.InsertCountB - diffBlock.DeleteCountA);
        }

        return new DiffSummary
        {
            AddedLines = addedLines,
            RemovedLines = removedLines,
            ChangedLines = changedLines,
        };
    }

    private static IReadOnlyList<DiffHunk> CreateHunks(string oldText, string newText, int contextLines)
    {
        var unifiedDiff = UnidiffRenderer.GenerateUnidiff(
            oldText,
            newText,
            "before",
            "after",
            false,
            false,
            Math.Max(0, contextLines));
        var lines = SplitLines(unifiedDiff);
        var hunks = new List<DiffHunk>();
        DiffHunkBuilder? currentHunk = null;

        foreach (var line in lines)
        {
            var match = _hunkHeaderPattern.Match(line);
            if (match.Success)
            {
                if (currentHunk is not null)
                {
                    hunks.Add(currentHunk.Build());
                }

                currentHunk = new DiffHunkBuilder(
                    ParseHunkNumber(match, "originalStart"),
                    ParseHunkCount(match, "originalCount"),
                    ParseHunkNumber(match, "updatedStart"),
                    ParseHunkCount(match, "updatedCount"));

                continue;
            }

            if (currentHunk is null || line.StartsWith("--- ", StringComparison.Ordinal) || line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                continue;
            }

            if (line == @"\ No newline at end of file")
            {
                continue;
            }

            currentHunk.Lines.Add(line);
        }

        if (currentHunk is not null)
        {
            hunks.Add(currentHunk.Build());
        }

        return hunks;
    }

    private static string[] SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);
    }

    private static async ValueTask<string> GetDocumentTextAsync(Document? document, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return string.Empty;
        }

        var text = await document.GetTextAsync(cancellationToken);
        return text.ToString();
    }

    private static int ParseHunkNumber(Match match, string groupName)
    {
        return int.Parse(match.Groups[groupName].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int ParseHunkCount(Match match, string groupName)
    {
        var group = match.Groups[groupName];
        return group.Success ? int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
    }

    private sealed class DiffHunkBuilder
    {
        public DiffHunkBuilder(int originalStartLine, int originalLineCount, int updatedStartLine, int updatedLineCount)
        {
            OriginalStartLine = originalStartLine;
            OriginalLineCount = originalLineCount;
            UpdatedStartLine = updatedStartLine;
            UpdatedLineCount = updatedLineCount;
        }

        public int OriginalStartLine { get; }

        public int OriginalLineCount { get; }

        public int UpdatedStartLine { get; }

        public int UpdatedLineCount { get; }

        public List<string> Lines { get; } = [];

        public DiffHunk Build()
        {
            return new DiffHunk
            {
                OriginalStartLine = OriginalStartLine,
                OriginalLineCount = OriginalLineCount,
                UpdatedStartLine = UpdatedStartLine,
                UpdatedLineCount = UpdatedLineCount,
                Lines = Lines,
            };
        }
    }
}
