namespace JdkFind.Providers;

/// <summary>
///     Scans the openjdk layout nixpkgs installs into Nix profiles: the JDK
///     image sits at <c>lib/openjdk</c> inside each profile, so every profile's
///     <c>lib</c> directory is the prefix whose children are the candidates (a
///     profile root itself carries no <c>release</c> file, so the root is never
///     a home). Profiles come from <c>NIX_PROFILES</c> when set, otherwise the
///     fixed defaults — the per-user profile (<c>~/.nix-profile</c> and the
///     newer <c>~/.local/state/nix/profiles/profile</c>), home-manager's
///     per-user directory, the daemon's default profile and the NixOS system
///     profile.
/// </summary>
public sealed class NixJvmProvider(IEnumerable<string> profiles) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "nix";

    private readonly string[] _profileList = [.. profiles.Where(p => !string.IsNullOrWhiteSpace(p))];

    /// <summary>Scans the default Nix profiles.</summary>
    public NixJvmProvider() : this(ResolveDefaultProfiles()) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() =>
        _profileList.Select(profile => Path.Combine(profile, "lib"));

    private static IEnumerable<string> ResolveDefaultProfiles()
    {
        var nixProfiles = Environment.GetEnvironmentVariable("NIX_PROFILES");
        if (!string.IsNullOrWhiteSpace(nixProfiles))
            return nixProfiles.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (OperatingSystem.IsWindows())
            return [];

        var candidates = new List<string>();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
        {
            candidates.Add(Path.Combine(profile, ".nix-profile"));
            candidates.Add(Path.Combine(profile, ".local", "state", "nix", "profiles", "profile"));

            var userName = Environment.UserName;
            if (!string.IsNullOrEmpty(userName))
                candidates.Add(Path.Combine("/etc/profiles/per-user", userName));
        }

        candidates.Add("/nix/var/nix/profiles/default");
        candidates.Add("/run/current-system/sw");
        return candidates;
    }
}
