using System.Collections.Immutable;

using Microsoft.CodeAnalysis.FindSymbols;

using Roslyn.Workbench.Mcp.Workspace.References.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.References.Caching;

public sealed class ReferenceDiscoveryCacheEntryTests
{
    [Fact]
    public void GIVEN_NoReferencedSymbols_WHEN_CreatingEntry_THEN_ShouldUseMinimumSize()
    {
        var target = new ReferenceDiscoveryCacheEntry(ImmutableArray<ReferencedSymbol>.Empty);

        target.ReferencedSymbols.Should().BeEmpty();
        target.Size.Should().Be(1);
    }
}
