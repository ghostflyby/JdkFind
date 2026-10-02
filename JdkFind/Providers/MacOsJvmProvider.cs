namespace JdkFind.Providers;

/// <summary>
///     Scans the macOS JVM directories: the system-wide
///     <c>/Library/Java/JavaVirtualMachines</c> and the per-user
///     <c>~/Library/Java/JavaVirtualMachines</c>. Together they cover everything
///     <c>/usr/libexec/java_home</c> reports, without spawning it.
/// </summary>
public sealed class MacOsJvmProvider(IEnumerable<string> prefixes) : ICommonPrefixesJvmProvider
{
    public string Name => "macos";

    private readonly string[] prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    public MacOsJvmProvider() : this(ResolveDefaultPrefixes()) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() => prefixList;

    string? ICommonPrefixesJvmProvider.GetJavaHome(string subDirectory) => JavaHomeLayout.Probe(subDirectory);

    private static IEnumerable<string> ResolveDefaultPrefixes()
    {
        if (!OperatingSystem.IsMacOS())
            yield break;

        yield return "/Library/Java/JavaVirtualMachines";

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
            yield return Path.Combine(profile, "Library", "Java", "JavaVirtualMachines");
    }
}
