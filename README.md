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
$ jdkfind                     # table: VERSION / VENDOR / ARCH / SOURCE / HOME
$ jdkfind --json              # machine-readable output (source-generated serializer)
$ jdkfind --path              # bare home paths, one per line
$ jdkfind --latest --path     # best single match — handy for shells:
$ export JAVA_HOME="$(jdkfind --latest --path)"
$ jdkfind -v 21               # filter by feature version
$ jdkfind --vendor azul --arch aarch64
$ jdkfind --latest --jdk-only --path   # newest JDK (ships javac)
$ jdkfind --no-probe          # skip executing JVMs for runtime properties
$ jdkfind --path -0 | xargs -0 -I{} echo {}   # space-safe piping
```

By default `jdkfind` executes each installation's own java executable
once to enrich the metadata with runtime properties (runtime and VM
name/version — the Gradle approach); failures degrade silently to the
release-file metadata.

Exit codes: `0` found, `1` none found, `2` usage error. When both are given,
`--path` wins over `--json`.

## Library usage

```csharp
using JdkFind;

// Locate every JDK; the result merges every provider that reported the same home.
foreach (var jvm in JdkFinder.Locate())
    Console.WriteLine($"{jvm.LanguageVersion} {jvm.Vendor} -> {jvm.Home.FullName}");

// JAVA_HOME if it points at a found JDK, otherwise the newest one.
Jvm? defaultJvm = JdkFinder.GetDefault();
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
