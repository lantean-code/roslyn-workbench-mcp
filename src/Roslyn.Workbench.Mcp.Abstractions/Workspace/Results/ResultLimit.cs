namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Resolves optional agent-facing collection limits against their declared defaults.
/// </summary>
public static class ResultLimit
{
    /// <summary>
    /// Gets the explicitly requested limit, or the declared default when no limit was provided.
    /// </summary>
    /// <param name="requestedLimit">
    /// The optional requested limit. Zero is valid and means that no items should be returned.
    /// </param>
    /// <param name="defaultLimit">
    /// The positive default used when <paramref name="requestedLimit"/> is <see langword="null"/>.
    /// </param>
    /// <returns>The requested limit or the declared default.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requestedLimit"/> is negative, or <paramref name="defaultLimit"/> is not positive.
    /// </exception>
    public static int GetEffectiveValue(int? requestedLimit, int defaultLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultLimit);

        if (requestedLimit is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedLimit),
                requestedLimit,
                "The requested limit must be zero or greater.");
        }

        return requestedLimit ?? defaultLimit;
    }
}
