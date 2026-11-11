using System.Text.Json.Serialization;

namespace JdkFind;

/// <summary>
///     Internal wire model for the fixed <see cref="Jvm" /> JSON projection. The
///     camelCase property names declare the key set once and are serialized by the
///     source generator; keeping the model internal keeps the public surface focused
///     on <see cref="Jvm" /> and <see cref="JvmJson" />.
/// </summary>
internal sealed class JvmJsonModel
{
    private readonly Jvm _value;

    internal JvmJsonModel(Jvm value) => _value = value;

    public string Home => _value.Home.FullName;

    public JvmVersion Version => _value.Version;

    public int? LanguageVersion => _value.LanguageVersion;

    public bool HasCompiler => _value.HasCompiler;

    public bool Prerelease => _value.Version.IsPreRelease;

    public JvmVendor Vendor => _value.Vendor;

    public JvmDistribution Distribution => _value.Distribution;

    public string VendorRaw => _value.Executable.VendorRaw;

    public string? RuntimeName => _value.Executable.RuntimeName;

    public string? RuntimeVersion => _value.Executable.RuntimeVersion;

    public string VmName => _value.Executable.VmName;

    public string VmVersion => _value.Executable.VmVersion;

    public string Architecture => _value.Executable.Architecture;

    public string OsName => _value.Executable.OsName;

    public IReadOnlyList<string> Providers => _value.Providers;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JvmJsonModel))]
internal sealed partial class JvmJsonModelContext : JsonSerializerContext;
