using System.ComponentModel.DataAnnotations;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Validation;

public sealed class RequiredWhenAttributeTests
{
    [Theory]
    [InlineData((int)ConditionKind.None, null, true)]
    [InlineData((int)ConditionKind.Required, null, false)]
    [InlineData((int)ConditionKind.Required, "Value", true)]
    public void GIVEN_ControllingAndDecoratedValues_WHEN_Validating_THEN_ShouldEnforceConditionalRequirement(
        int kindValue,
        string? value,
        bool expectedValid)
    {
        var kind = (ConditionKind)kindValue;
        var target = new ConditionalValue { Kind = kind, Value = value };

        var results = Validate(target);

        (results.Count == 0).Should().Be(expectedValid);
        if (!expectedValid)
        {
            results.Single().MemberNames.Should().Equal(nameof(ConditionalValue.Value));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GIVEN_InvalidControllingMember_WHEN_Constructing_THEN_ShouldRejectConfiguration(string? otherProperty)
    {
        var action = () => new RequiredWhenAttribute(otherProperty!, ConditionKind.Required);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_NullExpectedValue_WHEN_Constructing_THEN_ShouldRejectConfiguration()
    {
        var action = () => new RequiredWhenAttribute(nameof(ConditionalValue.Kind), null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_MissingMemberNameInContext_WHEN_ValidatingDirectly_THEN_ShouldReturnFailureWithoutMember()
    {
        var instance = new ConditionalValue { Kind = ConditionKind.Required };
        var context = new ValidationContext(instance);
        var target = new RequiredWhenAttribute(nameof(ConditionalValue.Kind), ConditionKind.Required);

        var result = target.GetValidationResult(null, context);

        result.Should().NotBeNull();
        result.MemberNames.Should().BeEmpty();
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(value);
        Validator.TryValidateObject(value, context, results, validateAllProperties: true);
        return results;
    }

    private sealed record ConditionalValue
    {
        public ConditionKind Kind { get; init; }

        [RequiredWhen(nameof(Kind), ConditionKind.Required)]
        public string? Value { get; init; }
    }

    private enum ConditionKind
    {
        None,
        Required,
    }
}
