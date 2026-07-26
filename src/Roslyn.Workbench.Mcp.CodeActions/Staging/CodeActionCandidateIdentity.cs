namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class CodeActionCandidateIdentity : IEquatable<CodeActionCandidateIdentity>
{
    private readonly int[] _actionPath;
    private readonly string[] _diagnosticIds;
    private readonly TextSpan? _targetSpan;

    private string ProviderId { get; }

    private string Title { get; }

    private string? EquivalenceKey { get; }

    public CodeActionCandidateIdentity(
        string providerId,
        string title,
        string? equivalenceKey,
        IReadOnlyList<int>? actionPath = null,
        IReadOnlyList<string>? diagnosticIds = null,
        TextSpan? targetSpan = null)
    {
        ProviderId = providerId;
        Title = title;
        EquivalenceKey = equivalenceKey;
        _actionPath = actionPath?.ToArray() ?? [];
        _diagnosticIds = diagnosticIds?.OrderBy(static id => id, StringComparer.Ordinal).ToArray() ?? [];
        _targetSpan = targetSpan;
    }

    public bool Equals(CodeActionCandidateIdentity? other)
    {
        return other is not null
            && string.Equals(ProviderId, other.ProviderId, StringComparison.Ordinal)
            && string.Equals(Title, other.Title, StringComparison.Ordinal)
            && string.Equals(EquivalenceKey, other.EquivalenceKey, StringComparison.Ordinal)
            && _actionPath.SequenceEqual(other._actionPath)
            && _diagnosticIds.SequenceEqual(other._diagnosticIds, StringComparer.Ordinal)
            && _targetSpan == other._targetSpan;
    }

    public override bool Equals(object? obj)
    {
        return obj is CodeActionCandidateIdentity other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ProviderId, StringComparer.Ordinal);
        hashCode.Add(Title, StringComparer.Ordinal);
        hashCode.Add(EquivalenceKey, StringComparer.Ordinal);
        hashCode.Add(_actionPath.Length);
        foreach (var pathSegment in _actionPath)
        {
            hashCode.Add(pathSegment);
        }

        hashCode.Add(_diagnosticIds.Length);
        foreach (var diagnosticId in _diagnosticIds)
        {
            hashCode.Add(diagnosticId, StringComparer.Ordinal);
        }

        hashCode.Add(_targetSpan);
        return hashCode.ToHashCode();
    }
}
