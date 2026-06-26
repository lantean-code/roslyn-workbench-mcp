# Query Result Bounding Design

**Status:** Deferred proposal for later implementation.

**Goal:** Replace byte-size-led query truncation as the primary behaviour with a more predictable result-bounding model that is easier for agents to reason about, cheaper for the server to execute, and clearer to document in tool contracts.

## Problem Statement

The current query helper in [ToolExecutionHelpers.cs](/mnt/c/Users/alexj/source/repos/roslyn-workbench-mcp/src/Roslyn.Workbench.Mcp.Plugins.Core/ToolExecutionHelpers.cs) implements `CreateBoundedCollectionResult(...)` by:

- starting from the largest allowed item count,
- serialising the full payload,
- decrementing the item count one element at a time if the payload is too large,
- re-creating and re-serialising the payload until it fits, or
- rejecting with `ResponseLimitExceeded` if even the empty payload does not fit.

This has some desirable properties:

- it returns the largest fitting prefix of an already ordered result set,
- it preserves deterministic ordering,
- it allows a query to succeed under a tight response-size budget.

However, it also has material drawbacks:

- the primary truncation mechanism is bytes rather than elements,
- the server may rebuild and serialise the same payload shape many times,
- the retry loop is linear from largest to smallest,
- the behaviour is difficult to predict from the caller side,
- the truncation model is not naturally aligned with how an agent should narrow a query.

For a local stdio MCP server, the most important constraint is usually not transport throughput. It is predictable payload shape, bounded memory/allocation work, client parse/render cost, and model-context usability.

## Current Behaviour

Today, query tools that return collections typically rely on `CreateBoundedCollectionResult(...)` to combine:

- a result-count ceiling via `maxResults`, and
- a serialised response-size ceiling via `context.MaxResponseBytes`.

The helper attempts to keep the response within both limits by repeatedly shrinking the item list until the serialised payload fits.

This means a request can return fewer than `maxResults` items even when the request-level limit is not reached, simply because the current payload shape serialises above the configured byte threshold.

## Design Goals

- Make result truncation predictable from the tool contract.
- Prefer element limits over serialised byte limits as the normal control surface.
- Keep deterministic ordering and `HasMore` semantics.
- Preserve a last-resort safeguard against pathological payloads.
- Avoid repeated serialise-and-shrink loops in the normal path.
- Keep the model understandable for both human users and LLM agents.

## Non-Goals

- This proposal does not remove all response-size safeguards.
- This proposal does not introduce continuation tokens or cursor paging at this stage.
- This proposal does not redesign every existing response DTO.
- This proposal does not change mutation payload semantics.

## Proposed Direction

### 1. Make element limits the primary bounding mechanism

Query tools should primarily be bounded by explicit item limits rather than response bytes.

The intended caller behaviour should be:

- request a bounded number of items,
- inspect `HasMore`,
- narrow the scope, selector, or filter criteria when more data exists than is useful in one response.

This is more predictable than byte-driven truncation because it aligns with the logical unit of the result set rather than with serialised JSON size.

### 2. Keep deterministic ordering and stable truncation semantics

Each query tool should continue to:

- build a deterministic ordered sequence,
- apply a predictable item cap,
- return the leading prefix of that sequence,
- set `HasMore = true` when more matching items existed than were returned.

This keeps the current “largest useful prefix” behaviour but moves the decision point from “largest payload that fits” to “largest allowed result slice”.

### 3. Use shape limits for verbose per-item fields

Where payload size varies significantly even for the same item count, the preferred control should be field-level shaping rather than repeated serialisation retries.

Examples:

- cap context-snippet length,
- cap number of declarations per symbol,
- cap number of locations per item,
- prefer summary projections over fully expanded nested data by default.

This reduces payload variance while keeping the item count meaningful.

### 4. Retain a hard response-size ceiling as an emergency brake

A hard serialised response-size ceiling should still exist, but it should be a final safety mechanism rather than the main truncation algorithm.

If the bounded and shaped response still exceeds the configured maximum, the tool should fail clearly with a structured rejection such as:

- `ResponseLimitExceeded`
- `RequiredAction.NarrowRequest`

The server should not repeatedly serialise smaller and smaller payloads to force success in the normal path.

## Recommended Behaviour Model

The preferred long-term model for collection-returning query tools is:

1. Compute the full ordered logical result set.
2. Apply the requested or configured item limit.
3. Build a shaped DTO for that bounded result.
4. Serialise once for enforcement.
5. If the single bounded payload exceeds the hard maximum, reject with `ResponseLimitExceeded`.

This model is intentionally simple:

- count controls normal result size,
- DTO shape controls verbosity,
- byte limit protects against pathological results.

## Alternatives Considered

### Alternative A: Keep the current linear byte-shrinking loop

**Rejected**

This preserves the most permissive success path, but it is costly, difficult to reason about, and poorly aligned with how tools should guide users or agents to narrow broad queries.

### Alternative B: Replace the linear shrink loop with binary search

**Viable, but not preferred as the end-state**

This would reduce repeated work while preserving “largest fitting prefix” semantics. It is a reasonable tactical improvement if the current behaviour must remain temporarily.

However, it still keeps bytes as the primary truncation control, which this proposal considers the wrong default model.

### Alternative C: Count-first plus last-resort hard size rejection

**Recommended**

This gives the clearest contract, the cheapest steady-state execution path, and the most predictable behaviour for callers.

## Contract and API Implications

No catalogue-wide rename is proposed.

However, existing query DTOs should consistently use:

- explicit item arrays,
- `ReturnedCount`,
- `HasMore`,
- existing limit selectors where already defined.

Where a tool is especially prone to large payloads, future contract work may add explicit verbosity controls instead of relying on byte-size truncation side effects.

## Suggested Implementation Approach

When this work is scheduled:

1. Introduce a dedicated collection-bounding helper built around count-first semantics.
2. Migrate one representative query tool first, such as `search-symbols`.
3. Add targeted shape controls only where needed.
4. Replace remaining collection query tools in small batches.
5. Remove or narrow the old iterative byte-shrinking helper once no longer needed.

If transitional compatibility is required, the existing helper can be retained temporarily, but new tools should not be built around it.

## Test Plan For Later Work

Add direct unit coverage for the new count-first helper:

- returns all items when under the limit,
- truncates to the requested limit,
- sets `HasMore` correctly when truncating,
- preserves deterministic ordering,
- rejects when the already bounded payload exceeds the hard size ceiling,
- does not retry by shrinking one item at a time.

Add integration coverage for representative tools:

- `search-symbols` with count-based truncation,
- `find-references` with broad-scope narrowing expectations,
- one verbose tool with shape controls,
- MCP-facing tests that verify the returned structured result and rejection behaviour.

## Migration Risks

- Some existing tests may assume byte-driven success instead of explicit rejection.
- A few tools may need additional shaping controls before count-first semantics are practical.
- Very small `MaxResponseBytes` values in tests may need revisiting if they currently depend on iterative shrinking behaviour.

## Decision Summary

- Byte size should not be the primary query truncation model.
- Element limits should be the normal way collection query tools are bounded.
- Response size should remain as a hard safeguard only.
- The current iterative serialise-and-shrink loop should be treated as a temporary design, not a preferred long-term pattern.
