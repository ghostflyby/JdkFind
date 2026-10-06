namespace JdkFind;

/// <summary>
///     A java binary that can run programs — the minimal JRE: what running a
///     java program actually needs. The probed properties are the binary's own
///     word; <c>-XshowSettings:properties</c> output is an implementation detail
///     rather than a spec promise, so any property except <c>java.version</c>
///     may be absent (null). Where a release file backs the binary, its values
///     fill the properties the binary did not report.
/// </summary>
public sealed record JavaRuntime
{
    /// <summary>The binary's path, taken verbatim.</summary>
    public required string Path { get; init; }

    /// <summary>The installation directory backing this binary, when one was
    /// recognized; null for a standalone binary.</summary>
    public DirectoryInfo? Home { get; init; }

    /// <summary>
    ///     Derives the installation this binary belongs to: the home is derived
    ///     by walking up from <see cref="Path" /> and the release file supplies
    ///     the installation facts, with <see cref="Jvm.Runtime" /> being this
    ///     instance — no child process is spawned. Filesystem reads happen on
    ///     every access, and discovery data such as <see cref="Jvm.Providers" />
    ///     cannot be restored, so for a runtime obtained from a
    ///     <see cref="Jvm" />, that instance remains the better handle — for a
    ///     JDK 8 outer home whose binary lives in <c>jre/bin</c>, the derivation
    ///     yields the inner JRE rather than the outer JDK. Null when no home is
    ///     recognizable or its release file is unreadable.
    /// </summary>
    public Jvm? Installation
    {
        get
        {
            string? home = null;
            if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)) is { } directory)
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

    /// <summary>The probed <c>java.vendor</c>, falling back to the release file's
    /// <c>IMPLEMENTOR</c>; null when neither reports it.</summary>
    public string? VendorRaw { get; init; }

    /// <summary>The normalized vendor that <see cref="VendorRaw" /> matched, or
    /// <see cref="JvmVendor.Unknown" />.</summary>
    public JvmVendor Vendor { get; init; }

    /// <summary>The probed <c>os.name</c>, falling back to the release file's
    /// <c>OS_NAME</c>; null when neither reports it.</summary>
    public string? OsName { get; init; }

    /// <summary>The probed <c>os.arch</c>, falling back to the release file's
    /// <c>OS_ARCH</c>; null when neither reports it.</summary>
    public string? Architecture { get; init; }

    /// <summary>The probed <c>java.runtime.name</c>; null when not probed or not reported.</summary>
    public string? RuntimeName { get; init; }

    /// <summary>The probed <c>java.runtime.version</c>; null when not probed or not reported.</summary>
    public string? RuntimeVersion { get; init; }

    /// <summary>The probed <c>java.vm.name</c>; null when not probed or not reported.</summary>
    public string? VmName { get; init; }

    /// <summary>The probed <c>java.vm.version</c>; null when not probed or not reported.</summary>
    public string? VmVersion { get; init; }

    /// <summary>Summarizes the binary: version, vendor and path.</summary>
    public override string ToString() => $"{Version} {Vendor} — {Path}";
}
