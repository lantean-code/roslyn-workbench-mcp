using System.ComponentModel.DataAnnotations;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Validation;

public sealed class ProhibitedUnlessAttributeTests
{
    [Theory]
    [InlineData((int)ConditionKind.Allowed, "Value", true)]
    [InlineData((int)ConditionKind.Prohibited, null, true)]
    [InlineData((int)ConditionKind.Prohibited, "Value", false)]
    public void GIVEN_ControllingAndDecoratedValues_WHEN_Validating_THEN_ShouldEnforceConditionalProhibition(
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
        var action = () => new ProhibitedUnlessAttribute(otherProperty!, ConditionKind.Allowed);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_NullExpectedValue_WHEN_Constructing_THEN_ShouldRejectConfiguration()
    {
        var action = () => new ProhibitedUnlessAttribute(nameof(ConditionalValue.Kind), null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_MissingMemberNameInContext_WHEN_ValidatingDirectly_THEN_ShouldReturnFailureWithoutMember()
    {
        var instance = new ConditionalValue { Kind = ConditionKind.Prohibited, Value = "Value" };
        var context = new ValidationContext(instance);
        var target = new ProhibitedUnlessAttribute(nameof(ConditionalValue.Kind), ConditionKind.Allowed);

        var result = target.GetValidationResult(instance.Value, context);

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

        [ProhibitedUnless(nameof(Kind), ConditionKind.Allowed)]
        public string? Value { get; init; }
    }

    private enum ConditionKind
    {
        Prohibited,
        Allowed,
    }
}
