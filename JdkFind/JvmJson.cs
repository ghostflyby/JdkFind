using System.Text.Json.Serialization.Metadata;

namespace JdkFind;

/// <summary>
///     Pre-generated, AOT-safe JSON metadata for the fixed <see cref="Jvm" />
///     projection. Serialize with the <see cref="JsonTypeInfo{T}" /> overloads:
///     <c>JsonSerializer.Serialize(jvm, JvmJson.TypeInfo)</c>.
/// </summary>
public static class JvmJson
{
    /// <summary>The fixed projection of a single installation.</summary>
    public static JsonTypeInfo<Jvm> TypeInfo => JvmJsonContext.Default.Jvm;

    /// <summary>The fixed projection of an installation array.</summary>
    public static JsonTypeInfo<Jvm[]> ArrayTypeInfo => JvmJsonContext.Default.JvmArray;

    /// <summary>The fixed projection of an installation list.</summary>
    public static JsonTypeInfo<IReadOnlyList<Jvm>> ListTypeInfo => JvmJsonContext.Default.IReadOnlyListJvm;
}
