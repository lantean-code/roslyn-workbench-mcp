using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceMutationCandidateValidatorTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspacePathComparison> _pathComparison;
    private readonly WorkspaceMutationCandidateValidator _target;

    public WorkspaceMutationCandidateValidatorTests()
    {
        _workspace = new AdhocWorkspace();
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathComparison.SetupGet(item => item.Comparison).Returns(StringComparison.Ordinal);
        _pathComparison.Setup(item => item.GetComparison(It.IsAny<string>())).Returns(StringComparison.Ordinal);
        _target = new WorkspaceMutationCandidateValidator(_pathComparison.Object);
    }

    [Fact]
    public void GIVEN_CandidateFromDifferentWorkspace_WHEN_Validating_THEN_ShouldRejectIt()
    {
        var currentSolution = CreateSolution();
        using var otherWorkspace = new AdhocWorkspace();

        var result = _target.Validate(currentSolution, otherWorkspace.CurrentSolution);

        AssertError(result, "InvalidMutationProposal", "Mutation proposals must belong to the current workspace.");
    }

    [Fact]
    public void GIVEN_CandidateAddsProject_WHEN_Validating_THEN_ShouldRejectIt()
    {
        var currentSolution = CreateSolution();
        var candidateSolution = currentSolution.AddProject("AddedProject", "AddedProject", LanguageNames.CSharp).Solution;

        var result = _target.Validate(currentSolution, candidateSolution);

        AssertError(result, "UnsupportedChange", "Mutation proposals must not add or remove projects.");
    }

    [Theory]
    [InlineData("ProjectIdentity", "Mutation proposals must not alter project identity.")]
    [InlineData("ProjectFilePath", "Mutation proposals must not alter project identity or options.")]
    [InlineData("ProjectName", "Mutation proposals must not alter project identity or options.")]
    [InlineData("AssemblyName", "Mutation proposals must not alter project identity or options.")]
    [InlineData("DefaultNamespace", "Mutation proposals must not alter project identity or options.")]
    [InlineData("CompilationOptions", "Mutation proposals must not alter project identity or options.")]
    [InlineData("ParseOptions", "Mutation proposals must not alter project identity or options.")]
    [InlineData("DocumentMetadata", "Mutation proposals must not alter source document metadata.")]
    public void GIVEN_UnsupportedCandidateShape_WHEN_Validating_THEN_ShouldRejectIt(string changeKind, string message)
    {
        var currentSolution = CreateSolution();
        var currentProject = currentSolution.Projects.Single();
        var currentDocument = currentProject.Documents.Single();
        var candidateSolution = changeKind switch
        {
            "ProjectIdentity" => currentSolution
                .RemoveProject(currentProject.Id)
                .AddProject("ReplacementProject", "ReplacementProject", LanguageNames.CSharp).Solution,
            "ProjectFilePath" => currentSolution.WithProjectFilePath(currentProject.Id, "DifferentProjectPath"),
            "ProjectName" => currentSolution.WithProjectName(currentProject.Id, "DifferentProjectName"),
            "AssemblyName" => currentSolution.WithProjectAssemblyName(currentProject.Id, "DifferentAssemblyName"),
            "DefaultNamespace" => currentSolution.WithProjectDefaultNamespace(currentProject.Id, "DifferentNamespace"),
            "CompilationOptions" => currentSolution.WithProjectCompilationOptions(
                currentProject.Id,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)),
            "ParseOptions" => currentSolution.WithProjectParseOptions(
                currentProject.Id,
                new CSharpParseOptions(LanguageVersion.CSharp13)),
            "DocumentMetadata" => currentSolution.WithDocumentName(currentDocument.Id, "DifferentDocumentName.cs"),
            _ => throw new InvalidOperationException("Unsupported test change kind."),
        };

        var result = _target.Validate(currentSolution, candidateSolution);

        AssertError(result, "UnsupportedChange", message);
    }

    [Theory]
    [InlineData("AddedWithoutPath", "Mutation proposals must use regular source documents for created files.")]
    [InlineData("AddedScript", "Mutation proposals must use regular source documents for created files.")]
    [InlineData("ProjectWithoutPath", "Mutation proposals must keep created source files within the owning project directory.")]
    [InlineData("AddedOutsideProject", "Mutation proposals must keep created source files within the owning project directory.")]
    public void GIVEN_InvalidAddedDocument_WHEN_Validating_THEN_ShouldRejectIt(string changeKind, string message)
    {
        var currentSolution = CreateSolution(projectHasPath: changeKind != "ProjectWithoutPath");
        var project = currentSolution.Projects.Single();
        string? filePath;
        if (changeKind == "AddedWithoutPath")
        {
            filePath = null;
        }
        else if (changeKind == "AddedOutsideProject")
        {
            filePath = Path.Combine(Path.GetTempPath(), "OutsideProject", "AddedDocument.cs");
        }
        else
        {
            filePath = Path.Combine(Path.GetTempPath(), "Project", "AddedDocument.cs");
        }

        var documentInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "AddedDocument.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("class Added { }"), VersionStamp.Default)),
            sourceCodeKind: changeKind == "AddedScript" ? SourceCodeKind.Script : SourceCodeKind.Regular,
            filePath: filePath);

        var candidateSolution = currentSolution.AddDocument(documentInfo);

        var result = _target.Validate(currentSolution, candidateSolution);

        AssertError(result, "UnsupportedChange", message);
    }

    [Theory]
    [InlineData("AddedMetadataReference")]
    [InlineData("RemovedMetadataReference")]
    [InlineData("AddedProjectReference")]
    [InlineData("RemovedProjectReference")]
    [InlineData("AddedAnalyzerReference")]
    [InlineData("RemovedAnalyzerReference")]
    [InlineData("AddedAdditionalDocument")]
    [InlineData("ChangedAdditionalDocument")]
    [InlineData("RemovedAdditionalDocument")]
    [InlineData("AddedAnalyzerConfigDocument")]
    [InlineData("ChangedAnalyzerConfigDocument")]
    [InlineData("RemovedAnalyzerConfigDocument")]
    public void GIVEN_ReferenceOrNonSourceDocumentChange_WHEN_Validating_THEN_ShouldRejectIt(string changeKind)
    {
        var analyzerReference = new Mock<AnalyzerReference>();
        var solutions = CreateReferenceOrNonSourceDocumentChange(changeKind, analyzerReference.Object);

        var result = _target.Validate(solutions.Current, solutions.Candidate);

        AssertError(result, "UnsupportedChange", "Mutation proposals must not alter project references or non-source documents.");
    }

    [Theory]
    [InlineData("Removed", "Mutation proposals must use regular source documents for deleted files.")]
    [InlineData("Changed", "Mutation proposals must use regular source documents for changed files.")]
    public void GIVEN_PathlessExistingDocument_WHEN_Validating_THEN_ShouldRejectIt(string changeKind, string message)
    {
        var currentSolution = CreateSolution(documentHasPath: false);
        var document = currentSolution.Projects.Single().Documents.Single();
        Solution candidateSolution;
        if (changeKind == "Removed")
        {
            candidateSolution = currentSolution.RemoveDocument(document.Id);
        }
        else
        {
            candidateSolution = currentSolution.WithDocumentText(document.Id, SourceText.From("class Updated { }"));
        }

        var result = _target.Validate(currentSolution, candidateSolution);

        AssertError(result, "UnsupportedChange", message);
    }

    [Fact]
    public void GIVEN_ValidCandidate_WHEN_Validating_THEN_ShouldAcceptIt()
    {
        _pathComparison.Setup(item => item.GetComparison(It.IsAny<string>())).Returns(StringComparison.OrdinalIgnoreCase);
        var currentSolution = CreateSolution(documentPathDiffersByCase: true);
        var document = currentSolution.Projects.Single().Documents.Single();
        var candidateSolution = document.WithText(SourceText.From("class Updated { }")).Project.Solution;

        var result = _target.Validate(currentSolution, candidateSolution);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ExistingLinkedDocumentOutsideProjectDirectory_WHEN_ChangingText_THEN_ShouldAcceptIt()
    {
        var currentSolution = CreateSolution(documentIsLinked: true);
        var document = currentSolution.Projects.Single().Documents.Single();
        var candidateSolution = document.WithText(SourceText.From("class Updated { }")).Project.Solution;

        var result = _target.Validate(currentSolution, candidateSolution);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private Solution CreateSolution(
        bool documentHasPath = true,
        bool projectHasPath = true,
        bool documentPathDiffersByCase = false,
        bool documentIsLinked = false)
    {
        var project = _workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Default, "Project", "Project", LanguageNames.CSharp,
            filePath: projectHasPath ? Path.Combine(Path.GetTempPath(), "Project", "Project.csproj") : null));

        _workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "Document.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("class C { }"), VersionStamp.Default)),
            filePath: documentHasPath
                ? Path.Combine(
                    Path.GetTempPath(),
                    documentIsLinked ? "Shared" : documentPathDiffersByCase ? "project" : "Project",
                    "Document.cs")
                : null));

        return _workspace.CurrentSolution;
    }

    private (Solution Current, Solution Candidate) CreateReferenceOrNonSourceDocumentChange(string changeKind, AnalyzerReference analyzerReference)
    {
        var current = CreateSolution();
        var project = current.Projects.Single();
        var metadataReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var referencedProjectId = ProjectId.CreateNewId();
        var projectReference = new ProjectReference(referencedProjectId);
        var additionalDocumentInfo = CreateTextDocumentInfo(DocumentId.CreateNewId(project.Id), "Additional.txt");
        var analyzerConfigDocumentInfo = CreateTextDocumentInfo(DocumentId.CreateNewId(project.Id), ".editorconfig");

        return changeKind switch
        {
            "AddedMetadataReference" => (current, current.AddMetadataReference(project.Id, metadataReference)),
            "RemovedMetadataReference" => RemoveMetadataReference(current, project.Id, metadataReference),
            "AddedProjectReference" => AddProjectReference(current, project.Id, referencedProjectId, projectReference),
            "RemovedProjectReference" => RemoveProjectReference(current, project.Id, referencedProjectId, projectReference),
            "AddedAnalyzerReference" => (current, current.AddAnalyzerReference(project.Id, analyzerReference)),
            "RemovedAnalyzerReference" => RemoveAnalyzerReference(current, project.Id, analyzerReference),
            "AddedAdditionalDocument" => (current, current.AddAdditionalDocument(additionalDocumentInfo)),
            "ChangedAdditionalDocument" => ChangeAdditionalDocument(current, additionalDocumentInfo),
            "RemovedAdditionalDocument" => RemoveAdditionalDocument(current, additionalDocumentInfo),
            "AddedAnalyzerConfigDocument" => (current, AddAnalyzerConfigDocument(current, analyzerConfigDocumentInfo)),
            "ChangedAnalyzerConfigDocument" => ChangeAnalyzerConfigDocument(current, analyzerConfigDocumentInfo),
            "RemovedAnalyzerConfigDocument" => RemoveAnalyzerConfigDocument(current, analyzerConfigDocumentInfo),
            _ => throw new InvalidOperationException("Unsupported reference test change kind."),
        };
    }

    private static void AssertError(WorkspaceOperationError? result, string code, string message)
    {
        result.Should().NotBeNull();
        result.Code.Should().Be(code);
        result.Message.Should().Be(message);
    }

    private static (Solution Current, Solution Candidate) RemoveMetadataReference(Solution solution, ProjectId projectId, MetadataReference reference)
    {
        var current = solution.AddMetadataReference(projectId, reference);
        return (current, current.RemoveMetadataReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) AddProjectReference(Solution solution, ProjectId projectId, ProjectId referencedProjectId, ProjectReference reference)
    {
        var current = solution.AddProject(ProjectInfo.Create(
            referencedProjectId, VersionStamp.Default, "ReferencedProject", "ReferencedProject", LanguageNames.CSharp));

        return (current, current.AddProjectReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) RemoveProjectReference(Solution solution, ProjectId projectId, ProjectId referencedProjectId, ProjectReference reference)
    {
        var current = solution
            .AddProject(ProjectInfo.Create(
                referencedProjectId, VersionStamp.Default, "ReferencedProject", "ReferencedProject", LanguageNames.CSharp))
            .AddProjectReference(projectId, reference);

        return (current, current.RemoveProjectReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) RemoveAnalyzerReference(Solution solution, ProjectId projectId, AnalyzerReference reference)
    {
        var current = solution.AddAnalyzerReference(projectId, reference);
        return (current, current.RemoveAnalyzerReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) ChangeAdditionalDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = solution.AddAdditionalDocument(documentInfo);
        return (current, current.WithAdditionalDocumentText(documentInfo.Id, SourceText.From("Changed")));
    }

    private static (Solution Current, Solution Candidate) RemoveAdditionalDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = solution.AddAdditionalDocument(documentInfo);
        return (current, current.RemoveAdditionalDocument(documentInfo.Id));
    }

    private static (Solution Current, Solution Candidate) ChangeAnalyzerConfigDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = AddAnalyzerConfigDocument(solution, documentInfo);
        return (current, current.WithAnalyzerConfigDocumentText(documentInfo.Id, SourceText.From("[*.cs]\nindent_style = space")));
    }

    private static (Solution Current, Solution Candidate) RemoveAnalyzerConfigDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = AddAnalyzerConfigDocument(solution, documentInfo);
        return (current, current.RemoveAnalyzerConfigDocument(documentInfo.Id));
    }

    private static Solution AddAnalyzerConfigDocument(Solution solution, DocumentInfo documentInfo)
    {
        return solution.AddAnalyzerConfigDocument(
            documentInfo.Id,
            documentInfo.Name,
            SourceText.From("[*.cs]\nindent_style = tab"),
            filePath: Path.Combine(Path.GetTempPath(), "Project", documentInfo.Name));
    }

    private static DocumentInfo CreateTextDocumentInfo(DocumentId documentId, string name)
    {
        return DocumentInfo.Create(
            documentId,
            name,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("Text"), VersionStamp.Default)));
    }
}
