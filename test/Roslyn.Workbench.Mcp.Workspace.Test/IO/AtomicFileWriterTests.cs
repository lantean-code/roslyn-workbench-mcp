using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class AtomicFileWriterTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GIVEN_InvalidDestinationPath_WHEN_Writing_THEN_ShouldThrowArgumentException(string destinationPath)
    {
        var target = new AtomicFileWriter(new Mock<IFileSystem>().Object, new Mock<IAtomicFileCommitter>().Object);

        var action = async () => await target.WriteAllTextAsync(
            destinationPath,
            "Contents",
            Encoding.UTF8,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Writing_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var target = new AtomicFileWriter(new Mock<IFileSystem>().Object, new Mock<IAtomicFileCommitter>().Object);

        var action = async () => await target.WriteAllTextAsync(
            "DestinationPath",
            "Contents",
            Encoding.UTF8,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_DestinationWithoutParentDirectory_WHEN_Writing_THEN_ShouldThrowArgumentException()
    {
        var fileSystem = new Mock<IFileSystem>();
        var path = new Mock<IPath>();
        fileSystem.SetupGet(item => item.Path).Returns(path.Object);
        path.Setup(item => item.GetDirectoryName("DestinationPath")).Returns((string?)null);
        var target = new AtomicFileWriter(fileSystem.Object, new Mock<IAtomicFileCommitter>().Object);

        var action = async () => await target.WriteAllTextAsync(
            "DestinationPath",
            "Contents",
            Encoding.UTF8,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }
}
