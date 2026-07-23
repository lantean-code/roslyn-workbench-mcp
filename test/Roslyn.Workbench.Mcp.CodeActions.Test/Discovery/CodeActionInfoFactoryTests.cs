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
            .Setup(item => item.Encode(It.Is<CodeActionTokenPayload>(payload =>
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
                && payload.Length == 4)))
            .Returns("ActionId");

        var target = new CodeActionInfoFactory(
            tokenService.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                TokenLifetime = TimeSpan.FromMinutes(5),
            }));

        var result = target.Create(action, context.Object, roslyn.Document, new TextSpan(3, 4), descriptor);

        result.ActionId.Should().Be("ActionId");
        result.WorkspaceId.Should().Be("WorkspaceId");
        result.Title.Should().Be("Title");
        result.ProviderId.Should().Be("ProviderId");
        result.Kind.Should().Be("Refactoring");
        result.EquivalenceKey.Should().Be("EquivalenceKey");
        result.ActionPath.Should().Equal(1, 2);
        result.DiagnosticIds.Should().Equal("DiagnosticId");
        result.WorkspaceEpoch.Should().Be(1);
        result.TransactionRevision.Should().Be(2);
        result.ExpiresAt.Should().Be("2000-01-01T00:05:00.0000000+00:00");
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        result.ExecutorTool.Should().Be("ExecutorTool");
        result.DescribeTool.Should().Be("DescribeTool");
        result.UnsupportedReasonCode.Should().Be("UnsupportedReasonCode");
        result.Requirements.Should().Equal("Requirement");
        tokenService.Verify(item => item.Encode(It.IsAny<CodeActionTokenPayload>()), Times.Once);
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
            .Setup(item => item.Encode(It.Is<CodeActionTokenPayload>(payload =>
                payload.Kind == "CodeFix"
                && payload.DocumentPath == "NormalizedDocumentName"
                && payload.ProjectId == document.Project.Id.Id.ToString())))
            .Returns("ActionId");

        var target = new CodeActionInfoFactory(
            tokenService.Object,
            timeProvider.Object,
            Options.Create(new CodeActionExecutionOptions
            {
                TokenLifetime = TimeSpan.FromMinutes(5),
            }));

        var result = target.Create(action, context.Object, document, new TextSpan(0, 1), new CodeActionDescriptorEntry());

        document.FilePath.Should().BeNull();
        result.Kind.Should().Be("CodeFix");
        resolver.Verify(item => item.NormalizeDocumentPath("DocumentName.cs"), Times.Once);
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
