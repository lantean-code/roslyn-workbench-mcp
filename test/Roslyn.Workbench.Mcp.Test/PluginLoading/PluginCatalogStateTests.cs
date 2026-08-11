namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogStateTests
{
    [Fact]
    public void GIVEN_UnpublishedState_WHEN_Reading_THEN_ShouldReturnEmptySnapshot()
    {
        using var target = new PluginCatalogState();

        var result = target.Current;

        result.Should().BeSameAs(PluginRuntimeCatalogSnapshot.Empty);
    }

    [Fact]
    public void GIVEN_Snapshot_WHEN_Publishing_THEN_ShouldExposeCompleteSnapshot()
    {
        using var target = new PluginCatalogState();
        var snapshot = new PluginRuntimeCatalogSnapshot
        {
            Catalog = new PluginCatalogSnapshot
            {
                Plugins = [new PluginStatus { PluginId = "plugin" }],
            },
        };

        target.Publish(snapshot);

        target.Current.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void GIVEN_AlreadyPublishedState_WHEN_PublishingAgain_THEN_ShouldRejectInvalidLifecycle()
    {
        using var target = new PluginCatalogState();
        target.Publish(new PluginRuntimeCatalogSnapshot());

        var action = () => target.Publish(new PluginRuntimeCatalogSnapshot());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been published*");
    }

    [Fact]
    public void GIVEN_UnpublishedSentinel_WHEN_Publishing_THEN_ShouldRejectInvalidState()
    {
        using var target = new PluginCatalogState();

        var action = () => target.Publish(PluginRuntimeCatalogSnapshot.Empty);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*sentinel cannot be published*");
    }

    [Fact]
    public void GIVEN_PublishedPluginServices_WHEN_Disposing_THEN_ShouldDisposeInReverseOrderAndClearState()
    {
        var firstLifetime = new Mock<IDisposable>();
        var secondLifetime = new Mock<IDisposable>();
        var disposalOrder = new MockSequence();
        firstLifetime.InSequence(disposalOrder).Setup(item => item.Dispose());
        secondLifetime.InSequence(disposalOrder).Setup(item => item.Dispose());
        using var target = new PluginCatalogState();
        var snapshot = new PluginRuntimeCatalogSnapshot
        {
            Catalog = new PluginCatalogSnapshot
            {
                ServiceProviderLifetimes = [secondLifetime.Object, firstLifetime.Object],
            },
        };

        target.Publish(snapshot);

        target.Dispose();
        target.Dispose();

        target.Current.Should().BeSameAs(PluginRuntimeCatalogSnapshot.Empty);
        firstLifetime.Verify(item => item.Dispose(), Times.Once);
        secondLifetime.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_PluginServiceDisposalFails_WHEN_Disposing_THEN_ShouldDisposeRemainingProvidersAndAggregateFailure()
    {
        var failingLifetime = new Mock<IDisposable>();
        var remainingLifetime = new Mock<IDisposable>();
        failingLifetime
            .Setup(item => item.Dispose())
            .Throws(new InvalidOperationException("Disposal failed."));

        using var target = new PluginCatalogState();
        var snapshot = new PluginRuntimeCatalogSnapshot
        {
            Catalog = new PluginCatalogSnapshot
            {
                ServiceProviderLifetimes = [remainingLifetime.Object, failingLifetime.Object],
            },
        };

        target.Publish(snapshot);

        var action = () => target.Dispose();

        action.Should().Throw<AggregateException>()
            .WithMessage("One or more plugin service providers failed during disposal.*");

        remainingLifetime.Verify(item => item.Dispose(), Times.Once);
        target.Current.Should().BeSameAs(PluginRuntimeCatalogSnapshot.Empty);
    }

    [Fact]
    public async Task GIVEN_AsyncPluginServices_WHEN_DisposingAsync_THEN_ShouldDisposeInReverseOrderAndClearState()
    {
        var firstLifetime = new Mock<IDisposable>();
        var secondLifetime = new Mock<IDisposable>();
        var firstAsyncLifetime = firstLifetime.As<IAsyncDisposable>();
        var secondAsyncLifetime = secondLifetime.As<IAsyncDisposable>();
        var disposalOrder = new MockSequence();
        firstAsyncLifetime
            .InSequence(disposalOrder)
            .Setup(item => item.DisposeAsync())
            .Returns(ValueTask.CompletedTask);
        secondAsyncLifetime
            .InSequence(disposalOrder)
            .Setup(item => item.DisposeAsync())
            .Returns(ValueTask.CompletedTask);
        using var target = new PluginCatalogState();
        var snapshot = new PluginRuntimeCatalogSnapshot
        {
            Catalog = new PluginCatalogSnapshot
            {
                ServiceProviderLifetimes = [secondLifetime.Object, firstLifetime.Object],
            },
        };

        target.Publish(snapshot);

        await target.DisposeAsync();
        await target.DisposeAsync();

        target.Current.Should().BeSameAs(PluginRuntimeCatalogSnapshot.Empty);
        firstAsyncLifetime.Verify(item => item.DisposeAsync(), Times.Once);
        secondAsyncLifetime.Verify(item => item.DisposeAsync(), Times.Once);
        firstLifetime.Verify(item => item.Dispose(), Times.Never);
        secondLifetime.Verify(item => item.Dispose(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AsyncPluginServiceDisposalFails_WHEN_DisposingAsync_THEN_ShouldDisposeRemainingProvidersAndAggregateFailure()
    {
        var failingLifetime = new Mock<IDisposable>();
        var remainingLifetime = new Mock<IDisposable>();
        var failingAsyncLifetime = failingLifetime.As<IAsyncDisposable>();
        var remainingAsyncLifetime = remainingLifetime.As<IAsyncDisposable>();
        failingAsyncLifetime
            .Setup(item => item.DisposeAsync())
            .Returns(static () => ValueTask.FromException(new InvalidOperationException("Disposal failed.")));
        remainingAsyncLifetime
            .Setup(item => item.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        using var target = new PluginCatalogState();
        var snapshot = new PluginRuntimeCatalogSnapshot
        {
            Catalog = new PluginCatalogSnapshot
            {
                ServiceProviderLifetimes = [remainingLifetime.Object, failingLifetime.Object],
            },
        };

        target.Publish(snapshot);

        var action = async () => await target.DisposeAsync();

        await action.Should().ThrowAsync<AggregateException>()
            .WithMessage("One or more plugin service providers failed during disposal.*");

        remainingAsyncLifetime.Verify(item => item.DisposeAsync(), Times.Once);
        target.Current.Should().BeSameAs(PluginRuntimeCatalogSnapshot.Empty);
    }
}
