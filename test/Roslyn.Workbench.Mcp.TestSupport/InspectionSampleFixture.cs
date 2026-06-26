using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class InspectionSampleFixture : IDisposable
{
    private readonly string _directoryPath;

    private InspectionSampleFixture(string directoryPath, string projectPath, string documentPath)
    {
        _directoryPath = directoryPath;
        ProjectPath = projectPath;
        DocumentPath = documentPath;
    }

    public string DocumentPath { get; }

    public string ProjectPath { get; }

    public static async Task<InspectionSampleFixture> CreateAsync()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-inspection-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);

        var projectPath = Path.Combine(directoryPath, "Sample.csproj");
        var documentPath = Path.Combine(directoryPath, "Formatting.cs");
        var partialDocumentOnePath = Path.Combine(directoryPath, "PartialFormatter.cs");
        var partialDocumentTwoPath = Path.Combine(directoryPath, "PartialFormatter.Implementation.cs");
        var usingsDocumentPath = Path.Combine(directoryPath, "Usings.cs");
        var directoryBuildPropsPath = Path.Combine(directoryPath, "Directory.Build.props");
        var editorConfigPath = Path.Combine(directoryPath, ".editorconfig");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <GenerateDocumentationFile>true</GenerateDocumentationFile>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(documentPath, """
            using System;

            namespace Sample;

            /// <summary>Formats greeting values.</summary>
            public interface IMessageFormatter
            {
                string Format(string value);
            }

            [Obsolete("Use DerivedGreetingFormatter")]
            public abstract class FormatterBase
            {
                public string Prefix => "prefix";

                public abstract string Format(string value);

                public virtual string Decorate(string value)
                {
                    return $"[{value}]";
                }
            }

            /// <summary>Formats greeting values.</summary>
            [Serializable]
            public class GreetingFormatter : FormatterBase, IMessageFormatter
            {
                /// <summary>Formats a greeting.</summary>
                public override string Format(string value)
                {
                    var unused = 42;
                    if (value.Length == 0)
                    {
                        return string.Empty;
                    }

                    var upper = value.ToUpperInvariant();
                    return Decorate(upper);
                }

                public string Format(string value, bool excited)
                {
                    var formatted = Format(value);
                    return excited ? formatted + "!" : formatted;
                }

                public override string Decorate(string value)
                {
                    return $"Hello {value}";
                }
            }

            public sealed class DerivedGreetingFormatter : GreetingFormatter
            {
                public override string Decorate(string value)
                {
                    return base.Decorate(value) + " from derived";
                }
            }

            public static class FormatterCaller
            {
                public static string Call()
                {
                    var formatter = new GreetingFormatter();
                    return formatter.Format("hi");
                }
            }

            public static class FlowSamples
            {
                public static string Analyse(string value)
                {
                    var trimmed = value.Trim();
                    var unusedFlow = trimmed.Length;
                    if (trimmed.Length == 0)
                    {
                        return string.Empty;
                    }

                    var upper = trimmed.ToUpperInvariant();
                    return upper;
                }
            }

            public sealed class StateHolder
            {
                public string Current { get; private set; } = string.Empty;

                public void Set(string value)
                {
                    Current = value;
                }

                public string Get()
                {
                    return Current;
                }
            }
            """);
        await File.WriteAllTextAsync(partialDocumentOnePath, """
            namespace Sample;

            public partial class PartialFormatter
            {
                public partial string Build(string value);
            }
            """);
        await File.WriteAllTextAsync(partialDocumentTwoPath, """
            namespace Sample;

            public partial class PartialFormatter
            {
                public partial string Build(string value)
                {
                    var unusedPartial = value.Length;
                    return value.Trim();
                }
            }
            """);
        await File.WriteAllTextAsync(usingsDocumentPath, """
            using Sample;
            using System.Text;
            using System;

            namespace Sample;

            public static class UsingSamples
            {
                public static string BuildText( )
                {
                    StringBuilder builder=new( );
                    builder.Append(nameof(FormatterBase));
                    return builder.ToString( );
                }
            }
            """);
        await File.WriteAllTextAsync(directoryBuildPropsPath, """
            <Project>
              <PropertyGroup>
                <LangVersion>preview</LangVersion>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(editorConfigPath, """
            root = true

            [*.cs]
            dotnet_diagnostic.CS0219.severity = warning
            dotnet_diagnostic.IDE0005.severity = warning
            """);

        return new InspectionSampleFixture(directoryPath, projectPath, documentPath);
    }

    public LocationSelector GetLocation(string text)
    {
        var sourceText = File.ReadAllText(DocumentPath);
        var start = FindWholeToken(sourceText, text);

        return new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = Path.GetFileName(DocumentPath),
                },
                Start = start,
                Length = text.Length,
            },
        };
    }

    private static int FindWholeToken(string sourceText, string text)
    {
        var index = 0;
        while ((index = sourceText.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
        {
            var hasLeadingBoundary = index == 0 || !IsIdentifierCharacter(sourceText[index - 1]);
            var trailingIndex = index + text.Length;
            var hasTrailingBoundary = trailingIndex >= sourceText.Length || !IsIdentifierCharacter(sourceText[trailingIndex]);
            if (hasLeadingBoundary && hasTrailingBoundary)
            {
                return index;
            }

            index = trailingIndex;
        }

        return -1;
    }

    private static bool IsIdentifierCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }
}
