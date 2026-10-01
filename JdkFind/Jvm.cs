namespace JdkFind;

/// <summary>A located JVM installation. Metadata comes from the JEP 223 <c>release</c> file.</summary>
public sealed record Jvm
{
    /// <summary>The Java home directory.</summary>
    public required DirectoryInfo Home { get; init; }

    /// <summary>Identifier of the provider that discovered this JVM first.</summary>
    public required string Provider { get; init; }

    /// <summary>Raw <c>JAVA_VERSION</c> value, e.g. <c>21.0.5</c> or <c>1.8.0_402</c>.</summary>
    public string? Version { get; init; }

    /// <summary>Feature version, e.g. 21 for <c>21.0.5</c> and 8 for <c>1.8.0_402</c>.</summary>
    public int? LanguageVersion { get; init; }

    /// <summary>Raw <c>IMPLEMENTOR</c> value, e.g. <c>Eclipse Adoptium</c>.</summary>
    public string? Vendor { get; init; }

    /// <summary>Raw <c>OS_ARCH</c> value, e.g. <c>aarch64</c>.</summary>
    public string? Architecture { get; init; }

    /// <summary>Raw <c>OS_NAME</c> value, e.g. <c>Darwin</c>.</summary>
    public string? OsName { get; init; }

    public override string ToString() =>
        $"{LanguageVersion?.ToString() ?? "?"} ({Version ?? "?"}) {Vendor ?? "?"} — {Home.FullName}";
}
