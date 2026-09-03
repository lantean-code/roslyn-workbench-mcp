#!/usr/bin/env python3
"""Remove one deleted prerelease tag's documentation without rebuilding the site."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


_prerelease = re.compile(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-(alpha|beta|rc)\.(0|[1-9][0-9]*)")
_production = re.compile(r"[0-9]+\.[0-9]+\.[0-9]+")
_pages_branch = "refs/heads/gh-pages"


def prerelease_tag(value: str) -> str:
    if not _prerelease.fullmatch(value):
        raise argparse.ArgumentTypeError("Use an exact alpha, beta or rc tag such as 0.1.0-beta.294; production versions and aliases are protected.")
    return value


def git(repository: Path, *arguments: str) -> str:
    return subprocess.run(["git", *arguments], cwd=repository, check=True, capture_output=True, text=True).stdout.strip()


def tag_exists(repository: Path, tag: str) -> bool:
    # A remote lookup also covers tags recreated after the deletion event.
    return bool(git(repository, "ls-remote", "--tags", "origin", f"refs/tags/{tag}"))


def read_versions(repository: Path, revision: str) -> dict[str, dict]:
    entries = json.loads(git(repository, "show", f"{revision}:versions.json"))
    if not isinstance(entries, list):
        raise ValueError("The documentation version inventory must be an array.")
    versions = {}
    for entry in entries:
        if not isinstance(entry, dict) or not isinstance(entry.get("version"), str) or not isinstance(entry.get("aliases"), list):
            raise ValueError("The documentation version inventory contains an invalid entry.")
        name = entry["version"]
        if name in versions:
            raise ValueError(f"Duplicate documentation version: {name}.")
        versions[name] = entry
    return versions


def root_target(repository: Path, revision: str) -> str | None:
    if not git(repository, "ls-tree", "--name-only", revision, "--", "index.html"):
        return None
    html = git(repository, "show", f"{revision}:index.html")
    # Mike 2.2's redirect includes matching JavaScript, no-script and link targets.
    match = re.search(r'window\.location\.replace\(\s*"([^"/]+)/"', html)
    if match is None:
        raise ValueError("The documentation root is not a recognised Mike redirect; leave it unchanged for manual inspection.")
    target = match.group(1)
    if f'content="1; url={target}/"' not in html or f'href="{target}/"' not in html:
        raise ValueError("The documentation root contains inconsistent redirect targets.")
    return target


def replacement_root(versions: dict[str, dict]) -> str:
    for name, entry in versions.items():
        if "latest" in entry["aliases"] and _production.fullmatch(name):
            return "latest"
    if any(_production.fullmatch(name) for name in versions):
        raise ValueError("Production documentation exists without its latest alias; repair the alias before cleanup.")
    betas = []
    for name in versions:
        match = _prerelease.fullmatch(name)
        if match and match.group(4) == "beta":
            order = tuple(int(match.group(index)) for index in (1, 2, 3, 5))
            betas.append((order, name))
    if betas:
        return max(betas)[1]
    if "dev" in versions:
        return "dev"
    raise ValueError("No remaining beta or development documentation can replace the root redirect.")


def run_mike(repository: Path, *arguments: str) -> None:
    beside_python = Path(sys.executable).with_name("mike")
    executable = str(beside_python) if beside_python.is_file() else shutil.which("mike")
    if executable is None:
        raise FileNotFoundError("Install the documentation dependencies so the Mike CLI is available.")
    subprocess.run(
        [executable, *arguments, "--config-file", str(repository / "docs/mkdocs.yml"), "--branch", "gh-pages"],
        cwd=repository,
        check=True,
    )


def validate_cleanup(repository: Path, baseline: str, tag: str, remaining: dict[str, dict], root: str | None, root_changed: bool) -> None:
    if read_versions(repository, _pages_branch) != remaining:
        raise ValueError("Cleanup changed documentation inventory entries other than the requested version.")
    if git(repository, "ls-tree", "--name-only", _pages_branch, "--", tag):
        raise ValueError("The removed documentation directory is still present.")
    if root_target(repository, _pages_branch) != root:
        raise ValueError("Cleanup did not preserve or correctly replace the root redirect.")
    for line in git(repository, "diff", "--no-renames", "--name-status", baseline, _pages_branch).splitlines():
        status, path = line.split("\t", 1)
        if status == "D" and path.startswith(tag + "/"):
            continue
        if status == "M" and (path == "versions.json" or (root_changed and path == "index.html")):
            continue
        raise ValueError(f"Cleanup unexpectedly changed '{path}'; nothing will be pushed.")


def cleanup(repository: Path, tag: str, push: bool) -> bool:
    if tag_exists(repository, tag):
        print(f"Tag '{tag}' still exists or was recreated; no documentation removed.")
        return False
    if not git(repository, "ls-remote", "--heads", "origin", _pages_branch):
        print("No published documentation branch exists; nothing to remove.")
        return False
    git(repository, "fetch", "--no-tags", "origin", "+refs/heads/gh-pages:refs/remotes/origin/gh-pages")
    baseline = git(repository, "rev-parse", "refs/remotes/origin/gh-pages")
    versions = read_versions(repository, baseline)
    if tag not in versions:
        print(f"Documentation '{tag}' is already absent; the existing site can be redeployed.")
        # A rerun after a successful push but failed Pages deployment must still deploy.
        return True
    if versions[tag]["aliases"]:
        raise ValueError("The requested prerelease carries aliases; automatic cleanup will not remove them.")
    original_root = root_target(repository, baseline)
    remaining = {name: entry for name, entry in versions.items() if name != tag}
    new_root = replacement_root(remaining) if original_root == tag else original_root
    if new_root != original_root and not git(repository, "ls-tree", "--name-only", baseline, "--", new_root):
        raise ValueError("The replacement root target has no published files.")

    local = subprocess.run(["git", "rev-parse", "--verify", _pages_branch], cwd=repository, capture_output=True, text=True)
    previous = local.stdout.strip() if local.returncode == 0 else None
    if previous is not None and previous != baseline:
        raise ValueError("The local gh-pages branch differs from origin; use a fresh checkout rather than discarding local work.")
    if git(repository, "branch", "--show-current") == "gh-pages":
        raise ValueError("Run cleanup from a source checkout, not the generated gh-pages branch.")
    if previous is None:
        git(repository, "branch", "gh-pages", baseline)
    try:
        run_mike(repository, "delete", tag)
        if new_root != original_root:
            run_mike(repository, "set-default", new_root)
        validate_cleanup(repository, baseline, tag, remaining, new_root, new_root != original_root)
        if tag_exists(repository, tag):
            print(f"Tag '{tag}' was recreated during cleanup; nothing pushed.")
            return False
        if push:
            # Ordinary fast-forward push refuses to overwrite a concurrent publication.
            git(repository, "push", "origin", "gh-pages")
            git(repository, "fetch", "--no-tags", "origin", "+refs/heads/gh-pages:refs/remotes/origin/gh-pages")
            print(f"Removed documentation for '{tag}'.")
        else:
            print(f"Dry run validated removal of '{tag}'; nothing pushed.")
        return True
    finally:
        # Restore only this command's local branch; source files and the index are untouched.
        current = git(repository, "rev-parse", _pages_branch)
        if previous is None:
            git(repository, "update-ref", "-d", _pages_branch, current)
        else:
            git(repository, "update-ref", _pages_branch, previous, current)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tag", type=prerelease_tag)
    parser.add_argument("--push", action="store_true", help="Publish validated cleanup; otherwise perform a local dry run.")
    arguments = parser.parse_args()
    repository = Path(__file__).resolve().parent.parent
    ready = cleanup(repository, arguments.tag, arguments.push)
    if output := os.environ.get("GITHUB_OUTPUT"):
        with open(output, "a", encoding="utf-8") as stream:
            stream.write(f"deploy={'true' if ready and arguments.push else 'false'}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
