using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

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

    public static Task<InspectionSampleFixture> CreateAsync()
    {
        return CreateAsync(new InspectionSampleFixtureOptions());
    }

    public static async Task<InspectionSampleFixture> CreateAsync(InspectionSampleFixtureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-inspection-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);

        var projectPath = Path.Combine(directoryPath, "Sample.csproj");
        var documentPath = Path.Combine(directoryPath, "Formatting.cs");
        var partialDocumentOnePath = Path.Combine(directoryPath, "PartialFormatter.cs");
        var partialDocumentTwoPath = Path.Combine(directoryPath, "PartialFormatter.Implementation.cs");
        var missingUsingsDocumentPath = Path.Combine(directoryPath, "MissingUsings.cs");
        var usingsDocumentPath = Path.Combine(directoryPath, "Usings.cs");
        var bannerTargetDocumentPath = Path.Combine(directoryPath, "BannerTarget.cs");
        var bannerReferenceDocumentPath = Path.Combine(directoryPath, "BannerReference.cs");
        var namespaceConversionDocumentPath = Path.Combine(directoryPath, "NamespaceConversion.cs");
        var enableNullableDocumentPath = Path.Combine(directoryPath, "EnableNullable.cs");
        var consoleTopLevelDocumentPath = Path.Combine(directoryPath, "ConsoleTopLevel.cs");
        var consoleProgramMainDocumentPath = Path.Combine(directoryPath, "ConsoleProgramMain.cs");
        var rawStringDocumentPath = Path.Combine(directoryPath, "RawString.cs");
        var extensionMethodsDocumentPath = Path.Combine(directoryPath, "ExtensionMethods.cs");
        var addParameterCheckDocumentPath = Path.Combine(directoryPath, "AddParameterCheck.cs");
        var primaryConstructorInitializationDocumentPath = Path.Combine(directoryPath, "PrimaryConstructorInitialization.cs");
        var wrappingDocumentPath = Path.Combine(directoryPath, "Wrapping.cs");
        var fullyQualifyDocumentPath = Path.Combine(directoryPath, "FullyQualify.cs");
        var simplifyTypeNamesDocumentPath = Path.Combine(directoryPath, "SimplifyTypeNames.cs");
        var simplifyThisOrMeDocumentPath = Path.Combine(directoryPath, "SimplifyThisOrMe.cs");
        var removeUnusedVariableDocumentPath = Path.Combine(directoryPath, "RemoveUnusedVariable.cs");
        var spellCheckDocumentPath = Path.Combine(directoryPath, "SpellCheck.cs");
        var usePatternMatchingDocumentPath = Path.Combine(directoryPath, "UsePatternMatching.cs");
        var jsonDetectionDocumentPath = Path.Combine(directoryPath, "JsonDetection.cs");
        var namespaceSyncDirectoryPath = Path.Combine(directoryPath, "FolderSync");
        var namespaceSyncDocumentPath = Path.Combine(namespaceSyncDirectoryPath, "NamespaceSyncSample.cs");
        var directoryBuildPropsPath = Path.Combine(directoryPath, "Directory.Build.props");
        var editorConfigPath = Path.Combine(directoryPath, ".editorconfig");
        Directory.CreateDirectory(namespaceSyncDirectoryPath);

        var outputTypeElement = string.IsNullOrWhiteSpace(options.OutputType)
            ? string.Empty
            : $"""
                <OutputType>{options.OutputType}</OutputType>
            """;
        var additionalProjectPropertiesText = string.IsNullOrWhiteSpace(options.AdditionalProjectPropertiesText)
            ? string.Empty
            : $"""
                {options.AdditionalProjectPropertiesText}
            """;
        await File.WriteAllTextAsync(projectPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
            {{outputTypeElement}}
                <Nullable>{{options.Nullable}}</Nullable>
                <GenerateDocumentationFile>true</GenerateDocumentationFile>
            {{additionalProjectPropertiesText}}
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(documentPath, """
            using System;
            using System.Collections.Generic;
            using System.IO;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;

            namespace Sample;

            /// <summary>Formats greeting values.</summary>
            public interface IMessageFormatter
            {
                string Format(string value);
            }

            public interface IGoo
            {
                void Goo1();

                void Goo2();
            }

            public interface IBar
            {
                void Bar();
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

            public sealed class AlphaCycle
            {
                public BetaCycle? Beta { get; init; }
            }

            public sealed class BetaCycle
            {
                public AlphaCycle? Alpha { get; init; }
            }

            public static class FormatterCallerTests
            {
                public static void GIVEN_FormatterCaller_WHEN_CallingCall_THEN_ShouldReturnFormattedGreeting()
                {
                    _ = FormatterCaller.Call();
                }

                public static void Helper()
                {
                    _ = new GreetingFormatter().Decorate("helper");
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

                public static string AnalyseExceptional(string? value)
                {
                    try
                    {
                        if (value is null)
                        {
                            throw new ArgumentNullException(nameof(value));
                        }

                        return value.Trim();
                    }
                    catch (ArgumentNullException)
                    {
                        return string.Empty;
                    }
                    finally
                    {
                        _ = value?.Length ?? 0;
                    }
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

            public sealed class FieldHolder
            {
                private string _backingField = string.Empty;

                public string Read()
                {
                    return _backingField;
                }
            }

            public static class LinqSamples
            {
                public static IEnumerable<int> FilterPositive(IEnumerable<int> numbers)
                {
                    foreach (var number in numbers)
                    {
                        if (number > 0)
                        {
                            yield return number + 1;
                        }
                    }
                }

                public static IEnumerable<int> ExpandQuery(IEnumerable<int> numbers)
                {
                    return from number in numbers
                           where number > 0
                           select number + 1;
                }
            }

            public static class IntroduceVariableSamples
            {
                public static int Build()
                {
                    return 1 + 1;
                }
            }

            public abstract class PartialBase
            {
                public virtual string DecorateAgain(string value)
                {
                    return value;
                }
            }

            public sealed class OverrideCandidate : PartialBase
            {
            }

            public static class AwaitSamples
            {
                private static Task<string> GetValueAsync()
                {
                    return Task.FromResult("value");
                }

                public static async Task<string> BuildAsync()
                {
                    return GetValueAsync();
                }

                public static async Task<string> BuildAssignmentAsync()
                {
                    var value = GetValueAsync();
                    return string.Empty;
                }
            }

            public static class LoopSamples
            {
                public static int SumForeach(int[] values)
                {
                    var total = 0;
                    foreach (var value in values)
                    {
                        total += value;
                    }

                    return total;
                }

                public static int SumFor(int[] values)
                {
                    var total = 0;
                    for (var i = 0; i < values.Length; i++)
                    {
                        total += values[i];
                    }

                    return total;
                }
            }

            public static class ConditionalSamples
            {
                public static string DescribeCount(int count)
                {
                    return count == 0 ? "zero" : "non-zero";
                }

                public static string DescribeValue(int value)
                {
                    if (value == 0)
                    {
                        return "zero";
                    }
                    else if (value == 1)
                    {
                        return "one";
                    }
                    else
                    {
                        return "many";
                    }
                }

                public static int GuardedAdd(int left, int right)
                {
                    if (left > 0)
                    {
                        return left + right;
                    }

                    return right;
                }

                public static bool IsInRange(int value)
                {
                    return value > 0 && value < 10;
                }
            }

            public static class TypeStyleSamples
            {
                public static string UseExplicit()
                {
                    var explicitBuilder = new StringBuilder();
                    explicitBuilder.Append("value");
                    return explicitBuilder.ToString();
                }

                public static string UseImplicit()
                {
                    StringBuilder implicitBuilder = new StringBuilder();
                    implicitBuilder.Append("value");
                    return implicitBuilder.ToString();
                }
            }

            public static class DisposableSamples
            {
                public static int Build()
                {
                    var stream = new MemoryStream();
                    var length = stream.Length;
                    return (int)length;
                }
            }

            public static class NamedArgumentSamples
            {
                public static int Sum(int left, int right)
                {
                    return left + right;
                }

                public static int Build()
                {
                    return Sum(1, 2);
                }
            }

            public sealed class PatternPerson
            {
                public string Name { get; init; } = string.Empty;
            }

            public sealed class PatternCounter
            {
                public int C { get; init; }
            }

            public static class PatternSamples
            {
                public static bool IsAlpha(PatternPerson? person)
                {
                    return person != null && person.Name == "Alpha";
                }
            }

            public sealed class PatternFieldHolder
            {
                private PatternCounter? cf;

                public bool HasNonZeroCount()
                {
                    if (cf != null && cf.C != 0)
                    {
                        return true;
                    }

                    return false;
                }
            }

            public static class LocalFunctionSamples
            {
                public static int Build()
                {
                    var increment = 1;

                    int Local(int value)
                    {
                        return value + increment;
                    }

                    return Local(1);
                }
            }

            public static class MoveDeclarationSamples
            {
                public static int Build()
                {
                    var result = 10;
                    Console.WriteLine("prefix");
                    Console.WriteLine("middle");
                    return result;
                }

                public static void BuildNearest()
                {
                    int moved;
                    Console.WriteLine("prefix");
                    Console.WriteLine(moved);
                }
            }

            public static class QualifiedTypeSamples
            {
                public static string Build()
                {
                    System.Net.Http.HttpClient client = new();
                    return client.BaseAddress?.ToString() ?? string.Empty;
                }
            }

            public static class StringLiteralSamples
            {
                public static string BuildRegular()
                {
                    return "C:\\temp\\logs";
                }

                public static string BuildInterpolated(string value)
                {
                    return $"C:\\temp\\{value}";
                }
            }

            public static class CastSamples
            {
                public static object Box()
                {
                    return (object)1;
                }

                public static string? Unbox(object value)
                {
                    return value as string;
                }
            }

            public static class ConditionalRewriteSamples
            {
                public static int Build(bool enabled)
                {
                    var value = enabled ? 1 : 2;
                    return value;
                }

                public static int BuildAssignment(bool enabled)
                {
                    int value;
                    value = enabled ? 1 : 2;
                    return value;
                }
            }

            public static class DuplicateCodeSamples
            {
                public static int ComputeOne(int value)
                {
                    var adjusted = value + 1;
                    adjusted *= 2;
                    return adjusted - 3;
                }

                public static int ComputeTwo(int value)
                {
                    var adjusted = value + 1;
                    adjusted *= 2;
                    return adjusted - 3;
                }
            }

            public static class TupleSamples
            {
                public static (int Sum, int Count) Build()
                {
                    return (1 + 1, 2);
                }
            }

            public static class AnonymousTypeSamples
            {
                public static object Build()
                {
                    var item = new { Name = "Alpha", Count = 1 };
                    return item;
                }
            }

            public sealed class AutoPropertySamples
            {
                public int Goo { get; set; }
            }

            public sealed class FullPropertySamples
            {
                private int _score;

                public int Score
                {
                    get
                    {
                        return _score;
                    }
                    set
                    {
                        _score = value;
                    }
                }
            }

            public class ConvertibleToRecord
            {
                public int Id { get; init; }
            }

            /// <summary>Use System.IDisposable for the returned value.</summary>
            public static class DocCommentSamples
            {
                public static string Build(string value)
                {
                    return value;
                }
            }

            public sealed class PrimaryConstructorSamples(int value)
            {
                public int Value => value;
            }

            public static class NumericLiteralSamples
            {
                public static int Build()
                {
                    return 42;
                }
            }

            public static class ExpressionBodySamples
            {
                public static int Square(int value)
                {
                    return value * value;
                }

                public static Func<int, int> CreateLambda()
                {
                    return value =>
                    {
                        return value + 1;
                    };
                }
            }

            public static class InlineMethodSamples
            {
                public static int Caller()
                {
                    return AddOne(1);
                }

                private static int AddOne(int value)
                {
                    return value + 1;
                }
            }

            public sealed class ParameterInitializationSamples
            {
                private readonly string _name;

                public ParameterInitializationSamples(string name)
                {
                }
            }

            public sealed class ExplicitInterfaceSamples : IGoo, IBar
            {
                public void Goo1()
                {
                }

                public void Goo2()
                {
                }

                public void Bar()
                {
                }
            }

            public sealed class ImplicitInterfaceSamples : IGoo, IBar
            {
                void IGoo.Goo1()
                {
                }

                void IGoo.Goo2()
                {
                }

                void IBar.Bar()
                {
                }
            }

            public static class IfRewriteSamples
            {
                public static int MergeNested(bool left, bool right)
                {
                    if (left)
                    {
                        if (right)
                        {
                            return 1;
                        }
                    }

                    return 0;
                }

                public static int MergeConsecutive(bool left, bool right)
                {
                    if (left)
                    {
                        return 1;
                    }

                    if (right)
                    {
                        return 1;
                    }

                    return 0;
                }

                public static int SplitNested(bool left, bool right)
                {
                    if (left && right)
                    {
                        return 1;
                    }

                    return 0;
                }

                public static int SplitConsecutive(bool left, bool right)
                {
                    if (left || right)
                    {
                        return 1;
                    }

                    return 0;
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
        await File.WriteAllTextAsync(missingUsingsDocumentPath, """
            namespace Sample;

            public static class MissingUsingSamples
            {
                public static string Build()
                {
                    StringBuilder builder = new();
                    builder.Append("value");
                    return builder.ToString();
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
        await File.WriteAllTextAsync(bannerTargetDocumentPath, """
            using System;

            namespace Sample;

            public static class BannerTarget
            {
                public static void Run()
                {
                    Console.WriteLine(nameof(BannerTarget));
                }
            }
            """);
        await File.WriteAllTextAsync(bannerReferenceDocumentPath, """
            // Sample banner

            namespace Sample;

            public sealed class BannerReference
            {
            }
            """);
        await File.WriteAllTextAsync(namespaceConversionDocumentPath, """
            namespace Sample.Nested
            {
                public sealed class NamespaceConversionSample
                {
                }
            }
            """);
        await File.WriteAllTextAsync(enableNullableDocumentPath, """
            #nullable enable

            namespace Sample;

            public sealed class EnableNullableSample
            {
                public string? Value { get; set; }

                public int GetLength()
                {
                    return Value.Length;
                }
            }
            """);
        if (options.IncludeConsoleTopLevelDocument)
        {
            await File.WriteAllTextAsync(consoleTopLevelDocumentPath, """
                System.Console.WriteLine(0);
                """);
        }

        if (options.IncludeConsoleProgramMainDocument)
        {
            await File.WriteAllTextAsync(consoleProgramMainDocumentPath, """
                class Program
                {
                    static void Main(string[] args)
                    {
                        System.Console.WriteLine(args[0]);
                    }
                }
                """);
        }
        await File.WriteAllTextAsync(rawStringDocumentPath, """
            namespace Sample;

            public static class RawStringSample
            {
                public static string Build()
                {
                    return "raw";
                }
            }
            """);
        await File.WriteAllTextAsync(extensionMethodsDocumentPath, """
            namespace Sample;

            public static class ExtensionMethodsSample
            {
                public static string ToGreeting(this string value)
                {
                    return "Hello " + value;
                }
            }
            """);
        await File.WriteAllTextAsync(addParameterCheckDocumentPath, """
            namespace Sample;

            public sealed class AddParameterCheckSample
            {
                public AddParameterCheckSample(object value)
                {
                }
            }
            """);
        await File.WriteAllTextAsync(primaryConstructorInitializationDocumentPath, """
            namespace Sample;

            public sealed class PrimaryConstructorInitializationSample(string s)
            {
                private readonly string s;
            }
            """);
        await File.WriteAllTextAsync(wrappingDocumentPath, """
            namespace Sample;

            public static class WrappingSample
            {
                public static void Goobar(int left, int right)
                {
                }

                public static void Run(int left, int right)
                {
                    Goobar(left, right);
                }
            }
            """);
        await File.WriteAllTextAsync(fullyQualifyDocumentPath, """
            namespace Sample;

            public static class FullyQualifySample
            {
                public static CancellationToken Create()
                {
                    CancellationToken token = default;
                    return token;
                }
            }
            """);
        await File.WriteAllTextAsync(simplifyTypeNamesDocumentPath, """
            using System.Text;

            namespace Sample;

            public static class SimplifyTypeNamesSample
            {
                public static StringBuilder Create()
                {
                    System.Text.StringBuilder builder = new System.Text.StringBuilder();
                    return builder;
                }
            }
            """);
        await File.WriteAllTextAsync(simplifyThisOrMeDocumentPath, """
            namespace Sample;

            public sealed class SimplifyThisOrMeSample
            {
                private readonly int x = 1;

                public int GetValue()
                {
                    var value = this.x;
                    return value;
                }
            }
            """);
        await File.WriteAllTextAsync(removeUnusedVariableDocumentPath, """
            namespace Sample;

            public static class RemoveUnusedVariableSample
            {
                public static int Run()
                {
                    var unused = 1;
                    return 0;
                }
            }
            """);
        await File.WriteAllTextAsync(spellCheckDocumentPath, """
            namespace Sample;

            public static class SpellCheckSample
            {
                public static int GetLength(string value)
                {
                    return value.Lenght;
                }
            }
            """);
        await File.WriteAllTextAsync(usePatternMatchingDocumentPath, """
            namespace Sample;

            public sealed class UsePatternMatchingSample
            {
                private int i;

                public bool GetValue(object obj)
                {
                    return obj is UsePatternMatchingSample && ((UsePatternMatchingSample)obj).i > 0;
                }
            }
            """);
        await File.WriteAllTextAsync(jsonDetectionDocumentPath, """
            namespace Sample;

            public static class JsonDetectionSample
            {
                public static string GetPayload()
                {
                    var payload = "{ \"a\": 0 }";
                    return payload;
                }
            }
            """);
        await File.WriteAllTextAsync(namespaceSyncDocumentPath, """
            namespace Sample;

            public sealed class NamespaceSyncSample
            {
            }
            """);
        await File.WriteAllTextAsync(directoryBuildPropsPath, """
            <Project>
              <PropertyGroup>
                <LangVersion>preview</LangVersion>
              </PropertyGroup>
            </Project>
            """);
        var additionalEditorConfigText = string.IsNullOrWhiteSpace(options.AdditionalEditorConfigText)
            ? string.Empty
            : $"""

                {options.AdditionalEditorConfigText}
            """;
        await File.WriteAllTextAsync(editorConfigPath, $$"""
            root = true

            [*.cs]
            dotnet_diagnostic.CS0219.severity = warning
            dotnet_diagnostic.IDE0005.severity = warning
            csharp_style_var_for_built_in_types = false:none
            csharp_style_var_when_type_is_apparent = false:none
            csharp_style_var_elsewhere = false:none
            {{additionalEditorConfigText}}
            """);

        return new InspectionSampleFixture(directoryPath, projectPath, documentPath);
    }

    public LocationSelector GetLocation(string text)
    {
        return GetLocationInDocument(Path.GetFileName(DocumentPath), text, 0);
    }

    public LocationSelector GetLocation(string text, int occurrenceIndex)
    {
        return GetLocationInDocument(Path.GetFileName(DocumentPath), text, occurrenceIndex);
    }

    public LocationSelector GetSelection(string selectedText)
    {
        return GetSelectionInDocument(Path.GetFileName(DocumentPath), selectedText, 0);
    }

    public LocationSelector GetSelection(string selectedText, int occurrenceIndex)
    {
        return GetSelectionInDocument(Path.GetFileName(DocumentPath), selectedText, occurrenceIndex);
    }

    public LocationSelector GetCursor(string text)
    {
        return GetCursor(text, 0, 0);
    }

    public LocationSelector GetCursor(string text, int occurrenceIndex)
    {
        return GetCursor(text, occurrenceIndex, 0);
    }

    public LocationSelector GetCursor(string text, int occurrenceIndex, int offset)
    {
        return GetCursorInDocument(Path.GetFileName(DocumentPath), text, occurrenceIndex, offset);
    }

    public LocationSelector GetCursorAfter(string text)
    {
        return GetCursorAfter(text, 0);
    }

    public LocationSelector GetCursorAfter(string text, int occurrenceIndex)
    {
        return GetCursor(text, occurrenceIndex, text.Length);
    }

    public LocationSelector GetLocationInDocument(string documentPath, string text)
    {
        return GetLocationInDocument(documentPath, text, 0);
    }

    public LocationSelector GetLocationInDocument(string documentPath, string text, int occurrenceIndex)
    {
        return CreateSelector(documentPath, text, occurrenceIndex, text.Length);
    }

    public LocationSelector GetSelectionInDocument(string documentPath, string text)
    {
        return GetSelectionInDocument(documentPath, text, 0);
    }

    public LocationSelector GetSelectionInDocument(string documentPath, string text, int occurrenceIndex)
    {
        return CreateSelector(documentPath, text, occurrenceIndex, text.Length);
    }

    public LocationSelector GetCursorInDocument(string documentPath, string text)
    {
        return GetCursorInDocument(documentPath, text, 0, 0);
    }

    public LocationSelector GetCursorInDocument(string documentPath, string text, int occurrenceIndex, int offset)
    {
        return CreateSelector(documentPath, text, occurrenceIndex, 0, offset);
    }

    public LocationSelector GetSpanSelection(string startText, string endText)
    {
        var sourceText = File.ReadAllText(DocumentPath);
        var start = sourceText.IndexOf(startText, StringComparison.Ordinal);
        var end = sourceText.IndexOf(endText, start, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            return new LocationSelector();
        }

        return new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = Path.GetFileName(DocumentPath),
                },
                Start = start,
                Length = (end - start) + endText.Length,
            },
        };
    }

    private static int FindWholeToken(string sourceText, string text, int occurrenceIndex)
    {
        var index = 0;
        var currentOccurrence = 0;

        while ((index = sourceText.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
        {
            var hasLeadingBoundary = index == 0 || !IsIdentifierCharacter(sourceText[index - 1]);
            var trailingIndex = index + text.Length;
            var hasTrailingBoundary = trailingIndex >= sourceText.Length || !IsIdentifierCharacter(sourceText[trailingIndex]);
            if (hasLeadingBoundary && hasTrailingBoundary && currentOccurrence++ == occurrenceIndex)
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

    private LocationSelector CreateSelector(string documentPath, string text, int occurrenceIndex, int length, int offset = 0)
    {
        var fullPath = Path.Combine(_directoryPath, documentPath);
        var sourceText = File.ReadAllText(fullPath);
        var start = FindWholeToken(sourceText, text, occurrenceIndex) + offset;

        return new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = documentPath,
                },
                Start = start,
                Length = length,
            },
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }
}
