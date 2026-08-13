using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Validation;

public sealed class NonEmptyGuidAttributeTests
{
    [Fact]
    public void GIVEN_NullValue_WHEN_Validating_THEN_ShouldReturnSuccess()
    {
        var target = new NonEmptyGuidAttribute();

        var result = target.IsValid(null);

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_EmptyGuid_WHEN_Validating_THEN_ShouldReturnFailure()
    {
        var target = new NonEmptyGuidAttribute();

        var result = target.IsValid(Guid.Empty);

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NonEmptyGuid_WHEN_Validating_THEN_ShouldReturnSuccess()
    {
        var target = new NonEmptyGuidAttribute();

        var result = target.IsValid(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_NonGuidValue_WHEN_Validating_THEN_ShouldReturnFailure()
    {
        var target = new NonEmptyGuidAttribute();

        var result = target.IsValid("Value");

        result.Should().BeFalse();
    }
}
