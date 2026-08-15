using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceMsBuildPropertiesResolution
{
    public WorkspaceOperationError? Error { get; }

    public WorkspaceMsBuildProperties? Properties { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;

    private WorkspaceMsBuildPropertiesResolution(
        WorkspaceMsBuildProperties? properties,
        WorkspaceOperationError? error)
    {
        Properties = properties;
        Error = error;
    }

    public static WorkspaceMsBuildPropertiesResolution Failure(WorkspaceOperationError error)
    {
        return new WorkspaceMsBuildPropertiesResolution(
            properties: null,
            error);
    }

    public static WorkspaceMsBuildPropertiesResolution Success(WorkspaceMsBuildProperties? properties)
    {
        return new WorkspaceMsBuildPropertiesResolution(
            properties,
            error: null);
    }
}
