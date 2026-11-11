using System.Text.Json.Serialization;

namespace JdkFind;

/// <summary>
///     Source-generated metadata for the fixed <see cref="Jvm" /> JSON projection.
///     The default instance emits compact JSON. Public consumers reach the
///     pre-generated type infos through <see cref="JvmJson" />; a caller that needs
///     custom presentation can declare its own source-generated context, which picks
///     up the converter applied to <see cref="Jvm" />. The key set stays fixed because
///     <see cref="JvmJsonConverter" /> writes it explicitly.
/// </summary>
[JsonSerializable(typeof(Jvm))]
[JsonSerializable(typeof(Jvm[]))]
[JsonSerializable(typeof(IReadOnlyList<Jvm>))]
internal sealed partial class JvmJsonContext : JsonSerializerContext;
