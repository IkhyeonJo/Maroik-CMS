namespace Maroik.Core.Contract.Misc.Enums;

/// <summary>
/// Outcome of scanning a file stream with the ClamAV daemon (see <c>IClamavClient</c>).
/// Only <see cref="Clean"/> lets an upload through; the other two are both refusals, kept apart so
/// a daemon outage is never reported to anyone (user or operator) as a virus detection.
/// </summary>
public enum ClamavScanResult
{
    /// <summary>The daemon scanned the stream and reported it clean.</summary>
    Clean = 0,

    /// <summary>The daemon scanned the stream and reported a detection.</summary>
    Infected,

    /// <summary>
    /// The scan could not be completed: the daemon was unreachable, timed out, answered with an
    /// error (e.g. stream too large) or sent a reply that could not be understood. Nothing is known
    /// about the file, so it is refused (fail-closed) — but it is not a detection.
    /// </summary>
    Unavailable
}
