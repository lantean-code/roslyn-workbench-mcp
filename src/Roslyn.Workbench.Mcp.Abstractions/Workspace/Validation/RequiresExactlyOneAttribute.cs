using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Workspace.Validation;

/// <summary>
/// Requires exactly one of the named public properties to contain a provided value.
/// </summary>
/// <remarks>
/// A value is provided when it is non-null and, for strings and collections, non-empty. Whitespace-only strings are not provided.
/// Roslyn Workbench publishes the same constraint in MCP input schemas.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequiresExactlyOneAttribute : ValidationAttribute
{
    private readonly string[] _memberNames;

    /// <summary>
    /// Gets the names of the properties participating in the requirement.
    /// </summary>
    public IReadOnlyList<string> MemberNames => _memberNames;

    /// <summary>
    /// Initialises a new instance of the <see cref="RequiresExactlyOneAttribute"/> class.
    /// </summary>
    /// <param name="memberNames">The names of the public properties participating in the requirement.</param>
    public RequiresExactlyOneAttribute(params string[] memberNames)
        : base("The {0} value must provide exactly one configured member.")
    {
        ArgumentNullException.ThrowIfNull(memberNames);

        _memberNames = ValidationMemberAccess.CaptureMemberNames(memberNames, nameof(memberNames), minimumCount: 2);
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is null)
        {
            return ValidationResult.Success;
        }

        var providedCount = 0;
        foreach (var memberName in _memberNames)
        {
            var memberValue = ValidationMemberAccess.GetValue(value, memberName);
            if (ValidationMemberAccess.IsProvided(memberValue))
            {
                providedCount++;
            }
        }

        if (providedCount == 1)
        {
            return ValidationResult.Success;
        }

        var errorMessage = FormatErrorMessage(validationContext.DisplayName);
        return new ValidationResult(errorMessage, _memberNames);
    }
}
