namespace Maroik.Core.Contract.Misc.Enums;

/// <summary>
/// Selects the RSA signature/encryption algorithm variant used by <c>IRsaService</c>.
/// </summary>
public enum RsaType
{
    /// <summary>RSA with SHA-1 padding. Suitable for shorter key lengths.</summary>
    Rsa = 0,

    /// <summary>RSA with SHA-256 padding. Requires a key length of at least 2048 bits.</summary>
    Rsa2
}
