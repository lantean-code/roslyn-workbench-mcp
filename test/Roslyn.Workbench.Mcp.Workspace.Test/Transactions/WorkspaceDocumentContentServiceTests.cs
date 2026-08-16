using System.Security.Cryptography;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class WorkspaceDocumentContentServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly WorkspaceDocumentContentService _target;

    public WorkspaceDocumentContentServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _target = new WorkspaceDocumentContentService();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task GIVEN_EncodedDocument_WHEN_CreatingContent_THEN_ShouldDescribeExactSerializedBytes()
    {
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        var document = CreateDocument(SourceText.From("class C { }", encoding));
        byte[] expectedBytes = [.. encoding.GetPreamble(), .. encoding.GetBytes("class C { }")];

        var result = await _target.CreateAsync(document, TestContext.Current.CancellationToken);

        result.SerializedBytes.ToArray().Should().Equal(expectedBytes);
        var sourceText = await document.GetTextAsync(TestContext.Current.CancellationToken);
        result.ContentHash.Should().Be(Convert.ToHexString(sourceText.GetContentHash().AsSpan()));
        result.SerializedBytesHash.Should().Be(Convert.ToHexString(SHA256.HashData(expectedBytes)));
        result.EncodingName.Should().Be(encoding.WebName);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutEncoding_WHEN_CreatingContent_THEN_ShouldUseUtf8()
    {
        var document = CreateDocument(SourceText.From("class C { }"));

        var result = await _target.CreateAsync(document, TestContext.Current.CancellationToken);

        byte[] expectedBytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("class C { }")];
        result.SerializedBytes.ToArray().Should().Equal(expectedBytes);
        result.EncodingName.Should().Be(Encoding.UTF8.WebName);
    }

    [Fact]
    public async Task GIVEN_SameOriginalBytesButDifferentOutputEncoding_WHEN_Matching_THEN_ShouldReturnFalse()
    {
        var bytes = Encoding.UTF8.GetBytes("class C { }");
        var withPreamble = CreateDocument(SourceText.From(bytes, bytes.Length, Encoding.UTF8));
        var encodingWithoutPreamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var textWithoutPreamble = SourceText.From(bytes, bytes.Length, encodingWithoutPreamble);
        var withoutPreamble = CreateDocument(textWithoutPreamble);
        var expected = await _target.CreateAsync(withPreamble, TestContext.Current.CancellationToken);
        var candidate = await _target.CreateAsync(withoutPreamble, TestContext.Current.CancellationToken);

        var result = _target.HasEquivalentContent(expected, candidate);

        result.Should().BeFalse();
        expected.ContentHash.Should().Be(candidate.ContentHash);
        expected.SerializedBytesHash.Should().NotBe(candidate.SerializedBytesHash);
    }

    [Fact]
    public async Task GIVEN_DifferentTextSerializesToSameBytes_WHEN_Matching_THEN_ShouldReturnFalse()
    {
        var first = CreateDocument(SourceText.From("// é", Encoding.ASCII));
        var second = CreateDocument(SourceText.From("// è", Encoding.ASCII));
        var expected = await _target.CreateAsync(first, TestContext.Current.CancellationToken);
        var candidate = await _target.CreateAsync(second, TestContext.Current.CancellationToken);

        var result = _target.HasEquivalentContent(expected, candidate);

        result.Should().BeFalse();
        expected.ContentHash.Should().NotBe(candidate.ContentHash);
        expected.SerializedBytesHash.Should().Be(candidate.SerializedBytesHash);
    }

    private Document CreateDocument(SourceText text)
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        return _workspace.AddDocument(project.Id, "Document.cs", text);
    }
}
