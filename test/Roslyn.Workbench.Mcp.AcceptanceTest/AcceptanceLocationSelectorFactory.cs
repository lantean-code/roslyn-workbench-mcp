namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed class AcceptanceLocationSelectorFactory
{
    private readonly Dictionary<string, string> _sourceByDocumentPath = new(StringComparer.Ordinal);
    private readonly string _workspaceRoot;

    public AcceptanceLocationSelectorFactory(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
    }

    public Dictionary<string, object?> CreateCursor(
        string documentPath,
        string text,
        int occurrenceIndex = 0,
        int offset = 0)
    {
        var start = FindText(documentPath, text, occurrenceIndex);
        return CreateLocation(documentPath, start + offset, length: 0);
    }

    public Dictionary<string, object?> CreateCursorAfter(
        string documentPath,
        string text,
        int occurrenceIndex = 0)
    {
        return CreateCursor(documentPath, text, occurrenceIndex, text.Length);
    }

    public Dictionary<string, object?> CreateCursorInsideTypeBody(
        string documentPath,
        string typeHeader)
    {
        var sourceText = GetSourceText(documentPath);
        var start = FindText(documentPath, typeHeader, occurrenceIndex: 0);
        var openingBrace = sourceText.IndexOf('{', start);
        openingBrace.Should().BeGreaterThanOrEqualTo(start);

        var openingBraceLineEnd = sourceText.IndexOf('\n', openingBrace);
        openingBraceLineEnd.Should().BeGreaterThanOrEqualTo(openingBrace);

        return CreateLocation(documentPath, openingBraceLineEnd + 1, length: 0);
    }

    public Dictionary<string, object?> CreateLocation(
        string documentPath,
        string text,
        int occurrenceIndex = 0)
    {
        var start = FindText(documentPath, text, occurrenceIndex);
        return CreateLocation(documentPath, start, text.Length);
    }

    public Dictionary<string, object?> CreateSelection(
        string documentPath,
        string startText,
        string endText)
    {
        var sourceText = GetSourceText(documentPath);
        var start = FindText(documentPath, startText, occurrenceIndex: 0);
        var end = sourceText.IndexOf(endText, start, StringComparison.Ordinal);
        end.Should().BeGreaterThanOrEqualTo(start);

        return CreateLocation(documentPath, start, (end - start) + endText.Length);
    }

    public static Dictionary<string, object?> CreateDocument(string documentPath)
    {
        return new Dictionary<string, object?>
        {
            ["path"] = documentPath,
        };
    }

    public static Dictionary<string, object?> CreateSymbol(Dictionary<string, object?> location)
    {
        return new Dictionary<string, object?>
        {
            ["location"] = location,
        };
    }

    public static Dictionary<string, object?> CreateSymbol(string documentationCommentId)
    {
        return new Dictionary<string, object?>
        {
            ["documentationCommentId"] = documentationCommentId,
        };
    }

    private int FindText(string documentPath, string text, int occurrenceIndex)
    {
        var sourceText = GetSourceText(documentPath);
        var index = 0;
        var currentOccurrence = 0;

        while ((index = sourceText.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
        {
            var hasLeadingBoundary = index == 0
                || !IsIdentifierCharacter(sourceText[index - 1]);

            var trailingIndex = index + text.Length;
            var hasTrailingBoundary = trailingIndex >= sourceText.Length
                || !IsIdentifierCharacter(sourceText[trailingIndex]);

            if (hasLeadingBoundary
                && hasTrailingBoundary
                && currentOccurrence++ == occurrenceIndex)
            {
                return index;
            }

            index = trailingIndex;
        }

        index.Should().BeGreaterThanOrEqualTo(0, $"{text} should exist in {documentPath}");
        return index;
    }

    private string GetSourceText(string documentPath)
    {
        if (_sourceByDocumentPath.TryGetValue(documentPath, out var sourceText))
        {
            return sourceText;
        }

        var fullPath = Path.Combine(_workspaceRoot, documentPath);
        sourceText = File.ReadAllText(fullPath);
        _sourceByDocumentPath.Add(documentPath, sourceText);
        return sourceText;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private static Dictionary<string, object?> CreateLocation(
        string documentPath,
        int start,
        int length)
    {
        return new Dictionary<string, object?>
        {
            ["span"] = new Dictionary<string, object?>
            {
                ["document"] = CreateDocument(documentPath),
                ["start"] = start,
                ["length"] = length,
            },
        };
    }
}
