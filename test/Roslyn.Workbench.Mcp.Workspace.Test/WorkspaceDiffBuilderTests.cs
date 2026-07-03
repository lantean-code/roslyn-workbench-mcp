namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceDiffBuilderTests
{
    [Fact]
    public async Task GIVEN_DistantLineEdits_WHEN_CreatingDocumentDiff_THEN_ShouldReturnSeparateHunks()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectPath = Path.Combine(Path.GetTempPath(), "WorkspaceDiffBuilderTests", Guid.NewGuid().ToString("n"), "Sample.csproj");
        var documentPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "Class1.cs");
        var baselineText = """
            Line 1
            Line 2
            Line 3
            Line 4
            Line 5
            Line 6
            Line 7
            Line 8
            """;
        var currentText = """
            Line 1
            Line 2 updated
            Line 3
            Line 4
            Line 5
            Line 6
            Line 7 updated
            Line 8
            """;
        var baselineSolution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp, filePath: projectPath))
            .AddDocument(documentId, "Class1.cs", SourceText.From(baselineText), filePath: documentPath);
        var currentSolution = baselineSolution.WithDocumentText(documentId, SourceText.From(currentText));
        var resolver = new WorkspaceResolver(currentSolution, null, null);
        var currentDocument = currentSolution.GetDocument(documentId)!;
        var documentReference = resolver.CreateDocumentReference(currentDocument)!;

        var diff = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            documentReference,
            resolver,
            contextLines: 1,
            CancellationToken.None);

        diff.Should().NotBeNull();
        diff!.Hunks.Should().HaveCount(2);
        diff.Hunks[0].Lines.Should().ContainInOrder(" Line 1", "-Line 2", "+Line 2 updated", " Line 3");
        diff.Hunks[1].Lines.Should().ContainInOrder(" Line 6", "-Line 7", "+Line 7 updated", " Line 8");
    }

    [Fact]
    public async Task GIVEN_MiddleLineInsertion_WHEN_CreatingChangeSummary_THEN_ShouldCountAddedLinesWithoutChangedLines()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var projectPath = Path.Combine(Path.GetTempPath(), "WorkspaceDiffBuilderTests", Guid.NewGuid().ToString("n"), "Sample.csproj");
        var documentPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "Class1.cs");
        var baselineText = """
            Line 1
            Line 2
            Line 3
            """;
        var currentText = """
            Line 1
            Inserted Line
            Line 2
            Line 3
            """;
        var baselineSolution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp, filePath: projectPath))
            .AddDocument(documentId, "Class1.cs", SourceText.From(baselineText), filePath: documentPath);
        var currentSolution = baselineSolution.WithDocumentText(documentId, SourceText.From(currentText));
        var resolver = new WorkspaceResolver(currentSolution, null, null);

        var summary = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            baselineSolution,
            currentSolution,
            resolver,
            CancellationToken.None);

        summary.Modified.Should().ContainSingle();
        summary.Modified[0].Preview.Should().BeEquivalentTo(new DiffSummary
        {
            AddedLines = 1,
            RemovedLines = 0,
            ChangedLines = 0,
        });
    }
}
