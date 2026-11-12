namespace JdkFind;

/// <summary>Lexical path equality for local directories and files.</summary>
internal static class PathComparison
{
    internal static readonly StringComparer Comparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    internal static bool DirectoryEquals(DirectoryInfo? left, DirectoryInfo? right) =>
        ReferenceEquals(left, right) ||
        (left is not null && right is not null && Comparer.Equals(left.FullName, right.FullName));

    internal static int DirectoryHash(DirectoryInfo? value) =>
        value is null ? 0 : Comparer.GetHashCode(value.FullName);

    internal static bool PathEquals(string? left, string? right) => Comparer.Equals(left, right);

    internal static int PathHash(string? value) => value is null ? 0 : Comparer.GetHashCode(value);
}
