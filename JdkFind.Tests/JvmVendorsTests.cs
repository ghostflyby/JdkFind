namespace JdkFind.Tests;

public class JvmVendorsTests
{
    [Theory]
    [InlineData("Azul Systems, Inc.", JvmVendor.Azul, "Azul Zulu")]
    [InlineData("Eclipse Adoptium", JvmVendor.Adoptium, "Eclipse Temurin")]
    [InlineData("Temurin-21.0.5+11", JvmVendor.Adoptium, "Eclipse Temurin")]
    [InlineData("JetBrains s.r.o.", JvmVendor.JetBrains, "JetBrains")]
    [InlineData("JBR-21.0.11+10-1163", JvmVendor.JetBrains, "JetBrains")]
    [InlineData("Oracle Corporation", JvmVendor.Oracle, "Oracle")]
    [InlineData("Amazon Corretto 21", JvmVendor.Amazon, "Amazon Corretto")]
    [InlineData("BellSoft Liberica 21", JvmVendor.BellSoft, "BellSoft Liberica")]
    [InlineData("SapMachine 21", JvmVendor.Sap, "SAP SapMachine")]
    [InlineData("IBM Semeru Runtime Open Edition", JvmVendor.Ibm, "IBM Semeru")]
    [InlineData("Tencent Kona 21", JvmVendor.Tencent, "Tencent Kona")]
    [InlineData("Homebrew", JvmVendor.Unknown, "Homebrew")]
    public void Parse_MatchesKnownVendorsCaseInsensitively(string raw, JvmVendor expected, string displayName)
    {
        var vendor = JvmVendors.Parse(raw);

        Assert.Equal(expected, vendor);
        Assert.Equal(displayName, JvmVendors.GetDisplayName(vendor, raw));
    }

    [Fact]
    public void Parse_NullOrEmpty_IsUnknown()
    {
        Assert.Equal(JvmVendor.Unknown, JvmVendors.Parse(null));
        Assert.Equal(JvmVendor.Unknown, JvmVendors.Parse(string.Empty));
        Assert.Equal("Unknown", JvmVendors.GetDisplayName(JvmVendor.Unknown));
    }
}
