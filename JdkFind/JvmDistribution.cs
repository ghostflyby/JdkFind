namespace JdkFind;

/// <summary>
///     JVM distributions, named after the foojay API's distribution identifiers
///     (api.foojay.io/disco/v3.0/distributions). An axis independent of
///     <see cref="JvmVendor" />, detected independently of it: one vendor may
///     ship several distributions — e.g. Oracle ships both
///     <see cref="OracleOpenJdk" /> and <see cref="OracleGraalVm" />.
/// </summary>
public enum JvmDistribution
{
    Unknown = 0,
    AdoptOpenJdk,
    Bisheng,
    Corretto,
    Dragonwell,
    Eliya,
    GluonGraalVm,
    GraalVmCommunity,
    JetBrainsRuntime,
    Kona,
    Liberica,
    Mandrel,
    Microsoft,
    OjdkBuild,
    OpenLogic,
    OracleGraalVm,
    OracleOpenJdk,
    RedHatBuildOfOpenJdk,
    SapMachine,
    Semeru,
    Temurin,
    Trava,
    Zulu,
}
