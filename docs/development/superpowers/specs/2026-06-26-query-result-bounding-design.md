# Query Result Bounding Design

**Status:** Resolved on 2026-07-18. Count-first bounding is implemented; a global serialised response-size ceiling is not planned.

**Goal:** Use predictable, tool-owned collection limits that agents can increase or narrow explicitly, without a global byte-size rejection policy.

## Problem Statement

The former query helper implemented `CreateBoundedCollectionResult(...)` by:

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

Query tools now use count-first `BoundedCollection<T>` results. Each tool applies its named request limit, or a sensible default chosen by the tool author with the Host-provided `DefaultMaxResults` available as the common baseline. The collection returns a deterministic prefix and sets `HasMore` when additional matching items exist.

There is no `MaxResponseBytes` execution-context capability, serialise-and-shrink loop or global `ResponseLimitExceeded` rejection. Agents can request a larger collection when useful or narrow the request when a broad result would be unhelpful.

## Design Goals

- Make result truncation predictable from the tool contract.
- Prefer element limits over serialised byte limits as the normal control surface.
- Keep deterministic ordering and `HasMore` semantics.
- Avoid repeated serialise-and-shrink loops in the normal path.
- Keep the model understandable for both human users and LLM agents.

## Non-Goals

- This design does not impose a global serialised response-size safeguard.
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

### 4. Do not add a global response-size ceiling

The Host does not reject a successful query solely because its serialised response crosses a fixed byte threshold. Such a limit is difficult to apply predictably across varied response shapes and can reject useful results without improving the logical contract.

Tool and plugin authors instead own sensible collection defaults and explicit shape controls for unusually verbose fields. Agents remain able to request larger bounded collections when the additional context is useful.

## Recommended Behaviour Model

The preferred long-term model for collection-returning query tools is:

1. Compute the full ordered logical result set.
2. Apply the requested or configured item limit.
3. Build a shaped DTO for that bounded result.
4. Return the bounded, shaped result without a second byte-led truncation or rejection pass.

This model is intentionally simple:

- count controls normal result size,
- DTO shape controls verbosity,
- tool-owned shape and count limits keep the response useful.

## Alternatives Considered

### Alternative A: Keep the current linear byte-shrinking loop

**Rejected**

This preserves the most permissive success path, but it is costly, difficult to reason about, and poorly aligned with how tools should guide users or agents to narrow broad queries.

### Alternative B: Replace the linear shrink loop with binary search

**Viable, but not preferred as the end-state**

This would reduce repeated work while preserving “largest fitting prefix” semantics. It is a reasonable tactical improvement if the current behaviour must remain temporarily.

However, it still keeps bytes as the primary truncation control, which this proposal considers the wrong default model.

### Alternative C: Count-first without a global hard size rejection

**Selected**

This gives the clearest contract, avoids a difficult cross-tool byte policy, and leaves each tool author responsible for meaningful count and shape defaults.

## Contract and API Implications

No catalogue-wide rename is proposed.

However, existing query DTOs should consistently use:

- explicit item arrays,
- `HasMore`,
- existing limit selectors where already defined.

Where a tool is especially prone to large payloads, future contract work may add explicit verbosity controls instead of relying on byte-size truncation side effects.

## Implemented Approach

1. `BoundedCollection<T>` provides count-first prefix bounding and deterministic `HasMore` semantics.
2. Bundled query tools expose named collection limits and use the Host default when their request omits a limit.
3. The old iterative byte-shrinking helper and `MaxResponseBytes` context capability were removed.
4. New and third-party query tools are expected to choose sensible defaults, expose explicit limits where agents may need more results, and shape verbose fields deliberately.

## Test Position

Add direct unit coverage for the new count-first helper:

- returns all items when under the limit,
- truncates to the requested limit,
- sets `HasMore` correctly when truncating,
- preserves deterministic ordering,
- does not retry by shrinking one item at a time.

Add integration coverage for representative tools:

- `search-symbols` with count-based truncation,
- `find-references` with broad-scope narrowing expectations,
- one verbose tool with shape controls; and
- MCP-facing tests that verify the returned structured bounded result.

## Decision Summary

- Byte size should not be the primary query truncation model.
- Element limits should be the normal way collection query tools are bounded.
- There is no global hard serialised response-size ceiling or `ResponseLimitExceeded` policy.
- Tool and plugin authors own sensible defaults and response shaping; agents can request more results or narrow requests as needed.
- The former iterative serialise-and-shrink loop is removed and must not be reintroduced.
