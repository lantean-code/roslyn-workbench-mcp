namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class AtomicFileWriterIntegrationTests
{
    [Fact]
    public async Task GIVEN_ExistingDestination_WHEN_WritingAtomically_THEN_ShouldReplaceExactBytesWithoutLeavingTemporaryFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-atomic-writer-tests", Guid.NewGuid().ToString("n"));
        var destinationPath = Path.Combine(directoryPath, "Status.json");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(destinationPath, [0x00, 0x01], TestContext.Current.CancellationToken);
        var target = new AtomicFileWriter(new FileSystem(), new NativeAtomicFileCommitter());
        byte[] expected = [0x00, 0xff, 0x80, 0x0d, 0x0a];

        try
        {
            await target.WriteAllBytesAsync(destinationPath, expected, TestContext.Current.CancellationToken);

            (await File.ReadAllBytesAsync(destinationPath, TestContext.Current.CancellationToken)).Should().Equal(expected);
            Directory.EnumerateFiles(directoryPath, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task GIVEN_MissingDestination_WHEN_WritingAtomically_THEN_ShouldCreateExactBytesWithoutLeavingTemporaryFile()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-atomic-writer-tests", Guid.NewGuid().ToString("n"));
        var destinationPath = Path.Combine(directoryPath, "Status.json");
        Directory.CreateDirectory(directoryPath);
        var target = new AtomicFileWriter(new FileSystem(), new NativeAtomicFileCommitter());
        byte[] expected = [0xef, 0xbb, 0xbf, 0x7b, 0x7d];

        try
        {
            await target.WriteAllBytesAsync(destinationPath, expected, TestContext.Current.CancellationToken);

            (await File.ReadAllBytesAsync(destinationPath, TestContext.Current.CancellationToken)).Should().Equal(expected);
            Directory.EnumerateFiles(directoryPath, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
