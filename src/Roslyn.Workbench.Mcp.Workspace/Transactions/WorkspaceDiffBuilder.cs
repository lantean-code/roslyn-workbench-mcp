using System.Text.RegularExpressions;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal static class WorkspaceDiffBuilder
{
    private static readonly Differ _differ = new();

    private static readonly Regex _hunkHeaderPattern = new(
        "^@@ -(?<originalStart>\\d+),(?<originalCount>\\d+) \\+(?<updatedStart>\\d+),(?<updatedCount>\\d+) @@",
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
                var document = GetRequiredDocument(currentSolution, documentId);

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
                var oldDocument = GetRequiredDocument(baselineSolution, documentId);
                var newDocument = GetRequiredDocument(currentSolution, documentId);

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
                var document = GetRequiredDocument(baselineSolution, documentId);

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

        var document = currentDocument ?? baselineDocument;
        if (document is null)
        {
            return null;
        }

        var oldText = await GetDocumentTextAsync(baselineDocument, cancellationToken);
        var newText = await GetDocumentTextAsync(currentDocument, cancellationToken);

        return new DocumentDiff
        {
            Document = resolver.CreateDocumentReference(document),
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

    private static Document GetRequiredDocument(Solution solution, DocumentId documentId)
    {
        return solution.GetDocument(documentId)
            ?? throw new InvalidOperationException($"The document '{documentId}' was not present in the expected solution.");
    }

    private static List<DiffHunk> CreateHunks(string oldText, string newText, int contextLines)
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
                    ParseHunkNumber(match, "originalCount"),
                    ParseHunkNumber(match, "updatedStart"),
                    ParseHunkNumber(match, "updatedCount"));

                continue;
            }

            if (currentHunk is null || line.StartsWith("--- ", StringComparison.Ordinal) || line.StartsWith("+++ ", StringComparison.Ordinal))
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
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
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
