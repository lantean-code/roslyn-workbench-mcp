using Roslyn.Workbench.Mcp.CodeActions.Execution.Application;
using Roslyn.Workbench.Mcp.CodeActions.Execution.Results;
using Roslyn.Workbench.Mcp.Plugins.Execution;
using Roslyn.Workbench.Mcp.Protocol.Results;
using Roslyn.Workbench.Mcp.Workspace.State;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Defines the closed set of production types that own published error-code values.
/// </summary>
internal static class PublishedErrorCodeCatalogue
{
    /// <summary>
    /// Gets the types whose public string constants form the published error-code catalogue.
    /// </summary>
    public static IReadOnlyList<Type> ConstantOwnerTypes { get; } =
    [
        typeof(HostToolErrorCodes),
        typeof(PluginErrorCodes),
        typeof(CodeActionErrorCodes),
        typeof(WorkspaceErrorCodes),
    ];

    /// <summary>
    /// Gets the enum types whose names form published error-code values.
    /// </summary>
    public static IReadOnlyList<Type> EnumOwnerTypes { get; } =
    [
        typeof(CodeActionApplyFailureKind),
    ];
}
