# Open-LRD 1.0 Release Readiness

**Status:** Readiness review in progress. The project version is set to `1.0.0`,
but no release tag or release artifact has been created.

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

## Remaining release gates

- Review the existing worktree changes, execute the repository/index cleanup in
  `CleanUpSteps.md`, and ensure release packaging includes only intended source,
  tests, documentation, and licensed corpus files.
- Perform the final Release-configuration build and Windows UI verification
  immediately before creating a release tag or artifact.

## Version metadata

The application project declares `1.0.0` package/informational version and
`1.0.0.0` assembly/file versions. This metadata prepares the project for release;
it does not indicate that the release gates above are complete.
