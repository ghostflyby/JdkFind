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
    [InlineData("GraalVM Community", null, false, JvmVendor.GraalVm, JvmDistribution.GraalVmCommunity)]
    [InlineData("TravaOpenJDK", null, false, JvmVendor.Trava, JvmDistribution.Trava)]
    public void Classify_MapsDistributionsFromTheImplementor(
        string vendorRaw,
        string? implementorVersion,
        bool graalVmRelease,
        JvmVendor expectedVendor,
        JvmDistribution expectedDistribution)
    {
        var (vendor, distribution) = JvmIdentity.Classify(vendorRaw, implementorVersion, graalVmRelease);

        Assert.Equal(expectedVendor, vendor);
        Assert.Equal(expectedDistribution, distribution);
    }

    [Fact]
    public void Classify_OracleNeedsHints_ToTellItsDistributionsApart()
    {
        // Oracle 的 IMPLEMENTOR 一律是 "Oracle Corporation"，只能靠辅助线索区分发行版。
        var (plainVendor, plainDistribution) = JvmIdentity.Classify("Oracle Corporation");
        Assert.Equal(JvmVendor.Oracle, plainVendor);
        Assert.Equal(JvmDistribution.OracleOpenJdk, plainDistribution);

        var (graalVendor, graalDistribution) = JvmIdentity.Classify(
            "Oracle Corporation", "Oracle GraalVM 21.0.5+1.1");
        Assert.Equal(JvmVendor.Oracle, graalVendor);
        Assert.Equal(JvmDistribution.OracleGraalVm, graalDistribution);

        var (graalKeyVendor, graalKeyDistribution) = JvmIdentity.Classify(
            "Oracle Corporation", null, graalVmRelease: true);
        Assert.Equal(JvmDistribution.OracleGraalVm, graalKeyDistribution);
    }

    [Fact]
    public void Classify_ProbeVendorFallback_StillClassifies()
    {
        // release 文件缺失 IMPLEMENTOR 时，用探测到的 java.vendor 兜底。
        var (vendor, distribution) = JvmIdentity.Classify("Azul Systems, Inc.");

        Assert.Equal(JvmVendor.Azul, vendor);
        Assert.Equal(JvmDistribution.Zulu, distribution);
    }

    [Fact]
    public void Classify_UnknownRaw_YieldsUnknownPair() =>
        Assert.Equal(
            (JvmVendor.Unknown, JvmDistribution.Unknown),
            JvmIdentity.Classify("Homebrew"));
}
