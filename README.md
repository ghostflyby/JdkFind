# JdkFind

Locate installed JDKs on Windows, macOS and Linux — a .NET library (`JdkFind`, zero third-party dependencies, trim/AOT-compatible) and the
`jdkfind` dotnet tool (`JdkFind.Cli`).

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

`--arch` matches case-insensitively, with aliases grouped (`x86_64`/`amd64`/`x64`, `aarch64`/`arm64`). Each installation's java is
run once to enrich metadata; failures fall back silently to the release file.

## Shell completions

`jdkfind completions <shell>` prints a completion script for `bash`, `zsh`,
`fish` or `powershell`. It completes subcommands, option values, installed
versions, and (after `--`) the child command itself; dynamic values come from
a `jdkfind list --json --no-probe` scan, so no runtime is probed.

- **bash** — `jdkfind completions bash > ~/.local/share/bash-completion/completions/jdkfind`
- **zsh** — `jdkfind completions zsh > ~/.zfunc/_jdkfind`, or
  `eval "$(jdkfind completions zsh)"` after `compinit`
- **fish** — `jdkfind completions fish > ~/.config/fish/completions/jdkfind.fish`
- **PowerShell** — `Invoke-Expression (jdkfind completions powershell)` in `$PROFILE`

The scripts are embedded from
[`JdkFind.Cli/completions`](JdkFind.Cli/completions); a packaging build can
exclude shells with the `JdkFindCompletionsExclude` MSBuild property.

## Shell integration

`jdkfind env [version]` prints shell code exporting `JAVA_HOME`; with the
wrapper from `jdkfind init`, plain `jdkfind env 21` also prepends the
installation's `bin` to `PATH` and removes the previously managed entry.

- **bash** — `eval "$(jdkfind init bash)"` in `~/.bashrc`
- **zsh** — `eval "$(jdkfind init zsh)"` in `~/.zshrc`
- **fish** — `jdkfind init fish | source` in `config.fish`; or drop the
  wrapper into `~/.config/fish/functions/jdkfind.fish` (packagers:
  `/usr/share/fish/vendor_functions.d/`)
- **PowerShell** — `jdkfind init powershell | Invoke-Expression` in `$PROFILE`

The environment applies to the current shell only. `cmd.exe` has no functions,
so use run mode (`jdkfind 21 -- java -version`) or `--format powershell`; raw
`--format` also accepts `sh` (default) and `fish`.

## JSON output

`--json` is a stable machine-readable contract: camelCase keys, UTF-8, every
key always present, additive-only within a major version.

| Key                              | Type           | Meaning                                                                 |
|----------------------------------|----------------|-------------------------------------------------------------------------|
| `home`                           | string         | Installation home directory                                             |
| `version`                        | string         | Raw `JAVA_VERSION` from the release file                                |
| `languageVersion`                | number \| null | Feature version (e.g. `21`)                                             |
| `hasCompiler`                    | boolean        | Ships `javac`                                                           |
| `prerelease`                     | boolean        | Pre-release build (`25-ea`, `24.0.1-ea+5`)                              |
| `vendor`                         | string         | Normalized upstream vendor (e.g. `Azul`)                                |
| `distribution`                   | string         | foojay-style distribution name (e.g. `Zulu`)                            |
| `vendorRaw`                      | string         | The binary's reported vendor, falling back to the release `IMPLEMENTOR` (empty when unknown) |
| `runtimeName` / `runtimeVersion` | string \| null | Probed, falling back to the release file (null when unknown)             |
| `vmName` / `vmVersion`           | string         | Probed, falling back to the release file (empty when unknown)           |
| `architecture` / `osName`        | string         | Probed, falling back to the release file (empty when unknown)           |
| `providers`                      | string[]       | Detection sources that reported this home                               |

`Jvm`, `JavaExecutable`, and the flattened `JvmInfo` can be serialized with
`System.Text.Json`.

Exit codes: `0` found, `1` none found, `2` usage error, `130` cancelled;
run mode (`--`) adds `126`/`127` when the command cannot be executed or is
missing. Ctrl+C (SIGINT) / SIGTERM cancel the scan — running probes are
killed — and exit with `130`.

## Library usage

```csharp
using JdkFind;

// Every locate is a fresh scan; the result merges every source that reported
// the same home.
foreach (var jvm in JdkFinder.Default.Locate())
    Console.WriteLine($"{jvm.LanguageVersion} {jvm.Vendor} -> {jvm.Home.FullName}");

// Customize with a with expression; the async overload takes a cancellation
// token for probes and release reads.
var finder = JdkFinder.Default with { Providers = [new NasSource()], ProbeRuntimeProperties = false };
IReadOnlyList<Jvm> jvms = await finder.LocateAsync(cancellationToken);
```

## Watching for changes

`JvmMonitor` turns discovery into a long-running stream. The first item is the
initial snapshot; later items carry the complete snapshot plus what was added,
removed or changed since the previous update. Scans run on the configured poll
interval and on demand; by default each located home is also watched for file
system changes as an additional hint.

```csharp
var monitor = new JvmMonitor(
    JdkFinder.Default with { ProbeRuntimeProperties = false },
    new JvmMonitorOptions { PollInterval = TimeSpan.FromSeconds(30) });

// Elsewhere, when the caller's configuration changes or a scan is due:
monitor.UpdateFinder(reconfiguredFinder);
monitor.RequestRefresh();

await foreach (var update in monitor.WatchAsync(cancellationToken))
{
    foreach (var jvm in update.Added)
        Console.WriteLine($"added: {jvm}");
    // update.Snapshot is the complete current state.
}
```

The monitor owns the previous snapshot, the polling loop and the optional home
watchers; discovery itself stays stateless and can still be called directly.

### Data binding

`JvmObservableMonitor` wraps a monitor in the
`INotifyPropertyChanged`/`ObservableCollection` model used by XAML, WinForms and
MAUI. `Jvms` is a read-only observable collection; `Count`, `IsRunning`,
`LastUpdate`, `LastUpdated` and `LastError` raise property changes. A changed
installation is replaced in place (`CollectionChanged` with `Replace`), so item
templates refresh without making `Jvm` mutable.

```csharp
var state = JvmObservableMonitor.Start(monitor); // captures the UI SynchronizationContext

// Bind an items control to state.Jvms and labels to the properties above.
await state.DisposeAsync();
```

`Start` captures `SynchronizationContext.Current`; call it on the UI thread (or
pass the UI context explicitly) so notifications arrive on the thread bindings
expect. With no context at all, updates are applied on the monitoring loop's
thread.

## Discovery sources

- **All platforms** — `JAVA_HOME`, `PATH` entries.
- **macOS** — `/System/Library/Java/JavaVirtualMachines`,
  `/Library/Java/JavaVirtualMachines`, `~/Library/Java/JavaVirtualMachines`,
  MacPorts (`/opt/local/share/java`), Homebrew kegs.
- **Linux** — `/usr/lib/jvm`, `/usr/java`, `/usr/lib64/jvm`, `/usr/lib32/jvm`,
  `/opt/jdk`, `/opt/jdks`, `/opt/ibm`, Gentoo layouts, `$SNAP` mirrors,
  Homebrew kegs; inside a flatpak sandbox the shared runtime extensions and
  `/app/jdk`; inside snap mounts `usr/lib/jvm` and the JetBrains `jbr`.
- **FreeBSD / OpenBSD** — `/usr/local` ports layout (`openjdk*`).
- **Nix** — `<profile>/lib/openjdk` under `NIX_PROFILES`, `~/.nix-profile`,
  `~/.local/state/nix/profiles`, `/nix/var/nix/profiles/default`,
  `/etc/profiles/per-user/*`, `/run/current-system/sw`.
- **Windows** — vendor directories under `%ProgramFiles%` /
  `%ProgramFiles(x86)%`, the JavaSoft/Adoptium/Microsoft/Azul/Corretto/
  AdoptOpenJDK/IBM Semeru/BellSoft registry keys (64- and 32-bit views),
  `%LOCALAPPDATA%\Programs`.
- **IDEs** — `~/.jdks` (IntelliJ) on Windows and Linux.
- **Version managers** — SDKMAN!, asdf, mise, jenv, Gradle, Jabba, Coursier,
  conda and Scoop, each honouring its environment variable (`SDKMAN_DIR`,
  `ASDF_DATA_DIR`, `MISE_DATA_DIR`, `JENV_ROOT`, `GRADLE_USER_HOME`,
  `JABBA_HOME`, `COURSIER_JVM_CACHE`/`COURSIER_CACHE`, `CONDA_PREFIX`,
  `SCOOP`/`SCOOP_GLOBAL`).

Environment variables win over defaults; missing or unreadable locations are
skipped silently; results are deduplicated by canonical path (symlinks
expanded), and `Providers` lists every source that reported the home.

## Probing a specific JVM

Discovery answers what exists; probing answers whether a given binary runs and
what it reports. `JavaExecutable` is the runnable binary; `Jvm` is an
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
    // Did not run to completion: spawn error, exit code or timeout,
    // plus the output tail for display.
}
else
{
    // runtime.Version, runtime.Architecture, ... describe what the binary
    // reported; runtime.Installation derives the full Jvm.
}
```

The probe runs `-XshowSettings:properties -version` with a 15-second cap that
kills the child on overrun (the synchronous factories take a `timeout`
override; the async twins take a cancellation token). JVM-affecting variables (`_JAVA_OPTIONS`, `JDK_JAVA_OPTIONS`, `JAVA_TOOL_OPTIONS`, `CLASSPATH`,
`LD_PRELOAD`, `LD_LIBRARY_PATH`) are stripped, so the verdict describes the
binary, not the shell. Binary-reported values win over the release file, which
fills only the gaps; a binary that rejects the option is a start failure.

## Build and test

```bash
dotnet build JdkFind.slnx
dotnet test JdkFind.Tests/JdkFind.Tests.csproj
```

Requires the .NET 10 SDK (see `global.json`). Tests are
[xunit.v3](https://xunit.net/), run by `dotnet test` through
Microsoft.Testing.Platform.

## License

[Apache-2.0](LICENSE)
