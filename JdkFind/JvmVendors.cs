using System.Text.RegularExpressions;

namespace JdkFind;

/// <summary>
///     Matches raw vendor strings (<c>IMPLEMENTOR</c> / <c>java.vendor</c>) against the
///     known-vendor list (patterns adapted from Gradle) and provides display names.
///     Matching is a case-insensitive substring search; the first hit wins.
/// </summary>
public static class JvmVendors
{
    private static readonly (JvmVendor Vendor, Regex Pattern, string DisplayName)[] Known =
    [
        (JvmVendor.Adoptium, Pattern("temurin|adoptium|eclipse foundation"), "Eclipse Temurin"),
        (JvmVendor.AdoptOpenJdk, Pattern("aoj|adoptopenjdk"), "AdoptOpenJDK"),
        (JvmVendor.Amazon, Pattern("amazon|corretto"), "Amazon Corretto"),
        (JvmVendor.Apple, Pattern("apple"), "Apple"),
        (JvmVendor.Azul, Pattern("azul|zulu"), "Azul Zulu"),
        (JvmVendor.BellSoft, Pattern("bellsoft|liberica"), "BellSoft Liberica"),
        (JvmVendor.GraalVm, Pattern("graalvm|graal vm"), "GraalVM Community"),
        (JvmVendor.HewlettPackard, Pattern("hp|hewlett"), "HP"),
        (JvmVendor.Ibm, Pattern("ibm|semeru|international business machines corporation"), "IBM Semeru"),
        (JvmVendor.JetBrains, Pattern("jbr|jetbrains"), "JetBrains"),
        (JvmVendor.Microsoft, Pattern("microsoft"), "Microsoft Build of OpenJDK"),
        (JvmVendor.Oracle, Pattern("oracle"), "Oracle"),
        (JvmVendor.Sap, Pattern("sap"), "SAP SapMachine"),
        (JvmVendor.Tencent, Pattern("tencent|kona"), "Tencent Kona"),
    ];

    /// <summary>Parses a raw vendor string into a known vendor; unrecognized strings map to <see cref="JvmVendor.Unknown" />.</summary>
    public static JvmVendor Parse(string? vendor)
    {
        if (string.IsNullOrWhiteSpace(vendor))
            return JvmVendor.Unknown;

        foreach (var (known, pattern, _) in Known)
            if (pattern.IsMatch(vendor))
                return known;

        return JvmVendor.Unknown;
    }

    /// <summary>
    ///     The display name of a known vendor; unknown vendors fall back to their raw
    ///     string (or <c>Unknown</c> when that is missing too).
    /// </summary>
    public static string GetDisplayName(JvmVendor vendor, string? rawVendor = null)
    {
        foreach (var (known, _, displayName) in Known)
            if (known == vendor)
                return displayName;

        return string.IsNullOrWhiteSpace(rawVendor) ? "Unknown" : rawVendor;
    }

    private static Regex Pattern(string indicators) =>
        new(indicators, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}