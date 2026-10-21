# Rename symbols and format source

Both tools stage changes in an active transaction; follow [review and commit](transactions.md) to persist them. `data.staged: false` means no source change, including an unchanged name or already formatted content.

## Rename

Resolve the symbol and inspect references before `rename-symbol`; assess public API callers outside the loaded solution separately. Prefer a returned documentation comment ID with project qualification; use a current source location for locals.

`renameOverloads`, `renameInStrings`, `renameInComments` and `renameFile` default to false. Enable only the requested extensions. `renameFile` asks Roslyn to rename a type's containing file within its directory; it does not update explicit `<Compile Include>` entries. Handle necessary project edits separately and reload afterwards.

Review the complete changed-file set for a broad rename. String matches and reflection-based callers need their own assessment; semantic references do not cover them.

## Format

Use `format-document` with workspace formatting options; inspect `get-document-options` when style is unclear. Omit `range` for the whole document, or select a current UTF-16 region. Roslyn may format surrounding syntax, so inspect the resulting diff.

After a file rename, use its current staged path even while disk retains the old name. Avoid running a disk formatter on files in the active transaction. For import organisation or analyser fixes, use [Code Actions](code-actions.md).
