namespace JdkFind;

/// <summary>
///     A java binary that can run programs — the minimal JRE: what running a
///     java program actually needs. The platform specification guarantees the
///     standard system properties (<c>java.version</c>, <c>java.vendor</c>,
///     <c>os.name</c>, <c>os.arch</c>, <c>java.vm.name</c>,
///     <c>java.vm.version</c>, ...), so their counterparts here are non-nullable:
///     they carry the binary's own reported values — which take precedence over
///     the backing release file — and are empty when no probe ran or an
///     implementation did not report one. The runtime-only
///     <c>java.runtime.name</c>/<c>java.runtime.version</c> pair is not part of
///     that guarantee and stays nullable.
/// </summary>
public sealed record JavaRuntime
{
    /// <summary>The binary's path, taken verbatim.</summary>
    public required string Path { get; init; }

    /// <summary>The installation directory backing this binary, when one was
    /// recognized; null for a standalone binary.</summary>
    public DirectoryInfo? Home { get; init; }

    /// <summary>
    ///     Derives the installation this binary belongs to: the backing
    ///     <see cref="Home" /> when known — otherwise the home derived by walking
    ///     up from <see cref="Path" /> (a JDK 8 outer home whose binary lives in
    ///     <c>jre/bin</c> yields the inner JRE rather than the outer JDK) — and
    ///     the release file found there supplies the installation facts, with
    ///     <see cref="Jvm.Runtime" /> being this instance — no child process is
    ///     spawned. Filesystem reads happen on every access, and discovery data
    ///     such as <see cref="Jvm.Providers" /> cannot be restored, so for a
    ///     runtime obtained from a <see cref="Jvm" />, that instance remains the
    ///     better handle. Null when no home is recognizable or its release file
    ///     is unreadable.
    /// </summary>
    public Jvm? Installation
    {
        get
        {
            string? home = Home?.FullName;
            if (home is null && System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)) is { } directory)
            {
                home = JavaHomeLayout.Probe(directory)
                    ?? (System.IO.Path.GetDirectoryName(directory) is { } parentDirectory
                        ? JavaHomeLayout.Probe(parentDirectory)
                        : null);
            }

            if (home is null)
                return null;

            var release = ReleaseFile.TryParse(home);
            return release is null ? null : Jvm.FromRelease(home, this, release);
        }
    }

    /// <summary>
    ///     Why the binary could not be started — a spawn error, the probe
    ///     timeout, or a non-zero exit carrying the output tail — or null when it
    ///     ran, reported nothing parseable, or no probe ran.
    /// </summary>
    public string? StartFailure { get; init; }

    /// <summary>The binary's version: the probed <c>java.version</c>, falling back
    /// to the release file's <c>JAVA_VERSION</c> where a release file backs the
    /// binary.</summary>
    public required JvmVersion Version { get; init; }

    /// <summary>The binary's reported <c>java.vendor</c>, falling back to the
    /// release file's <c>IMPLEMENTOR</c>; empty when neither reports it.</summary>
    public string VendorRaw { get; init; } = string.Empty;

    /// <summary>The normalized vendor that <see cref="VendorRaw" /> matched, or
    /// <see cref="JvmVendor.Unknown" />.</summary>
    public JvmVendor Vendor { get; init; }

    /// <summary>The binary's reported <c>os.name</c>, falling back to the release
    /// file's <c>OS_NAME</c>; empty when neither reports it.</summary>
    public string OsName { get; init; } = string.Empty;

    /// <summary>The binary's reported <c>os.arch</c>, falling back to the release
    /// file's <c>OS_ARCH</c>; empty when neither reports it.</summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>The probed <c>java.runtime.name</c>; null when not probed or not
    /// reported (not part of the standard property guarantee).</summary>
    public string? RuntimeName { get; init; }

    /// <summary>The probed <c>java.runtime.version</c>; null when not probed or not
    /// reported (not part of the standard property guarantee).</summary>
    public string? RuntimeVersion { get; init; }

    /// <summary>The probed <c>java.vm.name</c>; empty when not probed or not
    /// reported.</summary>
    public string VmName { get; init; } = string.Empty;

    /// <summary>The probed <c>java.vm.version</c>; empty when not probed or not
    /// reported.</summary>
    public string VmVersion { get; init; } = string.Empty;

    /// <summary>Summarizes the binary: version, vendor and path.</summary>
    public override string ToString() => $"{Version} {Vendor} — {Path}";
}
