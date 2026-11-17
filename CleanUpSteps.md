# Open-LRD 1.0 Repository & Build Artifact Cleanup Steps

**Status:** Completed. Tracked `bin/`, `obj/`, `.vs/`, and `.user` artifacts have been removed from the Git index, `.gitignore` is updated, all 26 `CIDE.A`–`CIDE.Z` corpus files are tracked, and the Release build and test suites (`194/194` smoke tests, `417/417` UI tests) pass with a clean working tree.

This guide documents the steps executed to clean up tracked build/IDE artifacts, update `.gitignore` so release documentation and bundled corpus files are properly tracked while generated build outputs stay ignored, and verify a clean Release build for `v1.0.0`.

---

## 1. Summary of Current Repository Hygiene Issues

1. **Tracked Build & IDE Artifacts:** Over 150 generated files under `OPEN-LRD/bin/`, `OPEN-LRD/obj/`, `OPEN-LRD.SmokeTests/bin/`, `OPEN-LRD.SmokeTests/obj/`, `OPEN-LRD.ManualUiTests/bin/`, `OPEN-LRD.ManualUiTests/obj/`, `.vs/`, and `OPEN-LRD/OPEN-LRD.csproj.user` were previously committed to the Git index. Because Git continues tracking files that are already in the index even if rules exist in `.gitignore`, every build dirties the working tree.
2. **Overly Broad `.gitignore` Rules:**
   - `*.md` at the bottom of `.gitignore` prevents `RELEASE_READINESS.md` (and `CleanUpSteps.md`) from being tracked unless force-added, while `README.md` is only tracked because it was committed earlier.
   - `gcide-0.54/` in `.gitignore` ignores the bundled GCIDE dictionary directory. While 23 of the 26 `CIDE.*` files were force-added earlier, **`CIDE.A`, `CIDE.D`, and `CIDE.O` are currently untracked and ignored**, meaning a fresh clone would be missing dictionary entries for the letters A, D, and O.
3. **Standard `.NET` Ignore Rules Missing:** `bin/` and `obj/` directory patterns and `*.user` files are not explicitly ignored in `.gitignore`, allowing new build outputs (such as `*.deps.json`, `*.runtimeconfig.json`, `*.xml`, `*.baml`, `*.resources`, and `project.assets.json`) to be picked up by `git add .`.
4. **Temporary Session Files:** `session-ses_e287.md` in the repository root is a local agent session transcript that should not be packaged in the release.

---

## 2. Step-by-Step Cleanup Instructions

Run the following commands from the repository root (`C:\Users\three\source\repos\OPEN-LRD` in PowerShell or Command Prompt).

### Step 1: Update `.gitignore`

Replace the contents of `.gitignore` with the following rules so that `bin/`, `obj/`, `.vs/`, `.logs/`, and `*.user` files are ignored, while Markdown documentation and the `gcide-0.54/` corpus are not blanket-ignored:

```gitignore
# Prerequisites & External Scratch Directories
*.d
github.com/

# .NET Build and Intermediate Directories
[Bb]in/
[Oo]bj/
[Bb]uild/
build-*/

# Visual Studio & User-Specific Files
.vs/
*.user
*.suo
*.userosscache
*.sln.docstates

# Compiled Object & Library Files
*.slo
*.lo
*.o
*.obj
*.gch
*.pch
*.ilk
*.pdb
*.so
*.dylib
*.dll
*.cache
*.editorconfig
*.g.*
*.developer.*
*.mod
*.smod
*.lai
*.la
*.a
*.lib
*.csproj.*
*.exe
*.out
*.app
*.dwo
*.AssemblyInfo*

# CMake & vcpkg
CMakeFiles/
CMakeCache.txt
cmake_install.cmake
Makefile
install_manifest.txt
compile_commands.json
vcpkg_installed/

# Temporary, Log, & Session Files
*.tmp
*.log
*.bak
*.swp
*_wpftmp.*
Testing/
.cache/
.logs/
session-*.md
```

---

### Step 2: Untrack `bin/`, `obj/`, `.vs/`, and `.user` Files from the Git Index

Remove the tracked build and IDE artifacts from Git's index while keeping the files on disk (`--cached`):

```powershell
git rm -r --cached .vs
git rm -r --cached OPEN-LRD/bin OPEN-LRD/obj
git rm -r --cached OPEN-LRD.SmokeTests/bin OPEN-LRD.SmokeTests/obj
git rm -r --cached OPEN-LRD.ManualUiTests/bin OPEN-LRD.ManualUiTests/obj
git rm --cached OPEN-LRD/OPEN-LRD.csproj.user
```

---

### Step 3: Remove Temporary Session Transcript (Optional)

Delete the local session transcript file from the workspace root so it is not included in release archives:

```powershell
Remove-Item -Path .\session-ses_e287.md -ErrorAction SilentlyContinue
```

---

### Step 4: Stage the Missing GCIDE Corpus Files (`CIDE.A`, `CIDE.D`, `CIDE.O`)

Ensure all 26 letter files (`CIDE.A` through `CIDE.Z`) in `gcide-0.54/gcide-0.54/` are tracked by Git:

```powershell
git add gcide-0.54/gcide-0.54/CIDE.A gcide-0.54/gcide-0.54/CIDE.D gcide-0.54/gcide-0.54/CIDE.O
```

Verify that all 26 files are tracked:

```powershell
(git ls-files "gcide-0.54/gcide-0.54/CIDE.*" | Measure-Object).Count
# Expected output: 26
```

---

### Step 5: Clean and Rebuild in `Release` Configuration

Perform a clean `Release` build and run both test suites to verify everything compiles with zero warnings and zero errors and that rebuilding no longer dirties `git status`:

```powershell
dotnet clean OPEN-LRD.slnx -c Release --nologo
dotnet clean OPEN-LRD.ManualUiTests/OPEN-LRD.ManualUiTests.csproj -c Release --nologo

dotnet build OPEN-LRD.slnx -c Release --nologo
dotnet build OPEN-LRD.ManualUiTests/OPEN-LRD.ManualUiTests.csproj -c Release --nologo

dotnet run --project OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj --no-build --no-restore -c Release
dotnet run --project OPEN-LRD.ManualUiTests/OPEN-LRD.ManualUiTests.csproj --no-build --no-restore -c Release
```

Expected results:
- Build: `0 Warning(s)`, `0 Error(s)`
- SmokeTests: `194 passed, 0 failed`
- ManualUiTests: `417 passed, 0 failed`

---

### Step 6: Stage Intended Files and Verify `git status`

Stage the updated source, test, `.gitignore`, and documentation files, then inspect `git status`:

```powershell
git add .gitignore RELEASE_READINESS.md CleanUpSteps.md README.md To-Do.txt Module_8_Plan.txt
git add OPEN-LRD OPEN-LRD.SmokeTests OPEN-LRD.ManualUiTests
git status
```

Confirm that:
- No files under `bin/`, `obj/`, `.vs/`, or `.logs/` appear as staged or untracked.
- Only intended source, test, documentation, and corpus files are staged for the release commit.
