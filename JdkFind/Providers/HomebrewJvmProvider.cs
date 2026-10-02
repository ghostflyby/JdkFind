namespace JdkFind.Providers;

/// <summary>
///     Scans Homebrew OpenJDK kegs (<c>&lt;prefix&gt;/opt/openjdk*</c>) under the prefixes
///     indicated by <c>HOMEBREW_PREFIX</c> and the conventional locations for Apple
///     Silicon, Intel Macs and Linuxbrew. The keg layout resolves through
///     <c>libexec/openjdk.jdk/Contents/Home</c>.
/// </summary>
public sealed class HomebrewJvmProvider(IEnumerable<string> prefixes) : IJvmProvider
{
    public string Name => "homebrew";

    private readonly string[] prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    public HomebrewJvmProvider() : this(ResolveDefaultPrefixes()) { }

    public IEnumerable<string> GetJavaHomes() => EnumerateKegs();

    private static IEnumerable<string> ResolveDefaultPrefixes()
    {
        var env = Environment.GetEnvironmentVariable("HOMEBREW_PREFIX");
        if (!string.IsNullOrWhiteSpace(env))
            yield return env;

        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) yield break;
        yield return "/opt/homebrew";
        yield return "/usr/local";
        yield return "/home/linuxbrew/.linuxbrew";
    }

    private IEnumerable<string> EnumerateKegs()
    {
        foreach (var prefix in prefixList)
        {
            var opt = Path.Combine(prefix, "opt");
            if (!Directory.Exists(opt))
                continue;

            foreach (var keg in Directory.EnumerateDirectories(opt, "openjdk*"))
                if (JavaHomeLayout.Probe(keg) is { } javaHome)
                    yield return javaHome;
        }
    }
}
