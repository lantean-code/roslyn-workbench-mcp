using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Validation;

using Xunit;

namespace Roslyn.Workbench.Mcp.Contracts.Test.Selectors;

public sealed class SelectorValidationTests
{
    [Fact]
    public void GIVEN_DocumentSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new DocumentSelector();

        var errors = ContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithBothSelectors_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new DocumentSelector
        {
            Path = "Path",
            DocumentId = "DocumentId",
        };

        var errors = ContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithPath_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var selector = new DocumentSelector
        {
            Path = "Path",
        };

        var errors = ContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ProjectSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new ProjectSelector();

        var errors = ContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("at least one"));
    }

    [Fact]
    public void GIVEN_LocationSelectorWithBothVariants_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = "Path",
                },
                Start = 1,
                Length = 1,
            },
            Selection = new TextSelectionSelector
            {
                Document = new DocumentSelector
                {
                    Path = "Path",
                },
                SelectedText = "SelectedText",
            },
        };

        var errors = ContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_ScopeSelectorWithMismatchedKind_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new ScopeSelector
        {
            Kind = ScopeKind.Document,
            Project = new ProjectSelector
            {
                Name = "Name",
            },
        };

        var errors = ContractValidator.Validate(selector);

        errors.Should().Contain(error => error.Contains("Kind"));
    }

    [Fact]
    public void GIVEN_ResultLimitBelowOne_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var limit = new ResultLimit
        {
            MaxResults = 0,
        };

        var errors = ContractValidator.Validate(limit);

        errors.Should().ContainSingle(error => error.Contains("at least 1"));
    }

    [Fact]
    public void GIVEN_SymbolSelectorWithoutAnyResolver_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new SymbolSelector();

        var errors = ContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_SymbolSelectorWithBothResolvers_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Path",
                    },
                    Start = 0,
                    Length = 1,
                },
            },
            DocumentationCommentId = "M:Sample.Type.Method",
        };

        var errors = ContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }
}
