namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCollisionPolicyTests
{
    private readonly PluginCollisionPolicy _target;

    public PluginCollisionPolicyTests()
    {
        _target = new PluginCollisionPolicy();
    }

    [Theory]
    [InlineData("tool", "tool")]
    [InlineData("other", null)]
    public void GIVEN_ProtectedToolNames_WHEN_FindingPluginCollision_THEN_ShouldReturnMatchingName(
        string protectedToolName,
        string? expected)
    {
        var plugin = CreatePreparedPlugin("plugin", "tool");

        var result = _target.FindProtectedToolCollision(
            plugin,
            new HashSet<string>([protectedToolName], StringComparer.Ordinal));

        result.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_DuplicateAndBlankExternalIdentities_WHEN_FindingDuplicates_THEN_ShouldReturnOnlyDuplicateIdentity()
    {
        var results = new[]
        {
            CreateDiscoveryResult("duplicate"),
            CreateDiscoveryResult("duplicate"),
            CreateDiscoveryResult(string.Empty),
            new PluginPackageDiscoveryResult { FallbackIdentity = "failed" },
        };

        var duplicates = _target.FindDuplicateExternalPluginIds(results);

        duplicates.Should().BeEquivalentTo(["duplicate"]);
    }

    [Fact]
    public void GIVEN_ProtectedSharedAndUniqueExternalTools_WHEN_FindingCollisions_THEN_ShouldReturnAffectedPlugins()
    {
        var protectedPlugin = CreatePreparedPlugin("protected", "reserved");
        var firstSharedPlugin = CreatePreparedPlugin("first", "shared");
        var secondSharedPlugin = CreatePreparedPlugin("second", "shared");
        var uniquePlugin = CreatePreparedPlugin("unique", "unique");

        var collisions = _target.FindExternalToolCollisions(
            [protectedPlugin, firstSharedPlugin, secondSharedPlugin, uniquePlugin],
            new HashSet<string>(["reserved"], StringComparer.Ordinal));

        collisions.Should().BeEquivalentTo(["protected", "first", "second"]);
    }

    private static PluginPackageDiscoveryResult CreateDiscoveryResult(string pluginId)
    {
        return new PluginPackageDiscoveryResult
        {
            FallbackIdentity = pluginId,
            Candidate = new PluginPackageCandidate
            {
                PackageDirectory = pluginId,
                EntryAssemblyPath = "EntryAssemblyPath",
                EntryPoint = new PluginEntryPointMetadata
                {
                    PluginId = pluginId,
                    DisplayName = "DisplayName",
                    Version = "1.0.0",
                    SupportedApiVersion = PluginApiVersions.V1,
                    EntryTypeName = "EntryTypeName",
                },
            },
        };
    }

    private static PreparedCatalogPlugin CreatePreparedPlugin(string pluginId, string toolName)
    {
        return new PreparedCatalogPlugin
        {
            Metadata = new PluginMetadata
            {
                PluginId = pluginId,
                DisplayName = "DisplayName",
                Version = "1.0.0",
                SupportedApiVersion = PluginApiVersions.V1,
            },
            Preparation = new PluginPreparationResult
            {
                Tools =
                [
                    new PreparedPluginTool
                    {
                        HandlerType = typeof(object),
                        HandlerContract = typeof(object),
                        HandlerFactory = static () => new object(),
                        Tool = new RegisteredTool
                        {
                            Metadata = new ToolRegistrationMetadata
                            {
                                Name = toolName,
                                Title = "Title",
                                Description = "Description",
                            },
                        },
                    },
                ],
            },
        };
    }
}
