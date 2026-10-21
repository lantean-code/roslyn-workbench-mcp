"""Publish the canonical agent skill as web pages and a complete offline bundle."""

from __future__ import annotations

import re
import shutil
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo


SKILL_FILES = (
    "SKILL.md",
    "references/workspaces.md",
    "references/selectors.md",
    "references/navigation.md",
    "references/structure.md",
    "references/diagnostics.md",
    "references/flow-analysis.md",
    "references/impact-analysis.md",
    "references/transactions.md",
    "references/code-actions.md",
    "references/rename-format.md",
    "references/recovery.md",
    "agents/openai.yaml",
    "assets/icon.svg",
)


def publish_skill(repository_root: Path, content_directory: Path) -> Path:
    """Copy exact source bytes to downloads and renderable Markdown pages."""
    source_directory = repository_root / "skills" / "roslyn-workbench"
    files = {name: (source_directory / name).read_bytes() for name in SKILL_FILES}
    for name, data in files.items():
        if not name.endswith(".md"):
            continue
        for link in re.findall(r"\[[^\]]*\]\(([^)]+)\)", data.decode("utf-8")):
            target = ((source_directory / name).parent / link).resolve()
            if not target.is_relative_to(source_directory.resolve()):
                raise ValueError(f"Skill reference does not resolve inside the bundle: {name}: {link}")
            if target.relative_to(source_directory.resolve()).as_posix() not in files:
                raise ValueError(f"Skill reference is not included in the bundle: {name}: {link}")

    pages_directory = content_directory / "agent" / "skill"
    if pages_directory.exists():
        shutil.rmtree(pages_directory)

    archive_path = content_directory / "assets" / "generated" / "roslyn-workbench-skill.zip"
    archive_path.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(archive_path, "w", compression=ZIP_DEFLATED) as archive:
        for name, data in files.items():
            entry = ZipInfo(f"roslyn-workbench/{name}", date_time=(2000, 1, 1, 0, 0, 0))
            entry.compress_type = ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            archive.writestr(entry, data)

            page = content_directory / "agent" / "skill" / name
            page.parent.mkdir(parents=True, exist_ok=True)
            page.write_bytes(data)

    return archive_path
