namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogStateTests
{
    [Fact]
    public void GIVEN_UnpublishedState_WHEN_Reading_THEN_ShouldReturnEmptySnapshot()
    {
        var target = new PluginCatalogState();

        var result = target.Current;

        result.Should().BeSameAs(PluginRuntimeCatalogSnapshot.Empty);
    }

    [Fact]
    public void GIVEN_Snapshot_WHEN_Publishing_THEN_ShouldExposeCompleteSnapshot()
    {
        var target = new PluginCatalogState();
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
        var target = new PluginCatalogState();
        target.Publish(new PluginRuntimeCatalogSnapshot());

        var action = () => target.Publish(new PluginRuntimeCatalogSnapshot());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been published*");
    }

    [Fact]
    public void GIVEN_UnpublishedSentinel_WHEN_Publishing_THEN_ShouldRejectInvalidState()
    {
        var target = new PluginCatalogState();

        var action = () => target.Publish(PluginRuntimeCatalogSnapshot.Empty);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*sentinel cannot be published*");
    }
}
