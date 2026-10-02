namespace JdkFind;

/// <summary>
///     JVM distributions, named after the foojay API's distribution identifiers.
///     One vendor may ship several distributions (e.g. Oracle ships both
///     <see cref="OracleOpenJdk" /> and <see cref="OracleGraalVm" />).
/// </summary>
public enum JvmDistribution
{
    Unknown,
    AdoptOpenJdk,
    Corretto,
    Dragonwell,
    GraalVmCommunity,
    Bisheng,
    JetBrainsRuntime,
    Kona,
    Liberica,
    Mandrel,
    Microsoft,
    OracleGraalVm,
    OracleOpenJdk,
    RedHatBuildOfOpenJdk,
    SapMachine,
    Semeru,
    Temurin,
    Trava,
    Zulu,
}
