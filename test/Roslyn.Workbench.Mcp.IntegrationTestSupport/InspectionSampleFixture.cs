namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal sealed class InspectionSampleFixture : IDisposable
{
    private readonly MaterializedWorkspaceAsset _asset;
    private readonly string _directoryPath;

    private InspectionSampleFixture(MaterializedWorkspaceAsset asset)
    {
        _asset = asset;
        _directoryPath = asset.WorkspaceRoot;
        ProjectPath = Path.Combine(asset.WorkspaceRoot, "Sample.csproj");
        DocumentPath = Path.Combine(asset.WorkspaceRoot, "Formatting.cs");
    }

    public string DocumentPath { get; }

    public string ProjectPath { get; }

    public string StateRoot
    {
        get
        {
            return _asset.StateRoot;
        }
    }

    public string WorkspaceRoot
    {
        get
        {
            return _asset.WorkspaceRoot;
        }
    }

    public static InspectionSampleFixture Create()
    {
        return Create(InspectionSampleProfile.Default);
    }

    public static InspectionSampleFixture Create(InspectionSampleProfile profile)
    {
        if (!Enum.IsDefined(profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "Inspection sample profile is not defined.");
        }

        var asset = WorkspaceAssetMaterializer.MaterializeProfiled("InspectionSample", profile.ToString());
        return new InspectionSampleFixture(asset);
    }

    public LocationSelector GetLocation(string text)
    {
        return GetLocationInDocument(Path.GetFileName(DocumentPath), text, 0);
    }

    public LocationSelector GetLocation(string text, int occurrenceIndex)
    {
        return GetLocationInDocument(Path.GetFileName(DocumentPath), text, occurrenceIndex);
    }

    public LocationSelector GetSelection(string selectedText)
    {
        return GetSelectionInDocument(Path.GetFileName(DocumentPath), selectedText, 0);
    }

    public LocationSelector GetSelection(string selectedText, int occurrenceIndex)
    {
        return GetSelectionInDocument(Path.GetFileName(DocumentPath), selectedText, occurrenceIndex);
    }

    public LocationSelector GetCursor(string text)
    {
        return GetCursor(text, 0, 0);
    }

    public LocationSelector GetCursor(string text, int occurrenceIndex)
    {
        return GetCursor(text, occurrenceIndex, 0);
    }

    public LocationSelector GetCursor(string text, int occurrenceIndex, int offset)
    {
        return GetCursorInDocument(Path.GetFileName(DocumentPath), text, occurrenceIndex, offset);
    }

    public LocationSelector GetCursorAfter(string text)
    {
        return GetCursorAfter(text, 0);
    }

    public LocationSelector GetCursorAfter(string text, int occurrenceIndex)
    {
        return GetCursor(text, occurrenceIndex, text.Length);
    }

    public LocationSelector GetLocationInDocument(string documentPath, string text)
    {
        return GetLocationInDocument(documentPath, text, 0);
    }

    public LocationSelector GetLocationInDocument(string documentPath, string text, int occurrenceIndex)
    {
        return CreateSelector(documentPath, text, occurrenceIndex, text.Length);
    }

    public LocationSelector GetSelectionInDocument(string documentPath, string text)
    {
        return GetSelectionInDocument(documentPath, text, 0);
    }

    public LocationSelector GetSelectionInDocument(string documentPath, string text, int occurrenceIndex)
    {
        return CreateSelector(documentPath, text, occurrenceIndex, text.Length);
    }

    public LocationSelector GetCursorInDocument(string documentPath, string text)
    {
        return GetCursorInDocument(documentPath, text, 0, 0);
    }

    public LocationSelector GetCursorInDocument(string documentPath, string text, int occurrenceIndex, int offset)
    {
        return CreateSelector(documentPath, text, occurrenceIndex, 0, offset);
    }

    public LocationSelector GetCursorOnFollowingLineInDocument(string documentPath, string text, int occurrenceIndex, int lineCount)
    {
        var fullPath = Path.Combine(_directoryPath, documentPath);
        var sourceText = File.ReadAllText(fullPath);
        var start = FindWholeToken(sourceText, text, occurrenceIndex);
        for (var index = 0; index < lineCount && start >= 0; index++)
        {
            var lineEnd = sourceText.IndexOf('\n', start);
            start = lineEnd < 0 ? -1 : lineEnd + 1;
        }

        return CreateSelector(documentPath, start, length: 0);
    }

    public LocationSelector GetSpanSelection(string startText, string endText)
    {
        return GetSpanSelectionInDocument(Path.GetFileName(DocumentPath), startText, endText);
    }

    public LocationSelector GetSpanSelectionInDocument(string documentPath, string startText, string endText)
    {
        var fullPath = Path.Combine(_directoryPath, documentPath);
        var sourceText = File.ReadAllText(fullPath);
        var start = sourceText.IndexOf(startText, StringComparison.Ordinal);
        if (start < 0)
        {
            return new LocationSelector();
        }

        var end = sourceText.IndexOf(endText, start, StringComparison.Ordinal);
        if (end < start)
        {
            return new LocationSelector();
        }

        return CreateSelector(documentPath, start, (end - start) + endText.Length);
    }

    private static int FindWholeToken(string sourceText, string text, int occurrenceIndex)
    {
        var index = 0;
        var currentOccurrence = 0;

        while ((index = sourceText.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
        {
            var hasLeadingBoundary = index == 0 || !IsIdentifierCharacter(sourceText[index - 1]);
            var trailingIndex = index + text.Length;
            var hasTrailingBoundary = trailingIndex >= sourceText.Length || !IsIdentifierCharacter(sourceText[trailingIndex]);
            if (hasLeadingBoundary && hasTrailingBoundary && currentOccurrence++ == occurrenceIndex)
            {
                return index;
            }

            index = trailingIndex;
        }

        return -1;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private LocationSelector CreateSelector(string documentPath, string text, int occurrenceIndex, int length, int offset = 0)
    {
        var fullPath = Path.Combine(_directoryPath, documentPath);
        var sourceText = File.ReadAllText(fullPath);
        var start = FindWholeToken(sourceText, text, occurrenceIndex) + offset;

        return CreateSelector(documentPath, start, length);
    }

    private static LocationSelector CreateSelector(string documentPath, int start, int length)
    {
        var document = new DocumentSelector
        {
            Path = documentPath,
        };

        var range = new TextSpanRange
        {
            Start = start,
            Length = length,
        };

        var span = new TextSpanSelector
        {
            Document = document,
            Range = range,
        };

        return new LocationSelector
        {
            Span = span,
        };
    }

    public void Dispose()
    {
        _asset.Dispose();
    }
}
