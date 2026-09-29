namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for RSA cryptographic operations used to protect values that leave the server
/// and come back (the registration / password-reset tokens embedded in mailed links, and the storage
/// path of each inline Summernote image carried in its <c>alt</c> attribute). Padding is always
/// OAEP with SHA-256; <see cref="Maroik.Core.Contract.Misc.Settings.ServerSetting.RsaAlgorithm"/> is a legacy
/// setting that no longer changes it.
/// </summary>
public interface IRsaService
{
    /// <summary>Decrypts a Base64-encoded RSA cipher text using the private key.</summary>
    string Decrypt(string cipherText);

    /// <summary>Encrypts plain text using the public key and returns a Base64-encoded cipher text.</summary>
    string Encrypt(string text);
}
