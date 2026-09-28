using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Test.ToolReference.EvidenceFixture;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class FactAttribute : Attribute
{
}

[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "The fixture exists as source input for semantic evidence validation and is not executed directly.")]
internal sealed class FakeFactEvidence
{
    [Fact]
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "The fixture intentionally models the instance method shape used by an xUnit test.")]
    public void MethodWithNonXunitFactAttribute()
    {
    }
}
