namespace Maroik.Core.Contract.Misc.Enums;

/// <summary>
/// Legacy RSA algorithm selector bound from <c>ServerSetting.RsaAlgorithm</c>. Kept for configuration
/// back-compat only: <c>RsaService</c> ignores it and always uses SHA-256 OAEP.
/// </summary>
public enum RsaType
{
    /// <summary>Formerly RSA with SHA-1 padding; now has no effect.</summary>
    Rsa = 0,

    /// <summary>RSA with SHA-256 padding — what <c>RsaService</c> always uses, whatever the setting.</summary>
    Rsa2
}
