namespace JdkFind.Providers;

/// <summary>
///     Scans Nix profiles for the openjdk layout nixpkgs installs: the JDK image
///     sits at <c>lib/openjdk</c> inside a profile; the profile root itself is
///     probed too, for environments that expose a home directly. Profiles come
///     from <c>NIX_PROFILES</c> when set, otherwise the fixed defaults — the
///     per-user profile (<c>~/.nix-profile</c> and the newer
///     <c>~/.local/state/nix/profiles/profile</c>), home-manager's per-user
///     directory, the daemon's default profile and the NixOS system profile.
/// </summary>
public sealed class NixJvmProvider(IEnumerable<string> profiles) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "nix";

    private readonly string[] _profileList = [.. profiles.Where(p => !string.IsNullOrWhiteSpace(p))];

    /// <summary>Scans the default Nix profiles.</summary>
    public NixJvmProvider() : this(ResolveDefaultProfiles()) { }

    IEnumerable<string> IJvmProvider.GetJavaHomes()
    {
        foreach (var profile in _profileList)
            foreach (var candidate in new[] { profile, Path.Combine(profile, "lib", "openjdk") })
                if (JavaHomeLayout.Probe(candidate) is { } home)
                    yield return home;
    }

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
