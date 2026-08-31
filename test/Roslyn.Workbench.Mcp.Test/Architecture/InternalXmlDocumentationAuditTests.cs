namespace Roslyn.Workbench.Mcp.Test.Architecture;

public sealed class InternalXmlDocumentationAuditTests
{
    private const string BaselinePath = "test/Roslyn.Workbench.Mcp.Test/Architecture/InternalXmlDocumentationBaseline.txt";

    [Fact]
    public void GIVEN_ProductionSource_WHEN_InspectingInternalDocumentation_THEN_ShouldMatchApprovedBaseline()
    {
        var findings = InternalXmlDocumentationAudit.FindUndocumentedDeclarations(
            ProductionSourceAudit.EnumerateSourceFiles());

        var expected = File
            .ReadAllLines(Path.Combine(ProductionSourceAudit.RepositoryRoot, BaselinePath))
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var actual = findings
            .Select(static finding => finding.Key)
            .ToArray();

        var locations = string.Join(
            Environment.NewLine,
            findings.Select(static finding => $"{finding.Path}:{finding.Line}: {finding.Key}"));

        actual.Should().Equal(expected, locations);
    }

    [Theory]
    [InlineData("Roslyn.Workbench.Mcp.Abstractions")]
    [InlineData("Roslyn.Workbench.Mcp.Plugins.Analyzers")]
    [InlineData("Roslyn.Workbench.Mcp.Plugins")]
    [InlineData("Roslyn.Workbench.Mcp.Workspace")]
    [InlineData("Roslyn.Workbench.Mcp.CodeActions")]
    [InlineData("Roslyn.Workbench.Mcp")]
    [InlineData("Roslyn.Workbench.Mcp.Plugins.Core")]
    public void GIVEN_ProductionProject_WHEN_InspectingDocumentationQuality_THEN_ShouldHaveCompleteMemberContracts(string projectName)
    {
        var projectPath = Path.Combine(ProductionSourceAudit.RepositoryRoot, "src", projectName)
            + Path.DirectorySeparatorChar;
        var projectFiles = ProductionSourceAudit
            .EnumerateSourceFiles()
            .Where(path => Path.GetFullPath(path).StartsWith(projectPath, StringComparison.Ordinal));
        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(
            projectFiles);

        var summary = string.Join(
            ", ",
            findings
                .GroupBy(static finding => finding.Rule)
                .OrderBy(static group => group.Key)
                .Select(static group => $"{group.Key}: {group.Count()}"));
        var locations = string.Join(
            Environment.NewLine,
            findings.Select(static finding => $"{finding.Path}:{finding.Line}: {finding.Rule}: {finding.Key}: {finding.Message}"));
        var details = $"{summary}{Environment.NewLine}{locations}";

        findings.Should().BeEmpty(details);
    }

    [Fact]
    public void GIVEN_MixedAccessibilityAndDocumentation_WHEN_InspectingSource_THEN_ShouldReportOnlyGovernedUndocumentedDeclarations()
    {
        const string source = """
            namespace Example;

            /// <summary>Represents an implicitly internal top-level type.</summary>
            class ImplicitInternalType
            {
            }

            /// <summary>Defines a maintained contract.</summary>
            internal interface IContract
            {
                void Missing(string value);

                /// <summary>Gets a covered value.</summary>
                int Covered { get; }

                /// <summary>Gets a value implemented explicitly.</summary>
                int ExplicitProperty { get; }

                /// <summary>Gets an item implemented explicitly.</summary>
                string this[int index] { get; }

                /// <summary>Occurs when the implementation changes.</summary>
                event EventHandler Changed;
            }

            /// <summary>Provides one documented partial declaration.</summary>
            internal partial class PartialType
            {
                /// <inheritdoc/>
                public void Inherited()
                {
                }

                private void PrivateMember()
                {
                }
            }

            /// <summary>Transforms a value.</summary>
            internal delegate TResult Transformer<TValue, TResult>(ref TValue value);

            /// <summary>Exercises every supported member form.</summary>
            internal class DocumentedMembers
            {
                /// <summary>Stores a value.</summary>
                public const int Value = 1;

                /// <summary>Initializes a new instance.</summary>
                public DocumentedMembers()
                {
                }

                /// <summary>Adds two instances.</summary>
                public static DocumentedMembers operator +(DocumentedMembers left, DocumentedMembers right)
                {
                    return left;
                }

                /// <summary>Converts an instance to its numeric value.</summary>
                public static implicit operator int(DocumentedMembers value)
                {
                    return Value;
                }

                /// <summary>Gets a value by index.</summary>
                public int this[int index] => index;

                /// <summary>Occurs when the value changes.</summary>
                public event EventHandler? Changed;

                /// <summary>Occurs when a custom change is raised.</summary>
                public event EventHandler? CustomChanged
                {
                    add
                    {
                    }

                    remove
                    {
                    }
                }

                /// <summary>Represents a publicly addressable nested type.</summary>
                public class PublicNestedType
                {
                }

                private class PrivateNestedType
                {
                    public void ExcludedMember()
                    {
                    }
                }

                class ImplicitlyPrivateNestedType
                {
                    public void ExcludedMember()
                    {
                    }
                }
            }

            /// <summary>Defines a container whose nested interface remains governed.</summary>
            internal interface IContainer
            {
                /// <summary>Defines a nested contract.</summary>
                interface INestedContract
                {
                    /// <summary>Runs the nested operation.</summary>
                    void Run();
                }
            }

            internal partial class PartialType
            {
            }

            file class FileLocalType
            {
                public void FileLocalMember()
                {
                }
            }

            internal enum MissingState
            {
                MissingValue,

                /// <summary>Represents a covered value.</summary>
                CoveredValue,
            }

            internal class MissingType
            {
                public int MissingProperty { get; }

                private protected void MissingHook()
                {
                }

                void IContract.Missing(string value)
                {
                }

                int IContract.ExplicitProperty => 0;

                string IContract.this[int index] => string.Empty;

                event EventHandler IContract.Changed
                {
                    add
                    {
                    }

                    remove
                    {
                    }
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindUndocumentedDeclarations(source, "src/Example/Example.cs");

        findings.Select(static finding => finding.Key).Should().Equal(
        [
            "Example|Example.IContract|method Missing(string)",
            "Example|Example.MissingState|enum member MissingValue",
            "Example|Example.MissingType|method MissingHook()",
            "Example|Example.MissingType|property MissingProperty",
            "Example|Example|type MissingState",
            "Example|Example|type MissingType",
        ]);
    }

    [Fact]
    public void GIVEN_DocumentedGlobalType_WHEN_InspectingSource_THEN_ShouldReportNoFindings()
    {
        const string source = """
            /// <summary>Represents an implicitly internal global type.</summary>
            class GlobalType
            {
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindUndocumentedDeclarations(source, "src/Example/GlobalType.cs");

        findings.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_NonDocumentationCommentsAndIncompleteDocumentation_WHEN_InspectingSource_THEN_ShouldReportEveryDeclaration()
    {
        const string source = """
            namespace Example;

            // <summary>This is an ordinary comment.</summary>
            internal class OrdinaryCommentType
            {
            }

            /* <summary>This is a block comment.</summary> */
            internal class BlockCommentType
            {
            }

            /// <summary></summary>
            internal class EmptySummaryType
            {
            }

            /// <summary>
            /// </summary>
            internal class MultilineEmptySummaryType
            {
            }

            /// <remarks>This is not the declaration summary.</remarks>
            internal class RemarksOnlyType
            {
            }

            #if false
            /// <summary>This documentation is disabled.</summary>
            #endif
            internal class DisabledDocumentationType
            {
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindUndocumentedDeclarations(source, "src/Example/Comments.cs");

        findings.Select(static finding => finding.Key).Should().Equal(
        [
            "Example|Example|type BlockCommentType",
            "Example|Example|type DisabledDocumentationType",
            "Example|Example|type EmptySummaryType",
            "Example|Example|type MultilineEmptySummaryType",
            "Example|Example|type OrdinaryCommentType",
            "Example|Example|type RemarksOnlyType",
        ]);
    }

    [Fact]
    public void GIVEN_IncompleteMemberDocumentation_WHEN_InspectingDocumentationQuality_THEN_ShouldReportEveryMissingContractElement()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Creates values for the requested type.
            /// </summary>
            internal static class ValueFactory
            {
                /// <summary>Creates a named value.</summary>
                public static TValue Create<TValue>(string name)
                {
                    return default!;
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFactory.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.SummaryLayout,
            InternalXmlDocumentationQualityRule.Parameter,
            InternalXmlDocumentationQualityRule.TypeParameter,
            InternalXmlDocumentationQualityRule.Returns,
        ]);
    }

    [Fact]
    public void GIVEN_CompleteMemberDocumentationAndImplementedInheritdoc_WHEN_InspectingDocumentationQuality_THEN_ShouldReportNoFindings()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Formats values.
            /// </summary>
            internal interface IValueFormatter
            {
                /// <summary>
                /// Formats a value.
                /// </summary>
                /// <param name="value">The value to format.</param>
                /// <returns>The formatted value.</returns>
                string Format(string value);
            }

            /// <summary>
            /// Creates values for the requested type.
            /// </summary>
            internal sealed class ValueFactory : IValueFormatter
            {
                /// <summary>
                /// Creates a named value.
                /// </summary>
                /// <typeparam name="TValue">The type of value to create.</typeparam>
                /// <param name="name">The value name.</param>
                /// <returns>The created value.</returns>
                public static TValue Create<TValue>(string name)
                {
                    return default!;
                }

                /// <inheritdoc/>
                public string Format(string value)
                {
                    return value;
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFactory.cs");

        findings.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_StandaloneInheritdoc_WHEN_InspectingDocumentationQuality_THEN_ShouldReportInheritdoc()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Formats values.
            /// </summary>
            internal static class ValueFormatter
            {
                /// <inheritdoc/>
                public static string Format(string value)
                {
                    return value;
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFormatter.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.Inheritdoc,
        ]);
    }

    [Fact]
    public void GIVEN_UnrelatedInstanceMemberInImplementingType_WHEN_InspectingDocumentationQuality_THEN_ShouldReportInheritdoc()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Releases held resources.
            /// </summary>
            internal interface IReleasable
            {
                /// <summary>
                /// Releases held resources.
                /// </summary>
                void Release();
            }

            /// <summary>
            /// Formats values.
            /// </summary>
            internal sealed class ValueFormatter : IReleasable
            {
                /// <inheritdoc/>
                public string Format(string value)
                {
                    return value;
                }

                /// <inheritdoc/>
                public void Release()
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFormatter.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.Inheritdoc,
        ]);
    }

    [Theory]
    [InlineData("Performs the create operation.")]
    [InlineData("Creates associated value.")]
    [InlineData("Creates the created .")]
    [InlineData("Resolves d.")]
    [InlineData("Acquires d.")]
    [InlineData("Gets id.")]
    [InlineData("Creates disabled.")]
    [InlineData("Creates structured.")]
    [InlineData("Registers all.")]
    [InlineData("Gets the succeeded.")]
    [InlineData("Gets the or create.")]
    [InlineData("Gets a value indicating whether has failure.")]
    [InlineData("Gets a value indicating whether is valid.")]
    [InlineData("Gets a value indicating whether can prepare.")]
    [InlineData("Gets a value indicating whether candidate solution.")]
    [InlineData("Uses the value used when performing the operation.")]
    [InlineData("Uses the value used when initializing the new instance.")]
    public void GIVEN_GeneratedPlaceholderSummary_WHEN_InspectingDocumentationQuality_THEN_ShouldReportSummaryContent(string summary)
    {
        var source = $$"""
            namespace Example;

            /// <summary>
            /// {{summary}}
            /// </summary>
            internal static class ValueFactory
            {
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFactory.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.SummaryContent,
        ]);
    }

    [Theory]
    [InlineData("Creates a new instance of the value factory.")]
    [InlineData("Constructs the value factory.")]
    public void GIVEN_NonStandardConstructorSummary_WHEN_InspectingDocumentationQuality_THEN_ShouldReportSummaryContent(string summary)
    {
        var source = $$"""
            namespace Example;

            /// <summary>
            /// Creates values.
            /// </summary>
            internal sealed class ValueFactory
            {
                /// <summary>
                /// {{summary}}
                /// </summary>
                public ValueFactory()
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFactory.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.SummaryContent,
        ]);
    }

    [Fact]
    public void GIVEN_StandardConstructorSummary_WHEN_InspectingDocumentationQuality_THEN_ShouldReportNoFindings()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Creates values.
            /// </summary>
            internal sealed class ValueFactory
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="ValueFactory"/> class.
                /// </summary>
                public ValueFactory()
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFactory.cs");

        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Initializes a new instance of the ValueFactory class.")]
    [InlineData("Initializes a new instance of the <see cref=\"UnrelatedType\"/> class.")]
    [InlineData("Initializes a new instance of the <see cref=\"ValueFactory\"/><see cref=\"ValueFactory\"/> class.")]
    [InlineData("Initializes a new instance of the class.<see cref=\"ValueFactory\"/>")]
    public void GIVEN_ConstructorSummaryWithoutMatchingCref_WHEN_InspectingDocumentationQuality_THEN_ShouldReportSummaryContent(string summary)
    {
        var source = $$"""
            namespace Example;

            /// <summary>
            /// Represents an unrelated type.
            /// </summary>
            internal sealed class UnrelatedType
            {
            }

            /// <summary>
            /// Creates values.
            /// </summary>
            internal sealed class ValueFactory
            {
                /// <summary>
                /// {{summary}}
                /// </summary>
                public ValueFactory()
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFactory.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.SummaryContent,
        ]);
    }

    [Fact]
    public void GIVEN_StandardStructConstructorSummary_WHEN_InspectingDocumentationQuality_THEN_ShouldReportNoFindings()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Represents a value.
            /// </summary>
            internal readonly struct Value
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="Value"/> structure.
                /// </summary>
                public Value()
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/Value.cs");

        findings.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ClassWordingOnStructConstructor_WHEN_InspectingDocumentationQuality_THEN_ShouldReportSummaryContent()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Represents a value.
            /// </summary>
            internal readonly record struct Value
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="Value"/> class.
                /// </summary>
                public Value()
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/Value.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.SummaryContent,
        ]);
    }

    [Theory]
    [InlineData("Maps the value mapper.")]
    [InlineData("Validates a value mapper.")]
    public void GIVEN_SummaryRestatesContainingType_WHEN_InspectingDocumentationQuality_THEN_ShouldReportSummaryContent(string summary)
    {
        var source = $$"""
            namespace Example;

            /// <summary>
            /// Maps values between representations.
            /// </summary>
            internal static class ValueMapper
            {
                /// <summary>
                /// {{summary}}
                /// </summary>
                public static void Map()
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueMapper.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.SummaryContent,
        ]);
    }

    [Theory]
    [InlineData("value", "The value.")]
    [InlineData("currentSolution", "The current solution.")]
    [InlineData("errorMessage", "An error message.")]
    public void GIVEN_NameOnlyParameterDescription_WHEN_InspectingDocumentationQuality_THEN_ShouldReportParameterContent(
        string parameterName,
        string description)
    {
        var source = $$"""
            namespace Example;

            /// <summary>
            /// Uses a supplied value.
            /// </summary>
            internal static class ValueConsumer
            {
                /// <summary>
                /// Processes the supplied value.
                /// </summary>
                /// <param name="{{parameterName}}">{{description}}</param>
                public static void Process(string {{parameterName}})
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueConsumer.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.ParameterContent,
        ]);
    }

    [Fact]
    public void GIVEN_MethodParameterDescribedAsConstructorState_WHEN_InspectingDocumentationQuality_THEN_ShouldReportParameterContent()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Maps values between representations.
            /// </summary>
            internal static class ValueMapper
            {
                /// <summary>
                /// Maps a value.
                /// </summary>
                /// <param name="value">The value retained by the new instance.</param>
                public static void Map(string value)
                {
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueMapper.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.ParameterContent,
        ]);
    }

    [Theory]
    [InlineData("The bool.")]
    [InlineData("The byte array.")]
    [InlineData("The string.")]
    [InlineData("The read only list.")]
    [InlineData("The t value?.")]
    [InlineData("The resolved d.")]
    [InlineData("A task that completes with the byte array.")]
    [InlineData("A task that completes with the int.")]
    [InlineData("A task that completes with the t value?.")]
    public void GIVEN_TypeOnlyReturnsDescription_WHEN_InspectingDocumentationQuality_THEN_ShouldReportReturnsContent(string description)
    {
        var source = $$"""
            namespace Example;

            /// <summary>
            /// Produces a result.
            /// </summary>
            internal static class ValueFactory
            {
                /// <summary>
                /// Produces a result.
                /// </summary>
                /// <returns>{{description}}</returns>
                public static string Create()
                {
                    return string.Empty;
                }
            }
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueFactory.cs");

        findings.Select(static finding => finding.Rule).Should().Equal(
        [
            InternalXmlDocumentationQualityRule.ReturnsContent,
        ]);
    }

    [Fact]
    public void GIVEN_VoidDelegateWithoutReturns_WHEN_InspectingDocumentationQuality_THEN_ShouldReportNoFindings()
    {
        const string source = """
            namespace Example;

            /// <summary>
            /// Handles a value.
            /// </summary>
            /// <param name="value">The value to handle.</param>
            internal delegate void ValueHandler(string value);
            """;

        var findings = InternalXmlDocumentationAudit.FindDocumentationQualityIssues(source, "src/Example/ValueHandler.cs");

        findings.Should().BeEmpty();
    }
}
