namespace JdkFind.Tests;

public class JavaHomeLayoutTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Probe_AcceptsPlainLayout()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal(home, JavaHomeLayout.Probe(home));
    }

    [Fact]
    public void Probe_AcceptsMacosBundleLayout()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "bundle.jdk", "Contents", "Home");
        var bundle = Path.Combine(_temp.FullPath, "bundle.jdk");

        Assert.Equal(home, JavaHomeLayout.Probe(bundle));
    }

    [Fact]
    public void Probe_AcceptsHomebrewKegLayout()
    {
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "keg", "libexec", "openjdk.jdk", "Contents", "Home");
        var keg = Path.Combine(_temp.FullPath, "keg");

        Assert.Equal(home, JavaHomeLayout.Probe(keg));
    }

    [Fact]
    public void Probe_AcceptsHomebrewKegLayout_UnderAnyBundleName()
    {
        // GraalVM's keg bundles the image as graalvm.jdk, not openjdk.jdk.
        var home = TestJdk.Create(_temp.FullPath, "25.0.4.1.1", "keg", "libexec", "graalvm.jdk", "Contents", "Home");
        var keg = Path.Combine(_temp.FullPath, "keg");

        Assert.Equal(home, JavaHomeLayout.Probe(keg));
    }

    [Fact]
    public void Probe_AcceptsLinuxHomebrewKegLayout()
    {
        // Linux formulae install the image straight into libexec.
        var home = TestJdk.Create(_temp.FullPath, "21.0.5", "keg", "libexec");

        Assert.Equal(home, JavaHomeLayout.Probe(Path.Combine(_temp.FullPath, "keg")));
    }

    [Fact]
    public void Probe_KegWithSeveralBundles_PicksDeterministically()
    {
        // Real kegs hold a single bundle; if one ever holds more, the choice must
        // not depend on filesystem enumeration order, or the same machine state
        // would select differently between runs.
        var first = TestJdk.Create(_temp.FullPath, "21.0.5", "keg", "libexec", "aaa.jdk", "Contents", "Home");
        TestJdk.Create(_temp.FullPath, "25.0.4.1.1", "keg", "libexec", "graalvm.jdk", "Contents", "Home");
        var keg = Path.Combine(_temp.FullPath, "keg");

        Assert.Equal(first, JavaHomeLayout.Probe(keg));
        Assert.Equal(first, JavaHomeLayout.Probe(keg));
    }

    [Fact]
    public void Probe_RejectsIncompleteHomes()
    {
        var root = _temp.FullPath;

        var javaOnly = Path.Combine(root, "java-only");
        Directory.CreateDirectory(Path.Combine(javaOnly, "bin"));
        File.WriteAllText(Path.Combine(javaOnly, "bin", JavaHomeLayout.JavaExecutableName), string.Empty);

        var releaseOnly = Path.Combine(root, "release-only");
        Directory.CreateDirectory(releaseOnly);
        File.WriteAllText(Path.Combine(releaseOnly, "release"), "JAVA_VERSION=\"21\"");

        var empty = Path.Combine(root, "empty");
        Directory.CreateDirectory(empty);

        Assert.Null(JavaHomeLayout.Probe(javaOnly));
        Assert.Null(JavaHomeLayout.Probe(releaseOnly));
        Assert.Null(JavaHomeLayout.Probe(empty));
        Assert.Null(JavaHomeLayout.Probe(Path.Combine(root, "missing")));
        Assert.Null(JavaHomeLayout.Probe(string.Empty));
    }

    public void Dispose() => _temp.Dispose();
}
