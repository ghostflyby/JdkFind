using JdkFind.Providers;

namespace JdkFind.Tests;

public class ProviderTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Fact]
    public async Task Gradle_ScansInjectedUserHome()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "temurin-21");
        var provider = new GradleJvmProvider(temp.FullPath);

        Assert.Equal("gradle", provider.Name);
        Assert.Equal([jdk], await provider.GetHomesAsync());
    }

    [Fact]
    public async Task Gradle_MissingRoot_YieldsNothing()
    {
        var provider = new GradleJvmProvider(Path.Combine(temp.FullPath, "missing"));

        Assert.Empty(await provider.GetHomesAsync());
    }

    [Fact]
    public async Task Jabba_ScansVendorAndVersionLevels()
    {
        var jdk = TestJdk.Create(temp.FullPath, "17.0.2", "jdk", "temurin", "17.0.2");
        TestJdk.Create(temp.FullPath, "21.0.5", "jdk", "amazon-corretto", "21.0.5");
        var provider = new JabbaJvmProvider(temp.FullPath);

        Assert.Equal(2, (await provider.GetHomesAsync()).Count);
        Assert.Contains(jdk, await provider.GetHomesAsync());
    }

    [Fact]
    public async Task Scoop_ProbesOnlyCurrentLeaf()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "apps", "temurin21", "current");
        // Valid JDK but not on the "current" leaf; must not appear.
        TestJdk.Create(temp.FullPath, "17.0.2", "apps", "temurin17", "stale");
        var provider = new ScoopJvmProvider([temp.FullPath]);

        Assert.Equal([jdk], await provider.GetHomesAsync());
    }

    [Fact]
    public async Task Sdkman_ScansInjectedCandidatesDirectory()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "candidates", "java", "21.0.5");
        var provider = new SdkmanJvmProvider(Path.Combine(temp.FullPath, "candidates", "java"));

        Assert.Equal([jdk], await provider.GetHomesAsync());
    }

    [Fact]
    public async Task IntelliJ_ScansInjectedPrefix()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "temurin-21");
        var provider = new IntelliJJvmProvider(temp.FullPath);

        Assert.Equal([jdk], await provider.GetHomesAsync());
    }

    [Fact]
    public async Task PlatformDirectories_ScanInjectedPrefix()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal([jdk], await new MacOsSystemJvmProvider(temp.FullPath).GetHomesAsync());
        Assert.Equal([jdk], await new LinuxJvmProvider(temp.FullPath).GetHomesAsync());
    }

    [Fact]
    public async Task JavaHome_ValidatesInjectedValue()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal([jdk], await new JavaHomeJvmProvider(jdk).GetHomesAsync());
        Assert.Empty(await new JavaHomeJvmProvider(temp.FullPath).GetHomesAsync());
        Assert.Empty(await new JavaHomeJvmProvider(null).GetHomesAsync());
    }

    [Fact]
    public async Task Path_ProbesEntriesAndBinParents()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var path = string.Join(separator, "/jdkfind/no-such-entry", Path.Combine(jdk, "bin"), jdk);
        var provider = new PathJvmProvider(path);

        // The bin parent and the directory itself each hit once; the facade deduplicates the repeat.
        Assert.Equal([jdk, jdk], await provider.GetHomesAsync());
    }

    [Fact]
    public async Task Homebrew_ProbesKegLayout()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "opt", "openjdk", "libexec", "openjdk.jdk", "Contents", "Home");
        var provider = new HomebrewJvmProvider([temp.FullPath]);

        Assert.Equal([jdk], await provider.GetHomesAsync());
    }

    public void Dispose() => temp.Dispose();
}
