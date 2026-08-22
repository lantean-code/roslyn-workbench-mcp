using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class RequestObjectGraphValidatorTests
{
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly RequestObjectGraphValidator _target = new();

    public RequestObjectGraphValidatorTests()
    {
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        _serializerOptions.MakeReadOnly();
    }

    [Fact]
    public void GIVEN_ValidSelectorGraph_WHEN_Validating_THEN_ShouldReturnNoError()
    {
        var project = new ProjectSelector { Name = "Name" };
        var document = new DocumentSelector { Path = "Path" };
        var range = new TextSpanRange();
        var span = new TextSpanSelector
        {
            Document = document,
            Range = range,
        };

        var location = new LocationSelector { Span = span };
        var workspace = new WorkspaceSelector { Alias = "Alias" };
        var symbol = new SymbolSelector { Location = location };
        var scope = new ScopeSelector { Kind = ScopeKind.Project, Project = project };
        var request = new SelectorRequest
        {
            Workspace = workspace,
            Project = project,
            Document = document,
            Location = location,
            Symbol = symbol,
            Scope = scope,
        };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeFalse();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NestedAttributesAndUndefinedEnum_WHEN_Validating_THEN_ShouldReturnOrderedPaths()
    {
        var item = new NestedValidationValue { Limit = -1 };
        var collectionItem = new NestedValidationValue { Kind = (TestEnum)999 };
        var value = new ValueNode { Number = -1 };
        var request = new NestedValidationRequest
        {
            Item = item,
            Items = [collectionItem],
            Value = value,
        };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Contain("'item.limit': The field Limit must be between 0 and 2147483647.");
        errorMessage.Should().Contain("'items[0].kind' is invalid");
        errorMessage.Should().Contain("'value.number': The field Number must be between 0 and 2147483647.");
    }

    [Fact]
    public void GIVEN_InvalidRootSelector_WHEN_Validating_THEN_ShouldReturnRootMemberPaths()
    {
        var request = new DocumentSelector();

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Be(
            "Invalid tool arguments: 'documentId', 'path': DocumentSelector must provide exactly one of Path or DocumentId.");
    }

    [Fact]
    public void GIVEN_SelectorAndAttributeFailures_WHEN_Validating_THEN_ShouldReturnBothFailureKinds()
    {
        var document = new DocumentSelector { Path = "Path", DocumentId = "DocumentId" };
        var spanDocument = new DocumentSelector { Path = "SpanPath" };
        var range = new TextSpanRange { Start = -1 };
        var span = new TextSpanSelector
        {
            Document = spanDocument,
            Range = range,
        };

        var request = new SelectorRequest
        {
            Document = document,
            Span = span,
        };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Contain("'span.range.start': The field Start must be between 0 and 2147483647.");
        errorMessage.Should().Contain("'document.documentId', 'document.path': DocumentSelector");
    }

    [Fact]
    public void GIVEN_MultipleSelectorFailures_WHEN_Validating_THEN_ShouldOrderErrorsByPath()
    {
        var location = new LocationSelector();
        var symbol = new SymbolSelector();
        var request = new SelectorRequest
        {
            Location = location,
            Symbol = symbol,
        };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().NotBeNull();
        errorMessage.IndexOf("'location.selection'", StringComparison.Ordinal).Should().BeLessThan(
            errorMessage.IndexOf("'symbol.documentationCommentId'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData((int)ScopeKind.Solution, false, false, false, true)]
    [InlineData((int)ScopeKind.Solution, true, false, false, false)]
    [InlineData((int)ScopeKind.Project, false, false, false, false)]
    [InlineData((int)ScopeKind.Project, true, false, false, true)]
    [InlineData((int)ScopeKind.Document, false, true, false, true)]
    [InlineData((int)ScopeKind.Document, true, true, false, false)]
    [InlineData((int)ScopeKind.Projects, false, false, true, true)]
    [InlineData((int)ScopeKind.Projects, false, false, false, false)]
    public void GIVEN_ScopeMemberCombination_WHEN_Validating_THEN_ShouldEnforceKindSpecificMembers(
        int kindValue,
        bool includeProject,
        bool includeDocument,
        bool includeProjects,
        bool expectedValid)
    {
        var project = includeProject ? new ProjectSelector { Name = "Name" } : null;
        var document = includeDocument ? new DocumentSelector { Path = "Path" } : null;
        IReadOnlyList<ProjectSelector>? projects = includeProjects
            ? [new ProjectSelector { Name = "Name" }]
            : null;
        var request = new ScopeSelector
        {
            Kind = (ScopeKind)kindValue,
            Project = project,
            Document = document,
            Projects = projects,
        };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().Be(!expectedValid);
        if (expectedValid)
        {
            errorMessage.Should().BeNull();
        }
        else
        {
            errorMessage.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("Object", "Invalid tool arguments: 'request': Request is invalid.")]
    [InlineData("UnknownMember", "Invalid tool arguments: 'unknownMember': Request is invalid.")]
    public void GIVEN_ObjectValidationFailure_WHEN_Validating_THEN_ShouldReturnExpectedPath(
        string scenario,
        string expectedError)
    {
        object request = scenario == "Object"
            ? new ObjectFailureRequest()
            : new UnknownMemberFailureRequest();

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Be(expectedError);
    }

    [Fact]
    public void GIVEN_NestedObjectValidationFailure_WHEN_Validating_THEN_ShouldReturnObjectPath()
    {
        var item = new ObjectFailureRequest();
        var request = new ObjectFailureContainer { Item = item };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Be("Invalid tool arguments: 'item': Request is invalid.");
    }

    [Fact]
    public void GIVEN_MemberMetadataAndNamingPolicyAreUnavailable_WHEN_Validating_THEN_ShouldUseContractMemberName()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            foreach (var property in typeInfo.Properties)
            {
                property.AttributeProvider = null;
            }
        });
        var serializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
        };
        serializerOptions.MakeReadOnly();
        var request = new PropertyFailureRequest();

        var result = _target.TryCreateInvalidRequestError(request, serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Be("Invalid tool arguments: 'UnknownMember': Request is invalid.");
    }

    [Fact]
    public void GIVEN_ReferenceCycleAndSetOnlyProperty_WHEN_Validating_THEN_ShouldVisitEachObjectOnce()
    {
        var request = new CyclicRequest { SetOnly = "Value" };
        request.Child = request;

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeFalse();
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(3, 3U, 0, false)]
    [InlineData(4, 4U, 999, true)]
    public void GIVEN_NestedEnumValues_WHEN_Validating_THEN_ShouldRecogniseDefinedBits(
        int signedValue,
        uint unsignedValue,
        int enumValue,
        bool expectedInvalid)
    {
        var request = new EnumRequest
        {
            SignedFlags = (TestSignedFlags)signedValue,
            UnsignedFlags = (TestUnsignedFlags)unsignedValue,
            Value = (TestEnum)enumValue,
        };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().Be(expectedInvalid);
        if (expectedInvalid)
        {
            errorMessage.Should().Be("Invalid values for tool arguments: 'signedFlags', 'unsignedFlags', 'value'.");
        }
        else
        {
            errorMessage.Should().BeNull();
        }
    }

    [Fact]
    public void GIVEN_OneUndefinedEnum_WHEN_Validating_THEN_ShouldUseSingularErrorMessage()
    {
        var request = new SingleEnumRequest { Value = (TestEnum)999 };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Be("Invalid value for tool argument: 'value'.");
    }

    [Theory]
    [InlineData(false, "Invalid tool arguments: 'request': The value is invalid.")]
    [InlineData(true, "Invalid tool arguments: 'value': The value is invalid.")]
    public void GIVEN_ValidationResultWithoutMessage_WHEN_Validating_THEN_ShouldUseFallbackMessage(
        bool includeMember,
        string expectedError)
    {
        var request = new MissingMessageRequest { IncludeMember = includeMember };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Be(expectedError);
    }

    private sealed record SelectorRequest
    {
        public WorkspaceSelector? Workspace { get; init; }

        public ProjectSelector? Project { get; init; }

        public DocumentSelector? Document { get; init; }

        public LocationSelector? Location { get; init; }

        public SymbolSelector? Symbol { get; init; }

        public ScopeSelector? Scope { get; init; }

        public TextSpanSelector? Span { get; init; }
    }

    private sealed record NestedValidationRequest
    {
        public NestedValidationValue Item { get; init; } = new();

        public IReadOnlyList<NestedValidationValue> Items { get; init; } = [];

        public ValueNode Value { get; init; }
    }

    private struct ValueNode
    {
        [Range(0, int.MaxValue)]
        public int Number { get; init; }
    }

    private sealed record NestedValidationValue
    {
        [Range(0, int.MaxValue)]
        public int Limit { get; init; }

        public TestEnum Kind { get; init; }
    }

    private sealed class ObjectFailureRequest : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield return new ValidationResult("Request is invalid.");
        }
    }

    private sealed class UnknownMemberFailureRequest : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield return new ValidationResult("Request is invalid.", ["UnknownMember"]);
        }
    }

    private sealed record ObjectFailureContainer
    {
        public required ObjectFailureRequest Item { get; init; }
    }

    private sealed class PropertyFailureRequest : IValidatableObject
    {
        public string Value { get; init; } = "Value";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            yield return new ValidationResult("Request is invalid.", ["UnknownMember"]);
        }
    }

    private sealed class CyclicRequest
    {
        public CyclicRequest? Child { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "The instance set-only property exercises request graph traversal through System.Text.Json metadata.")]
        public string SetOnly
        {
            set
            {
            }
        }
    }

    private sealed record EnumRequest
    {
        public TestSignedFlags SignedFlags { get; init; }

        public TestUnsignedFlags UnsignedFlags { get; init; }

        public TestEnum Value { get; init; }
    }

    private sealed record SingleEnumRequest
    {
        public TestEnum Value { get; init; }
    }

    private sealed class MissingMessageRequest : IValidatableObject
    {
        public bool IncludeMember { get; init; }

        public string Value { get; init; } = "Value";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var memberNames = IncludeMember ? new[] { nameof(Value) } : [];
            yield return new ValidationResult(null, memberNames);
        }
    }

    private enum TestEnum
    {
        Value,
    }

    [Flags]
    private enum TestSignedFlags
    {
        None = 0,
        First = 1,
        Second = 2,
    }

    [Flags]
    private enum TestUnsignedFlags : uint
    {
        None = 0,
        First = 1,
        Second = 2,
    }
}
