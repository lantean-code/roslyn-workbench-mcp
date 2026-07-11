using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public void GIVEN_NullFileSystem_WHEN_Constructing_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => new AtomicFileWriter(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GIVEN_InvalidDestinationPath_WHEN_Writing_THEN_ShouldThrowArgumentException(string destinationPath)
    {
        var target = new AtomicFileWriter(new Mock<IFileSystem>().Object);

        var action = async () => await target.WriteAllTextAsync(
            destinationPath,
            "Contents",
            Encoding.UTF8,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GIVEN_NullContents_WHEN_Writing_THEN_ShouldThrowArgumentNullException()
    {
        var target = new AtomicFileWriter(new Mock<IFileSystem>().Object);

        var action = async () => await target.WriteAllTextAsync(
            "DestinationPath",
            null!,
            Encoding.UTF8,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_NullEncoding_WHEN_Writing_THEN_ShouldThrowArgumentNullException()
    {
        var target = new AtomicFileWriter(new Mock<IFileSystem>().Object);

        var action = async () => await target.WriteAllTextAsync(
            "DestinationPath",
            "Contents",
            null!,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Writing_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var target = new AtomicFileWriter(new Mock<IFileSystem>().Object);

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
        var target = new AtomicFileWriter(fileSystem.Object);

        var action = async () => await target.WriteAllTextAsync(
            "DestinationPath",
            "Contents",
            Encoding.UTF8,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }
}
