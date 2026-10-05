namespace JdkFind.Tests;

public class JvmResolveTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Fact]
    public void Resolve_FindsBinExecutablesByName()
    {
        var home = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        File.WriteAllText(Path.Combine(home, "bin", JavaHomeLayout.CompilerExecutableName), string.Empty);
        var jvm = TestJvm(home);

        Assert.Equal(Path.Combine(home, "bin", JavaHomeLayout.JavaExecutableName), jvm.Resolve("java")!.FullName);
        Assert.Equal(Path.Combine(home, "bin", JavaHomeLayout.CompilerExecutableName), jvm.Resolve("javac")!.FullName);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        var jvm = TestJvm(TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21"));

        Assert.Null(jvm.Resolve("keytool"));
    }

    [Fact]
    public void Resolve_Jdk8InnerJreLayout_FallsBackToJreBin()
    {
        var home = Path.Combine(temp.FullPath, "jdk-8");
        Directory.CreateDirectory(Path.Combine(home, "jre", "bin"));
        File.WriteAllText(Path.Combine(home, "release"), "JAVA_VERSION=\"1.8.0_402\"");
        File.WriteAllText(Path.Combine(home, "jre", "bin", JavaHomeLayout.JavaExecutableName), string.Empty);
        var jvm = TestJvm(home);

        Assert.Equal(
            Path.Combine(home, "jre", "bin", JavaHomeLayout.JavaExecutableName),
            jvm.Resolve("java")!.FullName);
        // The inner JRE ships no compiler and the top-level bin does not exist.
        Assert.Null(jvm.Resolve("javac"));
    }

    [Fact]
    public void Resolve_NonFileName_Throws()
    {
        var home = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var jvm = TestJvm(home);

        Assert.Throws<ArgumentException>(() => jvm.Resolve(string.Empty));
        Assert.Throws<ArgumentException>(() => jvm.Resolve("bin/java"));
        Assert.Throws<ArgumentException>(() => jvm.Resolve(Path.Combine("bin", "java")));
        Assert.Throws<ArgumentException>(() => jvm.Resolve(home));
    }

    private Jvm TestJvm(string home) => new()
    {
        Home = new DirectoryInfo(home),
        Providers = ["test"],
        Version = new JvmVersion(new Version(21, 0, 0, 0), false, "21"),
    };

    public void Dispose() => temp.Dispose();
}
