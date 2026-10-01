namespace JdkFind.Providers;

/// <summary>
///     Scans the directory IntelliJ IDEA downloads JDKs into: macOS
///     <c>~/Library/Java/JavaVirtualMachines</c>, other platforms <c>~/.jdks</c>.
/// </summary>
public sealed class IntelliJJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "intellij";

    public IntelliJJvmProvider() : this(ResolveDefaultPrefix()) { }

    public override IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
        commonPrefix is { } prefix ? ScanPrefixes([prefix]).ToAsyncEnumerable() : AsyncEnumerable.Empty<string>();

    private static string? ResolveDefaultPrefix()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(profile))
            return null;

        return OperatingSystem.IsMacOS()
            ? Path.Combine(profile, "Library", "Java", "JavaVirtualMachines")
            : Path.Combine(profile, ".jdks");
    }
}
