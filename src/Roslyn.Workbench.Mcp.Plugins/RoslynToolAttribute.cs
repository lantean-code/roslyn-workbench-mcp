namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Declares the transport metadata for a plugin tool handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RoslynToolAttribute : Attribute
{
    /// <summary>
    /// Gets the globally unique MCP tool name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the title displayed to users.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the tool description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets or sets the optional concise result summary.
    /// </summary>
    public string? ResultSummary { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tool can replace, remove, or persist source.
    /// </summary>
    public bool Destructive { get; set; }

    /// <summary>
    /// Initialises tool metadata.
    /// </summary>
    /// <param name="name">The globally unique MCP tool name.</param>
    /// <param name="title">The title displayed to users.</param>
    /// <param name="description">The tool description.</param>
    public RoslynToolAttribute(string name, string title, string description)
    {
        Name = name;
        Title = title;
        Description = description;
    }
}
