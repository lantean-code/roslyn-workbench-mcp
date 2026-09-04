# Synchronising published documentation

Release documentation is retained only while an exact matching tag exists in the repository. The generated `gh-pages` branch also retains the `dev` documentation independently of tags. This desired-state model covers tags removed individually or in bulk and documentation left behind when a draft GitHub Release is never published.

The manually triggered **Synchronise documentation versions** workflow reads the complete remote tag set and Mike's published version inventory. In one queued publication run it removes every untagged release version, recalculates the production `latest` alias and repairs the root redirect before redeploying Pages. It does not rebuild the application or documentation, run .NET tests, delete GitHub Releases, tags, packages or release assets.

Run the workflow after deleting old tags or abandoning a draft release whose documentation was published. It is deliberately not scheduled: a scheduled run could race the interval between publishing release documentation and publishing the draft GitHub Release that creates its tag. Documentation publication and synchronisation share the `documentation-publication` concurrency group.

The resulting site follows these rules:

- `dev` is always retained.
- Every other published version must have an exact matching remote tag.
- `latest` belongs only to the highest tagged production version.
- The root opens `latest` when production documentation exists.
- Before production, the root opens the highest tagged beta, or `dev` when no tagged beta exists.
- Alpha and release-candidate documentation never become the pre-production root fallback.

The workflow validates that only orphaned version directories, `versions.json`, the `latest` alias and the root redirect changed. It refreshes the remote tag set immediately before pushing and stops if tags changed during the run. The ordinary fast-forward push also refuses to overwrite a concurrent publication.

For a local dry run in a fresh source checkout with the documentation dependencies installed and a Git author configured:

```sh
python docs/sync-versions.py
```

Without `--push`, the script validates the proposed reconciliation and restores its temporary local branch without changing the remote. Use the workflow for actual removal so the Pages deployment follows the branch update.
