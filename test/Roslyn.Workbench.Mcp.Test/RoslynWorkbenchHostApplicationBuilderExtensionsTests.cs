using Microsoft.Extensions.Hosting;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class RoslynWorkbenchHostApplicationBuilderExtensionsTests
{
    [Fact]
    public void GIVEN_NullBuilder_WHEN_AddingRoslynWorkbench_THEN_ShouldThrowArgumentNullException()
    {
        var action = () => RoslynWorkbenchHostApplicationBuilderExtensions.AddRoslynWorkbench(null!, []);

        action.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void GIVEN_NullArgs_WHEN_AddingRoslynWorkbench_THEN_ShouldThrowArgumentNullException()
    {
        var builder = Host.CreateApplicationBuilder([]);

        var action = () => builder.AddRoslynWorkbench(null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("args");
    }
}
