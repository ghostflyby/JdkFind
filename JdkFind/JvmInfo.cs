using System.Text.Json.Serialization;
using JdkFind.Json;

namespace JdkFind;

/// <summary>
///     Flattened, serializable view of a <see cref="Jvm" /> and its
///     <see cref="JavaExecutable" />.
/// </summary>
public sealed record JvmInfo
{
    /// <summary>The Java home directory.</summary>
    [JsonConverter(typeof(DirectoryInfoJsonConverter))]
    public required DirectoryInfo Home { get; init; }

    /// <summary>The JDK version.</summary>
    public required JvmVersion Version { get; init; }

    /// <summary>The feature version; null when unparseable.</summary>
    public int? LanguageVersion { get; init; }

    /// <summary>Whether the installation ships a compiler.</summary>
    public bool HasCompiler { get; init; }

    /// <summary>True when <see cref="Version" /> is a pre-release build.</summary>
    public bool Prerelease => Version.IsPreRelease;

    /// <summary>The normalized vendor from the release file.</summary>
    public JvmVendor Vendor { get; init; }

    /// <summary>The distribution per the foojay names.</summary>
    public JvmDistribution Distribution { get; init; }

    /// <summary>The binary's reported vendor, falling back to the release file.</summary>
    public string VendorRaw { get; init; } = string.Empty;

    /// <summary>The probed runtime name; null when unknown.</summary>
    public string? RuntimeName { get; init; }

    /// <summary>The probed runtime version; null when unknown.</summary>
    public string? RuntimeVersion { get; init; }

    /// <summary>The probed VM name; empty when unknown.</summary>
    public string VmName { get; init; } = string.Empty;

    /// <summary>The probed VM version; empty when unknown.</summary>
    public string VmVersion { get; init; } = string.Empty;

    /// <summary>The probed architecture; empty when unknown.</summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>The probed OS name; empty when unknown.</summary>
    public string OsName { get; init; } = string.Empty;

    /// <summary>Identifiers of every provider that reported this home.</summary>
    public required IReadOnlyList<string> Providers { get; init; }

    /// <summary>Compares all stored properties; paths use the platform path comparer.</summary>
    public bool Equals(JvmInfo? other) =>
        other is not null &&
        PathComparison.DirectoryEquals(Home, other.Home) &&
        Version.Equals(other.Version) &&
        LanguageVersion == other.LanguageVersion &&
        HasCompiler == other.HasCompiler &&
        Vendor == other.Vendor &&
        Distribution == other.Distribution &&
        string.Equals(VendorRaw, other.VendorRaw, StringComparison.Ordinal) &&
        string.Equals(RuntimeName, other.RuntimeName, StringComparison.Ordinal) &&
        string.Equals(RuntimeVersion, other.RuntimeVersion, StringComparison.Ordinal) &&
        string.Equals(VmName, other.VmName, StringComparison.Ordinal) &&
        string.Equals(VmVersion, other.VmVersion, StringComparison.Ordinal) &&
        string.Equals(Architecture, other.Architecture, StringComparison.Ordinal) &&
        string.Equals(OsName, other.OsName, StringComparison.Ordinal) &&
        ProviderSequence.Equals(Providers, other.Providers);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PathComparison.DirectoryHash(Home));
        hash.Add(Version);
        hash.Add(LanguageVersion);
        hash.Add(HasCompiler);
        hash.Add(Vendor);
        hash.Add(Distribution);
        hash.Add(VendorRaw);
        hash.Add(RuntimeName);
        hash.Add(RuntimeVersion);
        hash.Add(VmName);
        hash.Add(VmVersion);
        hash.Add(Architecture);
        hash.Add(OsName);
        ProviderSequence.AddHash(ref hash, Providers);
        return hash.ToHashCode();
    }

    /// <summary>Projects an installation into its flattened view.</summary>
    public static JvmInfo From(Jvm jvm) => new()
    {
        Home = jvm.Home,
        Version = jvm.Version,
        LanguageVersion = jvm.LanguageVersion,
        HasCompiler = jvm.HasCompiler,
        Vendor = jvm.Vendor,
        Distribution = jvm.Distribution,
        VendorRaw = jvm.Executable.VendorRaw,
        RuntimeName = jvm.Executable.RuntimeName,
        RuntimeVersion = jvm.Executable.RuntimeVersion,
        VmName = jvm.Executable.VmName,
        VmVersion = jvm.Executable.VmVersion,
        Architecture = jvm.Executable.Architecture,
        OsName = jvm.Executable.OsName,
        Providers = jvm.Providers,
    };
}
