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
internal static class JvmIdentity
{
    private static readonly Regex AmazonPattern = Create("amazon|corretto");
    private static readonly Regex AdoptiumPattern = Create("temurin|adoptium|eclipse foundation");
    private static readonly Regex AdoptOpenJdkPattern = Create("adoptopenjdk|aoj");
    private static readonly Regex AzulPattern = Create("azul|zulu");
    private static readonly Regex BellSoftPattern = Create("bellsoft|liberica");
    private static readonly Regex SapPattern = Create("sap");
    private static readonly Regex MicrosoftPattern = Create("microsoft");
    private static readonly Regex JetBrainsPattern = Create("jetbrains|jbr");
    private static readonly Regex MandrelPattern = Create("mandrel");
    private static readonly Regex SemeruPattern = Create("semeru|international business machines");
    private static readonly Regex RedHatPattern = Create("red ?hat");
    private static readonly Regex DragonwellPattern = Create("dragonwell|alibaba");
    private static readonly Regex BishengPattern = Create("bisheng|huawei");
    private static readonly Regex KonaPattern = Create("kona|tencent");
    private static readonly Regex TravaPattern = Create("trava");
    private static readonly Regex GraalVmCommunityPattern = Create("graalvm community|graal vm community");
    private static readonly Regex OracleGraalVmPattern = Create("oracle graalvm");
    private static readonly Regex GraalVmPattern = Create("graalvm|graal vm");
    private static readonly Regex GluonPattern = Create("gluon");
    private static readonly Regex OjdkBuildPattern = Create("ojdkbuild");
    private static readonly Regex OpenLogicPattern = Create("openlogic");
    private static readonly Regex EliyaPattern = Create("eliya");
    private static readonly Regex OraclePattern = Create("oracle");
    private static readonly Regex ApplePattern = Create("apple");
    private static readonly Regex HewlettPackardPattern = Create("hp|hewlett");

    private static bool Contains(string? value, string text) =>
        !string.IsNullOrEmpty(value) && value.Contains(text, StringComparison.OrdinalIgnoreCase);

    internal static (JvmVendor Vendor, JvmDistribution Distribution) Classify(
        string? vendorRaw, string? implementorVersion = null, bool graalVmRelease = false)
    {
        var raw = vendorRaw ?? string.Empty;

        // The GraalVM family first: community names itself, while Oracle builds hide
        // behind "Oracle Corporation" and need the release-file hints.
        if (GraalVmCommunityPattern.IsMatch(raw))
            return (JvmVendor.GraalVm, JvmDistribution.GraalVmCommunity);
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
            return (JvmVendor.Trava, JvmDistribution.Trava);
        if (OjdkBuildPattern.IsMatch(raw))
            return (JvmVendor.Community, JvmDistribution.OjdkBuild);
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

    private static Regex Create(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
