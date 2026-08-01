using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class RequestObjectGraphValidatorTests
{
    private readonly Mock<IWorkspaceContractValidator<DocumentSelector>> _documentSelectorValidator;
    private readonly Mock<IWorkspaceContractValidator<LocationSelector>> _locationSelectorValidator;
    private readonly Mock<IWorkspaceContractValidator<ProjectSelector>> _projectSelectorValidator;
    private readonly Mock<IWorkspaceContractValidator<ScopeSelector>> _scopeSelectorValidator;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly Mock<IWorkspaceContractValidator<SymbolSelector>> _symbolSelectorValidator;
    private readonly RequestObjectGraphValidator _target;
    private readonly Mock<IWorkspaceContractValidator<WorkspaceSelector>> _workspaceSelectorValidator;

    public RequestObjectGraphValidatorTests()
    {
        _documentSelectorValidator = new Mock<IWorkspaceContractValidator<DocumentSelector>>();
        _locationSelectorValidator = new Mock<IWorkspaceContractValidator<LocationSelector>>();
        _projectSelectorValidator = new Mock<IWorkspaceContractValidator<ProjectSelector>>();
        _scopeSelectorValidator = new Mock<IWorkspaceContractValidator<ScopeSelector>>();
        _symbolSelectorValidator = new Mock<IWorkspaceContractValidator<SymbolSelector>>();
        _workspaceSelectorValidator = new Mock<IWorkspaceContractValidator<WorkspaceSelector>>();

        var validResult = WorkspaceContractValidationResult.Valid();
        _documentSelectorValidator.Setup(item => item.Validate(It.IsAny<DocumentSelector>())).Returns(validResult);
        _locationSelectorValidator.Setup(item => item.Validate(It.IsAny<LocationSelector>())).Returns(validResult);
        _projectSelectorValidator.Setup(item => item.Validate(It.IsAny<ProjectSelector>())).Returns(validResult);
        _scopeSelectorValidator.Setup(item => item.Validate(It.IsAny<ScopeSelector>())).Returns(validResult);
        _symbolSelectorValidator.Setup(item => item.Validate(It.IsAny<SymbolSelector>())).Returns(validResult);
        _workspaceSelectorValidator.Setup(item => item.Validate(It.IsAny<WorkspaceSelector>())).Returns(validResult);

        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        _serializerOptions.MakeReadOnly();

        _target = new RequestObjectGraphValidator(
            _documentSelectorValidator.Object,
            _locationSelectorValidator.Object,
            _projectSelectorValidator.Object,
            _scopeSelectorValidator.Object,
            _symbolSelectorValidator.Object,
            _workspaceSelectorValidator.Object);
    }

    [Fact]
    public void GIVEN_ValidSelectorGraph_WHEN_Validating_THEN_ShouldReturnNoErrorAndUseEverySelectorValidator()
    {
        var project = new ProjectSelector { Name = "Name" };
        var document = new DocumentSelector { Path = "Path" };
        var span = new TextSpanSelector();
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
        _workspaceSelectorValidator.Verify(item => item.Validate(request.Workspace), Times.Once);
        _projectSelectorValidator.Verify(item => item.Validate(It.IsAny<ProjectSelector>()), Times.AtLeastOnce);
        _documentSelectorValidator.Verify(item => item.Validate(document), Times.Once);
        _locationSelectorValidator.Verify(item => item.Validate(It.IsAny<LocationSelector>()), Times.AtLeastOnce);
        _symbolSelectorValidator.Verify(item => item.Validate(request.Symbol), Times.Once);
        _scopeSelectorValidator.Verify(item => item.Validate(request.Scope), Times.Once);
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
        errorMessage.Should().Be("Invalid values for tool arguments: 'item.limit', 'items[0].kind', 'value.number'.");
    }

    [Fact]
    public void GIVEN_InvalidRootSelector_WHEN_Validating_THEN_ShouldReturnRootMemberPaths()
    {
        var failure = new WorkspaceContractValidationFailure(
            "Root selector failure",
            [nameof(DocumentSelector.Path), nameof(DocumentSelector.DocumentId)]);
        var invalidResult = WorkspaceContractValidationResult.Invalid([failure]);
        _documentSelectorValidator
            .Setup(item => item.Validate(It.IsAny<DocumentSelector>()))
            .Returns(invalidResult);
        var request = new DocumentSelector();

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Be("Invalid tool arguments: 'documentId', 'path': Root selector failure.");
    }

    [Fact]
    public void GIVEN_SelectorAndAttributeFailures_WHEN_Validating_THEN_ShouldReturnBothFailureKinds()
    {
        var failure = new WorkspaceContractValidationFailure(
            "DocumentSelector must provide exactly one of Path or DocumentId.",
            [nameof(DocumentSelector.Path), nameof(DocumentSelector.DocumentId)]);
        var invalidResult = WorkspaceContractValidationResult.Invalid([failure]);
        _documentSelectorValidator
            .Setup(item => item.Validate(It.IsAny<DocumentSelector>()))
            .Returns(invalidResult);
        var document = new DocumentSelector { Path = "Path", DocumentId = "DocumentId" };
        var span = new TextSpanSelector { Start = -1 };
        var request = new SelectorRequest
        {
            Document = document,
            Span = span,
        };

        var result = _target.TryCreateInvalidRequestError(request, _serializerOptions, out var errorMessage);

        result.Should().BeTrue();
        errorMessage.Should().Contain("'span.start' is invalid");
        errorMessage.Should().Contain("'document.documentId', 'document.path': DocumentSelector");
    }

    [Fact]
    public void GIVEN_MultipleSelectorFailures_WHEN_Validating_THEN_ShouldOrderErrorsByPath()
    {
        var locationFailure = new WorkspaceContractValidationFailure(
            "Location failure.",
            [nameof(LocationSelector.Span), nameof(LocationSelector.Selection)]);
        var invalidLocationResult = WorkspaceContractValidationResult.Invalid([locationFailure]);
        _locationSelectorValidator
            .Setup(item => item.Validate(It.IsAny<LocationSelector>()))
            .Returns(invalidLocationResult);
        var symbolFailure = new WorkspaceContractValidationFailure(
            "Symbol failure.",
            [nameof(SymbolSelector.Location), nameof(SymbolSelector.DocumentationCommentId)]);
        var invalidSymbolResult = WorkspaceContractValidationResult.Invalid([symbolFailure]);
        _symbolSelectorValidator
            .Setup(item => item.Validate(It.IsAny<SymbolSelector>()))
            .Returns(invalidSymbolResult);
        var span = new TextSpanSelector();
        var location = new LocationSelector { Span = span };
        var symbol = new SymbolSelector { Location = location };
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
    [InlineData("Object", "Invalid value for tool argument: 'request'.")]
    [InlineData("UnknownMember", "Invalid value for tool argument: 'unknownMember'.")]
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
        errorMessage.Should().Be("Invalid value for tool argument: 'item'.");
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
        errorMessage.Should().Be("Invalid value for tool argument: 'UnknownMember'.");
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
