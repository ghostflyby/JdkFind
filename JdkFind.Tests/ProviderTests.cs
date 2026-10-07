using JdkFind.Providers;

namespace JdkFind.Tests;

public class ProviderTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Fact]
    public void Gradle_ScansInjectedUserHome()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "temurin-21");
        var provider = new GradleJvmProvider(temp.FullPath);

        Assert.Equal("gradle", provider.Name);
        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Gradle_MissingRoot_YieldsNothing()
    {
        var provider = new GradleJvmProvider(Path.Combine(temp.FullPath, "missing"));

        Assert.Empty(provider.GetHomes());
    }

    [Fact]
    public void Jabba_ScansVendorAndVersionLevels()
    {
        var jdk = TestJdk.Create(temp.FullPath, "17.0.2", "jdk", "temurin", "17.0.2");
        TestJdk.Create(temp.FullPath, "21.0.5", "jdk", "amazon-corretto", "21.0.5");
        var provider = new JabbaJvmProvider(temp.FullPath);

        Assert.Equal(2, provider.GetHomes().Count);
        Assert.Contains(jdk, provider.GetHomes());
    }

    [Fact]
    public void Scoop_ProbesOnlyCurrentLeaf()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "apps", "temurin21", "current");
        // Valid JDK but not on the "current" leaf; must not appear.
        TestJdk.Create(temp.FullPath, "17.0.2", "apps", "temurin17", "stale");
        var provider = new ScoopJvmProvider([temp.FullPath]);

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Sdkman_ScansInjectedCandidatesDirectory()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "candidates", "java", "21.0.5");
        var provider = new SdkmanJvmProvider(Path.Combine(temp.FullPath, "candidates", "java"));

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Asdf_ScansInjectedDataHome()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "installs", "java", "21.0.5");
        var provider = new AsdfJvmProvider(temp.FullPath);

        Assert.Equal("asdf", provider.Name);
        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Asdf_MissingInstallsDirectory_YieldsNothing()
    {
        var provider = new AsdfJvmProvider(Path.Combine(temp.FullPath, "missing"));

        Assert.Empty(provider.GetHomes());
    }

    [Fact]
    public void Flatpak_ScansSharedExtensionJvms()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "usr", "lib", "sdk", "openjdk", "jvm", "21.0.5");
        // A non-jvm sibling extension dir and a non-jvm child of the extension must
        // not appear.
        Directory.CreateDirectory(Path.Combine(temp.FullPath, "usr", "lib", "sdk", "docs"));
        TestJdk.Create(temp.FullPath, "17.0.2", "usr", "lib", "sdk", "openjdk", "stale");
        var provider = new FlatpakJvmProvider(Path.Combine(temp.FullPath, "usr", "lib", "sdk"), active: true);

        Assert.Equal("flatpak", provider.Name);
        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Flatpak_InactiveOutsideSandbox_YieldsNothing()
    {
        _ = TestJdk.Create(temp.FullPath, "21.0.5", "usr", "lib", "sdk", "openjdk", "jvm", "21.0.5");
        var provider = new FlatpakJvmProvider(Path.Combine(temp.FullPath, "usr", "lib", "sdk"), active: false);

        Assert.Empty(provider.GetHomes());
    }

    [Fact]
    public void Probe_AcceptsJdk8InnerJreLayout()
    {
        var home = Path.Combine(temp.FullPath, "jdk8");
        Directory.CreateDirectory(Path.Combine(home, "jre", "bin"));
        File.WriteAllText(Path.Combine(home, "jre", "bin", JavaHomeLayout.JavaExecutableName), string.Empty);
        File.WriteAllText(Path.Combine(home, "release"), "JAVA_VERSION=\"1.8.0_402\"");

        // The home is the top directory; the java executable resolves through the
        // inner JRE layout.
        Assert.Equal(home, JavaHomeLayout.Probe(home));
        Assert.Equal(
            Path.Combine(home, "jre", "bin", JavaHomeLayout.JavaExecutableName),
            JavaHomeLayout.JavaExecutablePath(home));
    }

    [Fact]
    public void Unix_ScansPrefixList()
    {
        var a = TestJdk.Create(temp.FullPath, "21.0.5", "usr", "lib64", "jvm", "temurin-21");
        var b = TestJdk.Create(temp.FullPath, "17.0.2", "usr", "java", "jdk-17");
        // A non-home child of a scanned prefix must be filtered by the layout probe.
        Directory.CreateDirectory(Path.Combine(temp.FullPath, "usr", "lib64", "jvm", "not-a-home"));
        var provider = new UnixJvmProvider(
        [
            Path.Combine(temp.FullPath, "usr", "lib64", "jvm"),
            Path.Combine(temp.FullPath, "usr", "java"),
        ]);

        Assert.Equal([a, b], provider.GetHomes());
    }

    [Fact]
    public void IntelliJ_ScansInjectedPrefix()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "temurin-21");
        var provider = new IntelliJJvmProvider(temp.FullPath);

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void IntelliJ_DefaultCtor_IsANoOpOnMacOs()
    {
        // On macOS IntelliJ downloads land in the per-user JVM directory (macos-user),
        // so the ~/.jdks default has nothing to scan — by design.
        if (!OperatingSystem.IsMacOS())
            return;

        Assert.Empty(new IntelliJJvmProvider().GetHomes());
    }

    [Fact]
    public void Unix_ScansSystemAndUserPrefixes()
    {
        var system = TestJdk.Create(temp.FullPath, "21.0.5", "system-jvms", "jdk-21");
        var user = TestJdk.Create(temp.FullPath, "17.0.2", "user-jvms", "jdk-17");
        var provider = new UnixJvmProvider(
        [
            Path.Combine(temp.FullPath, "system-jvms"),
            Path.Combine(temp.FullPath, "user-jvms"),
        ]);

        Assert.Equal([system, user], provider.GetHomes());
    }

    [Fact]
    public void Unix_ScansInjectedPrefix()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal([jdk], new UnixJvmProvider(temp.FullPath).GetHomes());
    }

    [Fact]
    public void JavaHome_ValidatesInjectedValue()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal([jdk], new JavaHomeJvmProvider(jdk).GetHomes());
        Assert.Empty(new JavaHomeJvmProvider(temp.FullPath).GetHomes());
        Assert.Empty(new JavaHomeJvmProvider(null).GetHomes());
    }

    [Fact]
    public void Path_ProbesEntriesAndBinParents()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var path = string.Join(separator, "/jdkfind/no-such-entry", Path.Combine(jdk, "bin"), jdk);
        var provider = new PathJvmProvider(path);

        // The bin parent and the directory itself each hit once; the facade deduplicates the repeat.
        Assert.Equal([jdk, jdk], provider.GetHomes());
    }

    [Fact]
    public void Homebrew_ProbesKegLayout()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "opt", "openjdk", "libexec", "openjdk.jdk", "Contents",
            "Home");
        var provider = new HomebrewJvmProvider([temp.FullPath]);

        Assert.Equal([jdk], provider.GetHomes());
    }

    public void Dispose() => temp.Dispose();
}