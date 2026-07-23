using System.Reflection;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCandidatePreparerTests
{
    private readonly Mock<IPluginAssemblyMetadataReader> _metadataReader;
    private readonly Mock<IPluginEntryPointValidator> _entryPointValidator;
    private readonly Mock<ILoadedPluginPreparer> _loadedPluginPreparer;
    private readonly Mock<IPluginLoadContextFactory> _loadContextFactory;
    private readonly PluginCandidatePreparer _target;

    public PluginCandidatePreparerTests()
    {
        _metadataReader = new Mock<IPluginAssemblyMetadataReader>();
        _entryPointValidator = new Mock<IPluginEntryPointValidator>();
        _loadedPluginPreparer = new Mock<ILoadedPluginPreparer>();
        _loadContextFactory = new Mock<IPluginLoadContextFactory>();
        _target = new PluginCandidatePreparer(
            _metadataReader.Object,
            _entryPointValidator.Object,
            _loadedPluginPreparer.Object,
            _loadContextFactory.Object);
    }

    [Fact]
    public void GIVEN_ValidBundledAssembly_WHEN_Preparing_THEN_ShouldDelegateLoadedPluginPreparation()
    {
        var assembly = typeof(BundledCorePlugin).Assembly;
        var entryPoint = CreateEntryPoint("bundled");
        var preparedPlugin = CreatePreparedPlugin(entryPoint);
        _metadataReader.Setup(value => value.Inspect(assembly.Location)).Returns(CreateInspection(entryPoint));
        _loadedPluginPreparer.Setup(value => value.Prepare(assembly, entryPoint)).Returns(preparedPlugin);

        var result = _target.PrepareBundled([assembly]);

        result.Plugins.Should().ContainSingle().Which.Should().BeSameAs(preparedPlugin);
        result.Statuses.Should().BeEmpty();
        result.LoadContexts.Should().BeEmpty();
        _loadedPluginPreparer.Verify(value => value.Prepare(assembly, entryPoint), Times.Once);
    }

    [Theory]
    [InlineData(true, 0, "Malformed")]
    [InlineData(false, 0, "exactly one")]
    [InlineData(false, 2, "exactly one")]
    public void GIVEN_InvalidBundledInspection_WHEN_Preparing_THEN_ShouldDisableAssembly(
        bool hasError,
        int entryPointCount,
        string expectedMessage)
    {
        var assembly = typeof(BundledCorePlugin).Assembly;
        _metadataReader.Setup(value => value.Inspect(assembly.Location)).Returns(new PluginAssemblyInspection
        {
            Error = hasError ? "Malformed" : null,
            EntryPoints = Enumerable.Range(0, entryPointCount).Select(index => CreateEntryPoint($"plugin-{index}")).ToArray(),
        });

        var result = _target.PrepareBundled([assembly]);

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginDiscovery"
                && diagnostic.Message.Contains(expectedMessage, StringComparison.Ordinal)));

        _loadedPluginPreparer.Verify(static value => value.Prepare(It.IsAny<Assembly>(), It.IsAny<PluginEntryPointMetadata>()), Times.Never);
    }

    [Fact]
    public void GIVEN_BundledAssemblyWithoutIdentity_WHEN_PreparingInvalidInspection_THEN_ShouldUseFallbackIdentity()
    {
        var assembly = new Mock<Assembly>();
        assembly.SetupGet(static value => value.Location).Returns("AssemblyLocation");
        assembly.Setup(static value => value.GetName()).Returns(new AssemblyName());
        _metadataReader.Setup(static value => value.Inspect("AssemblyLocation")).Returns(new PluginAssemblyInspection());

        var result = _target.PrepareBundled([assembly.Object]);

        result.Statuses.Should().ContainSingle(status => status.PluginId == "bundled-plugin");
    }

    [Fact]
    public void GIVEN_InvalidBundledEntryPoint_WHEN_Preparing_THEN_ShouldDisableBeforePreparation()
    {
        var assembly = typeof(BundledCorePlugin).Assembly;
        var entryPoint = CreateEntryPoint("PluginId");
        _metadataReader.Setup(value => value.Inspect(assembly.Location)).Returns(CreateInspection(entryPoint));
        _entryPointValidator.Setup(value => value.GetValidationError(entryPoint)).Returns("Validation failed");

        var result = _target.PrepareBundled([assembly]);

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginMetadata"
                && diagnostic.Message == "Validation failed"));

        _loadedPluginPreparer.Verify(static value => value.Prepare(It.IsAny<Assembly>(), It.IsAny<PluginEntryPointMetadata>()), Times.Never);
    }

    [Fact]
    public void GIVEN_LoadedBundledPluginPreparationFails_WHEN_Preparing_THEN_ShouldDisableAssembly()
    {
        var assembly = typeof(BundledCorePlugin).Assembly;
        _metadataReader.Setup(value => value.Inspect(assembly.Location)).Returns(CreateInspection(CreateEntryPoint("bundled")));
        _loadedPluginPreparer.Setup(value => value.Prepare(assembly, It.IsAny<PluginEntryPointMetadata>()))
            .Throws(new InvalidOperationException("Configuration failed"));

        var result = _target.PrepareBundled([assembly]);

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginLoad"
                && diagnostic.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
                && !diagnostic.Message.Contains("Configuration failed", StringComparison.Ordinal)));
    }

    [Fact]
    public void GIVEN_BundledPluginHasPreparationErrors_WHEN_Preparing_THEN_ShouldDisableWithEveryDiagnostic()
    {
        var assembly = typeof(BundledCorePlugin).Assembly;
        var entryPoint = CreateEntryPoint("bundled");
        var diagnostics = new[]
        {
            CreateDiagnostic("PluginHandlerContract", DiagnosticSeverity.Error, "Contract failed"),
            CreateDiagnostic("PluginHandlerState", DiagnosticSeverity.Warning, "State warning"),
        };

        var preparedPlugin = CreatePreparedPlugin(entryPoint, diagnostics);
        _metadataReader.Setup(value => value.Inspect(assembly.Location)).Returns(CreateInspection(entryPoint));
        _loadedPluginPreparer.Setup(value => value.Prepare(assembly, entryPoint)).Returns(preparedPlugin);

        var result = _target.PrepareBundled([assembly]);

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.SequenceEqual(diagnostics));
    }

    [Theory]
    [InlineData("Discovery failed", "Discovery failed")]
    [InlineData(null, "Plugin package discovery failed.")]
    public void GIVEN_DiscoveryFailure_WHEN_PreparingExternal_THEN_ShouldPublishFallbackStatus(
        string? error,
        string expectedMessage)
    {
        var discoveryResult = new PluginPackageDiscoveryResult
        {
            FallbackIdentity = "package",
            Error = error,
        };

        var result = _target.PrepareExternal([discoveryResult], new HashSet<string>(StringComparer.Ordinal));

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            status.PluginId == "package"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginDiscovery"
                && diagnostic.Message == expectedMessage));
    }

    [Fact]
    public void GIVEN_InvalidExternalEntryPoint_WHEN_Preparing_THEN_ShouldDisableBeforeLoadingCode()
    {
        var entryPoint = CreateEntryPoint("external", version: "invalid");
        var discoveryResult = CreateDiscoveryResult(entryPoint);
        _entryPointValidator.Setup(value => value.GetValidationError(entryPoint)).Returns("Validation failed");

        var result = _target.PrepareExternal([discoveryResult], new HashSet<string>(StringComparer.Ordinal));

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.Any(static diagnostic => diagnostic.Id == "PluginMetadata"));

        _loadContextFactory.Verify(
            static value => value.TryCreate(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out It.Ref<AssemblyLoadContext?>.IsAny),
            Times.Never);
    }

    [Fact]
    public void GIVEN_DuplicateExternalIdentity_WHEN_Preparing_THEN_ShouldDisableBeforeLoadingCode()
    {
        var discoveryResult = CreateDiscoveryResult(CreateEntryPoint("duplicate"));

        var result = _target.PrepareExternal(
            [discoveryResult],
            new HashSet<string>(["duplicate"], StringComparer.Ordinal));

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginCollision"
                && diagnostic.Message.Contains("same plugin ID", StringComparison.Ordinal)));

        _loadContextFactory.Verify(
            static value => value.TryCreate(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out It.Ref<AssemblyLoadContext?>.IsAny),
            Times.Never);
    }

    [Fact]
    public void GIVEN_ValidExternalPackage_WHEN_Preparing_THEN_ShouldRetainLoadContextAndPreparePlugin()
    {
        var entryPoint = CreateEntryPoint("external");
        var discoveryResult = CreateDiscoveryResult(entryPoint);
        var preparedPlugin = CreatePreparedPlugin(entryPoint);
        var loadContext = AssemblyLoadContext.Default;
        _loadContextFactory
            .Setup(value => value.TryCreate(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out loadContext))
            .Returns(true);

        _loadedPluginPreparer.Setup(value => value.Prepare(It.IsAny<Assembly>(), entryPoint)).Returns(preparedPlugin);

        var result = _target.PrepareExternal([discoveryResult], new HashSet<string>(StringComparer.Ordinal));

        result.Plugins.Should().ContainSingle().Which.Should().BeSameAs(preparedPlugin);
        result.Statuses.Should().BeEmpty();
        result.LoadContexts.Should().ContainSingle().Which.Should().BeSameAs(AssemblyLoadContext.Default);
    }

    [Fact]
    public void GIVEN_ExternalAssemblyLoadFails_WHEN_Preparing_THEN_ShouldRetainContextAndDisablePlugin()
    {
        var entryPoint = CreateEntryPoint("external");
        var discoveryResult = new PluginPackageDiscoveryResult
        {
            FallbackIdentity = "package",
            Candidate = new PluginPackageCandidate
            {
                PackageDirectory = "package",
                EntryAssemblyPath = "missing.dll",
                EntryPoint = entryPoint,
            },
        };

        var loadContext = AssemblyLoadContext.Default;
        _loadContextFactory
            .Setup(value => value.TryCreate(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out loadContext))
            .Returns(true);

        var result = _target.PrepareExternal([discoveryResult], new HashSet<string>(StringComparer.Ordinal));

        result.Plugins.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.Any(static diagnostic => diagnostic.Id == "PluginLoad"));

        result.LoadContexts.Should().ContainSingle().Which.Should().BeSameAs(AssemblyLoadContext.Default);
    }

    [Fact]
    public void GIVEN_ExternalEntryAssemblyOutsidePackage_WHEN_Preparing_THEN_ShouldDisableWithoutCreatingContext()
    {
        var entryPoint = CreateEntryPoint("external");
        var discoveryResult = CreateDiscoveryResult(entryPoint);
        AssemblyLoadContext? loadContext = null;
        _loadContextFactory
            .Setup(value => value.TryCreate(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out loadContext))
            .Returns(false);

        var result = _target.PrepareExternal([discoveryResult], new HashSet<string>(StringComparer.Ordinal));

        result.Plugins.Should().BeEmpty();
        result.LoadContexts.Should().BeEmpty();
        result.Statuses.Should().ContainSingle(status =>
            !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginLoad"
                && diagnostic.Message.Contains("outside", StringComparison.Ordinal)));

        _loadedPluginPreparer.Verify(
            static value => value.Prepare(It.IsAny<Assembly>(), It.IsAny<PluginEntryPointMetadata>()),
            Times.Never);
    }

    private static PluginPackageDiscoveryResult CreateDiscoveryResult(PluginEntryPointMetadata entryPoint)
    {
        return new PluginPackageDiscoveryResult
        {
            FallbackIdentity = "package",
            Candidate = new PluginPackageCandidate
            {
                PackageDirectory = "package",
                EntryAssemblyPath = typeof(PluginCandidatePreparerTests).Assembly.Location,
                EntryPoint = entryPoint,
            },
        };
    }

    private static PreparedCatalogPlugin CreatePreparedPlugin(
        PluginEntryPointMetadata entryPoint,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null)
    {
        return new PreparedCatalogPlugin
        {
            Metadata = new PluginMetadata
            {
                PluginId = entryPoint.PluginId,
                DisplayName = entryPoint.DisplayName,
                Version = entryPoint.Version,
                SupportedApiVersion = entryPoint.SupportedApiVersion,
            },
            Preparation = new PluginPreparationResult
            {
                Diagnostics = diagnostics ?? [],
            },
        };
    }

    private static DiagnosticInfo CreateDiagnostic(string id, DiagnosticSeverity severity, string message)
    {
        return new DiagnosticInfo
        {
            Id = id,
            Severity = severity,
            Message = message,
        };
    }

    private static PluginAssemblyInspection CreateInspection(PluginEntryPointMetadata entryPoint)
    {
        return new PluginAssemblyInspection
        {
            IsManagedAssembly = true,
            EntryPoints = [entryPoint],
        };
    }

    private static PluginEntryPointMetadata CreateEntryPoint(
        string pluginId,
        string displayName = "DisplayName",
        string version = "1.0.0",
        string apiVersion = PluginApiVersions.V1)
    {
        return new PluginEntryPointMetadata
        {
            PluginId = pluginId,
            DisplayName = displayName,
            Version = version,
            SupportedApiVersion = apiVersion,
            EntryTypeName = "EntryTypeName",
        };
    }
}
