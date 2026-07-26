using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

#pragma warning disable CA1861 // Fresh mutable arrays keep each recipe scenario isolated from other tests.
public sealed class CodeActionInfoFactoryTests
{
    private static readonly Guid _actionId = new("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset _utcNow = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GIVEN_RefactoringAndSourceDocument_WHEN_CreatingInfo_THEN_ShouldStoreReplayRecipeAndDescriptorMetadata()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring);
        var descriptor = CreateDescriptor();
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4);
        var expiresAt = _utcNow.AddMinutes(5);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        };

        CodeActionReference? reference = new(_actionId, new CodeActionReplayRecipe(), expiresAt);
        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        resolver
            .Setup(item => item.NormalizeDocumentPath(roslyn.Document.FilePath ?? roslyn.Document.Name))
            .Returns("DocumentPath");

        context.SetupGet(item => item.WorkspaceIdentity).Returns(workspaceIdentity);
        context.SetupGet(item => item.TransactionRevision).Returns(2);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
        referenceStore
            .Setup(item => item.TryCreate(
                It.Is<CodeActionReplayRecipe>(recipe =>
                    recipe.Kind == DiscoveredActionKind.Refactoring
                    && recipe.ProviderId == "ProviderId"
                    && recipe.Title == "Title"
                    && recipe.EquivalenceKey == "EquivalenceKey"
                    && recipe.ActionPath.SequenceEqual(new[] { 1, 2 })
                    && recipe.DiagnosticIds.SequenceEqual(new[] { "DiagnosticId" })
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

        var target = new CodeActionInfoFactory(
            referenceStore.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                ReferenceLifetime = TimeSpan.FromMinutes(5),
            }));

        var created = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            resolvedLocation,
            descriptor,
            out var result);

        created.Should().BeTrue();
        var info = result.Should().BeOfType<CodeActionInfo>().Which;
        info.ActionId.Should().Be(_actionId);
        info.WorkspaceId.Should().Be("WorkspaceId");
        info.Title.Should().Be("Title");
        info.ProviderId.Should().Be("ProviderId");
        info.Kind.Should().Be("Refactoring");
        info.EquivalenceKey.Should().Be("EquivalenceKey");
        info.ActionPath.Should().Equal(1, 2);
        info.DiagnosticIds.Should().Equal("DiagnosticId");
        info.Location.Should().BeSameAs(resolvedLocation);
        info.WorkspaceEpoch.Should().Be(1);
        info.TransactionRevision.Should().Be(2);
        info.ExpiresAt.Should().Be("2000-01-01T00:05:00.0000000+00:00");
        info.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        info.ExecutorTool.Should().Be("ExecutorTool");
        info.DescribeTool.Should().Be("DescribeTool");
        info.UnsupportedReasonCode.Should().Be("UnsupportedReasonCode");
        info.Requirements.Should().Equal("Requirement");
    }

    [Fact]
    public void GIVEN_CodeFixDocumentHasNoFilePath_WHEN_CreatingInfo_THEN_ShouldNormalizeDocumentName()
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
        var action = CreateAction(workspace.CurrentSolution, DiscoveredActionKind.CodeFix);
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation("DocumentName.cs", 3, 4);
        var expiresAt = _utcNow.AddMinutes(5);
        CodeActionReference? reference = new(_actionId, new CodeActionReplayRecipe(), expiresAt);

        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        resolver.Setup(item => item.NormalizeDocumentPath("DocumentName.cs")).Returns("NormalizedDocumentName");
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
                    && recipe.DocumentPath == "NormalizedDocumentName"
                    && recipe.ProjectId == document.Project.Id.Id.ToString()),
                expiresAt,
                out reference))
            .Returns(true);

        var target = new CodeActionInfoFactory(
            referenceStore.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                ReferenceLifetime = TimeSpan.FromMinutes(5),
            }));

        var created = target.TryCreate(
            action,
            context.Object,
            document,
            resolvedLocation,
            new CodeActionDescriptorEntry(),
            out var result);

        created.Should().BeTrue();
        document.FilePath.Should().BeNull();
        result.Should().BeOfType<CodeActionInfo>().Which.Kind.Should().Be("CodeFix");
        resolver.Verify(item => item.NormalizeDocumentPath("DocumentName.cs"), Times.Once);
    }

    [Fact]
    public void GIVEN_MaximumSupportedReferenceLifetime_WHEN_CreatingInfo_THEN_ShouldCalculateExpiry()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring);
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4);
        var expiresAt = _utcNow.AddDays(1);
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

        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
        referenceStore
            .Setup(item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                expiresAt,
                out reference))
            .Returns(true);

        var target = new CodeActionInfoFactory(
            referenceStore.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                ReferenceLifetime = TimeSpan.FromDays(1),
            }));

        var created = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            resolvedLocation,
            new CodeActionDescriptorEntry(),
            out var result);

        created.Should().BeTrue();
        result.Should().BeOfType<CodeActionInfo>().Which.ExpiresAt.Should().Be("2000-01-02T00:00:00.0000000+00:00");
    }

    [Fact]
    public void GIVEN_ReferenceCannotBeStored_WHEN_CreatingInfo_THEN_ShouldNotPublishAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring);
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation("Code.cs", 3, 4);
        CodeActionReference? rejectedReference = null;

        timeProvider.Setup(item => item.GetUtcNow()).Returns(_utcNow);
        resolver
            .Setup(item => item.NormalizeDocumentPath(roslyn.Document.FilePath ?? roslyn.Document.Name))
            .Returns("DocumentPath");

        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });

        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
        referenceStore
            .Setup(item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                It.IsAny<DateTimeOffset>(),
                out rejectedReference))
            .Returns(false);

        var target = new CodeActionInfoFactory(
            referenceStore.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions()));

        var result = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            resolvedLocation,
            new CodeActionDescriptorEntry(),
            out var info);

        result.Should().BeFalse();
        info.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ExistingReference_WHEN_CreatingDescriptor_THEN_ShouldPreserveReferenceIdentityAndExpiry()
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

        var target = new CodeActionInfoFactory(
            referenceStore.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions()));

        var result = target.CreateFromReference(
            action,
            context.Object,
            new CodeActionDescriptorEntry(),
            reference,
            resolvedLocation);

        result.ActionId.Should().Be(_actionId);
        result.ExpiresAt.Should().Be("2000-01-01T00:05:00.0000000+00:00");
        result.Location.Should().BeSameAs(resolvedLocation);
        referenceStore.Verify(
            item => item.TryCreate(
                It.IsAny<CodeActionReplayRecipe>(),
                It.IsAny<DateTimeOffset>(),
                out It.Ref<CodeActionReference?>.IsAny),
            Times.Never);
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

    private static CodeActionDescriptorEntry CreateDescriptor()
    {
        return new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Parameterised,
            ExecutorTool = "ExecutorTool",
            DescribeTool = "DescribeTool",
            UnsupportedReasonCode = "UnsupportedReasonCode",
            Requirements = ["Requirement"],
        };
    }
}
#pragma warning restore CA1861
