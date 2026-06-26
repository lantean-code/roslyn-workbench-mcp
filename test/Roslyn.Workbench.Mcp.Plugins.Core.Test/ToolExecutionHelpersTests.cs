using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;

using Xunit;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class ToolExecutionHelpersTests
{
    [Fact]
    public void GIVEN_ResolutionResult_WHEN_CheckingHasRejection_THEN_ShouldExposeConditionalNullabilityMetadata()
    {
        var property = typeof(ToolExecutionHelpers.ResolutionResult<string, object>).GetProperty("HasRejection", BindingFlags.Instance | BindingFlags.Public);

        property.Should().NotBeNull();

        var attributes = property!
            .GetCustomAttributes<MemberNotNullWhenAttribute>(inherit: false)
            .OrderBy(static attribute => attribute.ReturnValue)
            .ToArray();

        attributes.Should().HaveCount(2);
        attributes[0].ReturnValue.Should().BeFalse();
        attributes[0].Members.Should().Equal(nameof(ToolExecutionHelpers.ResolutionResult<string, object>.Value));
        attributes[1].ReturnValue.Should().BeTrue();
        attributes[1].Members.Should().Equal(nameof(ToolExecutionHelpers.ResolutionResult<string, object>.Rejection));
    }

    [Fact]
    public void GIVEN_ResolutionResult_WHEN_CheckingHasRejection_THEN_ShouldReflectStoredOutcome()
    {
        var rejected = new ToolExecutionHelpers.ResolutionResult<string, object>
        {
            Rejection = PluginExecutionResult<object>.Rejected(new ToolError
            {
                Code = "Code",
                Message = "Message",
            }),
        };
        var resolved = new ToolExecutionHelpers.ResolutionResult<string, object>
        {
            Value = "Value",
        };

        rejected.HasRejection.Should().BeTrue();
        rejected.Rejection.Should().NotBeNull();
        rejected.Value.Should().BeNull();

        resolved.HasRejection.Should().BeFalse();
        resolved.Value.Should().Be("Value");
        resolved.Rejection.Should().BeNull();
    }
}
