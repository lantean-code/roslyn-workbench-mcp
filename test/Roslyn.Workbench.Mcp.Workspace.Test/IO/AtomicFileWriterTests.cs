using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class AtomicFileWriterTests : IDisposable
{
    private const string _destinationPath = "/Directory/File.txt";

    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IPath> _path;
    private readonly Mock<IFileStreamFactory> _fileStreamFactory;
    private readonly Mock<IFile> _file;
    private readonly Mock<IAtomicFileCommitter> _fileCommitter;
    private readonly MemoryStream _memoryStream;
    private readonly AtomicFileWriter _target;

    public AtomicFileWriterTests()
    {
        _fileSystem = new Mock<IFileSystem>();
        _path = new Mock<IPath>();
        _fileStreamFactory = new Mock<IFileStreamFactory>();
        _file = new Mock<IFile>();
        _fileCommitter = new Mock<IAtomicFileCommitter>();
        _memoryStream = new MemoryStream();
        var stream = new Mock<FileSystemStream>(_memoryStream, "TemporaryPath", false) { CallBase = true };
        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _fileSystem.SetupGet(item => item.FileStream).Returns(_fileStreamFactory.Object);
        _fileSystem.SetupGet(item => item.File).Returns(_file.Object);
        _path.Setup(item => item.GetDirectoryName(_destinationPath)).Returns("/Directory");
        _path.Setup(item => item.GetFileName(_destinationPath)).Returns("File.txt");
        _path.Setup(item => item.Combine("/Directory", It.IsAny<string>()))
            .Returns((string directory, string fileName) => directory + "/" + fileName);
        _fileStreamFactory.Setup(item => item.New(It.IsAny<string>(), It.IsAny<FileStreamOptions>()))
            .Returns(stream.Object);
        _target = new AtomicFileWriter(_fileSystem.Object, _fileCommitter.Object);
    }

    public void Dispose()
    {
        _memoryStream.Dispose();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GIVEN_InvalidDestinationPath_WHEN_WritingText_THEN_ShouldThrowArgumentException(string destinationPath)
    {
        var action = async () => await _target.WriteAllTextAsync(
            destinationPath,
            "Contents",
            Encoding.UTF8,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GIVEN_InvalidDestinationPath_WHEN_WritingBytes_THEN_ShouldThrowArgumentException(string destinationPath)
    {
        var action = async () => await _target.WriteAllBytesAsync(
            destinationPath,
            new byte[] { 1 },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Writing_THEN_ShouldPropagateCancellationWithoutCreatingTemporaryFile()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.WriteAllTextAsync(
            _destinationPath,
            "Contents",
            Encoding.UTF8,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _fileStreamFactory.Verify(item => item.New(It.IsAny<string>(), It.IsAny<FileStreamOptions>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DestinationWithoutParentDirectory_WHEN_Writing_THEN_ShouldThrowArgumentException()
    {
        _path.Setup(item => item.GetDirectoryName(_destinationPath)).Returns((string?)null);

        var action = async () => await _target.WriteAllBytesAsync(
            _destinationPath,
            new byte[] { 1 },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GIVEN_TextAndEncoding_WHEN_Writing_THEN_ShouldWriteEncodedBytesAndCommitTemporaryFile()
    {
        var encoding = Encoding.Unicode;

        await _target.WriteAllTextAsync(
            _destinationPath,
            "Contents",
            encoding,
            TestContext.Current.CancellationToken);

        _memoryStream.ToArray().Should().Equal(encoding.GetBytes("Contents"));
        _fileStreamFactory.Verify(item => item.New(
            It.Is<string>(path => path.StartsWith("/Directory/.File.txt.", StringComparison.Ordinal) && path.EndsWith(".tmp", StringComparison.Ordinal)),
            It.Is<FileStreamOptions>(options =>
                options.Access == FileAccess.Write
                && options.Mode == FileMode.CreateNew
                && options.Options == (FileOptions.Asynchronous | FileOptions.WriteThrough)
                && options.Share == FileShare.None)), Times.Once);
        _fileCommitter.Verify(item => item.Commit(
            It.Is<string>(path => path.StartsWith("/Directory/.File.txt.", StringComparison.Ordinal) && path.EndsWith(".tmp", StringComparison.Ordinal)),
            _destinationPath), Times.Once);
    }

    [Fact]
    public async Task GIVEN_BinaryContents_WHEN_Writing_THEN_ShouldWriteExactBytesAndCommitTemporaryFile()
    {
        var contents = new byte[] { 0, 1, 255 };

        await _target.WriteAllBytesAsync(_destinationPath, contents, TestContext.Current.CancellationToken);

        _memoryStream.ToArray().Should().Equal(contents);
        _fileCommitter.Verify(item => item.Commit(It.IsAny<string>(), _destinationPath), Times.Once);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("delete")]
    [InlineData("io")]
    [InlineData("access")]
    public async Task GIVEN_CommitFailure_WHEN_CleaningTemporaryFile_THEN_ShouldPreserveCommitFailure(string cleanupOutcome)
    {
        var expected = new InvalidOperationException("CommitFailure");
        _fileCommitter.Setup(item => item.Commit(It.IsAny<string>(), _destinationPath)).Throws(expected);
        _file.Setup(item => item.Exists(It.IsAny<string>())).Returns(cleanupOutcome != "missing");
        if (cleanupOutcome == "io")
        {
            _file.Setup(item => item.Delete(It.IsAny<string>())).Throws(new IOException());
        }
        else if (cleanupOutcome == "access")
        {
            _file.Setup(item => item.Delete(It.IsAny<string>())).Throws(new UnauthorizedAccessException());
        }

        var action = async () => await _target.WriteAllBytesAsync(
            _destinationPath,
            new byte[] { 1 },
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expected);
        var expectedDeletes = cleanupOutcome == "missing" ? Times.Never() : Times.Once();
        _file.Verify(item => item.Delete(It.IsAny<string>()), expectedDeletes);
    }
}
