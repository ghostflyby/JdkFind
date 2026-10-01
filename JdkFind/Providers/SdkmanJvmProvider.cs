namespace JdkFind.Providers;

/// <summary>
///     Scans SDKMAN! candidate installs at <c>&lt;candidates&gt;/java</c>, resolved from
///     <c>SDKMAN_CANDIDATES_DIR</c>, <c>SDKMAN_DIR</c>, or the default <c>~/.sdkman</c>.
/// </summary>
public sealed class SdkmanJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "sdkman";

    public SdkmanJvmProvider() : this(ResolveDefaultPrefix()) { }

    public override IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
        commonPrefix is not null ? ScanPrefixes([commonPrefix]).ToAsyncEnumerable() : AsyncEnumerable.Empty<string>();

    private static string? ResolveDefaultPrefix()
    {
        var candidates = Environment.GetEnvironmentVariable("SDKMAN_CANDIDATES_DIR");
        if (!string.IsNullOrWhiteSpace(candidates))
            return Path.Combine(candidates, "java");

        var sdkmanDir = Environment.GetEnvironmentVariable("SDKMAN_DIR");
        if (!string.IsNullOrWhiteSpace(sdkmanDir)) return Path.Combine(sdkmanDir, "candidates", "java");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(profile))
            return null;

        sdkmanDir = Path.Combine(profile, ".sdkman");

        return Path.Combine(sdkmanDir, "candidates", "java");
    }
}
