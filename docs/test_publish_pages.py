"""Regression coverage for Pages identity reuse and stale public deployments."""

import importlib.util
import json
import os
from pathlib import Path
import runpy
import signal
import tempfile
import unittest
from unittest.mock import patch
import urllib.error


spec = importlib.util.spec_from_file_location("publish_pages", Path(__file__).with_name("publish-pages.py"))
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)


class PublishPagesTests(unittest.TestCase):
    def environment(self) -> dict[str, str]:
        return {
            "GITHUB_SHA": "commit",
            "GITHUB_RUN_ID": "123",
            "GITHUB_RUN_ATTEMPT": "1",
            "PAGES_BUILD_VERSION": "a" * 40,
            "GITHUB_REPOSITORY": "owner/repository",
            "GITHUB_API_URL": "https://api.github.com",
            "GH_TOKEN": "Token",
            "ACTIONS_ID_TOKEN_REQUEST_URL": "https://oidc.example/token",
            "ACTIONS_ID_TOKEN_REQUEST_TOKEN": "RequestToken",
        }

    def test_generated_site_commits_have_distinct_identities_for_the_same_source(self) -> None:
        identities = []
        site_commits = ["a" * 40, "b" * 40, "c" * 40]
        for site_commit in site_commits:
            environment = self.environment()
            environment["PAGES_BUILD_VERSION"] = site_commit
            with patch.object(publisher, "request_bytes", side_effect=[
                b'{"value":"OidcToken"}', b'{"id":"deployment"}', b'{"status":"succeed"}',
            ]) as request, patch("builtins.print") as output:
                publisher.deploy(42, environment)
                payload = request.call_args_list[1].args[2]
                identities.append(payload["pages_build_version"])
                self.assertEqual(42, payload["artifact_id"])
                self.assertEqual("OidcToken", payload["oidc_token"])
                output.assert_any_call("::add-mask::OidcToken", flush=True)
                self.assertEqual("https://api.github.com/repos/owner/repository/pages/deployments/deployment", request.call_args.args[0])
        self.assertEqual(site_commits, identities)

    def test_same_generated_commit_retains_identity_across_runs_and_retries(self) -> None:
        for run, attempt in (("123", "1"), ("124", "1"), ("124", "2")):
            environment = self.environment()
            environment.update(GITHUB_RUN_ID=run, GITHUB_RUN_ATTEMPT=attempt)
            with patch.object(publisher, "request_bytes", side_effect=[
                b'{"value":"OidcToken"}', b'{"id":"deployment"}', b'{"status":"succeed"}',
            ]) as request, patch("builtins.print"):
                publisher.deploy(42, environment)
                self.assertEqual(environment["PAGES_BUILD_VERSION"], request.call_args_list[1].args[2]["pages_build_version"])

    def test_missing_or_invalid_generated_commit_stops_before_authentication(self) -> None:
        for build_version in (None, "", "commit-123-1", "a" * 39, "a" * 41, "g" * 40, "a" * 40 + "\n"):
            environment = self.environment()
            if build_version is None:
                del environment["PAGES_BUILD_VERSION"]
            else:
                environment["PAGES_BUILD_VERSION"] = build_version

            with self.subTest(build_version=build_version), patch.object(publisher, "request_bytes") as request:
                with self.assertRaisesRegex(ValueError, "full generated-site commit SHA"):
                    publisher.deploy(42, environment)
                request.assert_not_called()

    def test_pending_status_and_missing_id_use_build_version(self) -> None:
        with patch.object(publisher, "request_bytes", side_effect=[
            b'{"value":"OidcToken"}', b'{}', b'{"status":"queued"}', b'{"status":"succeed"}',
        ]) as request, patch.object(publisher.time, "sleep") as sleep, patch("builtins.print"):
            publisher.deploy(42, self.environment())
            sleep.assert_called_once_with(10)
            self.assertTrue(request.call_args.args[0].endswith("/" + self.environment()["PAGES_BUILD_VERSION"]))

    def test_failed_deployment_stops(self) -> None:
        for status in ("deployment_failed", "deployment_content_failed", "deployment_cancelled", "deployment_lost"):
            with self.subTest(status=status), patch.object(publisher, "request_bytes", side_effect=[
                b'{"value":"OidcToken"}', b'{"id":"deployment"}', json.dumps({"status": status}).encode(),
            ]), patch("builtins.print"):
                with self.assertRaisesRegex(ValueError, status):
                    publisher.deploy(42, self.environment())

    def test_timeout_cancels_deployment(self) -> None:
        responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}']
        responses.extend([b'{"status":"queued"}'] * 60)
        responses.append(b'{}')
        with (
            patch.object(publisher, "request_bytes", side_effect=responses) as request,
            patch.object(publisher.time, "sleep"),
            patch("builtins.print"),
        ):
            with self.assertRaises(TimeoutError):
                publisher.deploy(42, self.environment())
            self.assertEqual("https://api.github.com/repos/owner/repository/pages/deployments/deployment/cancel", request.call_args.args[0])

    def test_deployment_deadline_includes_network_time(self) -> None:
        responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}', b'{}']
        with (
            patch.object(publisher, "request_bytes", side_effect=responses),
            patch.object(publisher.time, "monotonic", side_effect=[0, 600]),
            patch("builtins.print"),
        ):
            with self.assertRaises(TimeoutError):
                publisher.deploy(42, self.environment())

    def test_transient_polling_errors_recover_without_cancelling(self) -> None:
        errors = [
            TimeoutError(),
            urllib.error.URLError("Connection unavailable"),
            urllib.error.HTTPError("https://api.github.com", 503, "Unavailable", {}, None),
            urllib.error.HTTPError("https://api.github.com", 429, "Limited", {}, None),
        ]
        for error in errors:
            responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}', error, b'{"status":"succeed"}']
            with (
                self.subTest(error=type(error).__name__),
                patch.object(publisher, "request_bytes", side_effect=responses) as request,
                patch.object(publisher.time, "sleep") as sleep,
                patch("builtins.print"),
            ):
                publisher.deploy(42, self.environment())
                self.assertEqual(4, request.call_count)
                self.assertFalse(any(call.args[0].endswith("/cancel") for call in request.call_args_list))
                sleep.assert_called_once_with(10)

    def test_exhausted_polling_errors_cancel_and_preserve_failure(self) -> None:
        responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}']
        responses.extend([TimeoutError()] * 5)
        responses.append(b'{}')
        with (
            patch.object(publisher, "request_bytes", side_effect=responses) as request,
            patch.object(publisher.time, "sleep"),
            patch("builtins.print"),
        ):
            with self.assertRaisesRegex(RuntimeError, "five consecutive"):
                publisher.deploy(42, self.environment())
            self.assertTrue(request.call_args.args[0].endswith("/deployment/cancel"))
            self.assertEqual(5, request.call_args.kwargs["timeout"])

    def test_successful_poll_resets_the_consecutive_error_budget(self) -> None:
        responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}']
        responses.extend([TimeoutError()] * 4)
        responses.append(b'{"status":"queued"}')
        responses.extend([TimeoutError()] * 4)
        responses.append(b'{"status":"succeed"}')
        with (
            patch.object(publisher, "request_bytes", side_effect=responses) as request,
            patch.object(publisher.time, "sleep"),
            patch("builtins.print"),
        ):
            publisher.deploy(42, self.environment())
            self.assertFalse(any(call.args[0].endswith("/cancel") for call in request.call_args_list))

    def test_permanent_polling_error_cancels_without_retry(self) -> None:
        error = urllib.error.HTTPError("https://api.github.com", 403, "Forbidden", {}, None)
        responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}', error, b'{}']
        with patch.object(publisher, "request_bytes", side_effect=responses) as request:
            with self.assertRaises(urllib.error.HTTPError) as failure:
                publisher.deploy(42, self.environment())
            self.assertIs(error, failure.exception)
            self.assertTrue(request.call_args.args[0].endswith("/cancel"))

    def test_cancellation_failure_does_not_hide_the_original_failure_or_log_credentials(self) -> None:
        responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}', ValueError("Invalid status"), TimeoutError("Token")]
        with (
            patch.object(publisher, "request_bytes", side_effect=responses),
            patch("builtins.print") as output,
        ):
            with self.assertRaisesRegex(ValueError, "Invalid status"):
                publisher.deploy(42, self.environment())
            warning = output.call_args.args[0]
            self.assertIn("Could not confirm cancellation", warning)
            self.assertNotIn("Token", warning)

    def test_signals_cancel_pending_deployment_and_restore_previous_handlers(self) -> None:
        for signum in (signal.SIGINT, signal.SIGTERM):
            original = {item: signal.getsignal(item) for item in (signal.SIGINT, signal.SIGTERM)}

            def response(url, token=None, payload=None, **kwargs):
                if url == self.environment()["ACTIONS_ID_TOKEN_REQUEST_URL"]:
                    return b'{"value":"OidcToken"}'
                if url.endswith("/cancel"):
                    return b'{}'
                if payload is not None:
                    return b'{"id":"deployment"}'
                self.assertTrue(callable(signal.getsignal(signum)))
                signal.raise_signal(signum)

            with (
                self.subTest(signum=signum),
                patch.object(publisher, "request_bytes", side_effect=response) as request,
                patch("builtins.print"),
            ):
                with self.assertRaises(SystemExit) as failure:
                    publisher.deploy(42, self.environment())
                self.assertEqual(128 + signum, failure.exception.code)
                self.assertTrue(request.call_args.args[0].endswith("/deployment/cancel"))
                self.assertEqual(5, request.call_args.kwargs["timeout"])
                for item, handler in original.items():
                    self.assertEqual(handler, signal.getsignal(item))

    def test_keyboard_interrupt_cancels_pending_deployment(self) -> None:
        responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}', KeyboardInterrupt(), b'{}']
        with patch.object(publisher, "request_bytes", side_effect=responses) as request:
            with self.assertRaises(KeyboardInterrupt):
                publisher.deploy(42, self.environment())
            self.assertTrue(request.call_args.args[0].endswith("/deployment/cancel"))

    def test_interrupted_creation_cancels_by_its_generated_site_commit(self) -> None:
        responses = [b'{"value":"OidcToken"}', KeyboardInterrupt(), b'{}']
        with patch.object(publisher, "request_bytes", side_effect=responses) as request:
            with self.assertRaises(KeyboardInterrupt):
                publisher.deploy(42, self.environment())
            self.assertTrue(request.call_args.args[0].endswith("/" + self.environment()["PAGES_BUILD_VERSION"] + "/cancel"))

    def create_site(self, root: Path) -> None:
        (root / "versions.json").write_text('[{"version":"1.0.0","aliases":["latest"]},{"version":"old","aliases":[]}]', encoding="utf-8")
        (root / "index.html").write_text("Root", encoding="utf-8")
        (root / "latest").mkdir()
        (root / "latest" / "index.html").write_text("Latest", encoding="utf-8")
        for name in ("1.0.0", "old"):
            (root / name).mkdir()
            (root / name / "index.html").write_text(name, encoding="utf-8")
        (root / "1.0.0" / "compatibility.html").write_text("Compatibility", encoding="utf-8")
        catalogue = root / "1.0.0" / "reference" / "tools"
        catalogue.mkdir(parents=True)
        (catalogue / "catalog.json").write_text('{"commit":"Commit"}', encoding="utf-8")

    def test_checks_include_new_pages_without_requiring_them_in_older_versions(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.create_site(root)
            self.assertEqual([
                "versions.json", "index.html", "1.0.0/index.html", "latest/index.html", "1.0.0/compatibility.html",
                "1.0.0/reference/tools/catalog.json", "old/index.html",
            ], [path.as_posix() for path in publisher.public_checks(root)])

    def test_invalid_version_is_rejected(self) -> None:
        for version in ("", ".", "..", "../outside", "back\\slash", 42):
            with self.subTest(version=version), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / "versions.json").write_text(json.dumps([{"version": version}]), encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "Invalid version"):
                    publisher.public_checks(root)

    def test_public_verification_waits_for_exact_bytes_not_just_success_status(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.create_site(root)
            expected = [(root / path).read_bytes() for path in publisher.public_checks(root)]
            missing = urllib.error.URLError("Unavailable")
            responses = [missing, b"Stale", *expected[2:], *expected[:2]]
            with (
                patch.object(publisher, "request_bytes", side_effect=responses) as request,
                patch.object(publisher.time, "sleep") as sleep,
            ):
                publisher.verify_public_site(root, "https://example.test/project/")
                self.assertEqual(9, request.call_count)
                sleep.assert_called_once_with(10)
                self.assertNotIn("Token", str(request.call_args_list))

    def test_stale_public_site_fails_after_bounded_retries(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.create_site(root)
            with patch.object(publisher, "request_bytes", return_value=b"Stale"), patch.object(publisher.time, "sleep"):
                with self.assertRaisesRegex(ValueError, "catalog.json"):
                    publisher.verify_public_site(root, "https://example.test/project")

    def test_unrecognised_alias_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "versions.json").write_text('[{"version":"1.0.0","aliases":["../outside"]}]', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Invalid alias"):
                publisher.public_checks(root)

    def test_public_timeout_is_retried(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.create_site(root)
            expected = [(root / path).read_bytes() for path in publisher.public_checks(root)]
            responses = [TimeoutError(), *expected[1:], expected[0]]
            with (
                patch.object(publisher, "request_bytes", side_effect=responses),
                patch.object(publisher.time, "sleep"),
            ):
                publisher.verify_public_site(root, "https://example.test/project")

    def test_public_deadline_includes_network_time(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.create_site(root)
            with patch.object(publisher.time, "monotonic", side_effect=[0, 300]):
                with self.assertRaisesRegex(TimeoutError, "five minutes"):
                    publisher.verify_public_site(root, "https://example.test/project")

    def test_http_request_anonymous_and_authenticated(self) -> None:
        with patch.object(publisher.urllib.request, "urlopen") as open_url:
            open_url.return_value.__enter__.return_value.read.return_value = b"Result"
            self.assertEqual(b"Result", publisher.request_bytes("https://example.test"))
            anonymous = open_url.call_args.args[0]
            self.assertIsNone(anonymous.get_header("Authorization"))
            self.assertEqual("GET", anonymous.get_method())
            publisher.request_bytes("https://example.test", "Token", {"artifact_id": 42})
            authenticated = open_url.call_args.args[0]
            self.assertEqual("Bearer Token", authenticated.get_header("Authorization"))
            self.assertEqual("POST", authenticated.get_method())
            self.assertEqual({"artifact_id": 42}, json.loads(authenticated.data))

    def test_main_outputs_url_only_after_public_verification(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "output"
            arguments = ["publish-pages.py", "--artifact-id", "42", "--site", directory, "--url", "https://example.test/"]
            with (
                patch.dict(os.environ, {"GITHUB_OUTPUT": str(output)}),
                patch("sys.argv", arguments),
                patch.object(publisher, "deploy") as deploy,
                patch.object(publisher, "verify_public_site") as verify,
            ):
                self.assertEqual(0, publisher.main())
                deploy.assert_called_once()
                verify.assert_called_once_with(Path(directory), "https://example.test/")
                self.assertEqual("page_url=https://example.test/\n", output.read_text(encoding="utf-8"))

    def test_command_line_entry_point_deploys_and_verifies_before_success(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.create_site(root)
            output = root / "output"
            responses = [b'{"value":"OidcToken"}', b'{"id":"deployment"}', b'{"status":"succeed"}']
            responses.extend((root / path).read_bytes() for path in publisher.public_checks(root))
            environment = self.environment()
            environment["GITHUB_OUTPUT"] = str(output)
            arguments = ["publish-pages.py", "--artifact-id", "42", "--site", directory, "--url", "https://example.test"]
            with (
                patch.dict(os.environ, environment),
                patch("sys.argv", arguments),
                patch.object(publisher.urllib.request, "urlopen") as open_url,
                patch("builtins.print"),
            ):
                open_url.return_value.__enter__.return_value.read.side_effect = responses
                with self.assertRaises(SystemExit) as exit_result:
                    runpy.run_path(str(Path(__file__).with_name("publish-pages.py")), run_name="__main__")
                self.assertEqual(0, exit_result.exception.code)
                self.assertEqual("page_url=https://example.test/\n", output.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
