namespace Roslyn.Workbench.Mcp.Performance;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record ExternalMemberInsertionDefinition
{
    public required string Path { get; init; }

    public required string TypeDeclaration { get; init; }

    public required string MemberDeclaration { get; init; }
}
#pragma warning restore CA1812
