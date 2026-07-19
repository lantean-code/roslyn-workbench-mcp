using System.Composition;

namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class RoslynPluginAttributeTests
{
    [Fact]
    public void GIVEN_PluginIdentity_WHEN_ConstructingAttribute_THEN_ShouldExportPluginContractAndMetadata()
    {
        var target = new RoslynPluginAttribute("PluginId", "DisplayName", PluginApiVersions.V1);

        target.PluginId.Should().Be("PluginId");
        target.DisplayName.Should().Be("DisplayName");
        target.SupportedApiVersion.Should().Be(PluginApiVersions.V1);
        target.ContractType.Should().Be<IRoslynPlugin>();
        typeof(RoslynPluginAttribute).GetCustomAttributes(typeof(MetadataAttributeAttribute), false).Should().ContainSingle();
    }

    [Fact]
    public void GIVEN_PluginAttributeType_WHEN_InspectingUsage_THEN_ShouldPermitOneConcreteTypeDeclaration()
    {
        var usage = typeof(RoslynPluginAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }
}
