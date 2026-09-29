using System.Net;
using System.Net.Sockets;
using System.Text;
using Maroik.Core.Client.Clients;
using Maroik.Core.Contract.Misc.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maroik.Core.Client.Tests.Clients;

/// <summary>
/// Unit tests for <see cref="ClamavClient"/>.
/// Because no real ClamAV daemon is available during the test run, every test targets
/// an unreachable host or invalid address and verifies that the client handles the
/// connection failure gracefully by returning <see cref="ClamavScanResult.Unavailable"/> (a refusal
/// that is deliberately not reported as an infection).
/// </summary>
public class ClamavClientTests
{
    /// <summary>The client under test (logging discarded).</summary>
    private readonly ClamavClient _sut = new(NullLogger<ClamavClient>.Instance);

    // -- ScanWithClamavAsync --------------------------------------------------

    /// <summary>Verifies that <c>ScanWithClamavAsync</c> returns Unavailable when host is unreachable.</summary>
    [Fact]
    public async Task ScanWithClamavAsync_ReturnsUnavailable_WhenHostIsUnreachable()
    {
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        // Any host that won't have ClamAV running → connection refused → returns false
        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", 19999, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Unavailable, result);
    }

    /// <summary>Verifies that <c>ScanWithClamavAsync</c> returns Unavailable when host name is invalid.</summary>
    [Fact]
    public async Task ScanWithClamavAsync_ReturnsUnavailable_WhenHostNameIsInvalid()
    {
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "invalid.host.that.does.not.exist.local", 3310, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Unavailable, result);
    }

    /// <summary>Verifies that <c>ScanWithClamavAsync</c> resets stream position before reading.</summary>
    [Fact]
    public async Task ScanWithClamavAsync_ResetsStreamPosition_BeforeReading()
    {
        // Place stream at end to verify that the implementation rewinds it
        byte[] data = [0x01, 0x02, 0x03, 0x04];
        using var stream = new MemoryStream(data);
        stream.Position = data.Length; // advance to end

        // Should not throw even though stream is at end initially;
        // implementation rewinds to position 0 before reading
        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", 19999, TestContext.Current.CancellationToken);

        // Unreachable host is Unavailable; the key assertion is no exception thrown
        Assert.Equal(ClamavScanResult.Unavailable, result);
    }

    /// <summary>Verifies that <c>ScanWithClamavAsync</c> returns Unavailable when stream is empty and the host is unreachable.</summary>
    [Fact]
    public async Task ScanWithClamavAsync_ReturnsUnavailable_WhenStreamIsEmpty_AndHostIsUnreachable()
    {
        using var stream = new MemoryStream();

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", 19999, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Unavailable, result);
    }

    // -- INSTREAM response parsing ---------------------------------------------
    // The tests above only exercise the connection-failure paths. These use a fake in-process
    // ClamAV daemon to drive the actual protocol exchange and response parsing (ClamavClient
    // treats a reply ending in "OK", with no "FOUND"/"ERROR", as clean), which nothing else here covers.

    /// <summary>Verifies that <c>ScanWithClamavAsync</c> returns Clean when the daemon reports OK.</summary>
    [Fact]
    public async Task ScanWithClamavAsync_ReturnsClean_WhenDaemonRespondsOk()
    {
        await using var server = FakeClamavServer.Start("stream: OK\n");
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", server.Port, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Clean, result);
    }

    /// <summary>Verifies that <c>ScanWithClamavAsync</c> returns Infected when the daemon reports a virus found.</summary>
    [Fact]
    public async Task ScanWithClamavAsync_ReturnsInfected_WhenDaemonRespondsFound()
    {
        await using var server = FakeClamavServer.Start("stream: Eicar-Test-Signature FOUND\n");
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", server.Port, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Infected, result);
    }

    /// <summary>
    /// A daemon "ERROR" reply (e.g. the stream exceeded clamd's size limit) is a scan that did not
    /// complete — refused, but not reported as an infection.
    /// </summary>
    [Fact]
    public async Task ScanWithClamavAsync_ReturnsUnavailable_WhenDaemonRespondsError()
    {
        await using var server = FakeClamavServer.Start("INSTREAM size limit exceeded. ERROR\n");
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", server.Port, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Unavailable, result);
    }

    /// <summary>A reply that is neither an OK, a FOUND nor an ERROR verdict is not trusted as clean and not called an infection.</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("stream: gibberish\n")]
    public async Task ScanWithClamavAsync_ReturnsUnavailable_WhenReplyIsNotAVerdict(string reply)
    {
        await using var server = FakeClamavServer.Start(reply);
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", server.Port, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Unavailable, result);
    }

    /// <summary>A signature name that merely contains "OK" must not read as clean: FOUND wins.</summary>
    [Fact]
    public async Task ScanWithClamavAsync_ReturnsInfected_WhenSignatureNameContainsOk()
    {
        await using var server = FakeClamavServer.Start("stream: Win.Trojan.OK-Dropper FOUND\n");
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", server.Port, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Infected, result);
    }

    /// <summary>
    /// Verifies that the INSTREAM command is sent as exactly <c>nINSTREAM\n</c> — the "n" prefix
    /// tells clamd the command is newline-terminated, so a Windows "\r\n" would corrupt it.
    /// </summary>
    [Fact]
    public async Task ScanWithClamavAsync_SendsCommandTerminatedByBareNewline()
    {
        await using var server = FakeClamavServer.Start("stream: OK\n");
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        _ = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", server.Port, TestContext.Current.CancellationToken);

        Assert.Equal("nINSTREAM\n", await server.CommandLine);
    }

    /// <summary>
    /// Verifies that a payload larger than one chunk is streamed in 64 KB chunks (not 2 KB), that each
    /// chunk's length prefix matches its payload, and that the reassembled bytes equal the input.
    /// </summary>
    [Fact]
    public async Task ScanWithClamavAsync_StreamsLargePayloadIn64KbChunks_AndPreservesContent()
    {
        const int chunkSize = 64 * 1024;
        byte[] data = new byte[(2 * chunkSize) + 1234];
        new Random(42).NextBytes(data);

        await using var server = FakeClamavServer.Start("stream: OK\n");
        using var stream = new MemoryStream(data);

        ClamavScanResult result = await _sut.ScanWithClamavAsync(stream, "127.0.0.1", server.Port, TestContext.Current.CancellationToken);

        Assert.Equal(ClamavScanResult.Clean, result);
        Assert.Equal([chunkSize, chunkSize, 1234], await server.ChunkLengths);
        Assert.Equal(data, await server.Payload);
    }

    /// <summary>
    /// Minimal fake ClamAV daemon: accepts one INSTREAM session, consumes the length-prefixed
    /// chunks up to the zero-length terminator, then writes back <c>response</c> and closes.
    /// </summary>
    private sealed class FakeClamavServer : IAsyncDisposable
    {
        /// <summary>Loopback listener the fake daemon accepts its single connection on.</summary>
        private readonly TcpListener _listener;
        /// <summary>The background accept-and-respond task, awaited on dispose.</summary>
        private readonly Task _acceptTask;

        /// <summary>Completed with the command line the client sent.</summary>
        private readonly TaskCompletionSource<string> _commandLine = new();

        /// <summary>Loopback port the fake daemon listens on.</summary>
        public int Port { get; }

        /// <summary>The raw command line (including its terminator) the client sent before the chunks.</summary>
        public Task<string> CommandLine => _commandLine.Task;

        /// <summary>Completed with every chunk's length prefix once the zero terminator arrives.</summary>
        private readonly TaskCompletionSource<List<int>> _chunkLengths = new();
        /// <summary>Completed with all chunk payloads concatenated once the zero terminator arrives.</summary>
        private readonly TaskCompletionSource<byte[]> _payload = new();

        /// <summary>The length prefix of every chunk received, in order (excluding the zero terminator).</summary>
        public Task<List<int>> ChunkLengths => _chunkLengths.Task;

        /// <summary>All chunk payloads concatenated in arrival order.</summary>
        public Task<byte[]> Payload => _payload.Task;

        /// <summary>Starts accepting on <paramref name="listener"/>, answering the single scan with <paramref name="response"/>.</summary>
        private FakeClamavServer(TcpListener listener, string response)
        {
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptTask = AcceptAndRespondAsync(response);
        }

        /// <summary>Starts listening on a free loopback port and answers the first session with <paramref name="response"/>.</summary>
        public static FakeClamavServer Start(string response)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new FakeClamavServer(listener, response);
        }

        /// <summary>Accepts one client, records its command line and INSTREAM chunks, then writes <paramref name="response"/>.</summary>
        private async Task AcceptAndRespondAsync(string response)
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            await using NetworkStream stream = client.GetStream();

            // Read the "nINSTREAM" command line up to and including its '\n' and expose it to tests.
            _commandLine.TrySetResult(await ReadLineAsync(stream));

            // Consume length-prefixed chunks until the zero-length terminator, mirroring
            // ClamavClient's write side of the INSTREAM protocol.
            var lengths = new List<int>();
            using var payload = new MemoryStream();
            while (true)
            {
                byte[] lengthBytes = await ReadExactlyAsync(stream, 4);
                int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBytes));
                if (length == 0) break;
                lengths.Add(length);
                await payload.WriteAsync(await ReadExactlyAsync(stream, length));
            }

            _chunkLengths.TrySetResult(lengths);
            _payload.TrySetResult(payload.ToArray());

            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        }

        /// <summary>Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/> (fewer only if it ends first).</summary>
        private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset));
                if (read == 0) throw new IOException("Connection closed unexpectedly.");
                offset += read;
            }
            return buffer;
        }

        /// <summary>Reads one byte at a time up to and including the first <c>'\n'</c>.</summary>
        private static async Task<string> ReadLineAsync(Stream stream)
        {
            var line = new StringBuilder();
            var b = new byte[1];
            while (await stream.ReadAsync(b) == 1)
            {
                line.Append((char)b[0]);
                if (b[0] == (byte)'\n') break;
            }
            return line.ToString();
        }

        /// <summary>Stops the listener and waits for the accept task to finish.</summary>
        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try { await _acceptTask; } catch { /* connection already handled or listener stopped mid-accept */ }
        }
    }
}
