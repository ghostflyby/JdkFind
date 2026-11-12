namespace JdkFind;

/// <summary>Ordered equality and hashing for provider names.</summary>
internal static class ProviderSequence
{
    internal static bool Equals(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    internal static void AddHash(ref HashCode hash, IReadOnlyList<string>? providers)
    {
        if (providers is null)
            return;

        foreach (var provider in providers)
            hash.Add(provider, StringComparer.Ordinal);
    }
}
