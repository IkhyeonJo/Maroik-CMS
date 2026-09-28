using Maroik.Core.Contract.Misc.Enums;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Client interface for scanning file streams with a ClamAV antivirus daemon.
/// The daemon address is configured in <see cref="Maroik.Core.Contract.Misc.Settings.ServerSetting"/>.
/// </summary>
public interface IClamavClient
{
    /// <summary>
    /// Streams <paramref name="fileStream"/> to the ClamAV daemon via the INSTREAM protocol.
    /// Returns <see cref="ClamavScanResult.Clean"/> when the daemon reports the file clean,
    /// <see cref="ClamavScanResult.Infected"/> on a detection, and <see cref="ClamavScanResult.Unavailable"/>
    /// when the scan could not be completed (connection error, timeout, daemon error, unreadable reply).
    /// Anything other than <see cref="ClamavScanResult.Clean"/> must be treated as a refusal.
    /// </summary>
    public Task<ClamavScanResult> ScanWithClamavAsync(Stream fileStream, string clamavHost, int clamavPort, CancellationToken ct = default);
}
