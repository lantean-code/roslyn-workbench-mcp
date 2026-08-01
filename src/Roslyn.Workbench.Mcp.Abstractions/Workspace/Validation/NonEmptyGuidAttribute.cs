using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Workspace.Validation;

/// <summary>
/// Validates that a supplied GUID is not empty.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class NonEmptyGuidAttribute : ValidationAttribute
{
    /// <summary>
    /// Initialises a new instance of the <see cref="NonEmptyGuidAttribute"/> class.
    /// </summary>
    public NonEmptyGuidAttribute()
        : base("The {0} field must not be an empty GUID.")
    {
    }

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        return value is null || value is Guid guid && guid != Guid.Empty;
    }
}
