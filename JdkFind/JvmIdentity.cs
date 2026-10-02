using System.Text.RegularExpressions;

namespace JdkFind;

/// <summary>
///     Classifies installations into the normalized vendor and the foojay-style
///     distribution. Acquisition sources follow Gradle's metadata detector: the
///     target's own identity strings — the release file's IMPLEMENTOR /
///     IMPLEMENTOR_VERSION plus the GRAALVM_VERSION hint, falling back to the
///     java.vendor system property captured by the runtime probe. Matching is a
///     case-insensitive substring search with the more specific families first
///     (the GraalVM community before Oracle, Mandrel before Red Hat).
/// </summary>
internal static partial class JvmIdentity
{
    [GeneratedRegex("amazon|corretto", RegexOptions.IgnoreCase)]
    private static partial Regex AmazonPattern { get; }

    [GeneratedRegex("temurin|adoptium|eclipse foundation", RegexOptions.IgnoreCase)]
    private static partial Regex AdoptiumPattern { get; }

    [GeneratedRegex("adoptopenjdk|aoj", RegexOptions.IgnoreCase)]
    private static partial Regex AdoptOpenJdkPattern { get; }

    [GeneratedRegex("azul|zulu", RegexOptions.IgnoreCase)]
    private static partial Regex AzulPattern { get; }

    [GeneratedRegex("bellsoft|liberica", RegexOptions.IgnoreCase)]
    private static partial Regex BellSoftPattern { get; }

    [GeneratedRegex("sap", RegexOptions.IgnoreCase)]
    private static partial Regex SapPattern { get; }

    [GeneratedRegex("microsoft", RegexOptions.IgnoreCase)]
    private static partial Regex MicrosoftPattern { get; }

    [GeneratedRegex("jetbrains|jbr", RegexOptions.IgnoreCase)]
    private static partial Regex JetBrainsPattern { get; }

    [GeneratedRegex("mandrel", RegexOptions.IgnoreCase)]
    private static partial Regex MandrelPattern { get; }

    [GeneratedRegex("semeru|international business machines", RegexOptions.IgnoreCase)]
    private static partial Regex SemeruPattern { get; }

    [GeneratedRegex("red ?hat", RegexOptions.IgnoreCase)]
    private static partial Regex RedHatPattern { get; }

    [GeneratedRegex("dragonwell|alibaba", RegexOptions.IgnoreCase)]
    private static partial Regex DragonwellPattern { get; }

    [GeneratedRegex("bisheng|huawei", RegexOptions.IgnoreCase)]
    private static partial Regex BishengPattern { get; }

    [GeneratedRegex("kona|tencent", RegexOptions.IgnoreCase)]
    private static partial Regex KonaPattern { get; }

    [GeneratedRegex("trava", RegexOptions.IgnoreCase)]
    private static partial Regex TravaPattern { get; }

    [GeneratedRegex("graalvm community|graal vm community", RegexOptions.IgnoreCase)]
    private static partial Regex GraalVmCommunityPattern { get; }

    [GeneratedRegex("oracle graalvm", RegexOptions.IgnoreCase)]
    private static partial Regex OracleGraalVmPattern { get; }

    [GeneratedRegex("gluon", RegexOptions.IgnoreCase)]
    private static partial Regex GluonPattern { get; }

    [GeneratedRegex("ojdkbuild", RegexOptions.IgnoreCase)]
    private static partial Regex OjdkBuildPattern { get; }

    [GeneratedRegex("openlogic", RegexOptions.IgnoreCase)]
    private static partial Regex OpenLogicPattern { get; }

    [GeneratedRegex("eliya", RegexOptions.IgnoreCase)]
    private static partial Regex EliyaPattern { get; }

    [GeneratedRegex("oracle", RegexOptions.IgnoreCase)]
    private static partial Regex OraclePattern { get; }

    [GeneratedRegex("apple", RegexOptions.IgnoreCase)]
    private static partial Regex ApplePattern { get; }

    [GeneratedRegex("hp|hewlett", RegexOptions.IgnoreCase)]
    private static partial Regex HewlettPackardPattern { get; }

    internal static (JvmVendor Vendor, JvmDistribution Distribution) Classify(
        string? vendorRaw, string? implementorVersion = null, bool graalVmRelease = false)
    {
        var raw = vendorRaw ?? string.Empty;

        // The GraalVM family first: community builds name themselves, while Oracle builds hide
        // behind "Oracle Corporation" and need the release-file hints.
        if (GraalVmCommunityPattern.IsMatch(raw))
            return (JvmVendor.Oracle, JvmDistribution.GraalVmCommunity);
        if (graalVmRelease || Contains(implementorVersion, "graalvm") || OracleGraalVmPattern.IsMatch(raw))
            return (JvmVendor.Oracle, JvmDistribution.OracleGraalVm);
        if (MandrelPattern.IsMatch(raw))
            return (JvmVendor.RedHat, JvmDistribution.Mandrel);
        if (GluonPattern.IsMatch(raw))
            return (JvmVendor.Gluon, JvmDistribution.GluonGraalVm);

        if (AmazonPattern.IsMatch(raw))
            return (JvmVendor.Amazon, JvmDistribution.Corretto);
        if (AdoptiumPattern.IsMatch(raw))
            return (JvmVendor.Adoptium, JvmDistribution.Temurin);
        if (AdoptOpenJdkPattern.IsMatch(raw))
            return (JvmVendor.AdoptOpenJdk, JvmDistribution.AdoptOpenJdk);
        if (AzulPattern.IsMatch(raw))
            return (JvmVendor.Azul, JvmDistribution.Zulu);
        if (BellSoftPattern.IsMatch(raw))
            return (JvmVendor.BellSoft, JvmDistribution.Liberica);
        if (SapPattern.IsMatch(raw))
            return (JvmVendor.Sap, JvmDistribution.SapMachine);
        if (MicrosoftPattern.IsMatch(raw))
            return (JvmVendor.Microsoft, JvmDistribution.Microsoft);
        if (JetBrainsPattern.IsMatch(raw))
            return (JvmVendor.JetBrains, JvmDistribution.JetBrainsRuntime);
        if (SemeruPattern.IsMatch(raw))
            return (JvmVendor.Ibm, JvmDistribution.Semeru);
        if (RedHatPattern.IsMatch(raw))
            return (JvmVendor.RedHat, JvmDistribution.RedHatBuildOfOpenJdk);
        if (KonaPattern.IsMatch(raw))
            return (JvmVendor.Tencent, JvmDistribution.Kona);
        if (DragonwellPattern.IsMatch(raw))
            return (JvmVendor.Alibaba, JvmDistribution.Dragonwell);
        if (BishengPattern.IsMatch(raw))
            return (JvmVendor.Huawei, JvmDistribution.Bisheng);
        if (TravaPattern.IsMatch(raw))
            return (JvmVendor.Unknown, JvmDistribution.Trava);
        if (OjdkBuildPattern.IsMatch(raw))
            return (JvmVendor.Unknown, JvmDistribution.OjdkBuild);
        if (OpenLogicPattern.IsMatch(raw))
            return (JvmVendor.OpenLogic, JvmDistribution.OpenLogic);
        if (EliyaPattern.IsMatch(raw))
            return (JvmVendor.Asymm, JvmDistribution.Eliya);
        if (OraclePattern.IsMatch(raw))
            return (JvmVendor.Oracle, JvmDistribution.OracleOpenJdk);

        return VendorOnly(raw);
    }

    /// <summary>Vendors recognized only through their name, with no distinct distribution.</summary>
    private static (JvmVendor, JvmDistribution) VendorOnly(string raw)
    {
        if (ApplePattern.IsMatch(raw))
            return (JvmVendor.Apple, JvmDistribution.Unknown);
        if (HewlettPackardPattern.IsMatch(raw))
            return (JvmVendor.HewlettPackard, JvmDistribution.Unknown);
        return (JvmVendor.Unknown, JvmDistribution.Unknown);
    }

    private static bool Contains(string? value, string text) =>
        !string.IsNullOrEmpty(value) && value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
