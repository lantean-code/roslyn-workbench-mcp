using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionCompilerValidationIntegrationTests
{
    [Fact]
    public async Task GIVEN_ExistingCompilerError_WHEN_ValidatingAndCommitting_THEN_ShouldPreserveBaselineAllowance()
    {
        using var fixture = TestWorkspaceFixture.Create();
        var baselineSource = "namespace Sample; public sealed class Class1 { private MissingType? value; }";
        var stagedSource = baselineSource + Environment.NewLine + "public sealed class TransactionMarker { }";
        await File.WriteAllTextAsync(
            fixture.DocumentPath,
            baselineSource,
            TestContext.Current.CancellationToken);

        await using var target = CreateValidationWorkspace(fixture.StateRoot);
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        var stage = await StageDocumentAsync(target, "Class1.cs", stagedSource);

        var validation = await target.ValidateTransactionCompilerImpactAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: stage.Data!.Snapshot);

        var commit = await target.CommitTransactionAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: stage.Data.Snapshot);

        var persistedSource = await File.ReadAllTextAsync(
            fixture.DocumentPath,
            TestContext.Current.CancellationToken);

        validation.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        validation.Data!.IsComplete.Should().BeTrue();
        validation.Data.Succeeded.Should().BeTrue();
        validation.Data.BaselineErrorCount.Should().BeGreaterThan(0);
        validation.Data.IntroducedErrorCount.Should().Be(0);
        commit.Status.Should().Be(WorkspaceOperationStatus.Succeeded, commit.Error?.Message);
        persistedSource.Should().Be(stagedSource);
    }

    [Fact]
    public async Task GIVEN_IntroducedCompilerError_WHEN_CorrectingAndRetryingCommit_THEN_ShouldPersistOnlyCorrectedRevision()
    {
        using var fixture = TestWorkspaceFixture.Create();
        var originalSource = await File.ReadAllTextAsync(
            fixture.DocumentPath,
            TestContext.Current.CancellationToken);

        var invalidSource = "namespace Sample; public sealed class Class1 { private MissingType? value; }";
        var correctedSource = "namespace Sample; public sealed class Class1 { public int Value => 1; }";
        await using var target = CreateValidationWorkspace(fixture.StateRoot);
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        var invalidStage = await StageDocumentAsync(target, "Class1.cs", invalidSource);

        var validation = await target.ValidateTransactionCompilerImpactAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: invalidStage.Data!.Snapshot);

        var rejectedCommit = await target.CommitTransactionAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: invalidStage.Data.Snapshot);

        var sourceAfterRejection = await File.ReadAllTextAsync(
            fixture.DocumentPath,
            TestContext.Current.CancellationToken);

        var activeStatus = await target.GetStatusAsync(TestContext.Current.CancellationToken);
        var correctedStage = await StageDocumentAsync(target, "Class1.cs", correctedSource);
        var correctedValidation = await target.ValidateTransactionCompilerImpactAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: correctedStage.Data!.Snapshot);

        var successfulCommit = await target.CommitTransactionAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: correctedStage.Data.Snapshot);

        var persistedSource = await File.ReadAllTextAsync(
            fixture.DocumentPath,
            TestContext.Current.CancellationToken);

        validation.Data!.IsComplete.Should().BeTrue();
        validation.Data.Succeeded.Should().BeFalse();
        validation.Data.IntroducedErrorCount.Should().BeGreaterThan(0);
        rejectedCommit.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        rejectedCommit.Error!.Code.Should().Be(WorkspaceErrorCodes.NewCompilerErrors);
        sourceAfterRejection.Should().Be(originalSource);
        activeStatus.Data!.State.Should().Be(WorkspaceLifecycleState.TransactionActive);
        correctedValidation.Data!.Succeeded.Should().BeTrue();
        successfulCommit.Status.Should().Be(WorkspaceOperationStatus.Succeeded, successfulCommit.Error?.Message);
        persistedSource.Should().Be(correctedSource);
    }

    [Fact]
    public async Task GIVEN_ChangedDependencyBreaksDependant_WHEN_ValidatingAndCommitting_THEN_ShouldRejectWithoutPersistence()
    {
        using var asset = WorkspaceAssetMaterializer.Materialize("SolutionHierarchy");
        var solutionPath = Path.Combine(asset.WorkspaceRoot, "Sample.slnx");
        var dependencyPath = Path.Combine(asset.WorkspaceRoot, "Lib", "MessageFormatter.cs");
        var originalDependency = await File.ReadAllTextAsync(
            dependencyPath,
            TestContext.Current.CancellationToken);

        var incompatibleDependency = "namespace Sample; public interface IMessageFormatter { int Format(string value); }";
        await using var target = CreateValidationWorkspace(asset.StateRoot);
        await target.OpenAsync(solutionPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        var stage = await StageDocumentAsync(target, "MessageFormatter.cs", incompatibleDependency);

        var validation = await target.ValidateTransactionCompilerImpactAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: stage.Data!.Snapshot);

        var commit = await target.CommitTransactionAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: stage.Data.Snapshot);

        var persistedDependency = await File.ReadAllTextAsync(
            dependencyPath,
            TestContext.Current.CancellationToken);

        validation.Data!.Succeeded.Should().BeFalse();
        validation.Data.IntroducedDiagnostics.Should().Contain(item => item.Id == "CS0738");
        validation.Data.Projects.Should().Contain(item =>
            item.Project.EndsWith("App.csproj", StringComparison.Ordinal)
            && item.IntroducedErrorCount > 0);

        commit.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        commit.Error!.Code.Should().Be(WorkspaceErrorCodes.NewCompilerErrors);
        persistedDependency.Should().Be(originalDependency);
    }

    [Fact]
    public async Task GIVEN_ExternalInputChangesDuringTransaction_WHEN_Validating_THEN_ShouldFailClosed()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = CreateValidationWorkspace(fixture.StateRoot);
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await target.StartTransactionAsync(TestContext.Current.CancellationToken);
        await StageDocumentAsync(
            target,
            "Class1.cs",
            "namespace Sample; public sealed class Class1 { public int Value => 1; }");

        await File.AppendAllTextAsync(
            fixture.DirectoryBuildPropsPath,
            Environment.NewLine + "<!-- External change -->",
            TestContext.Current.CancellationToken);

        var session = target.GetRequiredService<IWorkspaceSessionStore>()
            .ReadSnapshot()
            .Workspaces
            .Values
            .Single();

        var validation = await target.GetRequiredService<ITransactionCompilerValidationService>()
            .ValidateAsync(session, TestContext.Current.CancellationToken);

        validation.IsComplete.Should().BeFalse();
        validation.Succeeded.Should().BeFalse();
        validation.Limitations.Should().Contain(item =>
            item.StartsWith("External Workspace inputs changed", StringComparison.Ordinal));
    }

    private static ComponentWorkspace CreateValidationWorkspace(string stateDirectory)
    {
        return ComponentWorkspace.Create(new ComponentWorkspaceOptions
        {
            StateDirectory = stateDirectory,
            CompilerValidationRequired = true,
        });
    }

    private static async Task<PluginExecutionResult<MutationData>> StageDocumentAsync(
        ComponentWorkspace target,
        string documentName,
        string source)
    {
        var session = target.GetRequiredService<IWorkspaceSessionStore>()
            .ReadSnapshot()
            .Workspaces
            .Values
            .Single();

        var expectedSnapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            session.Transaction?.CurrentRevision);

        var request = new StageMutationRequest
        {
            ExpectedSnapshot = expectedSnapshot,
        };

        await using var lease = target.CreateMutationContext(
            request,
            TestContext.Current.CancellationToken);

        lease.HasFailure.Should().BeFalse();
        var document = lease.Context!.CurrentSolution.Projects
            .SelectMany(static project => project.Documents)
            .Single(item => item.Name == documentName);

        var candidateSolution = document.WithText(SourceText.From(source)).Project.Solution;
        return await lease.StageAsync(
            "test-stage-compiler-validation",
            new MutationCandidate
            {
                CandidateSolution = candidateSolution,
                Summary = "Stage compiler validation scenario.",
            },
            [],
            [],
            TestContext.Current.CancellationToken);
    }

    private sealed record StageMutationRequest : WorkspaceMutationRequest;
}
