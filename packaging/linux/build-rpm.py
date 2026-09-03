#!/usr/bin/env python3
"""Publish the Linux x64 Host and build an RPM without installing it."""

import argparse
import hashlib
import os
import platform
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", default=os.environ.get("RoslynWorkbenchVersion") or "0.0.0-dev")
    parser.add_argument("--output-directory", type=Path)
    parser.add_argument("--dotnet-path", default="dotnet")
    parser.add_argument("--artifacts-path", type=Path)
    args = parser.parse_args()

    if platform.system() != "Linux" or platform.machine() != "x86_64":
        raise ValueError("Build the x86_64 package on Linux x64 with the pinned .NET SDK.")
    match = re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z.-]+))?", args.version)
    if not match:
        raise ValueError("Version must be a semantic version without build metadata.")
    if match[4]:
        if "-" in match[4]:
            raise ValueError("RPM package versions do not support hyphens within prerelease identifiers.")
        for identifier in match[4].split("."):
            if not identifier or (identifier.isdigit() and len(identifier) > 1 and identifier.startswith("0")):
                raise ValueError("Prerelease identifiers must follow semantic version rules.")
    if os.environ.get("RoslynWorkbenchVersion", args.version) != args.version:
        raise ValueError("Version must match the release identity supplied by the environment.")

    # RPM's tilde sorts a prerelease before the corresponding production version.
    # Release 1 is the packaging revision and is deliberately distribution-neutral.
    rpm_version = args.version.replace("-", "~", 1)
    repo = Path(__file__).resolve().parents[2]
    is_wsl = "microsoft" in platform.release().lower()
    artifacts = Path("/tmp/artifacts/roslyn-workbench-mcp") if is_wsl else repo / "artifacts"
    if args.output_directory:
        output = args.output_directory.resolve()
        output.mkdir(parents=True, exist_ok=False)
    else:
        parent = artifacts / "rpm"
        parent.mkdir(parents=True, exist_ok=True)
        output = Path(tempfile.mkdtemp(prefix=f"{args.version}-", dir=parent))

    publish = output / "publish"
    command = [
        args.dotnet_path, "publish", str(repo / "src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj"),
        "--configuration", "Release", "--runtime", "linux-x64", "--self-contained", "false",
        "--output", str(publish), "-p:PackAsTool=false", "-p:UseAppHost=true",
        f"-p:RoslynWorkbenchVersion={args.version}",
    ]
    sdk_artifacts = args.artifacts_path or (Path("/tmp/artifacts/roslyn-workbench-mcp") if is_wsl else None)
    if sdk_artifacts:
        command.append(f"--artifacts-path={sdk_artifacts}")
    subprocess.run(command, cwd=repo, check=True)
    (publish / "Roslyn.Workbench.Mcp").rename(publish / "roslyn-workbench-mcp")
    shutil.copyfile(repo / "LICENSE", publish / "LICENSE")

    with tempfile.TemporaryDirectory(prefix="rwmcp-rpm-build-", dir="/tmp") as temporary:
        top = Path(temporary)
        for directory in ("BUILD", "BUILDROOT", "RPMS", "SOURCES", "SPECS", "SRPMS"):
            (top / directory).mkdir()
        # Normalise modes on a Linux filesystem because WSL-mounted source and
        # output directories may present every published file as executable.
        payload = top / "payload"
        shutil.copytree(publish, payload)
        payload.chmod(0o755)
        for path in payload.rglob("*"):
            if not path.is_symlink():
                path.chmod(0o755 if path.is_dir() else 0o644)
        (payload / "roslyn-workbench-mcp").chmod(0o755)
        template = (Path(__file__).parent / "rpm/roslyn-workbench-mcp.spec.in").read_text(encoding="utf-8")
        spec = top / "SPECS/roslyn-workbench-mcp.spec"
        spec.write_text(template.replace("@VERSION@", rpm_version), encoding="utf-8", newline="\n")
        notices = top / "THIRD-PARTY-NOTICES.md"
        notices.write_text((repo / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8"), encoding="utf-8", newline="\n")
        subprocess.run([
            "rpmbuild", "-bb", str(spec),
            "--define", f"_topdir {top}",
            # The apphost is already a published .NET artefact. Stripping it here
            # would make the installed payload differ from the validated publish.
            "--define", "__strip /bin/true",
            "--define", f"_payload_directory {payload}",
            "--define", f"_license_file {repo / 'LICENSE'}",
            "--define", f"_notices_file {notices}",
        ], check=True)
        packages = list((top / "RPMS").rglob("*.rpm"))
        if len(packages) != 1:
            raise ValueError(f"Expected one binary RPM, found {len(packages)}.")
        package = output / f"roslyn-workbench-mcp-{rpm_version}-1.x86_64.rpm"
        shutil.copyfile(packages[0], package)

    with package.open("rb") as stream:
        checksum = hashlib.file_digest(stream, "sha256").hexdigest()
    package.with_suffix(".rpm.sha256").write_text(f"{checksum}  {package.name}\n", encoding="ascii")
    subprocess.run([
        sys.executable, str(Path(__file__).with_name("test-rpm.py")), "--package", str(package),
        "--expected-version", args.version, "--publish-directory", str(publish),
    ], check=True)
    print(f"RPM package: {package}")
    print("Installation, repository signing and public publication are separate steps.")


if __name__ == "__main__":
    main()
