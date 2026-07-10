using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class SelectorValidationTests
{
    [Fact]
    public void GIVEN_DocumentSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new DocumentSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

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

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }

    [Fact]
    public void GIVEN_DocumentSelectorWithPath_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var selector = new DocumentSelector
        {
            Path = "Path",
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ProjectSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new ProjectSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("at least one"));
    }

    [Fact]
    public void GIVEN_WorkspaceSelectorWithoutAnySelector_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new WorkspaceSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("at least one"));
    }

    [Fact]
    public void GIVEN_WorkspaceSelectorWithWorkspaceId_WHEN_Validated_THEN_ShouldReturnNoValidationErrors()
    {
        var selector = new WorkspaceSelector
        {
            WorkspaceId = "WorkspaceId",
        };

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().BeEmpty();
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

        var errors = WorkspaceContractValidator.Validate(selector);

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

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().Contain(error => error.Contains("Kind"));
    }

    [Fact]
    public void GIVEN_SymbolSelectorWithoutAnyResolver_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var selector = new SymbolSelector();

        var errors = WorkspaceContractValidator.Validate(selector);

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

        var errors = WorkspaceContractValidator.Validate(selector);

        errors.Should().ContainSingle(error => error.Contains("exactly one"));
    }
}
