using Microsoft.CodeAnalysis.CSharp;

using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Selection;

public sealed class WorkspaceResolverTests
{
    [Fact]
    public void GIVEN_WorkspaceRelativeAndAbsolutePaths_WHEN_Normalizing_THEN_ShouldReturnWorkspaceRelativeSlashPaths()
    {
        using var workspace = new AdhocWorkspace();
        var root = Path.Combine(Path.GetTempPath(), "WorkspaceRoot");
        var target = CreateTarget(workspace.CurrentSolution, root);

        var relativeResult = target.NormalizeDocumentPath(Path.Combine("Folder", "Document.cs"));
        var absoluteResult = target.NormalizeProjectPath(Path.Combine(root, "Project", "Project.csproj"));

        relativeResult.Should().Be("Folder/Document.cs");
        absoluteResult.Should().Be("Project/Project.csproj");
    }

    [Fact]
    public void GIVEN_EmptyPathAndNoWorkspaceIdentity_WHEN_Normalizing_THEN_ShouldReturnEmptyAndAbsolutePaths()
    {
        using var workspace = new AdhocWorkspace();
        var target = new WorkspaceResolver(workspace.CurrentSolution, workspaceIdentity: null, transactionRevision: null);
        var relativePath = Path.Combine("Folder", "Document.cs");

        var emptyResult = target.NormalizeDocumentPath("   ");
        var pathResult = target.NormalizeDocumentPath(relativePath);

        emptyResult.Should().BeEmpty();
        pathResult.Should().Be(Path.GetFullPath(relativePath).Replace(Path.DirectorySeparatorChar, '/'));
    }

    [Fact]
    public void GIVEN_Document_WHEN_CreatingReference_THEN_ShouldIncludeIdsAndNormalizedPath()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.CreateDocumentReference(document);

        result!.DocumentId.Should().Be(document.Id.Id.ToString());
        result.ProjectId.Should().Be(document.Project.Id.Id.ToString());
        result.Path.Should().Be("Project/Document.cs");
    }

    [Fact]
    public void GIVEN_NullDocument_WHEN_CreatingReference_THEN_ShouldThrowArgumentNullException()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var action = () => target.CreateDocumentReference(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_SourceLocation_WHEN_CreatingResolvedLocation_THEN_ShouldIncludeDocumentSpanAndSnapshot()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var tree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var location = tree!.GetLocation(new TextSpan(6, 1));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot(), transactionRevision: 3);

        var result = target.CreateResolvedLocation(location);

        result!.WorkspaceId.Should().Be("WorkspaceId");
        result.Document!.DocumentId.Should().Be(document.Id.Id.ToString());
        result.Span!.Start.Should().Be(6);
        result.Span.Length.Should().Be(1);
        result.Line.Should().Be(1);
        result.Column.Should().Be(7);
        result.WorkspaceEpoch.Should().Be(2);
        result.TransactionRevision.Should().Be(3);
    }

    [Fact]
    public void GIVEN_NonSourceLocationOrMissingIdentity_WHEN_CreatingResolvedLocation_THEN_ShouldReturnNull()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var targetWithoutIdentity = new WorkspaceResolver(workspace.CurrentSolution, workspaceIdentity: null, transactionRevision: null);

        var nonSourceResult = target.CreateResolvedLocation(Location.None);
        var missingIdentityResult = targetWithoutIdentity.CreateResolvedLocation(Location.None);

        nonSourceResult.Should().BeNull();
        missingIdentityResult.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "Matched")]
    [InlineData("WorkspaceId", "Matched")]
    [InlineData("DifferentWorkspaceId", "WorkspaceEpochMismatch")]
    public void GIVEN_SnapshotWorkspaceId_WHEN_Validating_THEN_ShouldReturnExpectedMatchKind(
        string? workspaceId,
        string expectedKindName)
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot(), transactionRevision: 3);
        var precondition = workspaceId is null
            ? null
            : new SnapshotPrecondition
            {
                WorkspaceId = workspaceId,
                WorkspaceEpoch = 2,
                TransactionRevision = 3,
            };

        var result = target.ValidateSnapshot(precondition);

        result.Kind.ToString().Should().Be(expectedKindName);
    }

    [Fact]
    public void GIVEN_MissingWorkspaceIdentity_WHEN_ValidatingSnapshot_THEN_ShouldReturnWorkspaceEpochMismatch()
    {
        using var workspace = new AdhocWorkspace();
        var target = new WorkspaceResolver(workspace.CurrentSolution, workspaceIdentity: null, transactionRevision: null);

        var result = target.ValidateSnapshot(new SnapshotPrecondition());

        result.Kind.Should().Be(SnapshotMatchKind.WorkspaceEpochMismatch);
    }

    [Fact]
    public void GIVEN_DifferentEpoch_WHEN_ValidatingSnapshot_THEN_ShouldReturnWorkspaceEpochMismatch()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot(), transactionRevision: 3);

        var result = target.ValidateSnapshot(new SnapshotPrecondition
        {
            WorkspaceEpoch = 4,
            TransactionRevision = 3,
        });

        result.Kind.Should().Be(SnapshotMatchKind.WorkspaceEpochMismatch);
    }

    [Fact]
    public void GIVEN_DifferentRevision_WHEN_ValidatingSnapshot_THEN_ShouldReturnTransactionRevisionMismatch()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot(), transactionRevision: 3);

        var result = target.ValidateSnapshot(new SnapshotPrecondition
        {
            WorkspaceEpoch = 2,
            TransactionRevision = 4,
        });

        result.Kind.Should().Be(SnapshotMatchKind.TransactionRevisionMismatch);
    }

    [Fact]
    public void GIVEN_DocumentIdOrPath_WHEN_ResolvingDocument_THEN_ShouldReturnMatchingDocument()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var byId = target.ResolveDocument(new DocumentSelector { DocumentId = document.Id.Id.ToString() });
        var byPath = target.ResolveDocument(new DocumentSelector { Path = "Project/Document.cs" });

        byId.Status.Should().Be(SelectorResolveStatus.Resolved);
        byId.Value.Should().BeSameAs(document);
        byPath.Status.Should().Be(SelectorResolveStatus.Resolved);
        byPath.Value.Should().BeSameAs(document);
    }

    [Fact]
    public void GIVEN_InvalidDocumentIdAndNoPath_WHEN_ResolvingDocument_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveDocument(new DocumentSelector { DocumentId = "InvalidDocumentId" });

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public void GIVEN_UnknownDocumentPath_WHEN_ResolvingDocument_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveDocument(new DocumentSelector { Path = "Project/UnknownDocument.cs" });

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public void GIVEN_UnknownValidDocumentIdAndMatchingPath_WHEN_ResolvingDocument_THEN_ShouldFallBackToPath()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveDocument(new DocumentSelector
        {
            DocumentId = Guid.NewGuid().ToString(),
            Path = "Project/Document.cs",
        });

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value.Should().BeSameAs(document);
    }

    [Fact]
    public void GIVEN_SerializedDocumentIdSharedAcrossProjects_WHEN_ResolvingDocument_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = new AdhocWorkspace();
        var documentGuid = Guid.NewGuid();
        var firstProject = AddProject(workspace, "FirstProject");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(workspace, firstProject, "Document.cs", "class C { }", Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"), documentGuid);
        AddDocument(workspace, secondProject, "Document.cs", "class D { }", Path.Combine(GetWorkspaceRoot(), "SecondProject", "Document.cs"), documentGuid);
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveDocument(new DocumentSelector { DocumentId = documentGuid.ToString() });

        result.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    [Fact]
    public void GIVEN_NullDocumentSelector_WHEN_ResolvingDocument_THEN_ShouldThrowArgumentNullException()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var action = () => target.ResolveDocument(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_DuplicateDocumentPath_WHEN_ResolvingDocument_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var project = AddProject(workspace, "SecondProject");
        AddDocument(workspace, project, "Document.cs", "class D { }", Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveDocument(new DocumentSelector { Path = "FirstProject/Document.cs" });

        result.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    [Fact]
    public void GIVEN_ProjectIdNameOrPath_WHEN_ResolvingProject_THEN_ShouldReturnMatchingProject()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var project = workspace.CurrentSolution.Projects.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var byId = target.ResolveProject(new ProjectSelector { ProjectId = project.Id.Id.ToString().ToUpperInvariant() });
        var byName = target.ResolveProject(new ProjectSelector { Name = "Project" });
        var byPath = target.ResolveProject(new ProjectSelector { Path = "Project/Project.csproj" });

        byId.Value.Should().BeSameAs(project);
        byName.Value.Should().BeSameAs(project);
        byPath.Value.Should().BeSameAs(project);
    }

    [Fact]
    public void GIVEN_EmptyProjectSelectorWithMultipleProjects_WHEN_ResolvingProject_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        AddProject(workspace, "SecondProject");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveProject(new ProjectSelector());

        result.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    [Fact]
    public async Task GIVEN_ValidAndInvalidSpans_WHEN_ResolvingLocations_THEN_ShouldReturnResolvedAndNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var documentSelector = new DocumentSelector { DocumentId = document.Id.Id.ToString() };

        var resolved = await target.ResolveLocationAsync(new LocationSelector
        {
            Span = new TextSpanSelector { Document = documentSelector, Start = 6, Length = 1 },
        }, TestContext.Current.CancellationToken);
        var notFound = await target.ResolveLocationAsync(new LocationSelector
        {
            Span = new TextSpanSelector { Document = documentSelector, Start = -1, Length = 1 },
        }, TestContext.Current.CancellationToken);

        resolved.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolved.Value!.SourceSpan.Should().Be(new TextSpan(6, 1));
        notFound.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_UniqueAndRepeatedSelectedText_WHEN_ResolvingLocations_THEN_ShouldReturnResolvedAndAmbiguous()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "before value after value");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var documentSelector = new DocumentSelector { DocumentId = document.Id.Id.ToString() };

        var unique = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector
            {
                Document = documentSelector,
                SelectedText = "value",
                ContextBefore = "before ",
                ContextAfter = " after",
            },
        }, TestContext.Current.CancellationToken);
        var repeated = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector { Document = documentSelector, SelectedText = "value" },
        }, TestContext.Current.CancellationToken);

        unique.Status.Should().Be(SelectorResolveStatus.Resolved);
        repeated.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    [Fact]
    public async Task GIVEN_SourceSymbol_WHEN_CreatingSymbolReference_THEN_ShouldIncludeDisplayKindDocumentationIdAndLocation()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var compilation = await workspace.CurrentSolution.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);
        var symbol = compilation!.GetTypeByMetadataName("C");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.CreateSymbolReference(symbol!);

        result.DisplayName.Should().Be("C");
        result.Kind.Should().Be("NamedType");
        result.DocumentationCommentId.Should().Be("T:C");
        result.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task GIVEN_MetadataSymbol_WHEN_CreatingSymbolReference_THEN_ShouldOmitLocation()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var compilation = await workspace.CurrentSolution.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);
        var symbol = compilation!.GetSpecialType(SpecialType.System_Object);
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.CreateSymbolReference(symbol);

        result.Location.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NullSymbol_WHEN_CreatingSymbolReference_THEN_ShouldThrowArgumentNullException()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var action = () => target.CreateSymbolReference(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_SourceTreeOutsideSolution_WHEN_CreatingResolvedLocation_THEN_ShouldOmitDocumentReference()
    {
        using var workspace = new AdhocWorkspace();
        var tree = CSharpSyntaxTree.ParseText("class C { }", cancellationToken: TestContext.Current.CancellationToken);
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.CreateResolvedLocation(tree.GetLocation(new TextSpan(6, 1)));

        result!.Document.Should().BeNull();
        result.Span!.Start.Should().Be(6);
    }

    [Fact]
    public void GIVEN_UnknownProjectSelector_WHEN_ResolvingProject_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveProject(new ProjectSelector { Name = "UnknownProject" });

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public void GIVEN_NullProjectSelector_WHEN_ResolvingProject_THEN_ShouldThrowArgumentNullException()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var action = () => target.ResolveProject(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_LocationWithoutSpanOrSelection_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(new LocationSelector(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_NullLocationSelector_WHEN_ResolvingLocation_THEN_ShouldThrowArgumentNullException()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var action = async () => await target.ResolveLocationAsync(null!, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_SpanWithoutDocument_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(
            new LocationSelector { Span = new TextSpanSelector { Start = 0, Length = 1 } },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(100, 1)]
    public async Task GIVEN_InvalidSpanBounds_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound(int start, int length)
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                Start = start,
                Length = length,
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_SelectionWithoutDocumentOrText_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(
            new LocationSelector { Selection = new TextSelectionSelector() },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_SelectionWithDocumentButEmptyText_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector
            {
                Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                SelectedText = string.Empty,
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_SelectionContextMismatch_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "before value after");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector
            {
                Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                SelectedText = "value",
                ContextBefore = "wrong ",
                ContextAfter = " wrong",
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_SelectionAfterContextMismatch_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "before value after");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector
            {
                Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                SelectedText = "value",
                ContextBefore = "before ",
                ContextAfter = " wrong",
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_AmbiguousDocument_WHEN_ResolvingSpanOrSelection_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "value");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(workspace, secondProject, "Document.cs", "value", Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var documentSelector = new DocumentSelector { Path = "FirstProject/Document.cs" };

        var spanResult = await target.ResolveLocationAsync(new LocationSelector
        {
            Span = new TextSpanSelector { Document = documentSelector, Start = 0, Length = 1 },
        }, TestContext.Current.CancellationToken);
        var selectionResult = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector { Document = documentSelector, SelectedText = "value" },
        }, TestContext.Current.CancellationToken);

        spanResult.Status.Should().Be(SelectorResolveStatus.Ambiguous);
        selectionResult.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    [Fact]
    public async Task GIVEN_DocumentationCommentId_WHEN_ResolvingSymbol_THEN_ShouldReturnSourceSymbol()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(
            new SymbolSelector { DocumentationCommentId = "T:C" },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.Name.Should().Be("C");
    }

    [Fact]
    public async Task GIVEN_UnknownDocumentationCommentId_WHEN_ResolvingSymbol_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(
            new SymbolSelector { DocumentationCommentId = "T:Missing" },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ResolvingDocumentationCommentId_THEN_ShouldPropagateCancellation()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await target.ResolveSymbolAsync(
            new SymbolSelector { DocumentationCommentId = "T:C" },
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_LocationSelector_WHEN_ResolvingSymbol_THEN_ShouldReturnSymbolAtPosition()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                    Start = 6,
                    Length = 1,
                },
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.Name.Should().Be("C");
    }

    [Fact]
    public async Task GIVEN_SymbolSelectorWithoutIdOrLocation_WHEN_ResolvingSymbol_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_NullSymbolSelector_WHEN_ResolvingSymbol_THEN_ShouldThrowArgumentNullException()
    {
        using var workspace = new AdhocWorkspace();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var action = async () => await target.ResolveSymbolAsync(null!, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_AmbiguousDocumentLocation_WHEN_ResolvingSymbol_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(workspace, secondProject, "Document.cs", "class D { }", Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector { Path = "FirstProject/Document.cs" },
                    Start = 6,
                    Length = 1,
                },
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    [Fact]
    public async Task GIVEN_PositionWithoutSymbol_WHEN_ResolvingSymbol_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "// comment");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                    Start = 3,
                    Length = 0,
                },
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_SameDocumentationIdInMultipleProjects_WHEN_ResolvingSymbol_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(workspace, secondProject, "Document.cs", "class C { }", Path.Combine(GetWorkspaceRoot(), "SecondProject", "Document.cs"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(
            new SymbolSelector { DocumentationCommentId = "T:C" },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    private static WorkspaceResolver CreateTarget(Solution solution, string workspaceRoot, int? transactionRevision = null)
    {
        return new WorkspaceResolver(
            solution,
            new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 2,
                LoadedPath = Path.Combine(workspaceRoot, "Workspace.sln"),
            },
            transactionRevision);
    }

    private static AdhocWorkspace CreateWorkspace(string projectName, string documentName, string text)
    {
        var workspace = new AdhocWorkspace();
        var project = AddProject(workspace, projectName);
        AddDocument(workspace, project, documentName, text, Path.Combine(GetWorkspaceRoot(), projectName, documentName));
        return workspace;
    }

    private static Document AddDocument(
        AdhocWorkspace workspace,
        Project project,
        string documentName,
        string text,
        string filePath,
        Guid? documentGuid = null)
    {
        return workspace.AddDocument(DocumentInfo.Create(
            documentGuid is null
                ? DocumentId.CreateNewId(project.Id)
                : DocumentId.CreateFromSerialized(project.Id, documentGuid.Value),
            documentName,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Default)),
            filePath: filePath));
    }

    private static Project AddProject(AdhocWorkspace workspace, string projectName)
    {
        return workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            projectName,
            projectName,
            LanguageNames.CSharp,
            filePath: Path.Combine(GetWorkspaceRoot(), projectName, $"{projectName}.csproj"),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]));
    }

    private static string GetWorkspaceRoot()
    {
        return Path.Combine(Path.GetTempPath(), "WorkspaceRoot");
    }
}
