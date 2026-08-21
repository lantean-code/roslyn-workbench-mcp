namespace Roslyn.Workbench.Mcp.Workspace.Test.Operations;

public sealed class WorkspaceOperationResultFactoryTests
{
    private readonly WorkspaceOperationResultFactory _target;

    public WorkspaceOperationResultFactoryTests()
    {
        _target = new WorkspaceOperationResultFactory();
    }

    [Fact]
    public void GIVEN_DataAndOptionalValues_WHEN_CreatingSuccess_THEN_ShouldPreserveEveryValue()
    {
        var data = new TestOutcome();
        var context = CreateContext();
        var diagnostic = CreateDiagnostic();
        var warning = CreateWarning();

        var result = _target.Succeeded(data, context, [diagnostic], [warning]);

        result.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Context.Should().BeSameAs(context);
        result.Data.Should().BeSameAs(data);
        result.HasData.Should().BeTrue();
        result.HasError.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(diagnostic);
        result.Diagnostics[0].Id.Should().Be("Id");
        result.Diagnostics[0].Severity.Should().Be(global::Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity.Warning);
        result.Diagnostics[0].Message.Should().Be("Message");
        result.Diagnostics[0].Location.Should().NotBeNull();
        result.Warnings.Should().ContainSingle().Which.Should().BeSameAs(warning);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GIVEN_OnlyData_WHEN_CreatingSuccess_THEN_ShouldUseEmptyDefaults()
    {
        var result = _target.Succeeded(new TestOutcome());

        result.Context.Should().NotBeNull();
        result.Diagnostics.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ErrorFields_WHEN_CreatingRejection_THEN_ShouldCreateRejectedResult()
    {
        var result = _target.Rejected<TestOutcome>("Code", "Message", RequiredAction.Retry);

        result.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        result.HasData.Should().BeFalse();
        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("Code");
        result.Error.Message.Should().Be("Message");
        result.Error.RequiredAction.Should().Be(RequiredAction.Retry);
        result.Context.Should().NotBeNull();
        result.Diagnostics.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ErrorAndOptionalValues_WHEN_CreatingRejection_THEN_ShouldPreserveEveryValue()
    {
        var error = CreateError();
        var context = CreateContext();
        var diagnostic = CreateDiagnostic();
        var warning = CreateWarning();

        var result = _target.Rejected<TestOutcome>(error, context, [diagnostic], [warning]);

        result.Status.Should().Be(WorkspaceOperationStatus.Rejected);
        result.Error.Should().BeSameAs(error);
        result.Context.Should().BeSameAs(context);
        result.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(diagnostic);
        result.Warnings.Should().ContainSingle().Which.Should().BeSameAs(warning);
    }

    [Fact]
    public void GIVEN_ErrorFields_WHEN_CreatingConflict_THEN_ShouldCreateConflictResult()
    {
        var result = _target.Conflict<TestOutcome>("Code", "Message", RequiredAction.ResolveTargetAgain);

        result.Status.Should().Be(WorkspaceOperationStatus.Conflict);
        result.Error!.Code.Should().Be("Code");
        result.Error.Message.Should().Be("Message");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_ErrorAndOptionalValues_WHEN_CreatingConflict_THEN_ShouldPreserveEveryValue()
    {
        var error = CreateError();
        var context = CreateContext();
        var diagnostic = CreateDiagnostic();
        var warning = CreateWarning();

        var result = _target.Conflict<TestOutcome>(error, context, [diagnostic], [warning]);

        result.Status.Should().Be(WorkspaceOperationStatus.Conflict);
        result.Error.Should().BeSameAs(error);
        result.Context.Should().BeSameAs(context);
        result.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(diagnostic);
        result.Warnings.Should().ContainSingle().Which.Should().BeSameAs(warning);
    }

    [Fact]
    public void GIVEN_ErrorFieldsAndOptionalValues_WHEN_CreatingFault_THEN_ShouldPreserveEveryValue()
    {
        var context = CreateContext();
        var diagnostic = CreateDiagnostic();
        var warning = CreateWarning();

        var result = _target.Faulted<TestOutcome>("Code", "Message", RequiredAction.Retry, context, [diagnostic], [warning]);

        result.Status.Should().Be(WorkspaceOperationStatus.Faulted);
        result.Error!.Code.Should().Be("Code");
        result.Error.Message.Should().Be("Message");
        result.Error.RequiredAction.Should().Be(RequiredAction.Retry);
        result.Context.Should().BeSameAs(context);
        result.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(diagnostic);
        result.Warnings.Should().ContainSingle().Which.Should().BeSameAs(warning);
    }

    [Fact]
    public void GIVEN_DataAndOptionalValues_WHEN_CreatingNoChange_THEN_ShouldPreserveEveryValue()
    {
        var data = new TestOutcome();
        var context = CreateContext();
        var diagnostic = CreateDiagnostic();
        var warning = CreateWarning();

        var result = _target.NoChange(context, data, [diagnostic], [warning]);

        result.Status.Should().Be(WorkspaceOperationStatus.NoChange);
        result.Context.Should().BeSameAs(context);
        result.Data.Should().BeSameAs(data);
        result.Diagnostics.Should().ContainSingle().Which.Should().BeSameAs(diagnostic);
        result.Warnings.Should().ContainSingle().Which.Should().BeSameAs(warning);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NoOptionalValues_WHEN_CreatingNoChange_THEN_ShouldUseEmptyDefaults()
    {
        var result = _target.NoChange<TestOutcome>();

        result.Context.Should().NotBeNull();
        result.Data.Should().BeNull();
        result.Diagnostics.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    private static WorkspaceOperationContext CreateContext()
    {
        return new WorkspaceOperationContext
        {
            Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
        };
    }

    private static WorkspaceOperationError CreateError()
    {
        return new WorkspaceOperationError
        {
            Code = "Code",
            Message = "Message",
        };
    }

    private static DiagnosticInfo CreateDiagnostic()
    {
        return new DiagnosticInfo
        {
            Id = "Id",
            Severity = global::Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity.Warning,
            Message = "Message",
            Location = new ResolvedLocation
            {
                Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                    Guid.Parse("11111111-1111-1111-1111-111111111111")),
            },
        };
    }

    private static WarningInfo CreateWarning()
    {
        return new WarningInfo
        {
            Code = "Code",
            Message = "Message",
        };
    }

    private sealed record TestOutcome
    {
    }
}
