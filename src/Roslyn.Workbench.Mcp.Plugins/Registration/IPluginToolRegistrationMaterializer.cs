namespace Roslyn.Workbench.Mcp.Plugins.Registration;

/// <summary>
/// Creates plugin-owned singleton services and strongly typed handler registrations from prepared definitions.
/// </summary>
internal interface IPluginToolRegistrationMaterializer
{
    /// <summary>
    /// Materializes a prepared plugin configuration and transfers service-provider ownership to the result.
    /// </summary>
    /// <param name="preparation">The validated tools, services and preparation diagnostics.</param>
    /// <returns>The typed registrations, diagnostics and provider lifetime.</returns>
    PluginMaterializationResult Materialize(PluginPreparationResult preparation);
}
