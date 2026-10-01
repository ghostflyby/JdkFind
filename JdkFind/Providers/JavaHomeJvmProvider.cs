namespace JdkFind.Providers;

/// <summary>Exposes the JDK referenced by the <c>JAVA_HOME</c> environment variable.</summary>
public sealed class JavaHomeJvmProvider(string? home) : JvmProviderBase
{
    public override string Name => "java-home";

    public JavaHomeJvmProvider() : this(Environment.GetEnvironmentVariable("JAVA_HOME"))
    {
    }

    public override IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(home) || Probe(home) is not { } javaHome
            ? AsyncEnumerable.Empty<string>()
            : ((IEnumerable<string>)[javaHome]).ToAsyncEnumerable();
}
