using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

public sealed class CodeActionInfoFactoryTests
{
    private static readonly Guid _actionId = new("11111111-1111-1111-1111-111111111111");
    private static readonly int[] _actionPath = [1, 2];
    private static readonly string[] _diagnosticIds = ["DiagnosticId"];
    private static readonly DateTimeOffset _utcNow = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GIVEN_CodeFixAndSourceDocument_WHEN_CreatingItem_THEN_ShouldStoreStrongRecipeAndReturnConciseProjection()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        var diagnostic = new CodeActionDiagnosticIdentity
        {
            Id = "DiagnosticId",
            Message = "Diagnostic message.",
            Start = 3,
            Length = 4,
        };

        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.CodeFix) with
        {
            Diagnostics = [diagnostic],
            FixAllScopes = [CodeActionFixAllScope.Document, CodeActionFixAllScope.Project],
        };

        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4);
        var expiresAt = _utcNow.AddMinutes(5);
        var recipe = CodeActionExecutionTestFactory.CreateReplayRecipe();
        CodeActionReference? reference = new(_actionId, recipe, expiresAt);
        var normalizedDocumentPath = "DocumentPath";
        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        workspacePathService
            .Setup(item => item.TryNormalizePath(roslyn.Document.FilePath ?? roslyn.Document.Name, out normalizedDocumentPath))
            .Returns(true);

        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 1,
        });

        context.SetupGet(item => item.TransactionRevision).Returns(2);
        context.SetupGet(item => item.SnapshotIdentity).Returns(CreateSnapshotIdentity());
        context.SetupGet(item => item.WorkspacePathService).Returns(workspacePathService.Object);
        referenceStore
            .Setup(item => item.TryCreate(
                It.Is<CodeActionReplayRecipe>(recipe =>
                    recipe.Kind == DiscoveredActionKind.CodeFix
                    && recipe.ProviderId == "ProviderId"
                    && recipe.Title == "Title"
                    && recipe.EquivalenceKey == "EquivalenceKey"
                    && recipe.ActionPath.SequenceEqual(_actionPath)
                    && recipe.DiagnosticIds.SequenceEqual(_diagnosticIds)
                    && recipe.Diagnostics.SequenceEqual(new[] { diagnostic })
                    && recipe.SnapshotIdentity == CreateSnapshotIdentity()
                    && recipe.DocumentPath == "DocumentPath"
                    && recipe.ProjectId == roslyn.Document.Project.Id.Id.ToString()
                    && recipe.Start == 3
                    && recipe.Length == 4),
                expiresAt,
                out reference))
            .Returns(true);

        var target = CreateTarget(referenceStore, timeProvider, TimeSpan.FromMinutes(5));

        var created = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            resolvedLocation,
            out var result);

        created.Should().BeTrue();
        var item = result.Should().BeOfType<CodeActionListItem>().Which;
        item.ActionId.Should().Be(_actionId);
        item.Title.Should().Be("Title");
        item.Kind.Should().Be(CodeActionKind.CodeFix);
        item.Location.Should().BeEquivalentTo(new CodeActionLocation
        {
            Document = resolvedLocation.Document!,
            Span = resolvedLocation.Span!,
            Line = resolvedLocation.Line,
            Column = resolvedLocation.Column,
        });

        item.Diagnostics.Should().NotBeNull();
        item.Diagnostics!.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CodeActionDiagnosticContext
            {
                Id = "DiagnosticId",
                Message = "Diagnostic message.",
            });

        item.Diagnostics.HasMore.Should().BeFalse();
        item.Diagnostics.TotalCount.Should().Be(1);
        item.FixAllScopes.Should().NotBeNull();
        item.FixAllScopes!.Should().Equal(CodeActionFixAllScope.Document, CodeActionFixAllScope.Project);
    }

    [Fact]
    public void GIVEN_CodeFixHasMoreDiagnosticsThanTheProjectionLimit_WHEN_CreatingItem_THEN_ShouldReturnBoundedDiagnosticContexts()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        var diagnostics = Enumerable.Range(1, 3)
            .Select(index => new CodeActionDiagnosticIdentity
            {
                Id = $"Diagnostic{index}",
                Message = $"Message {index}",
                Start = index,
                Length = 1,
            })
            .ToArray();

        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.CodeFix) with
        {
            Diagnostics = diagnostics,
        };

        var expiresAt = _utcNow.AddMinutes(5);
        var recipe = CodeActionExecutionTestFactory.CreateReplayRecipe();
        CodeActionReference? reference = new(_actionId, recipe, expiresAt);
        var normalizedDocumentPath = "DocumentPath";

        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        workspacePathService
            .Setup(item => item.TryNormalizePath(roslyn.Document.FilePath ?? roslyn.Document.Name, out normalizedDocumentPath))
            .Returns(true);

        context.SetupGet(item => item.SnapshotIdentity).Returns(CreateSnapshotIdentity());
        context.SetupGet(item => item.WorkspacePathService).Returns(workspacePathService.Object);
        referenceStore
            .Setup(item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                expiresAt,
                out reference))
            .Returns(true);

        var target = CreateTarget(
            referenceStore,
            timeProvider,
            TimeSpan.FromMinutes(5),
            maximumDiagnosticContextsPerAction: 2);

        var created = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4),
            out var result);

        created.Should().BeTrue();
        var item = result.Should().BeOfType<CodeActionListItem>().Which;
        item.Diagnostics.Should().NotBeNull();
        item.Diagnostics!.Items.Select(static diagnostic => diagnostic.Id)
            .Should().Equal("Diagnostic1", "Diagnostic2");

        item.Diagnostics.HasMore.Should().BeTrue();
        item.Diagnostics.TotalCount.Should().Be(3);
    }

    [Fact]
    public void GIVEN_DocumentHasNoFilePath_WHEN_CreatingItem_THEN_ShouldNormalizeDocumentName()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution.AddProject("ProjectName", "AssemblyName", LanguageNames.CSharp);
        var solution = project.Solution.AddDocument(DocumentId.CreateNewId(project.Id), "DocumentName.cs", SourceText.From("class C { }"));
        workspace.TryApplyChanges(solution);
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        var action = CreateAction(workspace.CurrentSolution, DiscoveredActionKind.Refactoring);
        var expiresAt = _utcNow.AddMinutes(5);
        var recipe = CodeActionExecutionTestFactory.CreateReplayRecipe();
        CodeActionReference? reference = new(_actionId, recipe, expiresAt);
        var normalizedDocumentPath = "NormalizedDocumentName";

        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        workspacePathService
            .Setup(item => item.TryNormalizePath("DocumentName.cs", out normalizedDocumentPath))
            .Returns(true);
        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity());
        context.SetupGet(item => item.WorkspacePathService).Returns(workspacePathService.Object);
        referenceStore
            .Setup(item => item.TryCreate(
                It.Is<CodeActionReplayRecipe>(recipe => recipe.DocumentPath == "NormalizedDocumentName"),
                expiresAt,
                out reference))
            .Returns(true);

        var target = CreateTarget(referenceStore, timeProvider, TimeSpan.FromMinutes(5));

        var created = target.TryCreate(
            action,
            context.Object,
            document,
            SelectorTestFactory.CreateResolvedLocation("DocumentName.cs", 3, 4),
            out var result);

        created.Should().BeTrue();
        var item = result.Should().BeOfType<CodeActionListItem>().Which;
        item.Kind.Should().Be(CodeActionKind.Refactoring);
        item.Diagnostics.Should().BeNull();
        item.FixAllScopes.Should().BeNull();
        workspacePathService.Verify(item => item.TryNormalizePath("DocumentName.cs", out normalizedDocumentPath), Times.Once);
    }

    [Fact]
    public void GIVEN_ReferenceCannotBeStored_WHEN_CreatingItem_THEN_ShouldNotPublishAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        CodeActionReference? rejectedReference = null;
        var normalizedDocumentPath = "DocumentPath";
        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        workspacePathService
            .Setup(item => item.TryNormalizePath(roslyn.Document.FilePath ?? roslyn.Document.Name, out normalizedDocumentPath))
            .Returns(true);

        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity());
        context.SetupGet(item => item.WorkspacePathService).Returns(workspacePathService.Object);
        referenceStore
            .Setup(item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                It.IsAny<DateTimeOffset>(),
                out rejectedReference))
            .Returns(false);

        var target = CreateTarget(referenceStore, timeProvider, TimeSpan.FromMinutes(5));

        var created = target.TryCreate(
            CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring),
            context.Object,
            roslyn.Document,
            SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4),
            out var result);

        created.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_DocumentPathCannotBeNormalized_WHEN_CreatingItem_THEN_ShouldNotStoreReference()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        string? normalizedDocumentPath = null;
        workspacePathService
            .Setup(item => item.TryNormalizePath(roslyn.Document.FilePath ?? roslyn.Document.Name, out normalizedDocumentPath))
            .Returns(false);

        context.SetupGet(item => item.WorkspacePathService).Returns(workspacePathService.Object);
        var target = CreateTarget(referenceStore, timeProvider, TimeSpan.FromMinutes(5));

        var created = target.TryCreate(
            CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring),
            context.Object,
            roslyn.Document,
            SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4),
            out var result);

        created.Should().BeFalse();
        result.Should().BeNull();
        referenceStore.Verify(item => item.TryCreate(
            It.IsAny<CodeActionReplayRecipe>(),
            It.IsAny<DateTimeOffset>(),
            out It.Ref<CodeActionReference?>.IsAny), Times.Never);
    }

    [Fact]
    public void GIVEN_ResolvedLocationIsIncomplete_WHEN_CreatingItem_THEN_ShouldNotStoreReference()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var target = CreateTarget(referenceStore, timeProvider, TimeSpan.FromMinutes(5));

        var created = target.TryCreate(
            CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring),
            new Mock<ICodeActionExecutionContext>().Object,
            roslyn.Document,
            new ResolvedLocation(),
            out var result);

        created.Should().BeFalse();
        result.Should().BeNull();
        referenceStore.Verify(item => item.TryCreate(
            It.IsAny<CodeActionReplayRecipe>(),
            It.IsAny<DateTimeOffset>(),
            out It.Ref<CodeActionReference?>.IsAny), Times.Never);
    }

    private static CodeActionInfoFactory CreateTarget(
        Mock<ICodeActionReferenceStore> referenceStore,
        Mock<TimeProvider> timeProvider,
        TimeSpan referenceLifetime,
        int maximumDiagnosticContextsPerAction = CodeActionExecutionOptions.DefaultMaximumDiagnosticContextsPerAction)
    {
        var executionOptions = new CodeActionExecutionOptions
        {
            ReferenceLifetime = referenceLifetime,
            MaximumDiagnosticContextsPerAction = maximumDiagnosticContextsPerAction,
        };

        var options = Options.Create(executionOptions);

        return new CodeActionInfoFactory(referenceStore.Object, timeProvider.Object, options);
    }

    private static WorkspaceSnapshotIdentity CreateSnapshotIdentity()
    {
        var committedSnapshotId = new WorkspaceSnapshotId(2);
        var transactionId = new WorkspaceTransactionId(1);

        return new WorkspaceSnapshotIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            committedSnapshotId,
            transactionId);
    }

    private static DiscoveredCodeAction CreateAction(Solution solution, DiscoveredActionKind kind)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create("Title", _ => Task.FromResult(solution), "EquivalenceKey"),
            Kind = kind,
            ProviderId = "ProviderId",
            Title = "Title",
            TargetSpan = new TextSpan(3, 4),
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1, 2],
            DiagnosticIds = ["DiagnosticId"],
        };
    }
}
