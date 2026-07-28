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
        var resolver = new Mock<IWorkspaceResolver>();
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
        CodeActionReference? reference = new(_actionId, new CodeActionReplayRecipe(), expiresAt);
        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        resolver
            .Setup(item => item.NormalizeDocumentPath(roslyn.Document.FilePath ?? roslyn.Document.Name))
            .Returns("DocumentPath");
        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });
        context.SetupGet(item => item.TransactionRevision).Returns(2);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
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
                    && recipe.WorkspaceId == "WorkspaceId"
                    && recipe.WorkspaceEpoch == 1
                    && recipe.TransactionRevision == 2
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
        item.Diagnostics!.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CodeActionDiagnosticContext
            {
                Id = "DiagnosticId",
                Message = "Diagnostic message.",
            });
        item.FixAllScopes.Should().NotBeNull();
        item.FixAllScopes!.Should().Equal(CodeActionFixAllScope.Document, CodeActionFixAllScope.Project);
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
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(workspace.CurrentSolution, DiscoveredActionKind.Refactoring);
        var expiresAt = _utcNow.AddMinutes(5);
        CodeActionReference? reference = new(_actionId, new CodeActionReplayRecipe(), expiresAt);

        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        resolver.Setup(item => item.NormalizeDocumentPath("DocumentName.cs")).Returns("NormalizedDocumentName");
        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity());
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
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
        resolver.Verify(item => item.NormalizeDocumentPath("DocumentName.cs"), Times.Once);
    }

    [Fact]
    public void GIVEN_ReferenceCannotBeStored_WHEN_CreatingItem_THEN_ShouldNotPublishAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        CodeActionReference? rejectedReference = null;
        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        resolver
            .Setup(item => item.NormalizeDocumentPath(roslyn.Document.FilePath ?? roslyn.Document.Name))
            .Returns("DocumentPath");
        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity());
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
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

    [Fact]
    public void GIVEN_ExistingReference_WHEN_CreatingLegacyDescriptor_THEN_ShouldPreserveTemporaryDescriptorMetadata()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring);
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4);
        var reference = new CodeActionReference(
            _actionId,
            new CodeActionReplayRecipe(),
            _utcNow.AddMinutes(5));
        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });

        var target = CreateTarget(referenceStore, timeProvider, TimeSpan.FromMinutes(5));

        var result = target.CreateFromReference(
            action,
            context.Object,
            new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Parameterised,
                ExecutorTool = "ExecutorTool",
            },
            reference,
            resolvedLocation);

        result.ActionId.Should().Be(_actionId);
        result.ExpiresAt.Should().Be("2000-01-01T00:05:00.0000000+00:00");
        result.ExecutorTool.Should().Be("ExecutorTool");
        result.Location.Should().BeSameAs(resolvedLocation);
    }

    private static CodeActionInfoFactory CreateTarget(
        Mock<ICodeActionReferenceStore> referenceStore,
        Mock<TimeProvider> timeProvider,
        TimeSpan referenceLifetime)
    {
        return new CodeActionInfoFactory(
            referenceStore.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                ReferenceLifetime = referenceLifetime,
            }));
    }

    private static DiscoveredCodeAction CreateAction(Solution solution, DiscoveredActionKind kind)
    {
        return new DiscoveredCodeAction
        {
            Action = CodeAction.Create("Title", _ => Task.FromResult(solution), "EquivalenceKey"),
            Kind = kind,
            ProviderId = "ProviderId",
            Title = "Title",
            Descriptor = new CodeActionDescriptorEntry(),
            TargetSpan = new TextSpan(3, 4),
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1, 2],
            DiagnosticIds = ["DiagnosticId"],
        };
    }
}
