# JdkFind

Locate installed JDKs on Windows, macOS and Linux — a .NET library and the
`jdkfind` dotnet tool.

- **Library** (`JdkFind`, assembly `JdkFind.Core.dll`): net10.0, zero
  third-party dependencies, trim/AOT-compatible (`IsAotCompatible`).
- **Tool** (`JdkFind.Cli`, assembly `jdkfind.dll`): the `jdkfind` command,
  packaged as a portable dotnet tool.

## Install

```bash
dotnet tool install --global JdkFind.Cli
```

## CLI usage

```console
$ jdkfind                                        # newest home (stdout)
$ jdkfind 21                                     # newest 21.x home
$ jdkfind 8                                      # feature level; matches legacy 1.8.0_x installs
$ jdkfind 21 -- java -version                    # run with JAVA_HOME and bin/ (or jre/bin) of the selection
$ jdkfind 21 java                                # newest 21.x bin/java
$ jdkfind info 21                                # one installation's details (stderr)
$ jdkfind list                                   # human-readable table (stderr)
$ jdkfind list --json                            # all installations as JSON (stdout)
$ jdkfind list --distribution corretto           # filter by foojay distribution name
$ jdkfind list --arch x64                        # filter by architecture substring
$ jdkfind completions bash                       # print a completion script (bash, zsh, fish, powershell)
```

`--arch` is a case-insensitive substring match against the raw
`OS_ARCH`/`os.arch` value. Release files disagree on the spelling, so a
text naming one well-known alias matches every spelling in its group:
`x86_64`/`amd64`/`x64` and `aarch64`/`arm64`.

By default `jdkfind` executes each installation's own java executable
once to enrich the metadata with runtime properties (runtime and VM
name/version — the Gradle approach); failures degrade silently to the
release-file metadata.

## Shell completions

`jdkfind completions <shell>` prints a completion script to stdout — `bash`,
`zsh`, `fish` or `powershell`; run it without a shell to list what this build
carries. The scripts complete the subcommands, option names and option values
(vendors, distributions, architectures, release levels), version prefixes of
the installations actually present, and the bin tools of the current
selection. Dynamic values come from a `jdkfind list --json --no-probe` scan,
so completion never launches a runtime probe. After `--` the words form the
child command's own line, in the fashion of fish's `java_home --exec`
completion: the command word completes from the resolved installation's
`bin/`, and later words delegate to that command's own completion where the
shell provides one (file names as the fallback).

- **bash** — `jdkfind completions bash > ~/.local/share/bash-completion/completions/jdkfind`
- **zsh** — `jdkfind completions zsh > ~/.zfunc/_jdkfind` (a `$fpath`
  directory; the `#compdef` header registers it), or
  `eval "$(jdkfind completions zsh)"` in `~/.zshrc` after `compinit`.
- **fish** — `jdkfind completions fish > ~/.config/fish/completions/jdkfind.fish`
- **PowerShell** — `Invoke-Expression (jdkfind completions powershell)` in
  `$PROFILE`.

The scripts live in [`JdkFind.Cli/completions`](JdkFind.Cli/completions) and
are embedded into the tool as manifest resources. A packaging build can leave
shells out — for a channel that installs completion files itself — with the
`JdkFindCompletionsExclude` MSBuild property (canonical shell names,
semicolon-separated; on a command line escape the semicolons as `%3B`, e.g.
`-p:JdkFindCompletionsExclude=fish%3Bpowershell`, or pass it as an
environment variable). The `completions` command then lists and serves only
the rest.

## JSON output

`--json` writes a stable, machine-readable contract: camelCase keys,
UTF-8, one object per installation (`list` emits an array). Every key
is always present — unknown values are `null` — and within the same
major version the shape is additive-only: keys are never renamed or
removed.

| Key                              | Type           | Meaning                                                                 |
|----------------------------------|----------------|-------------------------------------------------------------------------|
| `home`                           | string         | Installation home directory                                             |
| `version`                        | string         | Raw `JAVA_VERSION` from the release file                                |
| `languageVersion`                | number \| null | Feature version (e.g. `21`)                                             |
| `hasCompiler`                    | boolean        | Ships `javac`                                                           |
| `prerelease`                     | boolean        | Pre-release build (`25-ea`, `24.0.1-ea+5`)                              |
| `vendor`                         | string         | Normalized upstream vendor (e.g. `Azul`)                                |
| `distribution`                   | string         | foojay-style distribution name (e.g. `Zulu`)                            |
| `vendorRaw`                      | string \| null | The binary's reported vendor, falling back to the release `IMPLEMENTOR` |
| `runtimeName` / `runtimeVersion` | string \| null | Probed `java.runtime.*`                                                 |
| `vmName` / `vmVersion`           | string \| null | Probed `java.vm.*`                                                      |
| `architecture` / `osName`        | string \| null | Probed, falling back to the release file                                |
| `providers`                      | string[]       | Detection sources that reported this home                               |

Exit codes: `0` found, `1` none found, `2` usage error, `130` cancelled;
run mode (`--`) adds `126`/`127` when the command cannot be executed or is
not found. `--json` switches every locating command to machine-readable output
on stdout; `jdkfind completions` prints its script verbatim. Ctrl+C / SIGINT / SIGTERM cancel the scan: running runtime probes
are killed and the process exits with `130`.

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

| Source                                                                                                                                                                                                                                                               | Platforms        |
|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------|
| `JAVA_HOME`, `PATH` entries                                                                                                                                                                                                                                          | all              |
| `/System/Library/Java/JavaVirtualMachines` (system), `/Library/Java/JavaVirtualMachines` (machine) and `~/Library/Java/JavaVirtualMachines` (per-user; also IntelliJ's download target)                                                                              | macOS            |
| Homebrew kegs — `openjdk`, `openjdk@N`, `graalvm` (`HOMEBREW_PREFIX`, `/opt/homebrew`, …); cask JDKs land in `/Library/Java` and are found above                                                                                                                     | macOS, Linux     |
| `/usr/lib/jvm`, `/usr/java`, `/usr/lib64/jvm`, `/usr/lib32/jvm`, `/opt/jdk`, `/opt/jdks`, `/opt/ibm`, Gentoo installs (`/usr/lib`, `/usr/lib64`, `/opt`), `$SNAP` mirrors                                                                                            | Linux            |
| `/usr/local` ports layout (`openjdk*`)                                                                                                                                                                                                                               | FreeBSD, OpenBSD |
| Flatpak sandbox JVMs — shared runtime extensions (`/usr/lib/sdk`) and the app-bundled `/app/jdk` (inside a flatpak sandbox)                                                                                                                                          | Linux (flatpak)  |
| `%ProgramFiles%`/`%ProgramFiles(x86)%` vendor directories (Java, Eclipse Adoptium, AdoptOpenJDK, Microsoft, Zulu, Amazon Corretto, BellSoft), Windows registry (JavaSoft, Adoptium, Microsoft, Azul, Corretto, AdoptOpenJDK, IBM Semeru, BellSoft — 64+32-bit views) | Windows          |
| `~/.jdks` (IntelliJ)                                                                                                                                                                                                                                                 | Windows, Linux   |
| SDKMAN! (`SDKMAN_DIR`), asdf (`ASDF_DATA_DIR`), Gradle (`GRADLE_USER_HOME`), Jabba (`JABBA_HOME`), Scoop (`SCOOP`/`SCOOP_GLOBAL`)                                                                                                                                    | per tool         |

Environment variables always take precedence over the default locations.
Missing or unreadable locations are skipped silently; results are
deduplicated by canonical path (symbolic links expanded), so the same
physical JDK is reported once no matter how many providers found it —
with `Providers` (the SOURCE column) faithfully listing every one of them.

## Probing a specific JVM

Discovery answers "what exists"; probing answers "does *this* binary run,
and what does it say about itself?" — the pre-flight check before launching
with a user-configured java. The two concepts live on separate types: a
`JavaExecutable` is the runnable binary — the minimal JRE — and a `Jvm` is an
installation directory whose `Executable` property carries its own java.

```csharp
JavaExecutable? runtime = JdkFinder.Default.FromExecutable(userConfiguredJavaPath);
// or: JdkFinder.Default.FromHome(javaHomeDirectory);  → Jvm?, with jvm.Executable
if (runtime is null)
{
    // The path does not yield a JVM at all.
}
else if (runtime.StartFailure is { } failure)
{
    // The binary did not run to completion — `failure` carries the reason
    // (spawn error, exit code, or timeout) plus the output tail for display.
}
else
{
    // runtime.Version, runtime.Architecture, ... describe what the binary
    // reported; runtime.Home is the backing installation directory (null for a
    // standalone binary) and runtime.Installation derives the full Jvm.
}
```

The probe runs `-XshowSettings:properties -version` under a 15-second cap
that kills the child on overrun — the synchronous factories accept a
`timeout` override, while the async twins take a cancellation token (compose
`CancellationTokenSource.CancelAfter` to bound the wait yourself); runtimes
that reject that option are reported as a start failure rather than specially
accommodated. The child's environment has the JVM-affecting
variables (`_JAVA_OPTIONS`, `JDK_JAVA_OPTIONS`, `JAVA_TOOL_OPTIONS`,
`CLASSPATH`, `LD_PRELOAD`, `LD_LIBRARY_PATH`) removed, so the verdict
describes the binary, not the launcher's shell. Values the binary reports
take precedence over the home's release file, which only fills what the
binary did not say. The platform specification guarantees the standard
property set (vendor, os.name, os.arch, java.vm.name, java.vm.version), so
their counterparts on `JavaExecutable` are non-nullable strings — empty only
when no probe ran, the probe degraded, or an implementation broke that
promise; the runtime-only `java.runtime.*` pair is null in those cases.

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
