namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceMutationCandidateProcessorTests
{
    private readonly Mock<IAddedDocumentProjectContextPropagator> _addedDocumentProjectContextPropagator;
    private readonly Mock<IWorkspaceMutationCandidateValidator> _candidateValidator;
    private readonly Mock<ILinkedDocumentChangeMerger> _linkedDocumentChangeMerger;
    private readonly WorkspaceMutationCandidateProcessor _target;

    public WorkspaceMutationCandidateProcessorTests()
    {
        _addedDocumentProjectContextPropagator = new Mock<IAddedDocumentProjectContextPropagator>();
        _candidateValidator = new Mock<IWorkspaceMutationCandidateValidator>();
        _linkedDocumentChangeMerger = new Mock<ILinkedDocumentChangeMerger>();
        _addedDocumentProjectContextPropagator
            .Setup(item => item.PropagateAsync(
                It.IsAny<Solution>(),
                It.IsAny<Solution>(),
                It.IsAny<CancellationToken>()))
            .Returns((Solution _, Solution candidateSolution, CancellationToken _) =>
                ValueTask.FromResult(candidateSolution));

        _target = new WorkspaceMutationCandidateProcessor(
            _addedDocumentProjectContextPropagator.Object,
            _candidateValidator.Object,
            _linkedDocumentChangeMerger.Object);
    }

    [Fact]
    public async Task GIVEN_CandidateValidationFails_WHEN_Processing_THEN_ShouldReturnFailureWithoutMerging()
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var error = CreateError("CandidateValidationFailed");
        _candidateValidator
            .Setup(item => item.Validate(solution, solution))
            .Returns(error);

        var result = await _target.ProcessAsync(
            solution,
            solution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        _addedDocumentProjectContextPropagator.Verify(item => item.PropagateAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _linkedDocumentChangeMerger.Verify(item => item.MergeAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LinkedDocumentMergeFails_WHEN_Processing_THEN_ShouldReturnMergeFailure()
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var error = CreateError("LinkedDocumentConflict");
        _linkedDocumentChangeMerger
            .Setup(item => item.MergeAsync(
                solution,
                solution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(LinkedDocumentChangeMergeResult.Failed(error));

        var result = await _target.ProcessAsync(
            solution,
            solution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        _candidateValidator.Verify(item => item.Validate(solution, solution), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MergedCandidateValidationFails_WHEN_Processing_THEN_ShouldReturnValidationFailure()
    {
        using var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var error = CreateError("MergedCandidateValidationFailed");
        _candidateValidator
            .SetupSequence(item => item.Validate(solution, solution))
            .Returns((WorkspaceOperationError?)null)
            .Returns(error);

        _linkedDocumentChangeMerger
            .Setup(item => item.MergeAsync(
                solution,
                solution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(LinkedDocumentChangeMergeResult.Succeeded(solution));

        var result = await _target.ProcessAsync(
            solution,
            solution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        _candidateValidator.Verify(item => item.Validate(solution, solution), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_CandidateAndMergedSolutionAreValid_WHEN_Processing_THEN_ShouldReturnMergedSolution()
    {
        using var workspace = new AdhocWorkspace();
        var currentSolution = workspace.CurrentSolution;
        var mergedProject = currentSolution.AddProject("ProjectName", "AssemblyName", LanguageNames.CSharp);
        var mergedSolution = mergedProject.Solution;
        _linkedDocumentChangeMerger
            .Setup(item => item.MergeAsync(
                currentSolution,
                currentSolution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(LinkedDocumentChangeMergeResult.Succeeded(mergedSolution));

        var result = await _target.ProcessAsync(
            currentSolution,
            currentSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        result.Solution.Should().BeSameAs(mergedSolution);
        _candidateValidator.Verify(item => item.Validate(currentSolution, currentSolution), Times.Once);
        _candidateValidator.Verify(item => item.Validate(currentSolution, mergedSolution), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AddedDocumentInMultiTargetProject_WHEN_Processing_THEN_ShouldReturnDocumentInEveryProjectContext()
    {
        using var workspace = new AdhocWorkspace();
        var projectDirectory = Path.Combine(Path.GetTempPath(), "Project");
        var projectPath = Path.Combine(projectDirectory, "Project.csproj");
        var documentPath = Path.Combine(projectDirectory, "Added.cs");
        var firstProjectId = ProjectId.CreateNewId();
        var secondProjectId = ProjectId.CreateNewId();
        var currentSolution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                firstProjectId,
                VersionStamp.Default,
                "Project (net10.0)",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath))
            .AddProject(ProjectInfo.Create(
                secondProjectId,
                VersionStamp.Default,
                "Project (net9.0)",
                "Project",
                LanguageNames.CSharp,
                filePath: projectPath));

        var candidateSolution = currentSolution.AddDocument(
            DocumentId.CreateNewId(firstProjectId),
            "Added.cs",
            SourceText.From("internal sealed class Added;"),
            filePath: documentPath);

        var pathComparison = new WorkspacePathComparison();
        var target = new WorkspaceMutationCandidateProcessor(
            new AddedDocumentProjectContextPropagator(pathComparison),
            new WorkspaceMutationCandidateValidator(
                new PhysicalPathContainment(new FileSystem(), pathComparison)),
            new LinkedDocumentChangeMerger());

        var result = await target.ProcessAsync(
            currentSolution,
            candidateSolution,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        var processedSolution = result.Solution
            ?? throw new InvalidOperationException("The processed solution was not returned.");

        processedSolution.GetProject(firstProjectId)?.Documents
            .Should().ContainSingle(document => document.FilePath == documentPath);

        processedSolution.GetProject(secondProjectId)?.Documents
            .Should().ContainSingle(document => document.FilePath == documentPath);
    }

    private static WorkspaceOperationError CreateError(string code)
    {
        return new WorkspaceOperationError
        {
            Code = code,
            Message = "Message",
        };
    }
}
