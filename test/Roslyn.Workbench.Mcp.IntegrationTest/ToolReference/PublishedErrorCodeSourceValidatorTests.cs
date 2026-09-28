using System.Reflection;
using Roslyn.Workbench.Mcp.ToolReferenceGenerator;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

[Trait("Category", "Integration")]
public sealed class PublishedErrorCodeSourceValidatorTests
{
    private static readonly string[] _sourceDirectories =
    [
        "Roslyn.Workbench.Mcp",
        "Roslyn.Workbench.Mcp.Workspace",
        "Roslyn.Workbench.Mcp.CodeActions",
        "Roslyn.Workbench.Mcp.Plugins",
        "Roslyn.Workbench.Mcp.Plugins.Core",
    ];

    [Fact]
    public void GIVEN_RepositoryProductionSources_WHEN_ValidatingErrorEmitters_THEN_ShouldUseOwnedConstants()
    {
        var repositoryRoot = typeof(PublishedErrorCodeSourceValidatorTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value
            ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");

        var action = () => PublishedErrorCodeSourceValidator.Validate(repositoryRoot);

        action.Should().NotThrow();
    }

    [Fact]
    public void GIVEN_AssemblyOwnedErrorCode_WHEN_ValidatingProductionSources_THEN_ShouldAcceptEmitter()
    {
        using var directory = TemporaryDirectory.Create("published-error-code-source-tests");
        CreateSourceTree(
            directory.DirectoryPath,
            "return ErrorDispatchResult.Rejected(errorMessage: message, errorCode: HostToolErrorCodes.InvalidRequest);");

        var action = () => PublishedErrorCodeSourceValidator.Validate(directory.DirectoryPath);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("return ToolResult.Rejected<T>(\"InvalidRequest\", message);", "InvalidRequest")]
    [InlineData("var error = new ToolError { Code = \"InvalidRequest\" };", "InvalidRequest")]
    [InlineData("writer.WriteString(\"code\", \"UnhandledException\");", "UnhandledException")]
    [InlineData("writer.WriteString(value: FeatureErrorCodes.InvalidRequest, propertyName: \"code\");", "InvalidRequest")]
    [InlineData("const string code = \"InvalidRequest\"; return ToolResult.Rejected<T>(code, message);", "InvalidRequest")]
    [InlineData("return ToolResult.Rejected<T>(FeatureErrorCodes.InvalidRequest, message);", "InvalidRequest")]
    [InlineData("return ToolResult.Rejected<T>(message: message, code: FeatureErrorCodes.InvalidRequest);", "InvalidRequest")]
    [InlineData("return ErrorDispatchResult.Rejected(errorMessage: message, errorCode: FeatureErrorCodes.InvalidRequest);", "InvalidRequest")]
    public void GIVEN_EmbeddedErrorCode_WHEN_ValidatingProductionSources_THEN_ShouldRejectEmitter(
        string source,
        string expectedCode)
    {
        using var directory = TemporaryDirectory.Create("published-error-code-source-tests");
        CreateSourceTree(directory.DirectoryPath, source);

        var action = () => PublishedErrorCodeSourceValidator.Validate(directory.DirectoryPath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Published tool error codes*{expectedCode}*");
    }

    private static void CreateSourceTree(string repositoryRoot, string source)
    {
        foreach (var sourceDirectory in _sourceDirectories)
        {
            var directory = Path.Combine(repositoryRoot, "src", sourceDirectory);
            Directory.CreateDirectory(directory);
        }

        var catalogueFile = Path.Combine(repositoryRoot, "src", "Roslyn.Workbench.Mcp", "HostToolErrorCodes.cs");
        File.WriteAllText(
            catalogueFile,
            "namespace Roslyn.Workbench.Mcp.Protocol.Results; "
            + "internal static class HostToolErrorCodes { public const string InvalidRequest = \"InvalidRequest\"; }");

        var sourceFile = Path.Combine(repositoryRoot, "src", "Roslyn.Workbench.Mcp", "Emitter.cs");
        var emitterSource = $$"""
            using Roslyn.Workbench.Mcp.Protocol.Results;
            using System.Text.Json;

            internal static class FeatureErrorCodes
            {
                public const string InvalidRequest = "InvalidRequest";
            }

            internal sealed class ToolError
            {
                public string Code { get; init; } = string.Empty;
            }

            internal static class ErrorDispatchResult
            {
                public static object Rejected(string errorCode, string errorMessage)
                {
                    return new object();
                }
            }

            internal sealed class Emitter
            {
                public dynamic Run<T>(dynamic ToolResult, Utf8JsonWriter writer, string message)
                {
                    {{source}}
                    return null;
                }
            }
            """;

        File.WriteAllText(sourceFile, emitterSource);
    }
}
