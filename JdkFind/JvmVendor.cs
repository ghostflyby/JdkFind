namespace JdkFind;

/// <summary>Normalized upstream organizations behind JVM installations.</summary>
public enum JvmVendor
{
    /// <summary>The IMPLEMENTOR was missing or unrecognized.</summary>
    Unknown = 0,

    /// <summary>AdoptOpenJDK.</summary>
    AdoptOpenJdk,

    /// <summary>Eclipse Adoptium (Temurin).</summary>
    Adoptium,

    /// <summary>Alibaba (Dragonwell).</summary>
    Alibaba,

    /// <summary>Amazon (Corretto).</summary>
    Amazon,

    /// <summary>Apple (macOS JVM).</summary>
    Apple,

    /// <summary>Asymm (Eliya JDK).</summary>
    Asymm,

    /// <summary>Azul Systems (Zulu).</summary>
    Azul,

    /// <summary>BellSoft (Liberica).</summary>
    BellSoft,

    /// <summary>Huawei (BiSheng).</summary>
    Huawei,

    /// <summary>Hewlett-Packard.</summary>
    HewlettPackard,

    /// <summary>IBM (Semeru).</summary>
    Ibm,

    /// <summary>JetBrains (JetBrains Runtime).</summary>
    JetBrains,

    /// <summary>Microsoft Build of OpenJDK.</summary>
    Microsoft,

    /// <summary>OpenLogic OpenJDK.</summary>
    OpenLogic,

    /// <summary>Oracle.</summary>
    Oracle,

    /// <summary>Red Hat.</summary>
    RedHat,

    /// <summary>SAP (SapMachine).</summary>
    Sap,

    /// <summary>Tencent (Kona).</summary>
    Tencent,
}
