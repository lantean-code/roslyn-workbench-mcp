namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Caches the request properties and validation rules needed to bind one tool request type.
/// </summary>
internal sealed class ToolRequestBindingMetadata
{
    /// <summary>
    /// Gets the names of properties that must be present in the incoming arguments.
    /// </summary>
    public string[] RequiredNames { get; }

    /// <summary>
    /// Gets each required property's position in the generated tool method signature.
    /// </summary>
    public Dictionary<string, int> RequiredIndexes { get; }

    /// <summary>
    /// Gets enum properties that require defined-value validation.
    /// </summary>
    public EnumArgumentMetadata[] EnumArguments { get; }

    /// <summary>
    /// Gets each enum property's position in the generated tool method signature.
    /// </summary>
    public Dictionary<string, int> EnumIndexes { get; }

    /// <summary>
    /// Gets properties with data-annotation validation rules.
    /// </summary>
    public ValidationArgumentMetadata[] ValidationArguments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolRequestBindingMetadata"/> class.
    /// </summary>
    /// <param name="requiredNames">The names of request properties required by the generated schema.</param>
    /// <param name="requiredIndexes">The parameter indexes corresponding to required request properties.</param>
    /// <param name="enumArguments">The enum argument accessors used during request validation.</param>
    /// <param name="enumIndexes">The parameter indexes of enum arguments in the tool method.</param>
    /// <param name="validationArguments">The argument metadata used by request validation rules.</param>
    public ToolRequestBindingMetadata(
        string[] requiredNames,
        Dictionary<string, int> requiredIndexes,
        EnumArgumentMetadata[] enumArguments,
        Dictionary<string, int> enumIndexes,
        ValidationArgumentMetadata[] validationArguments)
    {
        RequiredNames = requiredNames;
        RequiredIndexes = requiredIndexes;
        EnumArguments = enumArguments;
        EnumIndexes = enumIndexes;
        ValidationArguments = validationArguments;
    }
}
