using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Selectors;

/// <summary>
/// Represents the scope kind of a scoped tool request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScopeKind>))]
public enum ScopeKind
{
    /// <summary>
    /// The scope is the whole solution.
    /// </summary>
    Solution,

    /// <summary>
    /// The scope is one project.
    /// </summary>
    Project,

    /// <summary>
    /// The scope is one document.
    /// </summary>
    Document,

    /// <summary>
    /// The scope is a selected project set.
    /// </summary>
    Projects,
}
