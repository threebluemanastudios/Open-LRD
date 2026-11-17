# Open-LRD 1.0 Release Readiness

**Status:** Ready for `1.0.0` release. All `1.0.0` release gates, automated test
suites, member documentation audits, and repository packaging checks are complete.

## Current build and verification baseline

- Target: .NET 10 WPF (`net10.0-windows`); run the application and UI suite on
  Windows.
- Application, smoke-test, and Manual UI test builds pass with zero warnings and
  zero errors. XML documentation is generated, compiler warnings are treated as
  errors, and the public and internal/private member comment audit is complete.
  Release-configuration builds of the solution and Manual UI project pass.
- Smoke suite: **194 passed, 0 failed**.
- Manual WPF UI suite: **417 passed, 0 failed**.
- Manual file-import UI testing and verification are complete, including format
  detection/override, text-mode restriction, structured CSV dry run/import,
  checksum skip, Source reporting, and NLP-only Topic behavior.
- Module 8 Phase 5 automated schema, resource-limit, empty-file, duplicate,
  revision-reconciliation, maintenance merge, and persistence acceptance
  fixtures are complete and passing.
- Module 8 Phase 6 candidate expansion passes both a large-graph smoke regression
  and a Workbench UI regression with 60 unrelated Sense nodes ahead of the target
  senses in the generic candidate order.
- Repository hygiene and packaging cleanup (`CleanUpSteps.md`) are complete:
  generated build/IDE artifacts are untracked and ignored, and all intended
  source, test, documentation, and bundled GCIDE corpus files (`CIDE.A`–`CIDE.Z`)
  are tracked.
- Core documentation and current verification counts are maintained in
  `README.md`, `To-Do.txt`, and `Module_8_Plan.txt`.

## Reproduce the current checks

From the repository root:

```text
dotnet build OPEN-LRD.slnx --no-restore --nologo -c Release
dotnet build OPEN-LRD.ManualUiTests/OPEN-LRD.ManualUiTests.csproj --no-restore --nologo -c Release
dotnet run --project OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj --no-build --no-restore -c Release
dotnet run --project OPEN-LRD.ManualUiTests/OPEN-LRD.ManualUiTests.csproj --no-build --no-restore -c Release
```

## Completed release gates

- [x] Complete the member-comment audit across all public, internal, and private
  classes, constructors, functions, and methods (including test helpers).
- [x] Complete the automated Module 8 Phase 5 schema, resource-limit,
  revision-reconciliation, maintenance merge, and persistence fixtures listed in
  `Module_8_Plan.txt` Sections 6.4 and 7.
- [x] Clean up worktree/index artifacts and `.gitignore` (`CleanUpSteps.md`) so
  release packaging includes only intended source, tests, documentation, and
  licensed corpus files.
- [x] Verify the final Release-configuration build, smoke suite (`194/194`), and
  Windows WPF UI suite (`417/417`) with zero warnings and zero errors.

## Version metadata

The application project declares `1.0.0` package/informational version and
`1.0.0.0` assembly/file versions, matching the verified `1.0.0` release baseline.
