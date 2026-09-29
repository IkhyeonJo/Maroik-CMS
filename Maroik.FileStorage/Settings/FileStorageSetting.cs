namespace Maroik.FileStorage.Settings;

/// <summary>
/// Strongly-typed configuration bound to the "ServerSetting" section of appsettings.json.
/// Deliberately reuses the same section/env-var name as Maroik.Website's
/// <c>Maroik.Core.Contract.Misc.Settings.ServerSetting.MaxAttachedFileSizeBytes</c>: both
/// services load the same env file (.env.debug, see docker-compose.debug.yml), so a single
/// <c>ServerSetting__MaxAttachedFileSizeBytes</c> env var keeps the website-level check and
/// this microservice's hard cap in sync instead of drifting apart.
/// </summary>
public class FileStorageSetting
{
    /// <summary>Maximum allowed upload file size in bytes. Defaults to 10 MB when not configured.</summary>
    public long MaxAttachedFileSizeBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// Absolute path of the one directory every stored/served file must resolve within. Set from the
    /// content root at startup (see Program.cs). Requests whose resolved path escapes this root
    /// (via "..", an absolute path, …) are rejected — the check is lexical, so symlinks are not resolved — the callers only ever pass
    /// server-generated relative paths under "upload/", so anything outside is a bug or an attack.
    /// </summary>
    public string StorageRootPath { get; set; } = "";

    /// <summary>Host name or IP address of the ClamAV antivirus daemon. Bound from the "Clamav" section.</summary>
    public string? ClamavHost { get; set; }

    /// <summary>TCP port of the ClamAV daemon (clamd's standard port is 3310; no default is applied here). Bound from the "Clamav" section.</summary>
    public int ClamavPort { get; set; }
}
