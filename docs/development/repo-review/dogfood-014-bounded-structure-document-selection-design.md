# DOGFOOD-014 — Agent-facing document eligibility

**Status:** Confirmed through published dogfood validation.

## Confirmed issue

The clean committed dogfood build reproduced intermediate `obj` documents being exposed as ordinary source documents. `get-solution-structure` with two document slots returned only intermediate build files for each sampled project. `get-project-details` with five slots returned three intermediate build files before its first two authored source files.

The original investigation treated this only as a bounded-response ordering problem. Design discussion established the broader product rule: intermediate build documents are compiler inputs retained by Roslyn for correct compilation and semantic analysis, but they are not agent-authored code and should not be addressable or exposed by queries, mutations or Code Actions.

## Roslyn behaviour

Roslyn's `Project.Documents` collection contains regular project documents; compiler source-generator outputs are obtained separately through `GetSourceGeneratedDocumentsAsync`. The displaced `.NETCoreApp,Version=...AssemblyAttributes.cs`, assembly-information and global-usings files are ordinary MSBuild compile documents located under the intermediate `obj` tree, not `SourceGeneratedDocument` instances.

Removing those documents from the Roslyn `Solution` would be incorrect. Their declarations and imports participate in compilation, so semantic operations over authored code must continue to see them. The exclusion belongs at the agent-facing selection and projection boundary, not in workspace loading or compilation construction.

## Existing boundary and gap

`IWorkspaceResolver` is already the shared authority for direct document and location selectors and for projecting `DocumentReference` values. However, plugin solution/project scopes and Code Action solution/project scopes enumerate `Project.Documents` directly. This requires each consumer to remember to apply an exclusion rule and makes the unsafe collection the easiest one to use. The transaction candidate validator independently protects the final mutation boundary but currently permits changes to any regular document within the workspace root, including an intermediate build document.

Consequently, changing only the two structure tools would leave the same documents available to broad queries, direct selectors, Code Actions and malicious or defective mutation candidates. The policy must be shared by all host-owned agent-facing paths.

Third-party plugins receive the complete Roslyn `Solution` because semantic correctness requires it. A plugin can deliberately bypass the supported resolver and inspect compiler inputs; the host cannot make those documents physically absent without weakening analysis. The supported plugin contract and all bundled tools will use the resolver's pre-filtered collections, while the transaction validator provides the enforceable protection against changing an excluded document.

## Proposed design

Introduce a Workspace-owned internal document eligibility policy. A document is ineligible when its physical path contains an `obj` directory segment. Segment matching is used rather than substring matching, so names such as `objects` remain valid. The comparison follows the workspace filesystem's path comparison rules. Documents without a physical path and documents whose path does not contain an `obj` segment retain their current behaviour.

Expose pre-filtered document collections for a `Solution` or `Project` through `IWorkspaceResolver`. The resolver is already supplied on plugin and Code Action execution contexts and remains the single supported boundary for selecting and projecting Workspace objects. This makes the safe collection the normal application boundary while retaining raw solution access for compilation and semantic operations that genuinely require it.

Do not reuse `CompilerDiagnosticHelpers.IsGeneratedDocument`. That helper answers a different question based on generated filename conventions; this rule is specifically about intermediate build inputs. Do not parse syntax trees or call `SyntaxTreeOptionsProvider.IsGenerated`, because classification must be cheap, deterministic and independent of syntax-tree cache state.

Apply the policy at these boundaries:

1. `WorkspaceResolver.ResolveDocument` excludes ineligible candidates for both identifier and path selectors. Location and symbol selectors that depend on a document therefore inherit the same rule.
2. `WorkspaceResolver.CreateDocumentReference` returns no reference for an ineligible document. This is a final projection guard for query results, symbol locations and transaction responses.
3. Plugin `ToolRequestResolver.ResolveDocuments` obtains solution and project scopes from `IWorkspaceResolver`. Bundled queries that use these standard scope helpers therefore cannot scan intermediate documents.
4. `CodeActionScopeResolver` obtains solution, project and projects scopes from `IWorkspaceResolver`. Direct Code Action selections already pass through the same resolver.
5. `WorkspaceMutationCandidateValidator` rejects any added, changed or removed document that is not agent-addressable. This prevents a plugin or Code Action that bypassed normal selection from staging an intermediate-document mutation.
6. `GetSolutionStructureTool` and `GetProjectDetailsTool` receive pre-filtered collections before sorting and applying their existing bounds. Their deterministic ordinal ordering, request and response contracts, and `hasMore` calculation remain otherwise unchanged.

Keep the predicate internal to the Workspace assembly rather than exposing classification mechanics through `IWorkspaceResolver` or duplicating path logic in plugin and Code Action assemblies. Resolver collection methods apply the internal policy and mutation validation uses it as an enforcement backstop; ordinary consumers receive only pre-filtered collections. The implementation should use explicit control flow and a small path-segment helper rather than a compressed LINQ expression.

This change intentionally does not hide projects merely because all of their documents are excluded. Project inventory and compilation semantics remain intact; only agent-facing document collections and selectors are filtered.

## Tests

Add focused Workspace unit coverage proving that:

- direct identifier and path selectors cannot resolve a document beneath an `obj` segment;
- similarly named non-segments such as `objects` remain eligible;
- document-reference and resolved-location projection cannot expose an excluded document;
- path comparison follows the configured filesystem rules; and
- ordinary documents retain current selector and projection behaviour.

Add plugin resolver and Code Action scope tests for solution, project, projects and direct-document scopes. Require intermediate documents to be excluded while ordinary documents remain, including a project whose only Roslyn document is intermediate.

Add mutation-validator tests covering changed, added and removed intermediate documents so exclusion is enforced even when a producer bypasses the normal resolver.

Update the bounded structure-tool unit tests with mixed authored and intermediate documents. Require filtering to happen before the bound and require `hasMore` to describe the eligible collection only. Retain existing ordinal-order, zero-limit and projection-failure coverage.

Strengthen `WorkspaceProjectionIntegrationTests` using its real loaded project so both structure tools demonstrate that an intermediate MSBuild document is absent rather than merely displaced beyond a small bound. The affected production paths must retain 100% line and branch coverage.

No acceptance-test or Scenario Runner source change is proposed. Existing published and scenario coverage exercises the contracts; the missing cross-boundary policy is covered more directly by unit and real-Workspace integration tests. After implementation and review, a published dogfood run against the main solution will verify structure queries, a broad query, a rejected direct selector and a Code Action discovery scope.

## Rejected alternatives

### Prefer source documents only within bounded structure responses

Source-first ordering improves two summaries but leaves intermediate documents exposed to every other query, direct mutation target and Code Action. It does not implement the agreed product rule.

### Remove intermediate documents from the Roslyn solution

These documents participate in compilation. Removing them would make semantic analysis differ from the real build and could introduce false diagnostics or incorrect symbol information.

### Treat every generated-looking filename as ineligible

Filename conventions such as `.g.cs` are not equivalent to intermediate build output. A checked-in or intentionally included generated source file can be legitimate agent-facing code. This item excludes the confirmed `obj` compiler inputs without broadening policy to all generated source.

### Add a request option to include intermediate documents

The agreed default is that these documents are outside the agent-facing code surface, not merely lower-priority inventory. An opt-in flag would complicate every relevant contract and reintroduce mutation and Code Action ambiguity.
