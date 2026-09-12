using Roslyn.Workbench.Mcp.Workspace.Authority;
using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Configuration;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceReadOnlyDocumentValidatorTests
{
    [Fact]
    public async Task GIVEN_RejectPolicyAndExternalDocument_WHEN_Validating_THEN_ShouldRejectWithoutReadingDisk()
    {
        var fileSystem = new Mock<IFileSystem>();
        var file = new Mock<IFile>();
        var pathContainment = new Mock<IPhysicalPathContainment>();
        var pathComparison = new Mock<IWorkspacePathComparison>();
        var authority = new Mock<IWorkspaceAuthority>();
        fileSystem.SetupGet(item => item.File).Returns(file.Object);
        pathContainment
            .Setup(item => item.TryGetContainedPath("/workspace", "/external/External.cs", out It.Ref<string>.IsAny))
            .Returns(false);
        authority.SetupGet(item => item.ExternalDocumentPolicy).Returns(ExternalDocumentPolicy.RejectWorkspace);
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp))
            .AddDocument(
                DocumentId.CreateNewId(projectId),
                "External.cs",
                SourceText.From("internal sealed class External { }"),
                filePath: "/external/External.cs");
        var target = new WorkspaceReadOnlyDocumentValidator(
            fileSystem.Object,
            pathContainment.Object,
            pathComparison.Object,
            authority.Object);

        var result = await target.ValidateAsync(solution, "/workspace", TestContext.Current.CancellationToken);

        result.Should().Be(WorkspaceReadOnlyDocumentValidationStatus.Rejected);
        file.Verify(item => item.OpenRead(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AllowPolicyAndMatchingExternalDocument_WHEN_Validating_THEN_ShouldCertifyDiskText()
    {
        const string externalPath = "/external/External.cs";
        const string source = "internal sealed class External { }";
        var fileSystem = new Mock<IFileSystem>();
        var file = new Mock<IFile>();
        var pathContainment = new Mock<IPhysicalPathContainment>();
        var pathComparison = new Mock<IWorkspacePathComparison>();
        var authority = new Mock<IWorkspaceAuthority>();
        fileSystem.SetupGet(item => item.File).Returns(file.Object);
        file.Setup(item => item.OpenRead(externalPath)).Returns(() =>
        {
            var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(source));
            return new Mock<FileSystemStream>(memoryStream, externalPath, false) { CallBase = true }.Object;
        });
        pathContainment
            .Setup(item => item.TryGetContainedPath("/workspace", externalPath, out It.Ref<string>.IsAny))
            .Returns(false);
        pathComparison.Setup(item => item.CreateKey(externalPath)).Returns(new FileSystemPathKey(externalPath, isCaseSensitive: true));
        authority.SetupGet(item => item.ExternalDocumentPolicy).Returns(ExternalDocumentPolicy.AllowReadOnly);
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp))
            .AddDocument(DocumentId.CreateNewId(projectId), "External.cs", SourceText.From(source), filePath: externalPath);
        var target = new WorkspaceReadOnlyDocumentValidator(
            fileSystem.Object,
            pathContainment.Object,
            pathComparison.Object,
            authority.Object);

        var result = await target.ValidateAsync(solution, "/workspace", TestContext.Current.CancellationToken);

        result.Should().Be(WorkspaceReadOnlyDocumentValidationStatus.Valid);
        file.Verify(item => item.OpenRead(externalPath), Times.Once);
    }
}
