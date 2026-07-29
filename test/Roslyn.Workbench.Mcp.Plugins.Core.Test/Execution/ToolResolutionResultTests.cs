using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Execution;

public sealed class ToolResolutionResultTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_ResolutionResult_WHEN_CheckingHasRejection_THEN_ShouldExposeConditionalNullabilityMetadata()
    {
        var property = typeof(ToolResolutionResult<string, object>).GetProperty("HasRejection", BindingFlags.Instance | BindingFlags.Public);

        property.Should().NotBeNull();

        var attributes = property!
            .GetCustomAttributes<MemberNotNullWhenAttribute>(inherit: false)
            .OrderBy(static attribute => attribute.ReturnValue)
            .ToArray();

        attributes.Should().HaveCount(2);
        attributes[0].ReturnValue.Should().BeFalse();
        attributes[0].Members.Should().Equal(nameof(ToolResolutionResult<,>.Value));
        attributes[1].ReturnValue.Should().BeTrue();
        attributes[1].Members.Should().Equal(nameof(ToolResolutionResult<,>.Rejection));
    }

    [Fact]
    public void GIVEN_ResolutionResult_WHEN_CheckingHasRejection_THEN_ShouldReflectStoredOutcome()
    {
        var rejection = PluginExecutionResult.Rejected<object>(new PluginExecutionError
        {
            Code = "Code",
            Message = "Message",
        });

        var rejected = ToolResolutionResult.Rejected<string, object>(rejection);
        var resolved = ToolResolutionResult.Resolved<string, object>("Value");

        rejected.HasRejection.Should().BeTrue();
        rejected.Rejection.Should().NotBeNull();
        rejected.Value.Should().BeNull();

        resolved.HasRejection.Should().BeFalse();
        resolved.Value.Should().Be("Value");
        resolved.Rejection.Should().BeNull();
    }
}
