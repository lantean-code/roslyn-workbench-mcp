using Microsoft.CodeAnalysis.CSharp;

using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

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
        var target = new WorkspaceResolver(
            workspace.CurrentSolution,
            workspaceIdentity: null,
            transactionRevision: null,
            CreatePathComparison().Object);
        var relativePath = Path.Combine("Folder", "Document.cs");

        var emptyResult = target.NormalizeDocumentPath("   ");
        var pathResult = target.NormalizeDocumentPath(relativePath);

        emptyResult.Should().BeEmpty();
        pathResult.Should().Be(Path.GetFullPath(relativePath).Replace(Path.DirectorySeparatorChar, '/'));
    }

    [Fact]
    public void GIVEN_ExplicitWorkspaceRootAboveLoadedPath_WHEN_NormalizingAndResolvingDocument_THEN_ShouldUseWorkspaceRoot()
    {
        using var workspace = new AdhocWorkspace();
        var workspaceRoot = GetWorkspaceRoot();
        var project = AddProject(workspace, "Project");
        var documentPath = Path.Combine(workspaceRoot, "src", "Project", "Document.cs");
        var document = AddDocument(workspace, project, "Document.cs", "class C { }", documentPath);
        var pathComparison = CreatePathComparison();
        var target = new WorkspaceResolver(
            workspace.CurrentSolution,
            new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 2,
                LoadedPath = Path.Combine(workspaceRoot, "src", "Workspace.sln"),
                WorkspaceRoot = workspaceRoot,
            },
            transactionRevision: null,
            pathComparison.Object);

        var normalizedPath = target.NormalizeDocumentPath(documentPath);
        var result = target.ResolveDocument(new DocumentSelector { Path = normalizedPath });

        normalizedPath.Should().Be("src/Project/Document.cs");
        result.IsResolved.Should().BeTrue();
        result.Value.Should().BeSameAs(document);
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
    public void GIVEN_DocumentWithoutFilePath_WHEN_CreatingReference_THEN_ShouldUseEmptyPath()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Project", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Document.cs", SourceText.From("class C { }"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.CreateDocumentReference(document);

        result!.Path.Should().BeEmpty();
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
        var targetWithoutIdentity = new WorkspaceResolver(
            workspace.CurrentSolution,
            workspaceIdentity: null,
            transactionRevision: null,
            CreatePathComparison().Object);

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
        var target = new WorkspaceResolver(
            workspace.CurrentSolution,
            workspaceIdentity: null,
            transactionRevision: null,
            CreatePathComparison().Object);

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
    public void GIVEN_CaseInsensitiveWorkspace_WHEN_ResolvingDocumentAndProjectPathsWithDifferentCase_THEN_ShouldResolve()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var project = workspace.CurrentSolution.Projects.Single();
        var document = project.Documents.Single();
        var target = CreateTarget(
            workspace.CurrentSolution,
            GetWorkspaceRoot(),
            pathComparison: StringComparison.OrdinalIgnoreCase);

        var documentResult = target.ResolveDocument(new DocumentSelector { Path = "project/document.cs" });
        var projectResult = target.ResolveProject(new ProjectSelector { Path = "project/project.csproj" });

        documentResult.Value.Should().BeSameAs(document);
        projectResult.Value.Should().BeSameAs(project);
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
    public void GIVEN_PathlessDocument_WHEN_ResolvingByPath_THEN_ShouldReturnNotFound()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Project", LanguageNames.CSharp);
        workspace.AddDocument(project.Id, "Document.cs", SourceText.From("class C { }"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveDocument(new DocumentSelector { Path = "Document.cs" });

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
    public void GIVEN_DuplicateDocumentPathAndProjectSelector_WHEN_ResolvingDocument_THEN_ShouldReturnProjectDocument()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var expectedProject = AddProject(workspace, "SecondProject");
        var expectedDocument = AddDocument(
            workspace,
            expectedProject,
            "Document.cs",
            "class D { }",
            Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));

        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var selector = new DocumentSelector
        {
            Path = "FirstProject/Document.cs",
            Project = new ProjectSelector
            {
                ProjectId = expectedProject.Id.Id.ToString(),
            },
        };

        var result = target.ResolveDocument(selector);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.Id.Should().Be(expectedDocument.Id);
        result.Value.Project.Id.Should().Be(expectedProject.Id);
    }

    [Fact]
    public void GIVEN_MultiTargetDocumentPathAndTargetFramework_WHEN_ResolvingDocument_THEN_ShouldReturnTargetProjectDocument()
    {
        using var workspace = CreateWorkspace("Sample(net8.0)", "Document.cs", "class C { }");
        var expectedProject = AddProject(workspace, "Sample(net10.0)");
        var expectedDocument = AddDocument(
            workspace,
            expectedProject,
            "Document.cs",
            "class C { }",
            Path.Combine(GetWorkspaceRoot(), "Sample", "Document.cs"));

        var firstDocument = workspace.CurrentSolution.Projects
            .Single(static project => project.Name == "Sample(net8.0)")
            .Documents
            .Single();

        workspace.TryApplyChanges(workspace.CurrentSolution.WithDocumentFilePath(
            firstDocument.Id,
            Path.Combine(GetWorkspaceRoot(), "Sample", "Document.cs"))).Should().BeTrue();

        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var selector = new DocumentSelector
        {
            Path = "Sample/Document.cs",
            Project = new ProjectSelector
            {
                TargetFramework = "net10.0",
            },
        };

        var result = target.ResolveDocument(selector);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.Id.Should().Be(expectedDocument.Id);
        result.Value.Project.Id.Should().Be(expectedProject.Id);
    }

    [Fact]
    public void GIVEN_SharedSerializedDocumentIdAndProjectSelector_WHEN_ResolvingDocument_THEN_ShouldReturnProjectDocument()
    {
        using var workspace = new AdhocWorkspace();
        var documentGuid = Guid.NewGuid();
        var firstProject = AddProject(workspace, "FirstProject");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(
            workspace,
            firstProject,
            "Document.cs",
            "class C { }",
            Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"),
            documentGuid);

        var expectedDocument = AddDocument(
            workspace,
            secondProject,
            "Document.cs",
            "class D { }",
            Path.Combine(GetWorkspaceRoot(), "SecondProject", "Document.cs"),
            documentGuid);

        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var selector = new DocumentSelector
        {
            DocumentId = documentGuid.ToString(),
            Project = new ProjectSelector
            {
                Name = "SecondProject",
            },
        };

        var result = target.ResolveDocument(selector);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value.Should().BeSameAs(expectedDocument);
    }

    [Fact]
    public void GIVEN_UnknownDocumentProject_WHEN_ResolvingDocument_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var selector = new DocumentSelector
        {
            Path = "Project/Document.cs",
            Project = new ProjectSelector
            {
                Name = "UnknownProject",
            },
        };

        var result = target.ResolveDocument(selector);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public void GIVEN_AmbiguousDocumentProject_WHEN_ResolvingDocument_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        AddProject(workspace, "Project");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var selector = new DocumentSelector
        {
            Path = "Project/Document.cs",
            Project = new ProjectSelector
            {
                Name = "Project",
            },
        };

        var result = target.ResolveDocument(selector);

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
    public void GIVEN_TargetFrameworkInProjectName_WHEN_ResolvingProject_THEN_ShouldReturnTargetSpecificProject()
    {
        using var workspace = CreateWorkspace("Sample (net8.0)", "Document.cs", "class C { }");
        var expectedProject = AddProject(workspace, "Sample (net10.0)");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveProject(new ProjectSelector { TargetFramework = "NET10.0" });

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.Id.Should().Be(expectedProject.Id);
    }

    [Fact]
    public void GIVEN_TargetFrameworkInOutputPath_WHEN_ResolvingProject_THEN_ShouldReturnTargetSpecificProject()
    {
        using var workspace = CreateWorkspace("OuterProject", "Document.cs", "class C { }");
        var outputPath = Path.Combine(GetWorkspaceRoot(), "Project", "bin", "net10.0", "Project.dll");
        var expectedProject = AddProject(workspace, "TargetProject", outputPath);
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveProject(new ProjectSelector { TargetFramework = "net10.0" });

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.Id.Should().Be(expectedProject.Id);
    }

    [Fact]
    public void GIVEN_TargetFrameworkIsUnavailable_WHEN_ResolvingProject_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveProject(new ProjectSelector { TargetFramework = "net10.0" });

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
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
    public async Task GIVEN_ProjectQualifiedSharedPath_WHEN_ResolvingLocation_THEN_ShouldResolveWithoutAmbiguity()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(
            workspace,
            secondProject,
            "Document.cs",
            "class D { }",
            Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));

        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var selector = new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = "FirstProject/Document.cs",
                    Project = new ProjectSelector
                    {
                        Name = "SecondProject",
                    },
                },
                Start = 6,
                Length = 1,
            },
        };

        var result = await target.ResolveLocationAsync(selector, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.SourceSpan.Should().Be(new TextSpan(6, 1));
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
    public void GIVEN_PathlessProject_WHEN_ResolvingByPath_THEN_ShouldReturnNotFound()
    {
        using var workspace = new AdhocWorkspace();
        workspace.AddProject("Project", LanguageNames.CSharp);
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = target.ResolveProject(new ProjectSelector { Path = "Project.csproj" });

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
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
    public async Task GIVEN_SelectionContextBeforeExtendsBeforeSource_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "value after");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector
            {
                Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                SelectedText = "value",
                ContextBefore = "before ",
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_SelectionContextAfterExtendsBeyondSource_WHEN_ResolvingLocation_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "before value");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveLocationAsync(new LocationSelector
        {
            Selection = new TextSelectionSelector
            {
                Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                SelectedText = "value",
                ContextAfter = " after",
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
    public async Task GIVEN_MetadataDocumentationCommentId_WHEN_ResolvingSymbol_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(
            new SymbolSelector { DocumentationCommentId = "T:System.String" },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_ResolvingDocumentationCommentId_THEN_ShouldPropagateCancellation()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

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

    [Fact]
    public async Task GIVEN_MultiTargetProjectScopedDocumentationId_WHEN_ResolvingSymbol_THEN_ShouldReturnSymbolOwnedByTargetProject()
    {
        using var workspace = CreateWorkspace("Sample(net8.0)", "Document.cs", "class C { }");
        var secondProject = AddProject(workspace, "Sample(net10.0)");
        AddDocument(workspace, secondProject, "Document.cs", "class C { }", Path.Combine(GetWorkspaceRoot(), "Sample", "Document.cs"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            DocumentationCommentId = "T:C",
            Project = new ProjectSelector { Name = "Sample(net10.0)" },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.ContainingAssembly.Name.Should().Be("Sample(net10.0)");
    }

    [Fact]
    public async Task GIVEN_ProjectScopedLocationWithLinkedPath_WHEN_ResolvingSymbol_THEN_ShouldUseProjectDocument()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(workspace, secondProject, "Document.cs", "class D { }", Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            Project = new ProjectSelector { Name = "SecondProject" },
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

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.ContainingAssembly.Name.Should().Be("SecondProject");
    }

    [Fact]
    public async Task GIVEN_ProjectScopedMetadataLocation_WHEN_ResolvingSymbol_THEN_ShouldReturnMetadataSymbol()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { string Value = string.Empty; }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            Project = new ProjectSelector { Name = "Project" },
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector { DocumentId = document.Id.Id.ToString() },
                    Start = 10,
                    Length = 6,
                },
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.Name.Should().Be("String");
        result.Value.Locations.Should().Contain(static location => location.IsInMetadata);
    }

    [Fact]
    public async Task GIVEN_ProjectCanSeeSymbolOwnedByReferencedProject_WHEN_ResolvingDocumentationId_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var firstProject = workspace.CurrentSolution.Projects.Single();
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(workspace, secondProject, "Document.cs", "public class D { }", Path.Combine(GetWorkspaceRoot(), "SecondProject", "Document.cs"));
        workspace.TryApplyChanges(workspace.CurrentSolution.AddProjectReference(firstProject.Id, new ProjectReference(secondProject.Id))).Should().BeTrue();
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            DocumentationCommentId = "T:D",
            Project = new ProjectSelector { Name = "FirstProject" },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_UnknownProjectScope_WHEN_ResolvingSymbol_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            DocumentationCommentId = "T:C",
            Project = new ProjectSelector { ProjectId = Guid.NewGuid().ToString() },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_AmbiguousProjectScope_WHEN_ResolvingSymbol_THEN_ShouldReturnAmbiguous()
    {
        using var workspace = CreateWorkspace("Project", "Document.cs", "class C { }");
        AddProject(workspace, "Project");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            DocumentationCommentId = "T:C",
            Project = new ProjectSelector { Name = "Project" },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Ambiguous);
    }

    [Fact]
    public async Task GIVEN_ProjectScopeThatDoesNotContainSelectedDocument_WHEN_ResolvingSymbol_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var firstDocument = workspace.CurrentSolution.Projects.Single().Documents.Single();
        AddProject(workspace, "SecondProject");
        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());

        var result = await target.ResolveSymbolAsync(new SymbolSelector
        {
            Project = new ProjectSelector { Name = "SecondProject" },
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector { DocumentId = firstDocument.Id.Id.ToString() },
                    Start = 6,
                    Length = 1,
                },
            },
        }, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    public async Task GIVEN_MatchingSymbolAndDocumentProjects_WHEN_ResolvingSymbol_THEN_ShouldReturnProjectSymbol()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(
            workspace,
            secondProject,
            "Document.cs",
            "class D { }",
            Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));

        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var projectSelector = new ProjectSelector { Name = "SecondProject" };
        var selector = new SymbolSelector
        {
            Project = projectSelector,
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "FirstProject/Document.cs",
                        Project = projectSelector,
                    },
                    Start = 6,
                    Length = 1,
                },
            },
        };

        var result = await target.ResolveSymbolAsync(selector, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.Resolved);
        result.Value!.ContainingAssembly.Name.Should().Be("SecondProject");
    }

    [Fact]
    public async Task GIVEN_ConflictingSymbolAndDocumentProjects_WHEN_ResolvingSymbol_THEN_ShouldReturnNotFound()
    {
        using var workspace = CreateWorkspace("FirstProject", "Document.cs", "class C { }");
        var secondProject = AddProject(workspace, "SecondProject");
        AddDocument(
            workspace,
            secondProject,
            "Document.cs",
            "class D { }",
            Path.Combine(GetWorkspaceRoot(), "FirstProject", "Document.cs"));

        var target = CreateTarget(workspace.CurrentSolution, GetWorkspaceRoot());
        var selector = new SymbolSelector
        {
            Project = new ProjectSelector { Name = "FirstProject" },
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "FirstProject/Document.cs",
                        Project = new ProjectSelector { Name = "SecondProject" },
                    },
                    Start = 6,
                    Length = 1,
                },
            },
        };

        var result = await target.ResolveSymbolAsync(selector, TestContext.Current.CancellationToken);

        result.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    private static WorkspaceResolver CreateTarget(
        Solution solution,
        string workspaceRoot,
        int? transactionRevision = null,
        StringComparison pathComparison = StringComparison.Ordinal)
    {
        return new WorkspaceResolver(
            solution,
            new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 2,
                LoadedPath = Path.Combine(workspaceRoot, "Workspace.sln"),
                WorkspaceRoot = workspaceRoot,
            },
            transactionRevision,
            CreatePathComparison(pathComparison).Object);
    }

    private static Mock<IWorkspacePathComparison> CreatePathComparison(
        StringComparison comparison = StringComparison.Ordinal)
    {
        var pathComparison = new Mock<IWorkspacePathComparison>();
        pathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(comparison);

        return pathComparison;
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
        DocumentId documentId;
        if (documentGuid is null)
        {
            documentId = DocumentId.CreateNewId(project.Id);
        }
        else
        {
            documentId = DocumentId.CreateFromSerialized(project.Id, documentGuid.Value);
        }

        var documentInfo = DocumentInfo.Create(
            documentId,
            documentName,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Default)),
            filePath: filePath);

        return workspace.AddDocument(documentInfo);
    }

    private static Project AddProject(AdhocWorkspace workspace, string projectName, string? outputFilePath = null)
    {
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            projectName,
            projectName,
            LanguageNames.CSharp,
            filePath: Path.Combine(GetWorkspaceRoot(), projectName, $"{projectName}.csproj"),
            outputFilePath: outputFilePath,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return workspace.AddProject(projectInfo);
    }

    private static string GetWorkspaceRoot()
    {
        return Path.Combine(Path.GetTempPath(), "WorkspaceRoot");
    }
}
