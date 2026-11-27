# Product website and documentation publication

The product homepage occupies the project root. Mike owns `docs/versions.json`, the `/docs/<version>/` snapshots and `/docs/latest/`; the root homepage is not a Mike redirect. Hosting remains GitHub Pages at `https://lantean-code.github.io/roslyn-workbench-mcp/` until an explicitly approved domain change.

## Local build

Install `docs/docs-requirements.txt` in a local Python environment and use the SDK pinned in `global.json`. Run `python docs/build.py` to generate and strictly render the development documentation, then `python docs/validate.py`. To render the displayed version as `dev`, set `MIKE_DOCS_VERSION=dev` for the build and pass `--deployment-version dev` to the validator. Mike appends that version to `site_url`; do not append it manually as well.

Build a complete preview with `python docs/website.py --development docs/site --output artifacts/website`. This reads `origin/gh-pages` using `git archive` without fetching, checking out, committing or changing refs. For a more recent exported publication tree, use `--source <directory>`. The source must contain the actual published snapshots, not just the current MkDocs output. An existing output directory must be empty; use a new output location for each build.

The website builder resolves the exact Host package `Lantean.Roslyn.Workbench.Mcp` through NuGet V3, selects the highest listed stable version and downloads its package to confirm availability. It records the package URL and SHA-256 in `migration-manifest.json`; documentation aliases are resolved independently from Mike's version inventory. The displayed command runs a version check; permanent installation and MCP client setup remain in the getting-started guide. No package-version request runs in the visitor's browser.

For an offline local preview, save the `release` object from a successfully verified migration manifest and pass it using `--release-manifest <file>`. This is an explicit deterministic preview input, not a fresh verification of NuGet. Offline manifests are rejected with `--update-branch`.

Serve the output from a parent directory containing a `roslyn-workbench-mcp` directory to reproduce the current project prefix, for example `python -m http.server 8766 --bind 127.0.0.1 --directory artifacts/preview-root`, then open `http://127.0.0.1:8766/roslyn-workbench-mcp/`. Do not publish the development server.

## Validation and generated evidence

- `python -m unittest discover -s docs -p "test_*.py"` exercises publication verification, site assembly, legacy preservation and NuGet version selection.
- `node docs/test_website_copy.cjs` checks copying the exact rendered command and the manual-selection fallback.
- `python docs/website.py --validate <composed-site>` checks the assembled HTML link and fragment graph, version inventory, sitemap syntax and homepage structure.
- `migration-manifest.json` records the public base, verified package and old-to-new file mapping. `absolute-urls.json` lists absolute and root-relative URLs by source file, including URLs in generated JSON, CSS and JavaScript. These are build artefacts; regenerate them against the publication being reviewed.

The default `RW_SITE_URL` is the existing project URL. For an explicitly approved public-base change, set `RW_SITE_URL` for the website builder and set `RW_DOCS_SITE_URL` to that URL plus `docs/` for MkDocs. Both must describe the same externally visible site. Homepage metadata, canonical URLs and sitemaps are absolute; ordinary new HTML navigation is relative. New version navigation is normalised during assembly; historical prose and navigation are retained. The always-dark homepage wordmark is derived from the repository's canonical SVG, preserving the official artwork and forcing light lettering independently of the visitor's OS theme. The checked-in social PNG is a raster rendering of the same official wordmark.

## Publication and compatibility

`website.py --update-branch` is an explicit publication operation for CI. It exports the existing generated branch to a temporary directory, assembles and validates the complete tree, creates a temporary Git index and advances only the local `gh-pages` ref using a compare-and-swap. It never checks out source files or pushes. Do not run it during a read-only or no-commit local task. Its first run relocates existing snapshots mechanically; subsequent Mike commands use native `deploy_prefix: docs`. Historical HTML canonical tags and sitemap locations are adjusted narrowly, without rebuilding old source or changing its documentation substance.

Development, immutable release and cleanup publishing all assemble the same complete tree. The existing `documentation-publication` concurrency group, one archived commit per uploaded artefact, Pages OIDC identity and byte-for-byte public checks remain in place. A second stable-release refresh runs after Host publication to NuGet and waits for feed propagation before updating the homepage command. A failed feed lookup or inaccessible package fails publication. `website-refresh.yml` can also be dispatched manually without rebuilding documentation.

Every old HTML path has a zero-delay relative redirect, an accessible fallback link and JavaScript query/fragment preservation. Old non-HTML paths retain their original bytes, including JSON, skill ZIPs and images. Legacy `latest` pages point directly to the resolved version; `/docs/` also opens the resolved default. The compatibility inventory at root `versions.json` remains available, while the version selector uses `docs/versions.json`. All assembled documentation versions receive shared relative CSS and JavaScript for a click-operated version selector: the list opens below its button, supports keyboard activation and closes on Escape or an outside click. Existing historical CSS and JavaScript files retain their original bytes.

Explicit tag-based cleanup still removes the corresponding `/docs/<version>/` snapshot according to the existing retention policy. Its old HTML URLs then show an explanatory, non-indexable retirement page linking to retained documentation; their non-HTML compatibility assets remain available. Ordinary publication never performs tag cleanup. This avoids retaining dangling redirects after an owner deliberately retires a release.

Before the first migration deployment, record the existing remote `gh-pages` SHA, export it and retain the uploaded artefact. Roll back by redeploying that exact previous archive through the approved Pages publication path; do not reset or force-push history. A subsequent forward publication must also restore the intended branch layout before normal deployment resumes. No rollback, publication or domain setting change is authorised by a successful local preview alone.

The suggested GitHub About description is: “Roslyn MCP server for C#/.NET AI coding agents. Compiler-aware analysis, code fixes, refactoring and safe, reviewable source changes.” Repository settings, webmaster verification and sitemap submission are owner actions and are not performed by the site builder.
