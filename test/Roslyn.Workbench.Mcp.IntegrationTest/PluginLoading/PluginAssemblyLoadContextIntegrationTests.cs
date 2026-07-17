using System.Composition;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginAssemblyLoadContextIntegrationTests
{
    [Fact]
    public void GIVEN_ExternalEntryAssembly_WHEN_CreatingLoadContext_THEN_ShouldLoadPluginWithSharedContractIdentity()
    {
        var entryAssemblyPath = typeof(HostValidQueryPlugin).Assembly.Location;
        var packageDirectory = new FileInfo(entryAssemblyPath).DirectoryName ?? string.Empty;
        packageDirectory.Should().NotBeEmpty();
        var packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        var containedEntryAssemblyPath = entryAssemblyPath;
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, entryAssemblyPath, out containedEntryAssemblyPath))
            .Returns(true);
        var factory = new PluginLoadContextFactory(packagePathPolicy.Object);

        var created = factory.TryCreate(packageDirectory, entryAssemblyPath, out var target);
        created.Should().BeTrue();
        var loadContext = target ?? throw new InvalidOperationException("The plugin load context was not created.");
        var pluginAssembly = loadContext.LoadFromAssemblyPath(entryAssemblyPath);
        var pluginType = pluginAssembly.GetType("Roslyn.Workbench.Mcp.TestSupport.HostValidQueryPlugin", true);

        loadContext.Should().BeOfType<PluginAssemblyLoadContext>();
        loadContext.IsCollectible.Should().BeFalse();
        pluginType.Should().BeAssignableTo<IRoslynPlugin>();
        loadContext.LoadFromAssemblyName(typeof(IRoslynPlugin).Assembly.GetName()).Should().BeSameAs(typeof(IRoslynPlugin).Assembly);
        loadContext.LoadFromAssemblyName(typeof(WorkspaceBoundRequest).Assembly.GetName()).Should().BeSameAs(typeof(WorkspaceBoundRequest).Assembly);
        loadContext.LoadFromAssemblyName(typeof(Compilation).Assembly.GetName()).Should().BeSameAs(typeof(Compilation).Assembly);
        loadContext.LoadFromAssemblyName(typeof(ExportAttribute).Assembly.GetName()).Should().BeSameAs(typeof(ExportAttribute).Assembly);
    }

    [Fact]
    public void GIVEN_UnloadedSharedAssemblyName_WHEN_Resolving_THEN_ShouldDelegateToDefaultContext()
    {
        var entryAssemblyPath = typeof(HostValidQueryPlugin).Assembly.Location;
        var packageDirectory = new FileInfo(entryAssemblyPath).DirectoryName ?? string.Empty;
        packageDirectory.Should().NotBeEmpty();
        var packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        var containedEntryAssemblyPath = entryAssemblyPath;
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, entryAssemblyPath, out containedEntryAssemblyPath))
            .Returns(true);
        var factory = new PluginLoadContextFactory(packagePathPolicy.Object);
        var created = factory.TryCreate(packageDirectory, entryAssemblyPath, out var target);
        created.Should().BeTrue();
        var loadContext = target ?? throw new InvalidOperationException("The plugin load context was not created.");

        var action = () => loadContext.LoadFromAssemblyName(new AssemblyName("Microsoft.CodeAnalysis.NotInstalled"));

        action.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void GIVEN_EntryAssemblyOutsidePackage_WHEN_CreatingLoadContext_THEN_ShouldRejectPath()
    {
        var packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        var rejectedPath = string.Empty;
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath("package", "outside.dll", out rejectedPath))
            .Returns(false);
        var target = new PluginLoadContextFactory(packagePathPolicy.Object);

        var result = target.TryCreate("package", "outside.dll", out var loadContext);

        result.Should().BeFalse();
        loadContext.Should().BeNull();
    }

    [Fact]
    public void GIVEN_PrivateDependencyInsidePackage_WHEN_Resolving_THEN_ShouldLoadFromPackage()
    {
        var packageDirectory = Path.Combine(AppContext.BaseDirectory, "PluginFixtureAssets", "HostQuery");
        var entryAssemblyPath = Path.Combine(packageDirectory, "Roslyn.Workbench.Mcp.HostQueryPluginFixture.dll");
        var dependencyPath = Path.Combine(packageDirectory, "NuGet.Versioning.dll");
        var packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        var containedEntryAssemblyPath = entryAssemblyPath;
        var containedDependencyPath = dependencyPath;
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, entryAssemblyPath, out containedEntryAssemblyPath))
            .Returns(true);
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, dependencyPath, out containedDependencyPath))
            .Returns(true);
        var factory = new PluginLoadContextFactory(packagePathPolicy.Object);
        var created = factory.TryCreate(packageDirectory, entryAssemblyPath, out var target);
        created.Should().BeTrue();
        var loadContext = target ?? throw new InvalidOperationException("The plugin load context was not created.");
        _ = loadContext.LoadFromAssemblyPath(entryAssemblyPath);

        var result = loadContext.LoadFromAssemblyName(new AssemblyName("NuGet.Versioning"));

        result.Location.Should().Be(dependencyPath);
    }

    [Fact]
    public void GIVEN_PrivateDependencyOutsidePackage_WHEN_Resolving_THEN_ShouldRejectPath()
    {
        var packageDirectory = Path.Combine(AppContext.BaseDirectory, "PluginFixtureAssets", "HostQuery");
        var entryAssemblyPath = Path.Combine(packageDirectory, "Roslyn.Workbench.Mcp.HostQueryPluginFixture.dll");
        var dependencyPath = Path.Combine(packageDirectory, "NuGet.Versioning.dll");
        var packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        var containedEntryAssemblyPath = entryAssemblyPath;
        var rejectedDependencyPath = string.Empty;
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, entryAssemblyPath, out containedEntryAssemblyPath))
            .Returns(true);
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, dependencyPath, out rejectedDependencyPath))
            .Returns(false);
        var factory = new PluginLoadContextFactory(packagePathPolicy.Object);
        var created = factory.TryCreate(packageDirectory, entryAssemblyPath, out var target);
        created.Should().BeTrue();
        var loadContext = target ?? throw new InvalidOperationException("The plugin load context was not created.");
        _ = loadContext.LoadFromAssemblyPath(entryAssemblyPath);

        var action = () => loadContext.LoadFromAssemblyName(new AssemblyName("NuGet.Versioning"));

        action.Should().Throw<FileLoadException>();
        packagePathPolicy.Verify(
            value => value.TryGetContainedPath(packageDirectory, dependencyPath, out rejectedDependencyPath),
            Times.Once);
    }

    [Fact]
    public void GIVEN_UnavailablePrivateAssembly_WHEN_Resolving_THEN_ShouldDelegateUnresolvedName()
    {
        var entryAssemblyPath = typeof(HostValidQueryPlugin).Assembly.Location;
        var packageDirectory = new FileInfo(entryAssemblyPath).DirectoryName ?? string.Empty;
        packageDirectory.Should().NotBeEmpty();
        var packagePathPolicy = new Mock<IPluginPackagePathPolicy>();
        var containedEntryAssemblyPath = entryAssemblyPath;
        packagePathPolicy
            .Setup(value => value.TryGetContainedPath(packageDirectory, entryAssemblyPath, out containedEntryAssemblyPath))
            .Returns(true);
        var factory = new PluginLoadContextFactory(packagePathPolicy.Object);
        var created = factory.TryCreate(packageDirectory, entryAssemblyPath, out var target);
        created.Should().BeTrue();
        var loadContext = target ?? throw new InvalidOperationException("The plugin load context was not created.");

        var action = () => loadContext.LoadFromAssemblyName(new AssemblyName("Plugin.Dependency.NotInstalled"));

        action.Should().Throw<FileNotFoundException>();
    }
}
