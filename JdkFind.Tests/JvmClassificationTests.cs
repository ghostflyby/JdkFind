namespace JdkFind.Tests;

public class JvmClassificationTests
{
    [Theory]
    [InlineData("Amazon Corretto 21", null, false, JvmVendor.Amazon, JvmDistribution.Corretto)]
    [InlineData("Amazon.com Inc.", null, false, JvmVendor.Amazon, JvmDistribution.Corretto)]
    [InlineData("Eclipse Adoptium", null, false, JvmVendor.Adoptium, JvmDistribution.Temurin)]
    [InlineData("Temurin-21.0.5+11", null, false, JvmVendor.Adoptium, JvmDistribution.Temurin)]
    [InlineData("Azul Systems, Inc.", null, false, JvmVendor.Azul, JvmDistribution.Zulu)]
    [InlineData("BellSoft Liberica", null, false, JvmVendor.BellSoft, JvmDistribution.Liberica)]
    [InlineData("SapMachine", null, false, JvmVendor.Sap, JvmDistribution.SapMachine)]
    [InlineData("Microsoft Build of OpenJDK", null, false, JvmVendor.Microsoft, JvmDistribution.Microsoft)]
    [InlineData("JetBrains s.r.o.", null, false, JvmVendor.JetBrains, JvmDistribution.JetBrainsRuntime)]
    [InlineData("JBR-21.0.11+10-1163", null, false, JvmVendor.JetBrains, JvmDistribution.JetBrainsRuntime)]
    [InlineData("IBM Semeru Runtime Open Edition", null, false, JvmVendor.Ibm, JvmDistribution.Semeru)]
    [InlineData("Red Hat, Inc.", null, false, JvmVendor.RedHat, JvmDistribution.RedHatBuildOfOpenJdk)]
    [InlineData("Alibaba Dragonwell", null, false, JvmVendor.Alibaba, JvmDistribution.Dragonwell)]
    [InlineData("Huawei", null, false, JvmVendor.Huawei, JvmDistribution.Bisheng)]
    [InlineData("Tencent Kona 21", null, false, JvmVendor.Tencent, JvmDistribution.Kona)]
    [InlineData("Mandrel by Red Hat", null, false, JvmVendor.RedHat, JvmDistribution.Mandrel)]
    [InlineData("GraalVM Community", null, false, JvmVendor.Oracle, JvmDistribution.GraalVmCommunity)]
    [InlineData("TravaOpenJDK", null, false, JvmVendor.Unknown, JvmDistribution.Trava)]
    [InlineData("OJDKBuild", null, false, JvmVendor.Unknown, JvmDistribution.OjdkBuild)]
    [InlineData("OpenLogic OpenJDK", null, false, JvmVendor.OpenLogic, JvmDistribution.OpenLogic)]
    [InlineData("Gluon GraalVM", null, false, JvmVendor.Oracle, JvmDistribution.GluonGraalVm)]
    // A bare "Gluon" carries no "graalvm" indicator, so the vendor axis leaves
    // it Unknown while the distribution axis still names GluonGraalVm — Gluon
    // is not a vendor of its own (JvmVendor has no Gluon member).
    [InlineData("Gluon", null, false, JvmVendor.Unknown, JvmDistribution.GluonGraalVm)]
    [InlineData("Eliya JDK", null, false, JvmVendor.Asymm, JvmDistribution.Eliya)]
    public void Detect_MapsVendorAndDistributionIndependently(
        string vendorRaw,
        string? implementorVersion,
        bool graalVmRelease,
        JvmVendor expectedVendor,
        JvmDistribution expectedDistribution)
    {
        Assert.Equal(expectedVendor, JvmIdentity.DetectVendor(vendorRaw));
        Assert.Equal(
            expectedDistribution,
            JvmIdentity.DetectDistribution(implementorVersion, vendorRaw, graalVmRelease));
    }

    // The two axes read disjoint inputs: the vendor axis only ever sees the raw
    // vendor string, while the distribution axis prefers IMPLEMENTOR_VERSION,
    // falls back to the raw vendor string, and consults the GRAALVM_VERSION
    // release key only when neither identity string matched. Each row pins one
    // combination so the axes cannot silently re-couple.
    [Theory]
    // IMPLEMENTOR_VERSION tells Oracle's builds apart: same vendor, two
    // distributions.
    [InlineData("Oracle GraalVM 21.0.2+13.1", "Oracle Corporation", false, JvmVendor.Oracle, JvmDistribution.OracleGraalVm)]
    [InlineData("21.0.5+9-LTS", "Oracle Corporation", false, JvmVendor.Oracle, JvmDistribution.OracleOpenJdk)]
    // IMPLEMENTOR_VERSION outranks the raw vendor string on the distribution
    // axis, while the vendor axis still reads the raw vendor string only.
    [InlineData("Temurin-21.0.5+11", "Oracle Corporation", false, JvmVendor.Oracle, JvmDistribution.Temurin)]
    // With no IMPLEMENTOR_VERSION the raw vendor string is the distribution
    // fallback.
    [InlineData(null, "Azul Systems, Inc.", false, JvmVendor.Azul, JvmDistribution.Zulu)]
    // A raw vendor string the vendor axis cannot name does not stop the
    // distribution axis: with only IMPLEMENTOR_VERSION present the vendor is
    // Unknown while the distribution is still recognized.
    [InlineData("GraalVM Community 22", null, false, JvmVendor.Unknown, JvmDistribution.GraalVmCommunity)]
    [InlineData("trava 11.0.15", null, false, JvmVendor.Unknown, JvmDistribution.Trava)]
    // The same community string in the vendor position maps to Oracle — the
    // GraalVM family's upstream organization — plus GraalVmCommunity.
    [InlineData(null, "GraalVM Community 22", false, JvmVendor.Oracle, JvmDistribution.GraalVmCommunity)]
    // The GRAALVM_VERSION release key outranks the vendor string: real Oracle
    // GraalVM files carry IMPLEMENTOR="Oracle Corporation" with no branded
    // IMPLEMENTOR_VERSION, so the key is what tells those builds apart.
    [InlineData(null, "Oracle Corporation", true, JvmVendor.Oracle, JvmDistribution.OracleGraalVm)]
    [InlineData(null, null, true, JvmVendor.Unknown, JvmDistribution.OracleGraalVm)]
    [InlineData(null, "Homebrew", true, JvmVendor.Unknown, JvmDistribution.OracleGraalVm)]
    // Nothing to read on either axis and no release hint.
    [InlineData(null, null, false, JvmVendor.Unknown, JvmDistribution.Unknown)]
    public void Detect_ClassifiesEachAxisFromItsOwnInputs(
        string? implementorVersion,
        string? vendorRaw,
        bool graalVmRelease,
        JvmVendor expectedVendor,
        JvmDistribution expectedDistribution)
    {
        Assert.Equal(expectedVendor, JvmIdentity.DetectVendor(vendorRaw));
        Assert.Equal(
            expectedDistribution,
            JvmIdentity.DetectDistribution(implementorVersion, vendorRaw, graalVmRelease));
    }

    [Fact]
    public void DetectVendor_HitsKnownIndicators_CaseInsensitively()
    {
        Assert.Equal(JvmVendor.Oracle, JvmIdentity.DetectVendor("Oracle Corporation"));

        // The IBM indicators match regardless of casing.
        Assert.Equal(JvmVendor.Ibm, JvmIdentity.DetectVendor("SEMERU"));
        Assert.Equal(JvmVendor.Ibm, JvmIdentity.DetectVendor("International Business Machines"));
    }

    [Fact]
    public void DetectVendor_UnmatchedRaw_YieldsUnknown()
    {
        Assert.Equal(JvmVendor.Unknown, JvmIdentity.DetectVendor("Homebrew"));
        Assert.Equal(JvmVendor.Unknown, JvmIdentity.DetectVendor(string.Empty));
        Assert.Equal(JvmVendor.Unknown, JvmIdentity.DetectVendor(null));
    }

    [Fact]
    public void DetectDistribution_OracleNeedsHints_ToTellItsDistributionsApart()
    {
        // Oracle reports "Oracle Corporation" as IMPLEMENTOR for all of its
        // builds — only secondary hints tell its distributions apart.
        Assert.Equal(JvmVendor.Oracle, JvmIdentity.DetectVendor("Oracle Corporation"));
        Assert.Equal(
            JvmDistribution.OracleOpenJdk, JvmIdentity.DetectDistribution(null, "Oracle Corporation"));

        Assert.Equal(
            JvmDistribution.OracleGraalVm,
            JvmIdentity.DetectDistribution("Oracle GraalVM 21.0.5+1.1", "Oracle Corporation"));

        // The GRAALVM_VERSION release key outranks the vendor string — real
        // Oracle GraalVM releases carry no branded IMPLEMENTOR_VERSION, so
        // "Oracle Corporation" plus the key is an Oracle GraalVM build.
        Assert.Equal(
            JvmDistribution.OracleGraalVm,
            JvmIdentity.DetectDistribution(null, "Oracle Corporation", graalVmRelease: true));
        Assert.Equal(
            JvmDistribution.OracleGraalVm,
            JvmIdentity.DetectDistribution(null, "GraalVM", graalVmRelease: true));
    }

    [Fact]
    public void Detect_ProbeVendorFallback_StillClassifies()
    {
        // When the release file lacks IMPLEMENTOR, the probed java.vendor is the fallback.
        Assert.Equal(JvmVendor.Azul, JvmIdentity.DetectVendor("Azul Systems, Inc."));
        Assert.Equal(JvmDistribution.Zulu, JvmIdentity.DetectDistribution(null, "Azul Systems, Inc."));
    }

    [Fact]
    public void Detect_UnknownRaw_YieldsUnknownPair()
    {
        Assert.Equal(JvmVendor.Unknown, JvmIdentity.DetectVendor("Homebrew"));
        Assert.Equal(JvmDistribution.Unknown, JvmIdentity.DetectDistribution(null, "Homebrew"));
    }
}
