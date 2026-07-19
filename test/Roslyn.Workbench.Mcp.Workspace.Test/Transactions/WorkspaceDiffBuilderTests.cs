namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceDiffBuilderTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceResolver> _resolver;

    public WorkspaceDiffBuilderTests()
    {
        _workspace = new AdhocWorkspace();
        _resolver = new Mock<IWorkspaceResolver>();
    }

    [Fact]
    public async Task GIVEN_UnchangedSolution_WHEN_CreatingChangeSummary_THEN_ShouldReturnEmptyCollections()
    {
        var solution = CreateSolution(("Document.cs", "class C { }"));

        var result = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            solution,
            solution,
            _resolver.Object,
            TestContext.Current.CancellationToken);

        result.Added.Should().BeEmpty();
        result.Modified.Should().BeEmpty();
        result.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AddedModifiedAndDeletedDocuments_WHEN_CreatingChangeSummary_THEN_ShouldClassifyAndSummariseChanges()
    {
        var baselineSolution = CreateSolution(
            ("Modified.cs", "first\nsecond"),
            ("Deleted.cs", "deleted one\ndeleted two"));
        var project = baselineSolution.Projects.Single();
        var secondProjectId = ProjectId.CreateNewId();
        baselineSolution = baselineSolution.AddProject(ProjectInfo.Create(
            secondProjectId,
            VersionStamp.Default,
            "SecondProject",
            "SecondProject",
            LanguageNames.CSharp,
            filePath: "/workspace/SecondProject/SecondProject.csproj"));
        var modifiedDocument = project.Documents.Single(document => document.Name == "Modified.cs");
        var deletedDocument = project.Documents.Single(document => document.Name == "Deleted.cs");
        var addedDocumentId = DocumentId.CreateNewId(secondProjectId);
        var currentSolution = baselineSolution
            .WithDocumentText(modifiedDocument.Id, SourceText.From("changed\nsecond"))
            .RemoveDocument(deletedDocument.Id)
            .AddDocument(addedDocumentId, "Added.cs", SourceText.From("added one\nadded two"));
        _resolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns((Document document) => CreateDocumentReference(document));

        var result = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            baselineSolution,
            currentSolution,
            _resolver.Object,
            TestContext.Current.CancellationToken);

        result.Added.Should().ContainSingle().Which.Should().BeEquivalentTo(new DocumentChange
        {
            Document = CreateDocumentReference(currentSolution.GetDocument(addedDocumentId)!),
            ChangeKind = DocumentChangeKind.Added,
            Preview = new DiffSummary { AddedLines = 2 },
        });
        result.Modified.Should().ContainSingle().Which.Should().BeEquivalentTo(new DocumentChange
        {
            Document = CreateDocumentReference(currentSolution.GetDocument(modifiedDocument.Id)!),
            ChangeKind = DocumentChangeKind.Modified,
            Preview = new DiffSummary { ChangedLines = 1 },
        });
        result.Deleted.Should().ContainSingle().Which.Should().BeEquivalentTo(new DocumentChange
        {
            Document = CreateDocumentReference(deletedDocument),
            ChangeKind = DocumentChangeKind.Deleted,
            Preview = new DiffSummary { RemovedLines = 2 },
        });
    }

    [Fact]
    public async Task GIVEN_CancelledTokenAndChangedSolution_WHEN_CreatingChangeSummary_THEN_ShouldPropagateCancellation()
    {
        var baselineSolution = CreateSolution(("Document.cs", "class C { }"));
        var project = baselineSolution.Projects.Single();
        var currentSolution = baselineSolution.AddDocument(
            DocumentId.CreateNewId(project.Id),
            "Added.cs",
            SourceText.From("class Added { }"));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            baselineSolution,
            currentSolution,
            _resolver.Object,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_DocumentMissingFromBothSolutions_WHEN_CreatingDocumentDiff_THEN_ShouldReturnNull()
    {
        var solution = CreateSolution(("Document.cs", "class C { }"));
        var documentReference = new DocumentReference { DocumentId = Guid.NewGuid().ToString() };
        _resolver
            .Setup(item => item.ResolveDocument(It.Is<DocumentSelector>(selector => selector.DocumentId == documentReference.DocumentId)))
            .Returns(SelectorResolveResult<Document>.NotFound());

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            solution,
            solution,
            documentReference,
            _resolver.Object,
            3,
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_UnchangedDocument_WHEN_CreatingDocumentDiff_THEN_ShouldReturnEmptyHunks()
    {
        var solution = CreateSolution(("Document.cs", "class C { }"));
        var document = solution.Projects.Single().Documents.Single();
        var documentReference = CreateDocumentReference(document);
        _resolver.Setup(item => item.ResolveDocument(It.IsAny<DocumentSelector>()))
            .Returns(SelectorResolveResult<Document>.Resolved(document));
        _resolver.Setup(item => item.CreateDocumentReference(document)).Returns(documentReference);

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            solution,
            solution,
            documentReference,
            _resolver.Object,
            3,
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Document.Should().BeSameAs(documentReference);
        result.Hunks.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AddedDocument_WHEN_CreatingDocumentDiff_THEN_ShouldUseCurrentReferenceAndAddedHunk()
    {
        var baselineSolution = CreateSolution(("Existing.cs", "class Existing { }"));
        var project = baselineSolution.Projects.Single();
        var documentId = DocumentId.CreateNewId(project.Id);
        var currentSolution = baselineSolution.AddDocument(
            documentId,
            "Added.cs",
            SourceText.From("first\nsecond"));
        var currentDocument = currentSolution.GetDocument(documentId)!;
        var expectedReference = CreateDocumentReference(currentDocument);
        _resolver
            .Setup(item => item.ResolveDocument(It.Is<DocumentSelector>(selector => selector.DocumentId == expectedReference.DocumentId)))
            .Returns(SelectorResolveResult<Document>.Resolved(currentDocument));
        _resolver.Setup(item => item.CreateDocumentReference(currentDocument)).Returns(expectedReference);

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            expectedReference,
            _resolver.Object,
            0,
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Document.Should().BeSameAs(expectedReference);
        result.Truncated.Should().BeFalse();
        var hunk = result.Hunks.Should().ContainSingle().Which;
        hunk.OriginalStartLine.Should().Be(1);
        hunk.OriginalLineCount.Should().Be(0);
        hunk.UpdatedStartLine.Should().Be(1);
        hunk.UpdatedLineCount.Should().Be(2);
        hunk.Lines.Should().Equal("+first", "+second");
    }

    [Fact]
    public async Task GIVEN_DeletedDocument_WHEN_CreatingDocumentDiff_THEN_ShouldUseBaselineReferenceAndDeletedHunk()
    {
        var baselineSolution = CreateSolution(("Deleted.cs", "first\nsecond"));
        var baselineDocument = baselineSolution.Projects.Single().Documents.Single();
        var documentReference = CreateDocumentReference(baselineDocument);
        var expectedReference = documentReference with { Path = "BaselinePath" };
        var currentSolution = baselineSolution.RemoveDocument(baselineDocument.Id);
        _resolver
            .Setup(item => item.ResolveDocument(It.Is<DocumentSelector>(selector => selector.DocumentId == documentReference.DocumentId)))
            .Returns(SelectorResolveResult<Document>.NotFound());
        _resolver.Setup(item => item.CreateDocumentReference(baselineDocument)).Returns(expectedReference);

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            documentReference,
            _resolver.Object,
            0,
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Document.Should().BeSameAs(expectedReference);
        var hunk = result.Hunks.Should().ContainSingle().Which;
        hunk.OriginalStartLine.Should().Be(1);
        hunk.OriginalLineCount.Should().Be(2);
        hunk.UpdatedStartLine.Should().Be(1);
        hunk.UpdatedLineCount.Should().Be(0);
        hunk.Lines.Should().Equal("-first", "-second");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task GIVEN_NonPositiveContextLines_WHEN_CreatingModifiedDocumentDiff_THEN_ShouldClampContextToZero(
        int contextLines)
    {
        var baselineSolution = CreateSolution(("Document.cs", "unchanged\nold\nunchanged again\n"));
        var baselineDocument = baselineSolution.Projects.Single().Documents.Single();
        var currentSolution = baselineSolution.WithDocumentText(
            baselineDocument.Id,
            SourceText.From("unchanged\nnew\nunchanged again\n"));
        var currentDocument = currentSolution.GetDocument(baselineDocument.Id)!;
        var documentReference = CreateDocumentReference(currentDocument);
        _resolver.Setup(item => item.ResolveDocument(It.IsAny<DocumentSelector>()))
            .Returns(SelectorResolveResult<Document>.Resolved(currentDocument));
        _resolver.Setup(item => item.CreateDocumentReference(currentDocument)).Returns(documentReference);

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            documentReference,
            _resolver.Object,
            contextLines,
            TestContext.Current.CancellationToken);

        result!.Hunks.Should().ContainSingle().Which.Lines.Should().Equal("-old", "+new");
    }

    [Fact]
    public async Task GIVEN_SingleLineReplacement_WHEN_CreatingDocumentDiff_THEN_ShouldParseExplicitHunkCounts()
    {
        var baselineSolution = CreateSolution(("Document.cs", "old"));
        var document = baselineSolution.Projects.Single().Documents.Single();
        var currentSolution = baselineSolution.WithDocumentText(document.Id, SourceText.From("new"));
        var currentDocument = currentSolution.GetDocument(document.Id)!;
        var documentReference = CreateDocumentReference(currentDocument);
        _resolver.Setup(item => item.ResolveDocument(It.IsAny<DocumentSelector>()))
            .Returns(SelectorResolveResult<Document>.Resolved(currentDocument));
        _resolver.Setup(item => item.CreateDocumentReference(currentDocument)).Returns(documentReference);

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            documentReference,
            _resolver.Object,
            0,
            TestContext.Current.CancellationToken);

        var hunk = result!.Hunks.Should().ContainSingle().Which;
        hunk.OriginalStartLine.Should().Be(1);
        hunk.OriginalLineCount.Should().Be(1);
        hunk.UpdatedStartLine.Should().Be(1);
        hunk.UpdatedLineCount.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_AdjacentAndDistantEdits_WHEN_CreatingDocumentDiff_THEN_ShouldMergeAdjacentAndSeparateDistantHunks()
    {
        var baselineText = string.Join('\n', Enumerable.Range(1, 10).Select(number => $"line {number}")) + "\n";
        var baselineSolution = CreateSolution(("Document.cs", baselineText));
        var document = baselineSolution.Projects.Single().Documents.Single();
        var updatedLines = Enumerable.Range(1, 10).Select(number => $"line {number}").ToArray();
        updatedLines[1] = "changed 2";
        updatedLines[2] = "changed 3";
        updatedLines[8] = "changed 9";
        var currentSolution = baselineSolution.WithDocumentText(document.Id, SourceText.From(string.Join('\n', updatedLines) + "\n"));
        var currentDocument = currentSolution.GetDocument(document.Id)!;
        var documentReference = CreateDocumentReference(currentDocument);
        _resolver.Setup(item => item.ResolveDocument(It.IsAny<DocumentSelector>()))
            .Returns(SelectorResolveResult<Document>.Resolved(currentDocument));
        _resolver.Setup(item => item.CreateDocumentReference(currentDocument)).Returns(documentReference);

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            documentReference,
            _resolver.Object,
            0,
            TestContext.Current.CancellationToken);

        result!.Hunks.Should().HaveCount(2);
        result.Hunks[0].Lines.Should().Equal("-line 2", "-line 3", "+changed 2", "+changed 3");
        result.Hunks[1].Lines.Should().Equal("-line 9", "+changed 9");
    }

    [Fact]
    public async Task GIVEN_MixedLineEndingsAndMissingFinalNewline_WHEN_CreatingDocumentDiff_THEN_ShouldNormaliseLinesAndOmitMarker()
    {
        var baselineSolution = CreateSolution(("Document.cs", "first\r\nold"));
        var document = baselineSolution.Projects.Single().Documents.Single();
        var currentSolution = baselineSolution.WithDocumentText(document.Id, SourceText.From("first\nnew"));
        var currentDocument = currentSolution.GetDocument(document.Id)!;
        var documentReference = CreateDocumentReference(currentDocument);
        _resolver.Setup(item => item.ResolveDocument(It.IsAny<DocumentSelector>()))
            .Returns(SelectorResolveResult<Document>.Resolved(currentDocument));
        _resolver.Setup(item => item.CreateDocumentReference(currentDocument)).Returns(documentReference);

        var result = await WorkspaceDiffBuilder.CreateDocumentDiffAsync(
            baselineSolution,
            currentSolution,
            documentReference,
            _resolver.Object,
            1,
            TestContext.Current.CancellationToken);

        result!.Hunks.Should().ContainSingle();
        result.Hunks.Single().Lines.Should().NotContain(@"\ No newline at end of file");
        result.Hunks.Single().Lines.Should().Contain(" first");
        result.Hunks.Single().Lines.Should().Contain("-old");
        result.Hunks.Single().Lines.Should().Contain("+new");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private Solution CreateSolution(params (string Name, string Text)[] documents)
    {
        var projectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: "/workspace/Project/Project.csproj"));

        foreach (var document in documents)
        {
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                document.Name,
                SourceText.From(document.Text),
                filePath: $"/workspace/Project/{document.Name}");
        }

        return solution;
    }

    private static DocumentReference CreateDocumentReference(Document document)
    {
        return new DocumentReference
        {
            DocumentId = document.Id.Id.ToString(),
            Path = document.FilePath!,
            ProjectId = document.Project.Id.Id.ToString(),
        };
    }
}
