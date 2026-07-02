using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AwesomeAssertions;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
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

    [Fact]
    public void GIVEN_ResolvedLocation_WHEN_CreatingLocationSelector_THEN_ShouldPreserveDocumentIdentity()
    {
        var selector = ToolExecutionHelpers.CreateLocationSelector(new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "DocumentId",
                Path = "Shared/SharedClass.cs",
                ProjectId = "ProjectId",
            },
            Span = new TextSpanRange
            {
                Start = 10,
                Length = 5,
            },
        });

        selector.Should().NotBeNull();
        selector!.Span.Should().NotBeNull();
        selector.Span!.Document.Should().NotBeNull();
        selector.Span.Document!.DocumentId.Should().Be("DocumentId");
        selector.Span.Document.Path.Should().BeNull();
    }

    [Fact]
    public void GIVEN_ResolvedLocation_WHEN_CreatingLocationSymbolSelector_THEN_ShouldPreserveDocumentIdentity()
    {
        var selector = ToolExecutionHelpers.CreateLocationSymbolSelector(new ResolvedLocation
        {
            Document = new DocumentReference
            {
                DocumentId = "DocumentId",
                Path = "Shared/SharedClass.cs",
                ProjectId = "ProjectId",
            },
            Span = new TextSpanRange
            {
                Start = 10,
                Length = 5,
            },
        });

        selector.Should().NotBeNull();
        selector!.Location.Should().NotBeNull();
        selector.Location!.Span.Should().NotBeNull();
        selector.Location.Span!.Document.Should().NotBeNull();
        selector.Location.Span.Document!.DocumentId.Should().Be("DocumentId");
        selector.Location.Span.Document.Path.Should().BeNull();
    }
}
