namespace JdkFind;

/// <summary>
///     A source of candidate Java home directories. Implementations only enumerate
///     candidate paths; validation and metadata extraction are done by <see cref="JdkFinder" />.
/// </summary>
public interface IJvmProvider
{
    /// <summary>Stable lowercase identifier of this provider (e.g. <c>java-home</c>, <c>sdkman</c>).</summary>
    string Name { get; }

    /// <summary>Streams candidate Java home directories. Nonexistent locations must yield an empty stream.</summary>
    IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default);
}
