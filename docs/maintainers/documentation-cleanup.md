# Removing old prerelease documentation

Deleting an `X.Y.Z-alpha.N`, `X.Y.Z-beta.N` or `X.Y.Z-rc.N` tag triggers **Remove deleted prerelease documentation**. It removes only that version from the generated `gh-pages` branch and Mike's version selector, then redeploys Pages. It does not rebuild the application or documentation, run .NET tests, delete a GitHub Release or remove packages and release assets. Production documentation, `dev` and `/latest/` are not cleanup targets. A prerelease carrying any aliases requires manual inspection instead of automatic deletion.

The workflow must exist on the repository's default branch, and `PAGES_DEPLOYMENT_ENABLED` must be enabled. Branch deletion and production-tag deletion do not run cleanup. The remote tag is checked both before preparation and immediately before publication; an existing or recreated tag stops removal. Documentation publication and cleanup share a queued concurrency group, and a non-fast-forward push is rejected rather than overwriting another update.

If the root redirect points to the removed beta, it moves to the highest remaining beta version, comparing numeric version components and the beta number. If none remain, it moves to `dev`. When production documentation exists, its valid `/latest/` alias takes precedence. An unrelated root redirect is preserved. Cleanup stops without publishing if it cannot establish a safe replacement. Deleted URLs are not redirected individually and will return 404 after deployment; the generated branch's Git history retains the removed content.

## Manual cleanup and retries

In Actions, choose **Remove deleted prerelease documentation**, select **Run workflow**, and enter the exact tag that has already been deleted. Use the manual trigger for older deleted tags, cancelled jobs, or bulk tag deletion: [GitHub does not create deletion events when more than three tags are deleted together](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#delete). Delete the tag itself rather than only its GitHub Release if you want automatic documentation cleanup.

The manual trigger targets one explicit version; it never sweeps all versions without tags. Release documentation can legitimately exist before its draft GitHub Release creates the tag. Rerunning an already-completed cleanup is safe and redeploys the existing site, allowing recovery when the branch update succeeded but Pages deployment failed. Each workflow attempt uses a distinct Pages artefact name so retries cannot select an older deployment bundle.

For a local dry run in a fresh source checkout with the documentation dependencies installed and a Git author configured:

```sh
python docs/delete-version.py 0.1.0-beta.294
```

Without `--push`, the script validates the proposed changes and restores its temporary local branch without changing the remote. Use the workflow for actual removal so the Pages deployment follows the branch update. Never use this command as a way to remove production documentation or an existing tag's pages.
