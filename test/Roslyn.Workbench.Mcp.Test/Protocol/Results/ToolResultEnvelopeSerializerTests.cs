namespace Roslyn.Workbench.Mcp.Test.Protocol.Results;

public sealed class ToolResultEnvelopeSerializerTests
{
    [Fact]
    public void GIVEN_NullData_WHEN_SerializingSuccess_THEN_ShouldPublishNullData()
    {
        var result = ToolResultEnvelopeSerializer.CreateSuccess<TestData>(null);

        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.GetProperty("data").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public void GIVEN_StagedMutationWithoutData_WHEN_Serializing_THEN_ShouldOmitMutationDetails()
    {
        var currentSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var result = ToolResultEnvelopeSerializer.CreateMutationSuccess(
            data: null,
            staged: true,
            currentSnapshot: currentSnapshot);

        var data = result.GetProperty("data");
        data.GetProperty("staged").GetBoolean().Should().BeTrue();
        data.TryGetProperty("summary", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_StagedMutationWithoutTransaction_WHEN_Serializing_THEN_ShouldPublishSummaryWithoutTransaction()
    {
        var stagedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var currentSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var mutation = new MutationData
        {
            Snapshot = stagedSnapshot,
            Summary = "Summary",
        };

        var result = ToolResultEnvelopeSerializer.CreateMutationSuccess(
            mutation,
            staged: true,
            currentSnapshot: currentSnapshot);

        var data = result.GetProperty("data");
        data.GetProperty("summary").GetString().Should().Be("Summary");
        data.TryGetProperty("transaction", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NullFailureDetails_WHEN_Serializing_THEN_ShouldPublishNullErrorWithoutContinuation()
    {
        var result = ToolResultEnvelopeSerializer.CreateFailure(error: null, requiredAction: null);

        result.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.GetProperty("error").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        result.TryGetProperty("continuation", out _).Should().BeFalse();
        result.TryGetProperty("diagnostics", out _).Should().BeFalse();
        result.TryGetProperty("warnings", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_HandledFailureWithoutCorrelation_WHEN_Serializing_THEN_ShouldOmitCorrelationIdentifier()
    {
        var error = new ToolError
        {
            Code = "Code",
            Message = "Message",
        };

        var result = ToolResultEnvelopeSerializer.CreateFailure(error, RequiredAction.RollbackTransaction);

        result.GetProperty("error").TryGetProperty("correlationId", out _).Should().BeFalse();
        var continuation = result.GetProperty("continuation");
        continuation.GetProperty("kind").GetString().Should().Be("CallTool");
        continuation.GetProperty("tool").GetString().Should().Be("transaction-rollback");
        continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
        continuation.TryGetProperty("tools", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_HandledFailureWithCorrelation_WHEN_Serializing_THEN_ShouldPublishCorrelationIdentifier()
    {
        var error = new ToolError
        {
            Code = "Code",
            Message = "Message",
            CorrelationId = "CorrelationId",
        };

        var result = ToolResultEnvelopeSerializer.CreateFailure(error, RequiredAction.CommitOrRollback);

        result.GetProperty("error").GetProperty("correlationId").GetString().Should().Be("CorrelationId");
        var continuation = result.GetProperty("continuation");
        continuation.GetProperty("kind").GetString().Should().Be("ChooseTool");
        continuation.GetProperty("tools").EnumerateArray().Select(static item => item.GetString()).Should().Equal(
            "transaction-commit",
            "transaction-rollback");
        continuation.GetProperty("instruction").GetString().Should().NotBeNullOrWhiteSpace();
        continuation.TryGetProperty("tool", out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_FailureDiagnosticsAndWarnings_WHEN_Serializing_THEN_ShouldPublishDetails()
    {
        var diagnostic = new DiagnosticInfo
        {
            Id = "Id",
            Severity = DiagnosticSeverity.Error,
            Message = "Message",
        };
        var warning = new WarningInfo
        {
            Code = "Code",
            Message = "Message",
        };

        var result = ToolResultEnvelopeSerializer.CreateFailure(
            new ToolError
            {
                Code = "Code",
                Message = "Message",
            },
            RequiredAction.Retry,
            [diagnostic],
            [warning]);

        var publishedDiagnostic = result.GetProperty("diagnostics").EnumerateArray().Should().ContainSingle().Subject;
        publishedDiagnostic.GetProperty("id").GetString().Should().Be("Id");
        publishedDiagnostic.GetProperty("severity").GetString().Should().Be("Error");
        publishedDiagnostic.GetProperty("message").GetString().Should().Be("Message");
        var publishedWarning = result.GetProperty("warnings").EnumerateArray().Should().ContainSingle().Subject;
        publishedWarning.GetProperty("code").GetString().Should().Be("Code");
        publishedWarning.GetProperty("message").GetString().Should().Be("Message");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CanonicalResolvedLocationSelector_WHEN_SerializingSuccess_THEN_ShouldPublishResultMetadataAndSpanOnlySelector()
    {
        var snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var documentReference = new DocumentReference
        {
            DocumentId = "DocumentId",
            Path = "DocumentPath",
            ProjectId = "ProjectId",
        };

        var resolvedSpan = new TextSpanRange
        {
            Start = 1,
            Length = 2,
        };

        var projectSelector = new ProjectSelector
        {
            ProjectId = "ProjectId",
        };

        var documentSelector = new DocumentSelector
        {
            DocumentId = "DocumentId",
            Project = projectSelector,
        };

        var selectorRange = new TextSpanRange
        {
            Start = 1,
            Length = 2,
        };

        var spanSelector = new TextSpanSelector
        {
            Document = documentSelector,
            Range = selectorRange,
        };

        var locationSelector = new CanonicalLocationSelector
        {
            Span = spanSelector,
        };

        var location = new ResolvedLocation
        {
            Document = documentReference,
            Span = resolvedSpan,
            Line = 3,
            Column = 4,
            Snapshot = snapshot,
            Selector = locationSelector,
        };

        var result = ToolResultEnvelopeSerializer.CreateSuccess(location);

        var data = result.GetProperty("data");
        data.GetProperty("document").GetProperty("documentId").GetString().Should().Be("DocumentId");
        data.GetProperty("document").GetProperty("path").GetString().Should().Be("DocumentPath");
        data.GetProperty("span").GetProperty("start").GetInt32().Should().Be(1);
        data.GetProperty("line").GetInt32().Should().Be(3);
        data.GetProperty("column").GetInt32().Should().Be(4);

        var selector = data.GetProperty("selector");
        var selectorSpan = selector.GetProperty("span");
        selectorSpan.GetProperty("document").GetProperty("documentId").GetString().Should().Be("DocumentId");
        selectorSpan.GetProperty("document").GetProperty("project").GetProperty("projectId").GetString().Should().Be("ProjectId");
        selectorSpan.GetProperty("range").GetProperty("start").GetInt32().Should().Be(1);
        selectorSpan.GetProperty("range").GetProperty("length").GetInt32().Should().Be(2);
        selector.GetRawText().Should().NotContainAny("selection", "selectedText", "contextBefore", "contextAfter");
    }

    [Fact]
    public void GIVEN_MaximumDepthInspectionTrees_WHEN_SerializingSuccess_THEN_ShouldRemainWithinSerializerDepthAndOmitSourceText()
    {
        var outlineData = CreateMaximumDepthOutlineData();
        var operationData = CreateMaximumDepthOperationData();

        var outlineResult = ToolResultEnvelopeSerializer.CreateSuccess(outlineData);
        var operationResult = ToolResultEnvelopeSerializer.CreateSuccess(operationData);
        var outlineJson = outlineResult.GetRawText();
        var operationJson = operationResult.GetRawText();

        outlineJson.Length.Should().BeLessThan(100_000);
        operationJson.Length.Should().BeLessThan(100_000);
        operationJson.Should().NotContain("\"syntax\"");
        operationJson.Should().NotContain("\"constantValue\":");
        operationJson.Should().Contain("\"location\"");
    }

    private static DocumentOutlineData CreateMaximumDepthOutlineData()
    {
        IReadOnlyList<OutlineNode> children = [];
        for (var depth = 24; depth > 0; depth--)
        {
            var node = new OutlineNode
            {
                Name = $"Node{depth}",
                Kind = "NamedType",
                Children = children,
            };

            children = [node];
        }

        var root = new OutlineNode
        {
            Name = "Document",
            Kind = "Document",
            Children = children,
        };

        return new DocumentOutlineData
        {
            Root = root,
        };
    }

    private static OperationTreeData CreateMaximumDepthOperationData()
    {
        var root = new OperationNode
        {
            Kind = "Literal",
            Location = new ResolvedLocation
            {
                Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                    Guid.Parse("11111111-1111-1111-1111-111111111111")),
            },
        };

        for (var depth = 24; depth > 0; depth--)
        {
            root = new OperationNode
            {
                Kind = "Invocation",
                Location = new ResolvedLocation
                {
                    Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                        Guid.Parse("11111111-1111-1111-1111-111111111111")),
                },
                Children = [root],
            };
        }

        return new OperationTreeData
        {
            Root = root,
        };
    }

#pragma warning disable CA1812 // Payload fixture is consumed through generic serializer metadata.
    private sealed record TestData
    {
        public string Value { get; init; } = string.Empty;
    }
#pragma warning restore CA1812
}
