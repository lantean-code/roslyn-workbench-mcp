namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Provides the immutable workspace snapshot and shared services available while a mutation handler prepares candidate changes.
/// </summary>
public interface IMutationContext : IToolExecutionContext
{
}
