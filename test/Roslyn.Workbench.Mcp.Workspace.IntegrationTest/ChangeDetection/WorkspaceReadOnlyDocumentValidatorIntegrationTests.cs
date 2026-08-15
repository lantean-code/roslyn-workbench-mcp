namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceReadOnlyDocumentValidatorIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_MatchingExternalDocuments_WHEN_Validating_THEN_ShouldReturnValid()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-read-only-document-tests");
        var workspaceRoot = Path.Combine(directory.DirectoryPath, "workspace");
        var externalRoot = Path.Combine(directory.DirectoryPath, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);

        var projectPath = Path.Combine(workspaceRoot, "Sample.csproj");
        var internalPath = Path.Combine(workspaceRoot, "Internal.cs");
        var externalPath = Path.Combine(externalRoot, "External.cs");
        await File.WriteAllTextAsync(internalPath, "internal sealed class Internal { }", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(externalPath, "internal sealed class External { }", TestContext.Current.CancellationToken);

        using var workspace = new AdhocWorkspace();
        var firstProjectId = ProjectId.CreateNewId();
        var secondProjectId = ProjectId.CreateNewId();
        var externalText = SourceText.From(await File.ReadAllTextAsync(externalPath, TestContext.Current.CancellationToken));
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(firstProjectId, VersionStamp.Create(), "First", "First", LanguageNames.CSharp, filePath: projectPath))
            .AddProject(ProjectInfo.Create(secondProjectId, VersionStamp.Create(), "Second", "Second", LanguageNames.CSharp, filePath: projectPath))
            .AddDocument(DocumentId.CreateNewId(firstProjectId), "InMemory.cs", SourceText.From("internal sealed class InMemory { }"))
            .AddDocument(DocumentId.CreateNewId(firstProjectId), "Internal.cs", SourceText.From("internal sealed class Internal { }"), filePath: internalPath)
            .AddDocument(DocumentId.CreateNewId(firstProjectId), "External.cs", externalText, filePath: externalPath)
            .AddDocument(DocumentId.CreateNewId(secondProjectId), "External.cs", externalText, filePath: externalPath);

        var target = CreateTarget();

        var result = await target.ValidateAsync(solution, workspaceRoot, TestContext.Current.CancellationToken);

        result.Should().Be(WorkspaceReadOnlyDocumentValidationStatus.Valid);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_CaseSensitiveExternalPathsAndCaseInsensitiveWorkspace_WHEN_Validating_THEN_ShouldReadBothFiles()
    {
        var pathComparison = new WorkspacePathComparison();
        var workspaceRoot = "/mnt/c/Workspace";
        if (!pathComparison.IsWindowsFileSystemPath(workspaceRoot))
        {
            return;
        }

        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-read-only-document-tests");
        var upperCaseExternalPath = Path.Combine(directory.DirectoryPath, "External.cs");
        var lowerCaseExternalPath = Path.Combine(directory.DirectoryPath, "external.cs");
        await File.WriteAllTextAsync(upperCaseExternalPath, "internal sealed class Upper { }", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(lowerCaseExternalPath, "internal sealed class Lower { }", TestContext.Current.CancellationToken);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp, filePath: "/mnt/c/Workspace/Sample.csproj"))
            .AddDocument(DocumentId.CreateNewId(projectId), "Upper.cs", SourceText.From("internal sealed class Upper { }"), filePath: upperCaseExternalPath)
            .AddDocument(DocumentId.CreateNewId(projectId), "Lower.cs", SourceText.From("internal sealed class Lower { }"), filePath: lowerCaseExternalPath);

        var target = CreateTarget();

        var result = await target.ValidateAsync(solution, workspaceRoot, TestContext.Current.CancellationToken);

        result.Should().Be(WorkspaceReadOnlyDocumentValidationStatus.Valid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ExternalDocumentDoesNotMatchDisk_WHEN_Validating_THEN_ShouldRejectIt(bool fileExists)
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-read-only-document-tests");
        var workspaceRoot = Path.Combine(directory.DirectoryPath, "workspace");
        var externalRoot = Path.Combine(directory.DirectoryPath, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);

        var projectPath = Path.Combine(workspaceRoot, "Sample.csproj");
        var externalPath = Path.Combine(externalRoot, "External.cs");
        if (fileExists)
        {
            await File.WriteAllTextAsync(externalPath, "internal sealed class Changed { }", TestContext.Current.CancellationToken);
        }

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp, filePath: projectPath))
            .AddDocument(DocumentId.CreateNewId(projectId), "External.cs", SourceText.From("internal sealed class Original { }"), filePath: externalPath);

        var target = CreateTarget();

        var result = await target.ValidateAsync(solution, workspaceRoot, TestContext.Current.CancellationToken);

        result.Should().Be(WorkspaceReadOnlyDocumentValidationStatus.Invalid);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_CancelledToken_WHEN_Validating_THEN_ShouldPropagateCancellation()
    {
        using var workspace = new AdhocWorkspace();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var target = CreateTarget();

        var action = async () => await target.ValidateAsync(workspace.CurrentSolution, "/workspace", cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static WorkspaceReadOnlyDocumentValidator CreateTarget()
    {
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathContainment = new PhysicalPathContainment(fileSystem, pathComparison);
        return new WorkspaceReadOnlyDocumentValidator(fileSystem, pathContainment, pathComparison);
    }
}
