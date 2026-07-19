namespace Roslyn.Workbench.Mcp.Workspace.Test;

[Trait("Category", "Integration")]
public sealed class WorkspaceAssetMaterializerIntegrationTests
{
    [Fact]
    public async Task GIVEN_TemplateWithBinaryAndExcludedDirectories_WHEN_Materializing_THEN_ShouldCopyExactBytesAndExcludeGeneratedState()
    {
        using var template = TemporaryDirectory.Create("roslyn-workbench-mcp-asset-template-tests");
        var nestedDirectory = Path.Combine(template.DirectoryPath, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var expectedBytes = new byte[] { 0, 1, 2, 127, 128, 255 };
        await File.WriteAllBytesAsync(Path.Combine(nestedDirectory, "sample.bin"), expectedBytes, TestContext.Current.CancellationToken);
        foreach (var excludedDirectoryName in new[] { ".vs", "bin", "obj", "recovery" })
        {
            var excludedDirectory = Path.Combine(nestedDirectory, excludedDirectoryName);
            Directory.CreateDirectory(excludedDirectory);
            await File.WriteAllTextAsync(Path.Combine(excludedDirectory, "excluded.txt"), "excluded", TestContext.Current.CancellationToken);
        }

        string scenarioRoot;
        using (var target = WorkspaceAssetMaterializer.MaterializeFromTemplateRoot(template.DirectoryPath))
        {
            scenarioRoot = Path.GetDirectoryName(target.WorkspaceRoot)!;

            Directory.Exists(target.WorkspaceRoot).Should().BeTrue();
            Directory.Exists(target.StateRoot).Should().BeTrue();
            Directory.Exists(Path.Combine(target.WorkspaceRoot, ".git")).Should().BeTrue();
            (await File.ReadAllBytesAsync(Path.Combine(target.WorkspaceRoot, "nested", "sample.bin"), TestContext.Current.CancellationToken)).Should().Equal(expectedBytes);
            Directory.EnumerateDirectories(Path.Combine(target.WorkspaceRoot, "nested")).Should().BeEmpty();
        }

        Directory.Exists(scenarioRoot).Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_TemplateAndProfile_WHEN_Materializing_THEN_ShouldOverlayAndDeleteProfiledFiles()
    {
        using var template = TemporaryDirectory.Create("roslyn-workbench-mcp-asset-template-tests");
        using var profile = TemporaryDirectory.Create("roslyn-workbench-mcp-asset-profile-tests");
        await File.WriteAllTextAsync(Path.Combine(template.DirectoryPath, "overlaid.txt"), "base", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(template.DirectoryPath, "deleted.txt"), "deleted", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(profile.DirectoryPath, "overlaid.txt"), "profile", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(profile.DirectoryPath, ".asset-delete"), "deleted.txt", TestContext.Current.CancellationToken);

        using var target = WorkspaceAssetMaterializer.MaterializeFromTemplateRoots(template.DirectoryPath, profile.DirectoryPath);

        (await File.ReadAllTextAsync(Path.Combine(target.WorkspaceRoot, "overlaid.txt"), TestContext.Current.CancellationToken)).Should().Be("profile");
        File.Exists(Path.Combine(target.WorkspaceRoot, "deleted.txt")).Should().BeFalse();
        File.Exists(Path.Combine(target.WorkspaceRoot, ".asset-delete")).Should().BeFalse();
    }
}
