"""Publication orchestration checks without mutating Git refs or deploying."""

import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import call, patch


def load_script(name, file):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(file))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


release = load_script("release_docs", "deploy-release.py")
sync = load_script("sync_docs", "sync-versions.py")


class DocumentationDeploymentTests(unittest.TestCase):
    def test_release_bootstraps_before_native_mike_and_refreshes_after_validation(self):
        repository = Path("repository")
        record = {"version": "1.2.0"}
        with (
            patch.object(release, "resolve_mike", return_value="mike"),
            patch.object(release, "try_run_output", return_value="baseline"),
            patch.object(release, "resolve_release", return_value=record),
            patch.object(release, "update_branch") as update,
            patch.object(release.subprocess, "run") as run,
            patch.object(release, "restore_publication_branch") as restore,
        ):
            release.publish_rendered_documentation(repository, Path("mkdocs.yml"), "1.3.0", {}, ["validate"], published_versions=set(), push=False)
            self.assertEqual([call(repository, record, release.DEFAULT_URL), call(repository, record, release.DEFAULT_URL, {"1.3.0"})], update.call_args_list)
            commands = [item.args[0] for item in run.call_args_list]
            self.assertIn("latest", commands[0])
            self.assertEqual(["validate"], commands[1])
            self.assertEqual("set-default", commands[2][1])
            self.assertFalse(any("push" in command for command in commands))
            restore.assert_called_once_with(repository, "baseline")
            self.assertEqual(release.DEFAULT_URL + "docs/", run.call_args_list[0].kwargs["env"]["RW_DOCS_SITE_URL"])

    def test_failed_validation_restores_branch_and_never_pushes(self):
        with (
            patch.object(release, "resolve_mike", return_value="mike"),
            patch.object(release, "try_run_output", return_value="baseline"),
            patch.object(release, "resolve_release", return_value={}),
            patch.object(release, "update_branch"),
            patch.object(release.subprocess, "run", side_effect=[None, ValueError("bad docs")]) as run,
            patch.object(release, "restore_publication_branch") as restore,
        ):
            with self.assertRaisesRegex(ValueError, "bad docs"):
                release.publish_rendered_documentation(Path("repo"), Path("config"), "1.2.0", {}, ["validate"], published_versions=set(), push=True)
            self.assertEqual(2, run.call_count)
            restore.assert_called_once_with(Path("repo"), "baseline")

    def test_prefixed_root_parser_accepts_native_and_generated_redirects(self):
        for delay, function in ((0, "location.replace"), (1, "window.location.replace")):
            text = f'<meta content="{delay}; url=latest/"><script>{function}("latest/")</script><a href="latest/">Continue</a>'
            with self.subTest(delay=delay), patch.object(sync, "git", side_effect=["docs/index.html", text]) as git:
                self.assertEqual("latest", sync.root_target(Path("repo"), "revision"))
                self.assertEqual("revision:docs/index.html", git.call_args.args[-1])

    def test_cleanup_rejects_a_product_homepage_change(self):
        versions = {"dev": {"version": "dev", "aliases": []}}
        with (
            patch.object(sync, "read_versions", return_value=versions),
            patch.object(sync, "root_target", return_value="dev"),
            patch.object(sync, "git", return_value="M\tindex.html"),
            self.assertRaisesRegex(ValueError, "unexpectedly changed 'index.html'"),
        ):
            sync.validate_sync(Path("repo"), "baseline", versions, versions, [], "dev")

    def test_release_identity_can_be_read_before_or_after_migration(self):
        for listing, expected in ((None, ""), ("docs/versions.json", "docs/")):
            with self.subTest(prefix=expected), patch.object(release, "try_run_output", return_value=listing):
                self.assertEqual(expected, release.published_prefix(Path("repo")))

    def test_cleanup_keeps_latest_on_the_highest_retained_production_release(self):
        versions = {name: {"version": name, "aliases": ["latest"] if name == "1.3.0" else []}
                    for name in ("dev", "1.3.0", "1.2.0", "1.4.0-rc.1")}
        expected, removed, default = sync.desired_state(versions, {"1.2.0", "1.4.0-rc.1"})
        self.assertEqual(["1.3.0"], removed)
        self.assertEqual(["latest"], expected["1.2.0"]["aliases"])
        self.assertEqual("latest", default)


if __name__ == "__main__":
    unittest.main()
