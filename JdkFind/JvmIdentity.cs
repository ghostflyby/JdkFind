using System.Text.RegularExpressions;

namespace JdkFind;

/// <summary>
///     Detects the normalized vendor and the foojay-style distribution of an
///     installation as two independent axes. Both read the installation's own
///     identity strings — the release file's IMPLEMENTOR / IMPLEMENTOR_VERSION
///     plus the GRAALVM_VERSION hint, falling back to the java.vendor system
///     property captured by the runtime probe. The vendor applies Gradle's
///     known-vendor indicator set to the raw IMPLEMENTOR / java.vendor value,
///     while the distribution runs its own pattern set with IMPLEMENTOR_VERSION
///     taking precedence over the raw vendor string. Matching is a
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

    [GeneratedRegex("graalvm", RegexOptions.IgnoreCase)]
    private static partial Regex GraalVmPattern { get; }

    /// <summary>
    ///     Maps the raw IMPLEMENTOR / java.vendor string onto the normalized
    ///     upstream organization using Gradle's known-vendor indicator set,
    ///     plus the "graalvm" indicator: any string printing "graalvm" — the
    ///     community and Oracle GraalVM editions alike — reports Oracle, the
    ///     GraalVM family's upstream organization (foojay alignment). A bare
    ///     vendor name without an indicator (e.g. "Gluon") stays Unknown.
    /// </summary>
    internal static JvmVendor DetectVendor(string? vendorRaw)
    {
        var raw = vendorRaw ?? string.Empty;

        if (AmazonPattern.IsMatch(raw))
            return JvmVendor.Amazon;
        if (AdoptiumPattern.IsMatch(raw))
            return JvmVendor.Adoptium;
        if (AdoptOpenJdkPattern.IsMatch(raw))
            return JvmVendor.AdoptOpenJdk;
        if (AzulPattern.IsMatch(raw))
            return JvmVendor.Azul;
        if (BellSoftPattern.IsMatch(raw))
            return JvmVendor.BellSoft;
        if (SapPattern.IsMatch(raw))
            return JvmVendor.Sap;
        if (MicrosoftPattern.IsMatch(raw))
            return JvmVendor.Microsoft;
        if (JetBrainsPattern.IsMatch(raw))
            return JvmVendor.JetBrains;
        if (SemeruPattern.IsMatch(raw))
            return JvmVendor.Ibm;
        if (RedHatPattern.IsMatch(raw))
            return JvmVendor.RedHat;
        if (KonaPattern.IsMatch(raw))
            return JvmVendor.Tencent;
        if (DragonwellPattern.IsMatch(raw))
            return JvmVendor.Alibaba;
        if (BishengPattern.IsMatch(raw))
            return JvmVendor.Huawei;
        if (OpenLogicPattern.IsMatch(raw))
            return JvmVendor.OpenLogic;
        if (EliyaPattern.IsMatch(raw))
            return JvmVendor.Asymm;
        if (OraclePattern.IsMatch(raw))
            return JvmVendor.Oracle;
        if (ApplePattern.IsMatch(raw))
            return JvmVendor.Apple;
        if (HewlettPackardPattern.IsMatch(raw))
            return JvmVendor.HewlettPackard;
        if (GraalVmPattern.IsMatch(raw))
            return JvmVendor.Oracle;

        return JvmVendor.Unknown;
    }

    /// <summary>
    ///     Maps the installation onto its foojay-style distribution. The release
    ///     file's IMPLEMENTOR_VERSION is the sharpest hint and wins over the raw
    ///     vendor string. The GRAALVM_VERSION release key sits between the two:
    ///     real Oracle GraalVM files carry IMPLEMENTOR="Oracle Corporation" with
    ///     no branded IMPLEMENTOR_VERSION, so the key must outrank the plain
    ///     Oracle vendor name — but it must not outrank vendor strings that name
    ///     a GraalVM family member (community, Mandrel, Gluon) directly.
    /// </summary>
    internal static JvmDistribution DetectDistribution(
        string? implementorVersion, string? vendorRaw, bool graalVmRelease = false)
    {
        if (!string.IsNullOrEmpty(implementorVersion))
        {
            var fromImplementorVersion = MatchDistribution(implementorVersion);
            if (fromImplementorVersion != JvmDistribution.Unknown)
                return fromImplementorVersion;
        }

        if (!string.IsNullOrEmpty(vendorRaw))
        {
            var fromFamily = MatchGraalVmFamily(vendorRaw);
            if (fromFamily != JvmDistribution.Unknown)
                return fromFamily;
        }

        if (graalVmRelease)
            return JvmDistribution.OracleGraalVm;

        if (!string.IsNullOrEmpty(vendorRaw))
        {
            var fromVendor = MatchDistribution(vendorRaw);
            if (fromVendor != JvmDistribution.Unknown)
                return fromVendor;
        }

        return JvmDistribution.Unknown;
    }

    /// <summary>
    ///     Runs just the GraalVM-family head of the pattern set — the specific
    ///     names a GraalVM installation may report instead of hiding behind the
    ///     plain Oracle vendor string. Returns Unknown when nothing matched.
    /// </summary>
    private static JvmDistribution MatchGraalVmFamily(string raw)
    {
        if (GraalVmCommunityPattern.IsMatch(raw))
            return JvmDistribution.GraalVmCommunity;
        if (OracleGraalVmPattern.IsMatch(raw))
            return JvmDistribution.OracleGraalVm;
        if (MandrelPattern.IsMatch(raw))
            return JvmDistribution.Mandrel;
        if (GluonPattern.IsMatch(raw))
            return JvmDistribution.GluonGraalVm;

        return JvmDistribution.Unknown;
    }

    /// <summary>
    ///     Runs the distribution pattern set: the GraalVM family before Oracle so
    ///     a plain "Oracle" vendor name cannot mask it, and Mandrel before Red Hat
    ///     for the same reason. Returns Unknown when nothing matched.
    /// </summary>
    private static JvmDistribution MatchDistribution(string raw)
    {
        var family = MatchGraalVmFamily(raw);
        if (family != JvmDistribution.Unknown)
            return family;

        if (AmazonPattern.IsMatch(raw))
            return JvmDistribution.Corretto;
        if (AdoptiumPattern.IsMatch(raw))
            return JvmDistribution.Temurin;
        if (AdoptOpenJdkPattern.IsMatch(raw))
            return JvmDistribution.AdoptOpenJdk;
        if (AzulPattern.IsMatch(raw))
            return JvmDistribution.Zulu;
        if (BellSoftPattern.IsMatch(raw))
            return JvmDistribution.Liberica;
        if (SapPattern.IsMatch(raw))
            return JvmDistribution.SapMachine;
        if (MicrosoftPattern.IsMatch(raw))
            return JvmDistribution.Microsoft;
        if (JetBrainsPattern.IsMatch(raw))
            return JvmDistribution.JetBrainsRuntime;
        if (SemeruPattern.IsMatch(raw))
            return JvmDistribution.Semeru;
        if (RedHatPattern.IsMatch(raw))
            return JvmDistribution.RedHatBuildOfOpenJdk;
        if (KonaPattern.IsMatch(raw))
            return JvmDistribution.Kona;
        if (DragonwellPattern.IsMatch(raw))
            return JvmDistribution.Dragonwell;
        if (BishengPattern.IsMatch(raw))
            return JvmDistribution.Bisheng;
        if (TravaPattern.IsMatch(raw))
            return JvmDistribution.Trava;
        if (OjdkBuildPattern.IsMatch(raw))
            return JvmDistribution.OjdkBuild;
        if (OpenLogicPattern.IsMatch(raw))
            return JvmDistribution.OpenLogic;
        if (EliyaPattern.IsMatch(raw))
            return JvmDistribution.Eliya;
        if (OraclePattern.IsMatch(raw))
            return JvmDistribution.OracleOpenJdk;

        return JvmDistribution.Unknown;
    }
}
