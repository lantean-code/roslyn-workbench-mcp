# Inspect expression and control flow

Choose the analysis for the question left unanswered by reading source.

| Question | Tool and boundary |
| --- | --- |
| Which variables enter, leave, change or become captured? | `analyze-data-flow`: exact expression, complete statement or contiguous sibling statements. |
| Can this region complete, exit or return? | `analyze-control-flow`: complete statement or contiguous sibling statements within one executable body. |
| How does the compiler interpret an expression? | `get-operation-tree`: Roslyn `IOperation` kinds, types, constants and locations. |
| How do branches connect across a body? | `get-control-flow-graph`: selected executable symbol or the body enclosing a location. |

## Select an exact region

Region-flow analysis requires exact syntax spans. A caret, partial statement or approximate selection is unsuitable; leading/trailing trivia outside the syntax span can cause rejection. Reuse a verified span or construct one from the actual source using [selectors.md](selectors.md).

For extraction, inspect the proposed region with `get-code-context`. Use data flow when parameters, outputs or closure behavior need clarification; combine reads/writes, declarations, captures and flow-in/flow-out rather than inferring the signature from one category. Use control flow to examine statement-range exits and returns. Each category is independently bounded, so check the relevant collection before concluding a variable or exit is absent. Continue through [code-actions.md](code-actions.md) for a supported extraction.

## Check what was projected

`get-operation-tree` may fall back from the selected syntax node to the **first immediate child with an operation**. Check the returned root's kind and location. Result/node `truncated` flags identify omitted structure.

`get-control-flow-graph` returns the enclosing executable body's graph, not a graph clipped to the selected text. Successor ordinals can point to blocks omitted by the cap. Inspect outer and nested truncation before concluding an edge or branch is absent.
