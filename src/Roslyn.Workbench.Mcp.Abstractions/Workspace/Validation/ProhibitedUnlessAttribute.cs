using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Workspace.Validation;

/// <summary>
/// Prohibits a provided value for the decorated property unless another property equals a configured value.
/// </summary>
/// <remarks>
/// A value is provided when it is non-null and, for strings and collections, non-empty. Whitespace-only strings are not provided.
/// Roslyn Workbench publishes the same constraint in MCP input schemas.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ProhibitedUnlessAttribute : ValidationAttribute
{
    /// <summary>
    /// Gets the expected value of the controlling property.
    /// </summary>
    public object ExpectedValue { get; }

    /// <summary>
    /// Gets the name of the controlling property.
    /// </summary>
    public string OtherProperty { get; }

    /// <summary>
    /// Initialises a new instance of the <see cref="ProhibitedUnlessAttribute"/> class.
    /// </summary>
    /// <param name="otherProperty">The name of the property controlling whether the decorated property is permitted.</param>
    /// <param name="expectedValue">The value that permits the decorated property.</param>
    public ProhibitedUnlessAttribute(string otherProperty, object expectedValue)
        : base("The {0} field is prohibited for the selected value.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otherProperty);
        ArgumentNullException.ThrowIfNull(expectedValue);

        OtherProperty = otherProperty;
        ExpectedValue = expectedValue;
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        var controllingValue = ValidationMemberAccess.GetValue(validationContext.ObjectInstance, OtherProperty);
        if (Equals(controllingValue, ExpectedValue) || !ValidationMemberAccess.IsProvided(value))
        {
            return ValidationResult.Success;
        }

        var errorMessage = FormatErrorMessage(validationContext.DisplayName);
        var memberNames = validationContext.MemberName is null ? [] : new[] { validationContext.MemberName };
        return new ValidationResult(errorMessage, memberNames);
    }
}
