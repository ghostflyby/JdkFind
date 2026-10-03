# AGENTS.md

Guidance for coding agents working in this repository.

## What this is

JdkFind locates installed JDKs on Windows, macOS and Linux. It ships as:

- `JdkFind/` — the library (net10.0, zero third-party dependencies)
- `JdkFind.Cli/` — the `jdkfind` dotnet tool (`PackAsTool`, portable package)
- `JdkFind.Tests/` — xunit.v3 tests (Exe + Microsoft.Testing.Platform)

All project directories live at the repository root; there is no `src/` nesting.

## Commands

```bash
 dotnet build JdkFind.slnx
 dotnet test JdkFind.Tests/JdkFind.Tests.csproj          # MTP mode via global.json
 dotnet pack JdkFind/JdkFind.csproj -c Release           # library package
 dotnet pack JdkFind.Cli/JdkFind.Cli.csproj -c Release   # tool package (command: jdkfind)
```

`global.json` pins the SDK floor (10.0.100, `latestFeature`) and sets
`test.runner = Microsoft.Testing.Platform`. Do not remove the MTP setting: the
legacy VSTest mode on the .NET 10 SDK refuses the xunit.v3 project.

## Hard requirements

- **AOT compatibility.** Both assemblies carry `IsAotCompatible`. No
  reflection-based discovery: providers are assembled as an explicit list
  behind `JdkFinder.Default`. JSON goes through the source
  generator (`JvmJsonContext`); serialize only the primitive `JvmDto`, never
  `Jvm` directly — `DirectoryInfo` makes the generated serializer recurse
  without bound.
- **Environment variables win.** Version-manager providers read their env vars
  before falling back to default locations: `JAVA_HOME`, `GRADLE_USER_HOME`,
  `JABBA_HOME`, `SDKMAN_CANDIDATES_DIR`/`SDKMAN_DIR`, `SCOOP`/`SCOOP_GLOBAL`,
  `HOMEBREW_PREFIX`.
- **Platform guards.** Windows-only code checks `OperatingSystem.IsWindows()`
  and is annotated `[SupportedOSPlatform("windows")]`. The registry provider
  reads the 64-bit view only (documented trade-off: 32-bit installs under
  WOW6432Node are not covered).
- **Resilience.** Missing and unreadable locations are skipped silently; one
  bad directory must never abort the scan.

## Architecture rules

- Providers return **validated** home paths: the `IJvmProvider` contract
  requires every path to pass the public `JavaHomeLayout.Probe`, and the
  capability interfaces apply it by default through the `GetJavaHome` seam
  (which may resolve a subpath — macOS bundle / Homebrew keg layouts). The
  facade trusts the contract (no re-validation) and owns deduplication plus
  release-file parsing (internal `ReleaseFile`).
- Deduplication merges candidates by canonical path (every symlink expanded per
  segment; case-insensitive on Windows and macOS). `Jvm.Providers` lists every
  provider that reported the home, in provider order — enumeration therefore
  materializes all providers before yielding, it does not stream incrementally.
- Injection constructors take clean non-null roots; env-var lookups and null
  handling stay inside the parameterless constructors' default resolution.
- `GetDefault`: `JAVA_HOME` wins when resolvable, otherwise the newest JVM
  by feature version with the full version as tiebreaker (`1.8.0_402` style
  versions are handled).
- Discovery is synchronous by design: every source is local file-system or
  registry I/O measured in milliseconds, and the BCL has no async
  directory-enumeration API to make it real. A future genuinely-async provider
  should add a parallel async interface instead of converting this contract.
- Namespaces: the root carries the facade and core abstractions; built-in
  providers live in `JdkFind.Providers`. No `.Models`/`.Helpers`-style
  namespaces; helpers are `internal`, exposed to tests via `InternalsVisibleTo`.

## Public API contract

`JdkFind` is guarded by Microsoft.CodeAnalysis.PublicApiAnalyzers with
`TreatWarningsAsErrors`: every public symbol must be claimed in
`JdkFind/PublicAPI.Shipped.txt` or `JdkFind/PublicAPI.Unshipped.txt`, or the
build fails (RS0016 unclaimed addition, RS0017 unclaimed removal). When adding
or changing public API, update `PublicAPI.Unshipped.txt` — mark removals with a
`*REMOVED*<symbol>` line — then run `dotnet build`: the analyzer prints the
exact symbol strings to claim. Never touch `PublicAPI.Shipped.txt` by hand;
at release time the publish workflow opens a housekeeping PR moving that
release's Unshipped entries into Shipped, so Shipped mirrors what nuget.org
actually has.

## Tests

- xunit.v3 only. No VSTest packages (`Microsoft.NET.Test.Sdk`,
  `xunit.runner.visualstudio`); the test project is an `Exe` and `dotnet test`
  drives it through MTP directly.
- `TreatWarningsAsErrors` is on, so xunit analyzers fail the build.
- Tests run in parallel across classes: only `JdkFinderTests` may mutate
  process environment variables, and other test classes must not use
  `JdkFinder.Default` (its built-in sources read `JAVA_HOME`/`PATH` at
  construction). Build finders with explicit `Providers` instead — every
  scanning provider has a constructor accepting an explicit root.
- Symlink and permission tests skip Windows (link creation needs privileges)
  and skip when running as root (chmod has no effect).

## Conventions

- **English only** for code comments, XML docs, commit messages, and all
  documentation.
- Commit messages: imperative mood, concise subject (e.g. `Add Scoop
  provider`).
- `main` is protected: direct pushes are rejected, so land every change on a
  feature branch and open a pull request; merge only with green CI. Tag pushes
  (`v*.*.*`, the publish trigger) are not branch pushes and remain direct.
  Workflows are allowed to create and approve pull requests — the publish
  workflow's Shipped housekeeping PR relies on that setting.
- Before handing off, `dotnet build JdkFind.slnx` and
  `dotnet test JdkFind.Tests/JdkFind.Tests.csproj` must pass with zero
  warnings.
