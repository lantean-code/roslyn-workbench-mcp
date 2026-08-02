namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

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
            await target.WriteAllBytesAsync(
                destinationPath,
                expected,
                AtomicFileAccess.Default,
                TestContext.Current.CancellationToken);

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
            await target.WriteAllBytesAsync(
                destinationPath,
                expected,
                AtomicFileAccess.Default,
                TestContext.Current.CancellationToken);

            (await File.ReadAllBytesAsync(destinationPath, TestContext.Current.CancellationToken)).Should().Equal(expected);
            Directory.EnumerateFiles(directoryPath, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task GIVEN_PrivateDestination_WHEN_WritingAtomically_THEN_ShouldCreateOwnerOnlyUnixFile()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-atomic-writer-tests",
            Guid.NewGuid().ToString("n"));

        var destinationPath = Path.Combine(directoryPath, "Status.json");
        Directory.CreateDirectory(directoryPath);
        var target = new AtomicFileWriter(
            new FileSystem(),
            new NativeAtomicFileCommitter());

        try
        {
            await target.WriteAllBytesAsync(
                destinationPath,
                new byte[] { 1 },
                AtomicFileAccess.OwnerOnly,
                TestContext.Current.CancellationToken);

            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(destinationPath).Should().Be(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ExistingUnixDestination_WHEN_ReplacingWithExplicitMode_THEN_ShouldPreserveMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-atomic-writer-tests",
            Guid.NewGuid().ToString("n"));

        var destinationPath = Path.Combine(directoryPath, "Executable.sh");
        var expectedMode = UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead;

        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(destinationPath, [0x00], TestContext.Current.CancellationToken);
        File.SetUnixFileMode(destinationPath, expectedMode);
        var target = new AtomicFileWriter(new FileSystem(), new NativeAtomicFileCommitter());

        try
        {
            await target.WriteAllBytesAsync(
                destinationPath,
                new byte[] { 0x01 },
                AtomicFileAccess.Default,
                expectedMode,
                TestContext.Current.CancellationToken);

            File.GetUnixFileMode(destinationPath).Should().Be(expectedMode);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ExistingWindowsDestination_WHEN_Replacing_THEN_ShouldPreserveCreationTime()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-atomic-writer-tests",
            Guid.NewGuid().ToString("n"));

        var destinationPath = Path.Combine(directoryPath, "Source.cs");
        var expectedCreationTime = new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc);
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(destinationPath, [0x00], TestContext.Current.CancellationToken);
        File.SetCreationTimeUtc(destinationPath, expectedCreationTime);
        var target = new AtomicFileWriter(new FileSystem(), new NativeAtomicFileCommitter());

        try
        {
            await target.WriteAllBytesAsync(
                destinationPath,
                new byte[] { 0x01 },
                AtomicFileAccess.Default,
                TestContext.Current.CancellationToken);

            File.GetCreationTimeUtc(destinationPath).Should().Be(expectedCreationTime);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task GIVEN_TemporaryPathExceedsWindowsMaxPath_WHEN_WritingAtomically_THEN_ShouldReplaceExactBytes()
    {
        var rootDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-atomic-writer-tests",
            Guid.NewGuid().ToString("n"));

        var directoryPath = rootDirectoryPath;

        while (Path.Combine(directoryPath, ".Status.json.00000000000000000000000000000000.tmp").Length < 260)
        {
            directoryPath = Path.Combine(directoryPath, "LongDirectorySegment");
        }

        var destinationPath = Path.Combine(directoryPath, "Status.json");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllBytesAsync(destinationPath, [0x00, 0x01], TestContext.Current.CancellationToken);

        var target = new AtomicFileWriter(new FileSystem(), new NativeAtomicFileCommitter());
        byte[] expected = [0xef, 0xbb, 0xbf, 0x7b, 0x7d];

        try
        {
            await target.WriteAllBytesAsync(
                destinationPath,
                expected,
                AtomicFileAccess.Default,
                TestContext.Current.CancellationToken);

            (await File.ReadAllBytesAsync(destinationPath, TestContext.Current.CancellationToken)).Should().Equal(expected);
            Directory.EnumerateFiles(directoryPath, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(rootDirectoryPath, recursive: true);
        }
    }
}
