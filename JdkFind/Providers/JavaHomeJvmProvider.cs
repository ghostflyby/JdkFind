namespace JdkFind.Providers;

/// <summary>Exposes the JDK referenced by the <c>JAVA_HOME</c> environment variable.</summary>
public sealed class JavaHomeJvmProvider(string? home) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "java-home";

    /// <summary>Reads <c>JAVA_HOME</c>.</summary>
    public JavaHomeJvmProvider() : this(Environment.GetEnvironmentVariable("JAVA_HOME")) { }

    /// <inheritdoc />
    public IEnumerable<string> GetJavaHomes()
    {
        if (string.IsNullOrWhiteSpace(home))
            return [];

        var javaHome = JavaHomeLayout.Probe(home);
        return javaHome != null ? [javaHome] : Array.Empty<string>();
    }
}
