namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal interface IRegisteredPluginTool
{
    RegisteredTool Tool { get; }

    TResult Accept<TResult>(IPluginToolRegistrationVisitor<TResult> visitor);
}
