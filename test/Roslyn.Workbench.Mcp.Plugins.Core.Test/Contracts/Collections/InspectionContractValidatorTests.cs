namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Contracts.Collections;

public sealed class InspectionContractValidatorTests
{
    [Fact]
    public void GIVEN_UnspecifiedLimit_WHEN_Validating_THEN_ShouldReturnNoErrors()
    {
        var errors = InspectionContractValidator.Validate(new CollectionLimit());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ZeroLimit_WHEN_Validating_THEN_ShouldReturnNoErrors()
    {
        var errors = InspectionContractValidator.Validate(new CollectionLimit
        {
            MaxResults = 0,
        });

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_NegativeLimit_WHEN_Validating_THEN_ShouldReturnValidationError()
    {
        var errors = InspectionContractValidator.Validate(new CollectionLimit
        {
            MaxResults = -1,
        });

        errors.Should().ContainSingle().Which.Should().Be("CollectionLimit MaxResults must be zero or greater when provided.");
    }
}
