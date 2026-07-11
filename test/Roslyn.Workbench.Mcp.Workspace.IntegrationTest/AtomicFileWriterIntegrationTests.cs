using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class AtomicFileWriterIntegrationTests
{
    [Fact]
    public async Task GIVEN_ExistingDestination_WHEN_WritingAtomically_THEN_ShouldReplaceContentWithoutLeavingTemporaryFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-atomic-writer-tests", Guid.NewGuid().ToString("n"));
        var destinationPath = Path.Combine(directoryPath, "Status.json");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(destinationPath, "Before", TestContext.Current.CancellationToken);
        var target = new AtomicFileWriter(new FileSystem(), new NativeAtomicFileCommitter());

        try
        {
            await target.WriteAllTextAsync(destinationPath, "After", Encoding.UTF8, TestContext.Current.CancellationToken);

            (await File.ReadAllTextAsync(destinationPath, TestContext.Current.CancellationToken)).Should().Be("After");
            Directory.EnumerateFiles(directoryPath, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task GIVEN_ReplacementFailure_WHEN_WritingAtomically_THEN_ShouldRemoveTemporaryFile()
    {
        var parentPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-atomic-writer-tests", Guid.NewGuid().ToString("n"));
        var destinationDirectoryPath = Path.Combine(parentPath, "Destination");
        Directory.CreateDirectory(destinationDirectoryPath);
        var target = new AtomicFileWriter(new FileSystem(), new NativeAtomicFileCommitter());

        try
        {
            var action = async () => await target.WriteAllTextAsync(
                destinationDirectoryPath,
                "Contents",
                Encoding.UTF8,
                TestContext.Current.CancellationToken);

            await action.Should().ThrowAsync<IOException>();
            Directory.EnumerateFiles(parentPath, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(parentPath, recursive: true);
        }
    }
}
