using System.Buffers.Binary;
using System.Net.Sockets;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Client.Clients;

/// <summary>
/// Concrete implementation of <see cref="IClamavClient"/> that streams file data to a
/// ClamAV antivirus daemon using the INSTREAM protocol over a raw TCP connection.
/// </summary>
public class ClamavClient(ILogger<ClamavClient> logger) : IClamavClient
{
    // Bounds the whole scan (connect + stream + result) so a stalled or overloaded ClamAV daemon
    // can't pin the calling request thread forever — callers such as FileValidationService don't
    // thread their own CancellationToken into this call, so this is the only backstop.
    private static readonly TimeSpan _scanTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Size in bytes of the big-endian length prefix that precedes every INSTREAM chunk.</summary>
    private const int LengthPrefixSize = sizeof(int);

    /// <summary>
    /// Payload bytes per INSTREAM chunk. Large enough that a multi-MB upload is a few dozen socket
    /// writes rather than thousands, and far below clamd's default StreamMaxLength (25 MB), which
    /// bounds the whole stream, not a single chunk.
    /// </summary>
    private const int ChunkSize = 64 * 1024;

    /// <inheritdoc />
    public async Task<ClamavScanResult> ScanWithClamavAsync(Stream fileStream, string clamavHost, int clamavPort, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_scanTimeout);
        CancellationToken scanCt = timeoutCts.Token;

        try
        {
            // Rewind the stream so we read from the beginning
            fileStream.Position = 0;

            // Open a TCP connection to the ClamAV daemon. ConnectAsync (rather than the
            // TcpClient(host, port) constructor, which blocks the calling thread for the OS
            // connect timeout) honors scanCt so a network-unreachable host can't pin a thread-pool
            // thread for tens of seconds under concurrent uploads.
            using var client = new TcpClient();
            await client.ConnectAsync(clamavHost, clamavPort, scanCt);
            await using var stream = client.GetStream();
            await using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.AutoFlush = true;

            // Initiate a streaming scan session with the INSTREAM command. The "n" prefix makes clamd
            // read a newline-terminated command, so send an explicit "\n" — WriteLineAsync would use
            // Environment.NewLine, which is "\r\n" on Windows and leaves a stray '\r' in the command.
            await writer.WriteAsync("nINSTREAM\n");

            // Send file data in ChunkSize chunks, each prefixed with its 4-byte big-endian length.
            // The prefix is written into the first LengthPrefixSize bytes of the same buffer the
            // payload is read into, so each chunk goes out in one socket write instead of two.
            // (NetworkStream is unbuffered — a separate prefix write is its own TCP segment.)
            byte[] buffer = new byte[LengthPrefixSize + ChunkSize];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(LengthPrefixSize, ChunkSize), scanCt)) > 0)
            {
                // INSTREAM protocol requires network byte order (big-endian) for the length prefix
                BinaryPrimitives.WriteInt32BigEndian(buffer, bytesRead);
                await stream.WriteAsync(buffer.AsMemory(0, LengthPrefixSize + bytesRead), scanCt);
            }

            // A zero-length chunk signals end-of-stream to ClamAV
            byte[] zero = BitConverter.GetBytes(0);
            await stream.WriteAsync(zero, scanCt);

            // Read the single-line scan result (e.g. "stream: OK", "stream: <Sig> FOUND",
            // "stream: <reason> ERROR"). Some clamd builds / proxies terminate the reply with a
            // trailing NUL rather than a newline; TrimEnd() alone leaves that '\0' in place and
            // makes EndsWith("OK") false for a clean file, so strip it explicitly.
            using var reader = new StreamReader(stream);
            string response = (await reader.ReadLineAsync(scanCt) ?? "").Trim().TrimEnd('\0').TrimEnd();

            // A clean file's reply ends in "OK". Check the terminator rather than a bare Contains("OK")
            // so a malware signature name that happens to contain "OK" can't read as clean. A reply
            // containing "FOUND" is a detection. Anything else — an "ERROR" reply (e.g. the stream
            // exceeded clamd's size limit), an empty reply, gibberish — means the scan did not
            // complete: the file is still refused, but it is NOT reported as infected.
            if (response.EndsWith("OK", StringComparison.Ordinal)
                && !response.Contains("FOUND", StringComparison.Ordinal)
                && !response.Contains("ERROR", StringComparison.Ordinal))
                return ClamavScanResult.Clean;

            if (response.Contains("FOUND", StringComparison.Ordinal))
                return ClamavScanResult.Infected;

            logger.LogWarning("ClamAV at {ClamavHost}:{ClamavPort} returned a reply that is not a clean or infected verdict: {Response}",
                clamavHost, clamavPort, response);
            return ClamavScanResult.Unavailable;
        }
        catch (Exception ex)
        {
            // Any connection, protocol or timeout error means the scan did not complete: refuse the
            // file (fail-safe) but report Unavailable rather than Infected, and log it, so an outage
            // of the ClamAV daemon itself is distinguishable from an actual virus detection.
            logger.LogWarning(ex, "ClamAV scan failed against {ClamavHost}:{ClamavPort}", clamavHost, clamavPort);
            return ClamavScanResult.Unavailable;
        }
    }
}
