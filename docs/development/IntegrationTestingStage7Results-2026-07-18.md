# Integration Testing Stage 7 Results

Date: 18 July 2026

## Outcome

Stage 7 is complete. The normal component-integration median decreased from 48.23 seconds to 27.57 seconds, a 42.8% reduction, while retaining all 117 named real-boundary tests. A four-owner matrix has a 12.84-second local median critical path and was selected for pull-request CI because it provides faster feedback and owner-specific failure reporting.

VSTest remains the repository runner. An isolated Microsoft.Testing.Platform evaluation showed a modest representative improvement, but adoption would require repository-wide project, command, reporting and coverage changes. NuGet package caching remains disabled because the repository has no lock-file policy or `packages.lock.json` files.

## CI execution model

The general test workflow now has four responsibilities:

- the Ubuntu fast job restores and builds once, then runs Unit and Contract tests without another build or restore;
- an Ubuntu owner matrix independently runs Workspace, Plugins.Core, CodeActions and Host component integration;
- Ubuntu and Windows acceptance jobs build Release once, publish the Host once and run the ten tests against that published executable; the Windows job also runs the full Workspace durability project; and
- a scheduled macOS job runs Workspace durability and published-Host acceptance as platform evidence without making macOS a pull-request gate prematurely.

The Code Action compatibility audit retains its separate path-sensitive, main-branch, scheduled and manually dispatched workflow. Every job emits TRX, uploads results under `always()`, applies a five-minute hang threshold and verifies a minimum test count. The acceptance fixture retains server stderr, process details and isolated scenario state on failure; successful local runs continue to clean their roots.

The workflows deliberately use ordinary `dotnet` commands. They do not contain the WSL-only artifact routing used by agents working on the shared Windows filesystem.

During validation, the Host integration project was found to locate plugin fixture outputs through `$(ArtifactsPath)`. That would fail in ordinary CI where the property is correctly absent. Its copy target now selects the fixture assembly and private dependency from MSBuild's resolved project-reference outputs and derives the fixture `.deps.json` beside the resolved assembly. This works independently of output-routing policy.

## Component integration measurement

Measurements used already-built Debug projects with `--no-build --no-restore`. They are local WSL comparison evidence, not timing assertions. Each project passed on every sequential and concurrent run.

| Project | Tests | Run 1 | Run 2 | Run 3 | Median | Median peak memory |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Workspace | 52 | 10.73 s | 11.38 s | 10.83 s | 10.83 s | 344.2 MiB |
| Plugins.Core | 20 | 5.44 s | 5.75 s | 5.65 s | 5.65 s | 374.4 MiB |
| CodeActions | 5 | 8.42 s | 8.63 s | 8.43 s | 8.43 s | 381.7 MiB |
| Host | 40 | 2.71 s | 2.66 s | 2.61 s | 2.66 s | 506.5 MiB |

The sum of project medians is 27.57 seconds. Compared with the Stage 0 sum of 48.23 seconds, this is 20.66 seconds or 42.8% faster and satisfies the 40% target without treating elapsed time as a test assertion.

### Owner matrix versus consolidation

The consolidated proxy runs the four owners sequentially after a shared build. The owner-matrix proxy starts the same four already-built projects together and uses the longest owner as its critical path.

| Layout | Run 1 | Run 2 | Run 3 | Median |
| --- | ---: | ---: | ---: | ---: |
| Consolidated sequential | 27.30 s | 28.42 s | 27.52 s | 27.52 s |
| Four-owner concurrent critical path | 12.84 s | 12.84 s | 13.19 s | 12.84 s |

The concurrent proxy is 53.3% faster than the current sequential proxy and 73.4% faster than the Stage 0 sequential baseline. Hosted CI will add four checkouts, restores, builds and runner allocations, so this is not presented as a hosted-runner duration. The matrix was selected because pull-request wall time and isolated owner reporting are more valuable than minimising aggregate runner work. The four jobs retain exact project names, counts and result artifacts; future hosted-runner evidence can revisit the choice without changing test ownership.

## Microsoft.Testing.Platform evaluation

The evaluation used a temporary `global.json` selecting `Microsoft.Testing.Platform` and a temporary imported property file. No runner or package change was made in the repository.

The current xUnit 3.2.2 package can run under MTP, but the checked-in projects are libraries and therefore are not runnable MTP modules without changing each test project to an executable. The isolated configuration successfully ran all five CodeActions integration tests and enforced `--minimum-expected-tests 5`.

Compatibility is not yet equivalent:

- xUnit 3.2.2 selects its MTP v1 runner; moving to the current MTP v2 package is a separate package decision;
- VSTest's `--filter`, `--logger` and blame-hang command line cannot be reused unchanged;
- TRX is available through xUnit's MTP reporting extension, but its option names differ;
- category filters must move to xUnit/MTP trait or query filters;
- `--test-modules`, module parallelism and native minimum-count enforcement become useful only after every selected project is an executable MTP module;
- the current `coverlet.collector` path is VSTest-oriented, while MTP coverage requires the Microsoft Testing Platform code-coverage extension and a changed coverage command; and
- current Visual Studio versions support MTP discovery, but adopting it would still require the repository project changes above and verification across every contributor IDE path.

A representative CodeActions comparison produced:

| Mode | VSTest median | MTP median | MTP change |
| --- | ---: | ---: | ---: |
| Clean output, cached packages, build and test | 14.58 s | 13.48 s | 7.5% faster |
| Warm, already-built test execution | 8.65 s | 7.30 s | 15.6% faster |

The gain is not material enough to justify the compatibility and operational migration. Stage 7 therefore retains VSTest and uses the TRX count guard to prevent empty filtered runs. A future isolated runner change can reconsider MTP v2 after coverage and IDE policy are ready.

## NuGet caching decision

`setup-dotnet` caching remains disabled. The repository has no `packages.lock.json` files and no approved lock-file policy, so enabling cache keys now would silently introduce a dependency-management decision into CI work. Lock files and package caching remain a separate future action.

## Platform evidence

- Linux: all four component projects passed in every final sequential and concurrent run; the ten-test published-Host acceptance suite passed during Stage 7 implementation.
- Windows: all ten published-Host acceptance tests passed in Stage 3. During Stage 7, the full 52-test Workspace project passed natively after fixing live instance-status sharing, including atomic file, multi-file transaction, recovery and inter-process locking scenarios.
- macOS: scheduled coverage is configured. It remains non-gating until hosted runtime and reliability evidence exists.

## Verification

- Workflow YAML parsed successfully.
- CI and contributor instructions contain no agent-only artifact-routing switch or environment variable.
- Host component integration passed 40 tests after the fixture-output portability fix and in all final measurement runs.
- Workspace status publisher unit coverage passed 24 focused tests.
- Native Windows Workspace integration passed all 52 tests.
- The minimum-count script accepted a real ten-test acceptance TRX and rejects missing or insufficient results.
- Governed files were formatted where applicable and normalised to CRLF.

