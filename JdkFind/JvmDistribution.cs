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
    /// <summary>The distribution could not be determined.</summary>
    Unknown = 0,

    /// <summary>AdoptOpenJDK.</summary>
    AdoptOpenJdk,

    /// <summary>Huawei BiSheng.</summary>
    Bisheng,

    /// <summary>Amazon Corretto.</summary>
    Corretto,

    /// <summary>Alibaba Dragonwell.</summary>
    Dragonwell,

    /// <summary>Eliya JDK.</summary>
    Eliya,

    /// <summary>Gluon build of GraalVM.</summary>
    GluonGraalVm,

    /// <summary>GraalVM Community Edition.</summary>
    GraalVmCommunity,

    /// <summary>JetBrains Runtime.</summary>
    JetBrainsRuntime,

    /// <summary>Tencent Kona.</summary>
    Kona,

    /// <summary>BellSoft Liberica.</summary>
    Liberica,

    /// <summary>Red Hat build of Mandrel.</summary>
    Mandrel,

    /// <summary>Microsoft Build of OpenJDK.</summary>
    Microsoft,

    /// <summary>ojdkbuild OpenJDK.</summary>
    OjdkBuild,

    /// <summary>OpenLogic OpenJDK.</summary>
    OpenLogic,

    /// <summary>Oracle GraalVM.</summary>
    OracleGraalVm,

    /// <summary>Oracle OpenJDK.</summary>
    OracleOpenJdk,

    /// <summary>Red Hat build of OpenJDK.</summary>
    RedHatBuildOfOpenJdk,

    /// <summary>SAP SapMachine.</summary>
    SapMachine,

    /// <summary>IBM Semeru.</summary>
    Semeru,

    /// <summary>Eclipse Temurin.</summary>
    Temurin,

    /// <summary>TravaOpenJDK.</summary>
    Trava,

    /// <summary>Azul Zulu.</summary>
    Zulu,
}
