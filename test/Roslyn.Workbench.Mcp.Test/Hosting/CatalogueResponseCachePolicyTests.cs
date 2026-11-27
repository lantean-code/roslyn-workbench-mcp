namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class CatalogueResponseCachePolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("2025-11-25")]
    public void GIVEN_LegacySession_WHEN_ApplyingHints_THEN_ShouldLeaveHintsAbsent(string? protocolVersion)
    {
        var result = new ListToolsResult();

        CatalogueResponseCachePolicy.ApplyCatalogueHints(result, null, protocolVersion);

        result.TimeToLive.Should().BeNull();
        result.CacheScope.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 60000)]
    [InlineData(1, 60000)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public void GIVEN_CatalogueRequest_WHEN_ApplyingHints_THEN_ShouldUsePrivateFreshnessExceptForContinuations(int requestKind, int milliseconds)
    {
        ListToolsRequestParams? request = requestKind == 0 ? null : new ListToolsRequestParams();
        if (requestKind == 2)
        {
            request = new ListToolsRequestParams { InputResponses = new Dictionary<string, InputResponse>() };
        }
        else if (requestKind == 3)
        {
            request = new ListToolsRequestParams { RequestState = "RequestState" };
        }

        var result = new ListToolsResult();

        CatalogueResponseCachePolicy.ApplyCatalogueHints(result, request, "2026-07-28");

        result.TimeToLive.Should().Be(TimeSpan.FromMilliseconds(milliseconds));
        result.CacheScope.Should().Be(CacheScope.Private);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("probe://resource")]
    [InlineData("skill://roslyn-workbench-other/SKILL.md")]
    public void GIVEN_UnownedResource_WHEN_ApplyingHints_THEN_ShouldPreserveExistingPolicy(string? uri)
    {
        var result = new ReadResourceResult { TimeToLive = TimeSpan.Zero, CacheScope = CacheScope.Private };
        var request = uri is null ? null : new ReadResourceRequestParams { Uri = uri };

        CatalogueResponseCachePolicy.ApplySkillHints(result, request, "2026-07-28");

        result.TimeToLive.Should().Be(TimeSpan.Zero);
        result.CacheScope.Should().Be(CacheScope.Private);
    }

    [Theory]
    [InlineData(false, 300000)]
    [InlineData(true, 0)]
    public void GIVEN_EmbeddedSkill_WHEN_ApplyingHints_THEN_ShouldUsePrivateFreshnessExceptForContinuations(bool continuation, int milliseconds)
    {
        var result = new ReadResourceResult();
        var request = new ReadResourceRequestParams
        {
            Uri = "skill://roslyn-workbench/SKILL.md",
            RequestState = continuation ? "RequestState" : null,
        };

        CatalogueResponseCachePolicy.ApplySkillHints(result, request, "2026-07-28");

        result.TimeToLive.Should().Be(TimeSpan.FromMilliseconds(milliseconds));
        result.CacheScope.Should().Be(CacheScope.Private);
    }
}
