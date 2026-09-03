#!/usr/bin/env python3
"""Publish the Linux x64 Host and build a Debian package without installing it."""

import argparse
import gzip
import hashlib
import os
import platform
import re
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from email.utils import format_datetime
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", default=os.environ.get("RoslynWorkbenchVersion") or "0.0.0-dev")
    parser.add_argument("--output-directory", type=Path)
    parser.add_argument("--dotnet-path", default="dotnet")
    args = parser.parse_args()

    if platform.system() != "Linux" or platform.machine() != "x86_64":
        raise ValueError("Build the amd64 package on Linux x64 with the pinned .NET SDK.")
    match = re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z.-]+))?", args.version)
    if not match:
        raise ValueError("Version must be a semantic version without build metadata.")
    if match[4]:
        for identifier in match[4].split("."):
            if not identifier or (identifier.isdigit() and len(identifier) > 1 and identifier.startswith("0")):
                raise ValueError("Prerelease identifiers must follow semantic version rules.")
    if os.environ.get("RoslynWorkbenchVersion", args.version) != args.version:
        raise ValueError("Version must match the release identity supplied by the environment.")

    # The tilde sorts a prerelease before its production version. The final -1 is
    # the Debian packaging revision, not part of the Host's semantic version.
    deb_version = args.version.replace("-", "~", 1) + "-1"
    subprocess.run(["dpkg", "--validate-version", deb_version], check=True)
    repo = Path(__file__).resolve().parents[2]
    is_wsl = "microsoft" in platform.release().lower()
    artifacts = Path("/tmp/artifacts/roslyn-workbench-mcp") if is_wsl else repo / "artifacts"
    if args.output_directory:
        output = args.output_directory.resolve()
        output.mkdir(parents=True, exist_ok=False)
    else:
        parent = artifacts / "debian"
        parent.mkdir(parents=True, exist_ok=True)
        output = Path(tempfile.mkdtemp(prefix=f"{args.version}-", dir=parent))

    publish = output / "publish"
    command = [
        args.dotnet_path, "publish", str(repo / "src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj"),
        "--configuration", "Release", "--runtime", "linux-x64", "--self-contained", "false",
        "--output", str(publish), "-p:PackAsTool=false", "-p:UseAppHost=true",
        f"-p:RoslynWorkbenchVersion={args.version}",
    ]
    if is_wsl:
        command.append("--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp")
    subprocess.run(command, cwd=repo, check=True)
    (publish / "Roslyn.Workbench.Mcp").rename(publish / "roslyn-workbench-mcp")
    shutil.copyfile(repo / "LICENSE", publish / "LICENSE")

    # Stage on the Linux filesystem even if the chosen output is on a WSL mount.
    # dpkg records these modes; Windows-backed mounts may not preserve chmod.
    with tempfile.TemporaryDirectory(prefix="rwmcp-deb-stage-", dir="/tmp") as staging:
        root = Path(staging)
        application = root / "usr/lib/roslyn-workbench-mcp"
        shutil.copytree(publish, application)
        binary = root / "usr/bin/roslyn-workbench-mcp"
        binary.parent.mkdir(parents=True)
        binary.symlink_to("../lib/roslyn-workbench-mcp/roslyn-workbench-mcp")
        documentation = root / "usr/share/doc/roslyn-workbench-mcp"
        documentation.mkdir(parents=True)
        shutil.copyfile(repo / "LICENSE", documentation / "copyright")
        shutil.copyfile(repo / "THIRD-PARTY-NOTICES.md", documentation / "THIRD-PARTY-NOTICES.md")
        changelog = (
            f"roslyn-workbench-mcp ({deb_version}) unstable; urgency=medium\n\n"
            f"  * Package upstream release {args.version}.\n\n"
            f" -- Lantean Code <lanteancode@gmail.com>  {format_datetime(datetime.now(timezone.utc))}\n"
        )
        (documentation / "changelog.Debian.gz").write_bytes(gzip.compress(changelog.encode(), mtime=0))
        checksums = []
        for path in sorted((root / "usr").rglob("*")):
            if path.is_file() and not path.is_symlink():
                with path.open("rb") as stream:
                    checksum = hashlib.file_digest(stream, "md5").hexdigest()
                checksums.append(f"{checksum}  {path.relative_to(root).as_posix()}\n")
        control = root / "DEBIAN/control"
        control.parent.mkdir()
        size = sum(path.stat().st_size for path in root.rglob("*") if path.is_file() and not path.is_symlink())
        template = (Path(__file__).parent / "debian/control.in").read_text(encoding="utf-8")
        control.write_text(template.replace("@VERSION@", deb_version).replace("@INSTALLED_SIZE@", str((size + 1023) // 1024)), encoding="utf-8", newline="\n")
        # Debian's per-file inventory enables dpkg --verify after installation.
        # The adjacent SHA-256 remains the checksum for the complete download.
        (control.parent / "md5sums").write_text("".join(checksums), encoding="utf-8", newline="\n")
        for path in root.rglob("*"):
            if not path.is_symlink():
                path.chmod(0o755 if path.is_dir() else 0o644)
        root.chmod(0o755)
        (application / "roslyn-workbench-mcp").chmod(0o755)
        package = output / f"roslyn-workbench-mcp_{deb_version}_amd64.deb"
        subprocess.run(["dpkg-deb", "--build", "--root-owner-group", "-Zxz", str(root), str(package)], check=True)

    with package.open("rb") as stream:
        checksum = hashlib.file_digest(stream, "sha256").hexdigest()
    package.with_suffix(".deb.sha256").write_text(f"{checksum}  {package.name}\n", encoding="ascii")
    subprocess.run([
        sys.executable, str(Path(__file__).with_name("test-deb.py")), "--package", str(package),
        "--expected-version", args.version, "--publish-directory", str(publish),
    ], check=True)
    print(f"Debian package: {package}")
    print("Installation, repository signing and public publication are separate steps.")


if __name__ == "__main__":
    main()
