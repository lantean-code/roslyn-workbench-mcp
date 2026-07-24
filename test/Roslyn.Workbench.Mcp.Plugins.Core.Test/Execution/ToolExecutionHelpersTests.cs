using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Execution;

public sealed class ToolExecutionHelpersTests
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
        var rejection = PluginExecutionResult<object>.Rejected(new PluginExecutionError
        {
            Code = "Code",
            Message = "Message",
        });

        var rejected = ToolResolutionResult<string, object>.Rejected(rejection);
        var resolved = ToolResolutionResult<string, object>.Resolved("Value");

        rejected.HasRejection.Should().BeTrue();
        rejected.Rejection.Should().NotBeNull();
        rejected.Value.Should().BeNull();

        resolved.HasRejection.Should().BeFalse();
        resolved.Value.Should().Be("Value");
        resolved.Rejection.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ResolvedLocation_WHEN_CreatingLocationSelector_THEN_ShouldPreserveDocumentIdentity()
    {
        var selector = ToolExecutionHelpers.CreateLocationSelector(
            SelectorTestFactory.CreateResolvedLocation("Shared/SharedClass.cs", 10, 5));

        selector.Should().NotBeNull();
        selector!.Span.Should().NotBeNull();
        selector.Span!.Document.Should().NotBeNull();
        selector.Span.Document!.DocumentId.Should().Be("DocumentId");
        selector.Span.Document.Path.Should().BeNull();
        selector.Span.Document.Project!.ProjectId.Should().Be("ProjectId");
    }

    [Fact]
    public void GIVEN_ResolvedLocation_WHEN_CreatingLocationSymbolSelector_THEN_ShouldPreserveDocumentIdentity()
    {
        var selector = ToolExecutionHelpers.CreateLocationSymbolSelector(
            SelectorTestFactory.CreateResolvedLocation("Shared/SharedClass.cs", 10, 5));

        selector.Should().NotBeNull();
        selector!.Location.Should().NotBeNull();
        selector.Location!.Span.Should().NotBeNull();
        selector.Location.Span!.Document.Should().NotBeNull();
        selector.Location.Span.Document!.DocumentId.Should().Be("DocumentId");
        selector.Location.Span.Document.Path.Should().BeNull();
        selector.Location.Span.Document.Project!.ProjectId.Should().Be("ProjectId");
    }

    [Fact]
    public void GIVEN_PathOnlyResolvedLocation_WHEN_CreatingLocationSelector_THEN_ShouldPreserveUnqualifiedPath()
    {
        var resolvedLocation = new ResolvedLocation
        {
            Document = new DocumentReference
            {
                Path = "Shared/SharedClass.cs",
            },
            Span = new TextSpanRange
            {
                Start = 10,
                Length = 5,
            },
        };

        var selector = ToolExecutionHelpers.CreateLocationSelector(resolvedLocation);

        selector.Should().NotBeNull();
        selector!.Span.Should().NotBeNull();
        selector.Span!.Document.Should().NotBeNull();
        selector.Span.Document!.Path.Should().Be("Shared/SharedClass.cs");
        selector.Span.Document.DocumentId.Should().BeNull();
        selector.Span.Document.Project.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NoRequestedResultLimit_WHEN_GettingMaxResults_THEN_ShouldReturnToolDefault()
    {
        var result = ToolExecutionHelpers.GetMaxResults(requestLimit: null, defaultMaxResults: 25);

        result.Should().Be(25);
    }

    [Fact]
    public void GIVEN_RequestedResultLimit_WHEN_GettingMaxResults_THEN_ShouldReturnRequestedLimit()
    {
        var result = ToolExecutionHelpers.GetMaxResults(
            7,
            defaultMaxResults: 25);

        result.Should().Be(7);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void GIVEN_NonPositiveRequestedResultLimit_WHEN_GettingMaxResults_THEN_ShouldReturnZero(int requestLimit)
    {
        var result = ToolExecutionHelpers.GetMaxResults(
            requestLimit,
            defaultMaxResults: 25);

        result.Should().Be(0);
    }
}
