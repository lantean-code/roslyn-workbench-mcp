#!/usr/bin/env python3
"""Create a release draft or reuse a matching draft without changing its contents."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--prerelease", choices=("true", "false"), required=True)
    parser.add_argument("--notes-file", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    arguments = parser.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", arguments.commit):
        parser.error("--commit must be a complete lowercase commit SHA, not a branch name.")

    repository = os.environ["GH_REPO"]
    # Listing includes drafts and distinguishes an absent release from an API failure.
    # Pagination also avoids treating an older draft as a new release.
    release_lines = subprocess.check_output(
        ["gh", "api", f"repos/{repository}/releases?per_page=100", "--paginate", "--jq", ".[] | @json"],
        text=True,
    )
    matches = []
    for line in release_lines.splitlines():
        release = json.loads(line)
        if release["tag_name"] == arguments.version:
            matches.append(release)

    if len(matches) > 1:
        raise ValueError(f"Multiple GitHub releases identify '{arguments.version}'; resolve them manually.")
    if matches:
        validate_draft(matches[0], arguments.commit, arguments.prerelease == "true")
        print(f"Reusing matching draft '{arguments.version}'; its notes and assets are unchanged.")
        return

    if arguments.dry_run:
        print(f"No release exists for '{arguments.version}'; publication would create a draft.")
        return

    subprocess.run(
        [
            "gh", "release", "create", arguments.version,
            "--repo", repository,
            "--draft",
            f"--prerelease={arguments.prerelease}",
            "--target", arguments.commit,
            "--title", arguments.version,
            "--notes-file", str(arguments.notes_file),
        ],
        check=True,
    )


def validate_draft(release: dict[str, object], commit: str, prerelease: bool) -> None:
    if release.get("draft") is not True:
        raise ValueError("The GitHub Release is already published and will not be modified.")
    if release.get("target_commitish") != commit:
        raise ValueError("The existing draft does not target the exact release commit and will not be reused.")
    if release.get("prerelease") is not prerelease:
        raise ValueError("The existing draft's prerelease status differs from this release and will not be changed.")


if __name__ == "__main__":
    main()
