namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve a control-flow graph for a symbol or location.
/// </summary>
internal sealed record GetControlFlowGraphRequest : WorkspaceBoundRequest
{
    private const int _defaultMaxBlocks = 64;
    private const int _defaultMaxOperationsPerBlock = 32;
    private const int _defaultMaxRegions = 32;

    /// <summary>
    /// Gets the maximum number of projected basic blocks.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxBlocks)]
    public int? MaxBlocks { get; init; } = _defaultMaxBlocks;

    /// <summary>
    /// Gets the maximum number of projected flow regions.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxRegions)]
    public int? MaxRegions { get; init; } = _defaultMaxRegions;

    /// <summary>
    /// Gets the maximum number of projected operations in each basic block.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxOperationsPerBlock)]
    public int? MaxOperationsPerBlock { get; init; } = _defaultMaxOperationsPerBlock;

    /// <summary>
    /// Gets the optional symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional location selector.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveMaxBlocks => ResultLimit.GetEffectiveValue(MaxBlocks, _defaultMaxBlocks);

    internal int EffectiveMaxRegions => ResultLimit.GetEffectiveValue(MaxRegions, _defaultMaxRegions);

    internal int EffectiveMaxOperationsPerBlock => ResultLimit.GetEffectiveValue(MaxOperationsPerBlock, _defaultMaxOperationsPerBlock);
}
