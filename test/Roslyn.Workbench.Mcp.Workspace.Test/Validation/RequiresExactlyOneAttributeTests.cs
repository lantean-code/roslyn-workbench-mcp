using System.ComponentModel.DataAnnotations;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Validation;

public sealed class RequiresExactlyOneAttributeTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("First", null, true)]
    [InlineData(null, "Second", true)]
    [InlineData("First", "Second", false)]
    public void GIVEN_MemberValues_WHEN_Validating_THEN_ShouldRequireExactlyOneProvidedValue(
        string? first,
        string? second,
        bool expectedValid)
    {
        var target = new ExactlyOneValue { First = first, Second = second };

        var results = Validate(target);

        (results.Count == 0).Should().Be(expectedValid);
    }

    [Fact]
    public void GIVEN_NullObject_WHEN_ValidatingAttributeDirectly_THEN_ShouldReturnSuccess()
    {
        var target = new RequiresExactlyOneAttribute(nameof(ExactlyOneValue.First), nameof(ExactlyOneValue.Second));
        var instance = new ExactlyOneValue();
        var context = new ValidationContext(instance);

        var result = target.GetValidationResult(null, context);

        result.Should().Be(ValidationResult.Success);
        target.MemberNames.Should().Equal(nameof(ExactlyOneValue.First), nameof(ExactlyOneValue.Second));
    }

    [Fact]
    public void GIVEN_OnlyOneConfiguredMember_WHEN_Constructing_THEN_ShouldRejectConfiguration()
    {
        var action = () => new RequiresExactlyOneAttribute(nameof(ExactlyOneValue.First));

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_NullMemberCollection_WHEN_Constructing_THEN_ShouldRejectConfiguration()
    {
        var action = () => new RequiresExactlyOneAttribute(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(value);
        Validator.TryValidateObject(value, context, results, validateAllProperties: true);
        return results;
    }

    [RequiresExactlyOne(nameof(First), nameof(Second))]
    private sealed record ExactlyOneValue
    {
        public string? First { get; init; }

        public string? Second { get; init; }
    }
}
