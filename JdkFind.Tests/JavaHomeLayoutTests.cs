namespace JdkFind.Tests;

public class JavaHomeLayoutTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Fact]
    public void Probe_AcceptsPlainLayout()
    {
        var home = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal(home, JavaHomeLayout.Probe(home));
    }

    [Fact]
    public void Probe_AcceptsMacosBundleLayout()
    {
        var home = TestJdk.Create(temp.FullPath, "21.0.5", "bundle.jdk", "Contents", "Home");
        var bundle = Path.Combine(temp.FullPath, "bundle.jdk");

        Assert.Equal(home, JavaHomeLayout.Probe(bundle));
    }

    [Fact]
    public void Probe_AcceptsHomebrewKegLayout()
    {
        var home = TestJdk.Create(temp.FullPath, "21.0.5", "keg", "libexec", "openjdk.jdk", "Contents", "Home");
        var keg = Path.Combine(temp.FullPath, "keg");

        Assert.Equal(home, JavaHomeLayout.Probe(keg));
    }

    [Fact]
    public void Probe_RejectsIncompleteHomes()
    {
        var root = temp.FullPath;

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

    public void Dispose() => temp.Dispose();
}
