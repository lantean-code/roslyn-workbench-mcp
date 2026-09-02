#!/usr/bin/env python3
"""Render the current release notes using the build's calculated version."""

import argparse
import re
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.version and not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", args.version):
        raise ValueError("Release notes require the calculated public SemVer.")

    repo = Path(__file__).resolve().parents[2]
    notes = (repo / "docs/release/release-notes.md").read_text(encoding="utf-8")
    notes = notes.replace("{{VERSION}}", args.version or "VERSION")
    notes = notes.replace("{{DOCS_VERSION}}", args.version or "dev")
    if "{{" in notes or "}}" in notes:
        raise ValueError("Release notes contain an unresolved template value.")
    if not args.version:
        notes = "> Unpublished release-notes preview. The version below is a placeholder, not an installable release.\n\n" + notes
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(notes, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
