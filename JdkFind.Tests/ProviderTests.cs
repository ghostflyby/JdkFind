using JdkFind.Providers;

namespace JdkFind.Tests;

public class ProviderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Gradle_ScansInjectedUserHome()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "jdks", "temurin-21");
        var provider = new GradleJvmProvider(_temp.FullPath);

        Assert.Equal("gradle", provider.Name);
        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Gradle_MissingRoot_YieldsNothing()
    {
        var provider = new GradleJvmProvider(Path.Combine(_temp.FullPath, "missing"));

        Assert.Empty(provider.GetHomes());
    }

    [Fact]
    public void Jabba_ScansVendorAndVersionLevels()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "17.0.2", "jdk", "temurin", "17.0.2");
        TestJdk.Create(_temp.FullPath, "21.0.5", "jdk", "amazon-corretto", "21.0.5");
        var provider = new JabbaJvmProvider(_temp.FullPath);

        Assert.Equal(2, provider.GetHomes().Count);
        Assert.Contains(jdk, provider.GetHomes());
    }

    [Fact]
    public void Scoop_ProbesOnlyCurrentLeaf()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "apps", "temurin21", "current");
        // Valid JDK but not on the "current" leaf; must not appear.
        TestJdk.Create(_temp.FullPath, "17.0.2", "apps", "temurin17", "stale");
        var provider = new ScoopJvmProvider([_temp.FullPath]);

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Sdkman_ScansInjectedCandidatesDirectory()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "candidates", "java", "21.0.5");
        var provider = new SdkmanJvmProvider(Path.Combine(_temp.FullPath, "candidates", "java"));

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Asdf_ScansInjectedDataHome()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "installs", "java", "21.0.5");
        var provider = new AsdfJvmProvider(_temp.FullPath);

        Assert.Equal("asdf", provider.Name);
        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Asdf_MissingInstallsDirectory_YieldsNothing()
    {
        var provider = new AsdfJvmProvider(Path.Combine(_temp.FullPath, "missing"));

        Assert.Empty(provider.GetHomes());
    }

    [Fact]
    public void Flatpak_ScansSharedExtensionsAndAppJdk()
    {
        var extensionJdk = TestJdk.Create(_temp.FullPath, "21.0.5", "usr", "lib", "sdk", "openjdk", "jvm", "21.0.5");
        var appJdk = TestJdk.Create(_temp.FullPath, "17.0.2", "app", "jdk");
        // A non-jvm sibling extension dir and a non-jvm child of the extension must
        // not appear.
        Directory.CreateDirectory(Path.Combine(_temp.FullPath, "usr", "lib", "sdk", "docs"));
        TestJdk.Create(_temp.FullPath, "17.0.2", "usr", "lib", "sdk", "openjdk", "stale");
        var provider = new FlatpakJvmProvider(Path.Combine(_temp.FullPath, "usr", "lib", "sdk"), active: true);

        Assert.Equal("flatpak", provider.Name);
        Assert.Equal([extensionJdk, appJdk], provider.GetHomes());
    }

    [Fact]
    public void Flatpak_InactiveOutsideSandbox_YieldsNothing()
    {
        _ = TestJdk.Create(_temp.FullPath, "21.0.5", "usr", "lib", "sdk", "openjdk", "jvm", "21.0.5");
        var provider = new FlatpakJvmProvider(Path.Combine(_temp.FullPath, "usr", "lib", "sdk"), active: false);

        Assert.Empty(provider.GetHomes());
    }

    [Fact]
    public void Probe_AcceptsJdk8InnerJreLayout()
    {
        var home = Path.Combine(_temp.FullPath, "jdk8");
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
        var a = TestJdk.Create(_temp.FullPath, "21.0.5", "usr", "lib64", "jvm", "temurin-21");
        var b = TestJdk.Create(_temp.FullPath, "17.0.2", "usr", "java", "jdk-17");
        // A non-home child of a scanned prefix must be filtered by the layout probe.
        Directory.CreateDirectory(Path.Combine(_temp.FullPath, "usr", "lib64", "jvm", "not-a-home"));
        var provider = new UnixJvmProvider(
        [
            Path.Combine(_temp.FullPath, "usr", "lib64", "jvm"),
            Path.Combine(_temp.FullPath, "usr", "java"),
        ]);

        Assert.Equal([a, b], provider.GetHomes());
    }

    [Fact]
    public void IntelliJ_ScansInjectedPrefix()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "temurin-21");
        var provider = new IntelliJJvmProvider(_temp.FullPath);

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
        var system = TestJdk.Create(_temp.FullPath, "21.0.5", "system-jvms", "jdk-21");
        var user = TestJdk.Create(_temp.FullPath, "17.0.2", "user-jvms", "jdk-17");
        var provider = new UnixJvmProvider(
        [
            Path.Combine(_temp.FullPath, "system-jvms"),
            Path.Combine(_temp.FullPath, "user-jvms"),
        ]);

        Assert.Equal([system, user], provider.GetHomes());
    }

    [Fact]
    public void Unix_ScansInjectedPrefix()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal([jdk], new UnixJvmProvider(_temp.FullPath).GetHomes());
    }

    [Fact]
    public void JavaHome_ValidatesInjectedValue()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");

        Assert.Equal([jdk], new JavaHomeJvmProvider(jdk).GetHomes());
        Assert.Empty(new JavaHomeJvmProvider(_temp.FullPath).GetHomes());
        Assert.Empty(new JavaHomeJvmProvider(null).GetHomes());
    }

    [Fact]
    public void Path_ProbesEntriesAndBinParents()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "jdk-21");
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var path = string.Join(separator, "/jdkfind/no-such-entry", Path.Combine(jdk, "bin"), jdk);
        var provider = new PathJvmProvider(path);

        // The bin parent and the directory itself each hit once; the facade deduplicates the repeat.
        Assert.Equal([jdk, jdk], provider.GetHomes());
    }

    [Fact]
    public void Homebrew_ProbesKegLayout()
    {
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "opt", "openjdk", "libexec", "openjdk.jdk", "Contents",
            "Home");
        var provider = new HomebrewJvmProvider([_temp.FullPath]);

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Homebrew_ProbesGraalVmKegLayout()
    {
        // The macOS bundle inside a keg is named after the formula: graalvm.jdk,
        // not openjdk.jdk.
        var jdk = TestJdk.Create(_temp.FullPath, "25.0.4.1.1", "opt", "graalvm", "libexec", "graalvm.jdk", "Contents",
            "Home");
        var provider = new HomebrewJvmProvider([_temp.FullPath]);

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Homebrew_ProbesLinuxKegLayout()
    {
        // Linux formulae install the image straight into libexec, with no bundle.
        var jdk = TestJdk.Create(_temp.FullPath, "21.0.5", "opt", "openjdk@21", "libexec");
        var provider = new HomebrewJvmProvider([_temp.FullPath]);

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Homebrew_ProbesJavaAliasKeg()
    {
        // Homebrew publishes the java alias for openjdk; both names appear in opt
        // and lead to the same keg.
        var jdk = TestJdk.Create(_temp.FullPath, "27", "opt", "java", "libexec", "openjdk.jdk", "Contents", "Home");
        var provider = new HomebrewJvmProvider([_temp.FullPath]);

        Assert.Equal([jdk], provider.GetHomes());
    }

    [Fact]
    public void Homebrew_SkipsKegsOutsideTheKnownJdkNames()
    {
        var openjdk = TestJdk.Create(_temp.FullPath, "27", "opt", "openjdk", "libexec", "openjdk.jdk", "Contents", "Home");
        var graalvm = TestJdk.Create(_temp.FullPath, "25.0.4.1.1", "opt", "graalvm", "libexec", "graalvm.jdk", "Contents",
            "Home");

        // Only the known JDK formulae are probed. A keg holding a perfectly valid
        // JDK under another name is not scanned — the name list is what keeps the
        // scan from walking every keg's libexec on the machine.
        TestJdk.Create(_temp.FullPath, "21.0.5", "opt", "unlisted-jdk", "libexec", "openjdk.jdk", "Contents", "Home");

        // A keg that is not a JDK at all must not contribute either.
        var plain = Path.Combine(_temp.FullPath, "opt", "wget", "bin");
        Directory.CreateDirectory(plain);
        File.WriteAllText(Path.Combine(plain, "wget"), string.Empty);

        var provider = new HomebrewJvmProvider([_temp.FullPath]);

        Assert.Equal([graalvm, openjdk], provider.GetHomes().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Homebrew_ProbesEveryPrefixInOrder()
    {
        var first = TestJdk.Create(_temp.FullPath, "21.0.5", "prefix-a", "opt", "openjdk", "libexec");
        var second = TestJdk.Create(_temp.FullPath, "17.0.2", "prefix-b", "opt", "graalvm", "libexec", "graalvm.jdk",
            "Contents", "Home");
        var provider = new HomebrewJvmProvider([
            Path.Combine(_temp.FullPath, "prefix-a"),
            Path.Combine(_temp.FullPath, "prefix-b"),
        ]);

        Assert.Equal([first, second], provider.GetHomes());
    }

    public void Dispose() => _temp.Dispose();
}
