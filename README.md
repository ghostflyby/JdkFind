# JdkFind

Locate installed JDKs on Windows, macOS and Linux — a .NET library and the
`jdkfind` dotnet tool.

- **Library** (`JdkFind`): net10.0, zero third-party dependencies,
  trim/AOT-compatible (`IsAotCompatible`).
- **Tool** (`JdkFind.Cli`): the `jdkfind` command, packaged as a portable
  dotnet tool.

## Install

```bash
dotnet tool install --global JdkFind.Cli
```

## CLI usage

```console
$ jdkfind                                        # newest home (stdout)
$ jdkfind 21                                     # newest 21.x home
$ jdkfind 21 java                                # newest 21.x bin/java
$ jdkfind info 21                                # one installation's details (stderr)
$ jdkfind list                                   # human-readable table (stderr)
$ jdkfind list --json                            # all installations as JSON (stdout)
$ jdkfind list --distribution corretto           # filter by foojay distribution name
$ jdkfind list --arch x64                        # filter by architecture substring
```

`--arch` is a case-insensitive substring match against the raw
`OS_ARCH`/`os.arch` value. Release files disagree on the spelling, so a
text naming one well-known alias matches every spelling in its group:
`x86_64`/`amd64`/`x64` and `aarch64`/`arm64`.

By default `jdkfind` executes each installation's own java executable
once to enrich the metadata with runtime properties (runtime and VM
name/version — the Gradle approach); failures degrade silently to the
release-file metadata.

## JSON output

`--json` writes a stable, machine-readable contract: camelCase keys,
UTF-8, one object per installation (`list` emits an array). Every key
is always present — unknown values are `null` — and within the same
major version the shape is additive-only: keys are never renamed or
removed.

| Key | Type | Meaning |
|---|---|---|
| `home` | string | Installation home directory |
| `version` | string | Raw `JAVA_VERSION` from the release file |
| `languageVersion` | number \| null | Feature version (e.g. `21`) |
| `hasCompiler` | boolean | Ships `javac` |
| `prerelease` | boolean | Early-access build (e.g. `25-ea`) |
| `vendor` | string | Normalized upstream vendor (e.g. `Azul`) |
| `distribution` | string | foojay-style distribution name (e.g. `Zulu`) |
| `vendorRaw` | string \| null | Raw `IMPLEMENTOR` string |
| `runtimeName` / `runtimeVersion` | string \| null | Probed `java.runtime.*` |
| `vmName` / `vmVersion` | string \| null | Probed `java.vm.*` |
| `architecture` / `osName` | string \| null | Release file or probe |
| `providers` | string[] | Detection sources that reported this home |

Exit codes: `0` found, `1` none found, `2` usage error, `130` cancelled.
`--json` switches every command to machine-readable output on stdout.
Ctrl+C / SIGINT / SIGTERM cancel the scan: running runtime probes are killed
and the process exits with `130` after a short grace period.

## Library usage

```csharp
using JdkFind;

// Every locate is a fresh scan; the result merges every source that reported
// the same home.
foreach (var jvm in JdkFinder.Default.Locate())
    Console.WriteLine($"{jvm.LanguageVersion} {jvm.Vendor} -> {jvm.Home.FullName}");

// Customize through a with expression; the async overload honours the
// cancellation token for probes and release reads.
var finder = JdkFinder.Default with { Providers = [new NasSource()], ProbeRuntimeProperties = false };
IReadOnlyList<Jvm> jvms = await finder.LocateAsync(cancellationToken);
```

## Discovery sources

| Source | Platforms |
|---|---|
| `JAVA_HOME`, `PATH` entries | all |
| `/Library/Java/JavaVirtualMachines` (system) and `~/Library/Java/JavaVirtualMachines` (per-user; also IntelliJ's download target) | macOS |
| Homebrew OpenJDK kegs (`HOMEBREW_PREFIX`, `/opt/homebrew`, …) | macOS, Linux |
| `/usr/lib/jvm` | Linux |
| `%ProgramFiles%` vendor directories, Windows registry (JavaSoft, Adoptium, Microsoft, Azul, Corretto) | Windows |
| `~/.jdks` (IntelliJ) | Windows, Linux |
| SDKMAN! (`SDKMAN_DIR`), Gradle (`GRADLE_USER_HOME`), Jabba (`JABBA_HOME`), Scoop (`SCOOP`) | per tool |

Environment variables always take precedence over the default locations.
Missing or unreadable locations are skipped silently; results are
deduplicated by canonical path (symbolic links expanded), so the same
physical JDK is reported once no matter how many providers found it —
with `Providers` (the SOURCE column) faithfully listing every one of them.

## Build and test

```bash
dotnet build JdkFind.slnx
dotnet test JdkFind.Tests/JdkFind.Tests.csproj
```

Requires the .NET 10 SDK (see `global.json`). Tests run on
[xunit.v3](https://xunit.net/) driven natively by `dotnet test` through
Microsoft.Testing.Platform.

## License

[Apache-2.0](LICENSE)
