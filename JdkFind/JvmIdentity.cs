namespace JdkFind;

/// <summary>
///     Maps raw vendor strings (IMPLEMENTOR / java.vendor) plus optional release-file
///     hints to the normalized vendor and foojay-style distribution. GraalVM family
///     needs the hints: both community and Oracle builds report "Oracle Corporation"
///     as IMPLEMENTOR.
/// </summary>
internal static class JvmIdentity
{
    internal static (JvmVendor Vendor, JvmDistribution Distribution) Classify(
        string? vendorRaw, string? implementorVersion = null, bool graalVmRelease = false)
    {
        var raw = vendorRaw ?? string.Empty;

        if (Matches(raw, "amazon|corretto"))
            return (JvmVendor.Amazon, JvmDistribution.Corretto);
        if (Matches(raw, "temurin|adoptium|eclipse foundation"))
            return (JvmVendor.Adoptium, JvmDistribution.Temurin);
        if (Matches(raw, "adoptopenjdk|aoj"))
            return (JvmVendor.AdoptOpenJdk, JvmDistribution.AdoptOpenJdk);
        if (Matches(raw, "azul|zulu"))
            return (JvmVendor.Azul, JvmDistribution.Zulu);
        if (Matches(raw, "liberica"))
            return (JvmVendor.BellSoft, JvmDistribution.Liberica);
        if (Matches(raw, "sap"))
            return (JvmVendor.Sap, JvmDistribution.SapMachine);
        if (Matches(raw, "microsoft"))
            return (JvmVendor.Microsoft, JvmDistribution.Microsoft);
        if (Matches(raw, "jetbrains|jbr"))
            return (JvmVendor.JetBrains, JvmDistribution.JetBrainsRuntime);
        if (Matches(raw, "mandrel"))
            return (JvmVendor.RedHat, JvmDistribution.Mandrel);
        if (Matches(raw, "semeru"))
            return (JvmVendor.Ibm, JvmDistribution.Semeru);
        if (Matches(raw, "red ?hat"))
            return (JvmVendor.RedHat, JvmDistribution.RedHatBuildOfOpenJdk);
        if (Matches(raw, "dragonwell|alibaba"))
            return (JvmVendor.Alibaba, JvmDistribution.Dragonwell);
        if (Matches(raw, "bisheng|huawei"))
            return (JvmVendor.Huawei, JvmDistribution.Bisheng);
        if (Matches(raw, "kona|tencent"))
            return (JvmVendor.Tencent, JvmDistribution.Kona);
        if (Matches(raw, "trava"))
            return (JvmVendor.Trava, JvmDistribution.Trava);

        // The GraalVM family: community builds say "GraalVM", Oracle builds say
        // "Oracle Corporation" but ship a GRAALVM_VERSION release entry or an
        // Oracle GraalVM IMPLEMENTOR_VERSION.
        if (Matches(raw, "graalvm|graal vm"))
            return (JvmVendor.GraalVm, JvmDistribution.GraalVmCommunity);
        if (graalVmRelease || Contains(implementorVersion, "graalvm"))
            return OracleGraalVm(raw);

        if (Matches(raw, "oracle"))
            return (JvmVendor.Oracle, JvmDistribution.OracleOpenJdk);

        return VendorOnly(raw);
    }

    private static (JvmVendor, JvmDistribution) OracleGraalVm(string raw) =>
        (Contains(raw, "oracle") ? JvmVendor.Oracle : JvmVendor.GraalVm, JvmDistribution.OracleGraalVm);

    /// <summary>Vendors recognized only through their name, with no distinct distribution.</summary>
    private static (JvmVendor, JvmDistribution) VendorOnly(string raw)
    {
        if (Matches(raw, "apple"))
            return (JvmVendor.Apple, JvmDistribution.Unknown);
        if (Matches(raw, "hp|hewlett"))
            return (JvmVendor.HewlettPackard, JvmDistribution.Unknown);
        return (JvmVendor.Unknown, JvmDistribution.Unknown);
    }

    private static bool Matches(string raw, string pattern) =>
        !string.IsNullOrWhiteSpace(raw) &&
        System.Text.RegularExpressions.Regex.IsMatch(raw, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool Contains(string? value, string text) =>
        !string.IsNullOrEmpty(value) && value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
