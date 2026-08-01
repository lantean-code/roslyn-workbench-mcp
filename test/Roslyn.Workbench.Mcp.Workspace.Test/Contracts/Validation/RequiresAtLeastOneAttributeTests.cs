using System.ComponentModel.DataAnnotations;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Validation;

public sealed class RequiresAtLeastOneAttributeTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("   ", null, false)]
    [InlineData("Value", null, true)]
    [InlineData(null, "Value", true)]
    public void GIVEN_MemberValues_WHEN_Validating_THEN_ShouldRequireAtLeastOneProvidedValue(
        string? first,
        string? second,
        bool expectedValid)
    {
        var target = new AtLeastOneValue { First = first, Second = second };

        var results = Validate(target);

        (results.Count == 0).Should().Be(expectedValid);
        if (!expectedValid)
        {
            results.Single().MemberNames.Should().Equal(nameof(AtLeastOneValue.First), nameof(AtLeastOneValue.Second));
        }
    }

    [Fact]
    public void GIVEN_NonEmptyCollection_WHEN_Validating_THEN_ShouldTreatCollectionAsProvided()
    {
        var target = new CollectionValue { Items = ["Items"] };

        var results = Validate(target);

        results.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_EmptyCollection_WHEN_Validating_THEN_ShouldTreatCollectionAsAbsent()
    {
        var target = new CollectionValue { Items = [] };

        var results = Validate(target);

        results.Should().ContainSingle();
    }

    [Fact]
    public void GIVEN_NonEnumerableObject_WHEN_Validating_THEN_ShouldTreatObjectAsProvided()
    {
        var target = new ObjectValue { Item = new object() };

        var results = Validate(target);

        results.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_NullObject_WHEN_ValidatingAttributeDirectly_THEN_ShouldReturnSuccess()
    {
        var target = new RequiresAtLeastOneAttribute(nameof(AtLeastOneValue.First));
        var instance = new AtLeastOneValue();
        var context = new ValidationContext(instance);

        var result = target.GetValidationResult(null, context);

        result.Should().Be(ValidationResult.Success);
        target.MemberNames.Should().Equal(nameof(AtLeastOneValue.First));
    }

    [Theory]
    [InlineData("Empty")]
    [InlineData("Blank")]
    [InlineData("Duplicate")]
    public void GIVEN_InvalidMemberConfiguration_WHEN_Constructing_THEN_ShouldRejectConfiguration(string scenario)
    {
        string[] memberNames = scenario switch
        {
            "Empty" => [],
            "Blank" => [""],
            _ => ["First", "First"],
        };
        var action = () => new RequiresAtLeastOneAttribute(memberNames);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_NullMemberCollection_WHEN_Constructing_THEN_ShouldRejectConfiguration()
    {
        var action = () => new RequiresAtLeastOneAttribute(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GIVEN_UnknownMember_WHEN_Validating_THEN_ShouldRejectConfiguration()
    {
        var target = new UnknownMemberValue();

        var action = () => Validate(target);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown*");
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(value);
        Validator.TryValidateObject(value, context, results, validateAllProperties: true);
        return results;
    }

    [RequiresAtLeastOne(nameof(First), nameof(Second), ErrorMessage = "A value is required.")]
    private sealed record AtLeastOneValue
    {
        public string? First { get; init; }

        public string? Second { get; init; }
    }

    [RequiresAtLeastOne(nameof(Items))]
    private sealed record CollectionValue
    {
        public IReadOnlyList<string>? Items { get; init; }
    }

    [RequiresAtLeastOne(nameof(Item))]
    private sealed record ObjectValue
    {
        public object? Item { get; init; }
    }

    [RequiresAtLeastOne("Unknown")]
    private sealed record UnknownMemberValue;
}
