using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Represents either normalised MSBuild workspace properties or their validation error.
/// </summary>
internal sealed class WorkspaceMsBuildPropertiesResolution
{
    /// <summary>
    /// Gets the validation error when resolution failed.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    /// <summary>
    /// Gets the normalised properties when resolution succeeded and at least one value was supplied.
    /// </summary>
    public WorkspaceMsBuildProperties? Properties { get; }

    /// <summary>
    /// Gets a value indicating whether resolution failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;

    private WorkspaceMsBuildPropertiesResolution(
        WorkspaceMsBuildProperties? properties,
        WorkspaceOperationError? error)
    {
        Properties = properties;
        Error = error;
    }

    /// <summary>
    /// Creates a failed property resolution.
    /// </summary>
    /// <param name="error">The validation error.</param>
    /// <returns>The failed resolution.</returns>
    public static WorkspaceMsBuildPropertiesResolution Failure(WorkspaceOperationError error)
    {
        return new WorkspaceMsBuildPropertiesResolution(
            properties: null,
            error);
    }

    /// <summary>
    /// Creates a successful property resolution.
    /// </summary>
    /// <param name="properties">The normalised properties, or <see langword="null"/> when no values were supplied.</param>
    /// <returns>The successful resolution.</returns>
    public static WorkspaceMsBuildPropertiesResolution Success(WorkspaceMsBuildProperties? properties)
    {
        return new WorkspaceMsBuildPropertiesResolution(
            properties,
            error: null);
    }
}
