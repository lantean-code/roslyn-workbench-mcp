using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

#pragma warning disable CA1861 // Fresh mutable arrays keep each payload scenario isolated from other tests.
public sealed class CodeActionInfoFactoryTests
{
    [Fact]
    public void GIVEN_RefactoringAndSourceDocument_WHEN_CreatingInfo_THEN_ShouldEncodeReplayPayloadAndDescriptorMetadata()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var tokenService = new Mock<ICodeActionTokenService>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring);
        var descriptor = CreateDescriptor();
        var actionId = "ActionId";
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        };

        timeProvider
            .Setup(item => item.GetUtcNow())
            .Returns(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        resolver
            .Setup(item => item.NormalizeDocumentPath(roslyn.Document.FilePath ?? roslyn.Document.Name))
            .Returns("DocumentPath");

        context.SetupGet(item => item.WorkspaceIdentity).Returns(workspaceIdentity);
        context.SetupGet(item => item.TransactionRevision).Returns(2);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
        tokenService
            .Setup(item => item.TryEncode(It.Is<CodeActionTokenPayload>(payload =>
                payload.Kind == "Refactoring"
                && payload.ProviderId == "ProviderId"
                && payload.Title == "Title"
                && payload.EquivalenceKey == "EquivalenceKey"
                && payload.ActionPath.SequenceEqual(new[] { 1, 2 })
                && payload.DiagnosticIds.SequenceEqual(new[] { "DiagnosticId" })
                && payload.WorkspaceId == "WorkspaceId"
                && payload.WorkspaceEpoch == 1
                && payload.TransactionRevision == 2
                && payload.ExpiresAt == "2000-01-01T00:05:00.0000000+00:00"
                && payload.DocumentPath == "DocumentPath"
                && payload.ProjectId == roslyn.Document.Project.Id.Id.ToString()
                && payload.Start == 3
                && payload.Length == 4), out actionId))
            .Returns(true);

        var target = new CodeActionInfoFactory(
            tokenService.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                TokenLifetime = TimeSpan.FromMinutes(5),
            }));

        var created = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            new TextSpan(3, 4),
            descriptor,
            out var result);

        created.Should().BeTrue();
        var info = result.Should().BeOfType<CodeActionInfo>().Which;
        info.ActionId.Should().Be("ActionId");
        info.WorkspaceId.Should().Be("WorkspaceId");
        info.Title.Should().Be("Title");
        info.ProviderId.Should().Be("ProviderId");
        info.Kind.Should().Be("Refactoring");
        info.EquivalenceKey.Should().Be("EquivalenceKey");
        info.ActionPath.Should().Equal(1, 2);
        info.DiagnosticIds.Should().Equal("DiagnosticId");
        info.WorkspaceEpoch.Should().Be(1);
        info.TransactionRevision.Should().Be(2);
        info.ExpiresAt.Should().Be("2000-01-01T00:05:00.0000000+00:00");
        info.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        info.ExecutorTool.Should().Be("ExecutorTool");
        info.DescribeTool.Should().Be("DescribeTool");
        info.UnsupportedReasonCode.Should().Be("UnsupportedReasonCode");
        info.Requirements.Should().Equal("Requirement");
        tokenService.Verify(
            item => item.TryEncode(It.IsAny<CodeActionTokenPayload>(), out It.Ref<string>.IsAny),
            Times.Once);
    }

    [Fact]
    public void GIVEN_CodeFixDocumentHasNoFilePath_WHEN_CreatingInfo_THEN_ShouldNormalizeDocumentName()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution.AddProject("ProjectName", "AssemblyName", LanguageNames.CSharp);
        var solution = project.Solution.AddDocument(DocumentId.CreateNewId(project.Id), "DocumentName.cs", SourceText.From("class C { }"));
        workspace.TryApplyChanges(solution);
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var tokenService = new Mock<ICodeActionTokenService>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(workspace.CurrentSolution, DiscoveredActionKind.CodeFix);
        var actionId = "ActionId";
        timeProvider
            .Setup(item => item.GetUtcNow())
            .Returns(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        resolver.Setup(item => item.NormalizeDocumentPath("DocumentName.cs")).Returns("NormalizedDocumentName");
        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });

        context.SetupGet(item => item.TransactionRevision).Returns(2);
        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
        tokenService
            .Setup(item => item.TryEncode(It.Is<CodeActionTokenPayload>(payload =>
                payload.Kind == "CodeFix"
                && payload.DocumentPath == "NormalizedDocumentName"
                && payload.ProjectId == document.Project.Id.Id.ToString()), out actionId))
            .Returns(true);

        var target = new CodeActionInfoFactory(
            tokenService.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                TokenLifetime = TimeSpan.FromMinutes(5),
            }));

        var created = target.TryCreate(
            action,
            context.Object,
            document,
            new TextSpan(0, 1),
            new CodeActionDescriptorEntry(),
            out var result);

        created.Should().BeTrue();
        document.FilePath.Should().BeNull();
        result.Should().BeOfType<CodeActionInfo>().Which.Kind.Should().Be("CodeFix");
        resolver.Verify(item => item.NormalizeDocumentPath("DocumentName.cs"), Times.Once);
    }

    [Fact]
    public void GIVEN_MaximumSupportedTokenLifetime_WHEN_CreatingInfo_THEN_ShouldCalculateExpiry()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var tokenService = new Mock<ICodeActionTokenService>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring);
        var actionId = "ActionId";

        timeProvider
            .Setup(item => item.GetUtcNow())
            .Returns(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        resolver
            .Setup(item => item.NormalizeDocumentPath(roslyn.Document.FilePath ?? roslyn.Document.Name))
            .Returns("DocumentPath");

        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });

        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
        tokenService
            .Setup(item => item.TryEncode(It.IsAny<CodeActionTokenPayload>(), out actionId))
            .Returns(true);

        var target = new CodeActionInfoFactory(
            tokenService.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                TokenLifetime = TimeSpan.FromDays(1),
            }));

        var created = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            new CodeActionDescriptorEntry(),
            out var result);

        created.Should().BeTrue();
        result.Should().BeOfType<CodeActionInfo>().Which.ExpiresAt.Should().Be("2000-01-02T00:00:00.0000000+00:00");
    }

    [Fact]
    public void GIVEN_TokenCannotBeEncoded_WHEN_CreatingInfo_THEN_ShouldNotPublishAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var tokenService = new Mock<ICodeActionTokenService>();
        var timeProvider = new Mock<TimeProvider>();
        var context = new Mock<ICodeActionExecutionContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var action = CreateAction(roslyn.Solution, DiscoveredActionKind.Refactoring);
        var rejectedToken = string.Empty;

        timeProvider
            .Setup(item => item.GetUtcNow())
            .Returns(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        resolver
            .Setup(item => item.NormalizeDocumentPath(roslyn.Document.FilePath ?? roslyn.Document.Name))
            .Returns("DocumentPath");

        context.SetupGet(item => item.WorkspaceIdentity).Returns(new WorkspaceIdentity
        {
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 1,
        });

        context.SetupGet(item => item.WorkspaceResolver).Returns(resolver.Object);
        tokenService
            .Setup(item => item.TryEncode(It.IsAny<CodeActionTokenPayload>(), out rejectedToken))
            .Returns(false);

        var target = new CodeActionInfoFactory(
            tokenService.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions()));

        var result = target.TryCreate(
            action,
            context.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            new CodeActionDescriptorEntry(),
            out var info);

        result.Should().BeFalse();
        info.Should().BeNull();
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
