namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve a control-flow graph for a symbol or location.
/// </summary>
[Description("Provide exactly one of symbol or location.")]
[RequiresExactlyOne(
    nameof(Symbol),
    nameof(Location),
    ErrorMessage = "Specify exactly one of symbol or location.")]
internal sealed record GetControlFlowGraphRequest : WorkspaceBoundRequest
{
    private const int _defaultMaxBlocks = 64;
    private const int _defaultMaxOperationsPerBlock = 32;
    private const int _defaultMaxRegions = 32;

    /// <summary>
    /// Gets the maximum number of projected basic blocks.
    /// </summary>
    [Description("The maximum number of projected basic blocks.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxBlocks)]
    public int? MaxBlocks { get; init; } = _defaultMaxBlocks;

    /// <summary>
    /// Gets the maximum number of projected flow regions.
    /// </summary>
    [Description("The maximum number of projected flow regions.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxRegions)]
    public int? MaxRegions { get; init; } = _defaultMaxRegions;

    /// <summary>
    /// Gets the maximum number of projected operations in each basic block.
    /// </summary>
    [Description("The maximum number of projected operations in each basic block.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxOperationsPerBlock)]
    public int? MaxOperationsPerBlock { get; init; } = _defaultMaxOperationsPerBlock;

    /// <summary>
    /// Gets the optional symbol selector.
    /// </summary>
    [Description("Symbol whose control-flow graph should be returned.")]
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional location selector.
    /// </summary>
    [Description("Source location whose enclosing executable body should be graphed.")]
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective max blocks.
    /// </summary>
    internal int EffectiveMaxBlocks => ResultLimit.GetEffectiveValue(MaxBlocks, _defaultMaxBlocks);

    /// <summary>
    /// Gets the effective max regions.
    /// </summary>
    internal int EffectiveMaxRegions => ResultLimit.GetEffectiveValue(MaxRegions, _defaultMaxRegions);

    /// <summary>
    /// Gets the effective max operations per block.
    /// </summary>
    internal int EffectiveMaxOperationsPerBlock => ResultLimit.GetEffectiveValue(MaxOperationsPerBlock, _defaultMaxOperationsPerBlock);
}
