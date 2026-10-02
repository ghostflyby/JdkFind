using JdkFind.Providers;

namespace JdkFind;

/// <summary>Options controlling JVM discovery.</summary>
public sealed class JdkFindOptions
{
    /// <summary>Providers consulted in order; duplicates merge into one Jvm listing every source.</summary>
    public IList<IJvmProvider> Providers { get; init; } = CreateDefaultProviders();

    /// <summary>Collapse candidates that resolve to the same physical directory. Default is true.</summary>
    public bool DeduplicateHomes { get; init; } = true;

    /// <summary>The built-in provider set for the current platform, in priority order.</summary>
    public static List<IJvmProvider> CreateDefaultProviders() =>
    [
        new JavaHomeJvmProvider(),
        new PathJvmProvider(),
        new MacOsJvmProvider(),
        new LinuxJvmProvider(),
        new HomebrewJvmProvider(),
        new WindowsProgramFilesJvmProvider(),
        new WindowsRegistryJvmProvider(),
        new IntelliJJvmProvider(),
        new SdkmanJvmProvider(),
        new GradleJvmProvider(),
        new JabbaJvmProvider(),
        new ScoopJvmProvider(),
    ];
}
