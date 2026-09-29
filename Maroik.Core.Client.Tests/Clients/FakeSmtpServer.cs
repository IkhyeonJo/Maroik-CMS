using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Maroik.Core.Client.Tests.Clients;

/// <summary>
/// A minimal in-process SMTP server for exercising <c>MailClient.SendMailAsync</c> through the real
/// MailKit client without a network dependency. It speaks just enough of RFC 5321 (greeting, EHLO with
/// AUTH PLAIN, MAIL/RCPT/DATA, QUIT) on a loopback port, offers STARTTLS only when a Certificate is set (otherwise the client's
/// opportunistic-TLS mode continues in cleartext), and records what it received.
/// </summary>
internal sealed class FakeSmtpServer : IAsyncDisposable
{
    /// <summary>Loopback listener on an OS-assigned port.</summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    /// <summary>Signalled on dispose to stop the accept loop.</summary>
    private readonly CancellationTokenSource _stop = new();
    /// <summary>The running accept loop.</summary>
    private readonly Task _acceptLoop;
    /// <summary>One task per accepted connection, awaited on dispose.</summary>
    private readonly List<Task> _sessions = [];

    /// <summary>How the server reacts to <c>AUTH</c>.</summary>
    public bool RejectAuthentication { get; init; }

    /// <summary>When true, the connection is dropped on <c>QUIT</c> instead of answering <c>221</c>.</summary>
    public bool DropConnectionOnQuit { get; init; }

    /// <summary>When set, the server advertises STARTTLS and upgrades the connection with this certificate.</summary>
    public X509Certificate2? Certificate { get; init; }

    /// <summary>True once a client completed the STARTTLS upgrade.</summary>
    public bool TlsEstablished { get; private set; }

    /// <summary>The loopback port the server listens on.</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Every command line received (verbatim, without the trailing CRLF), in order.</summary>
    public List<string> Commands { get; } = [];

    /// <summary>The decoded <c>authzid\0user\0password</c> parts of the last successful/attempted PLAIN login.</summary>
    public (string User, string Password)? Credentials { get; private set; }

    /// <summary>The raw message text of the last <c>DATA</c> transaction (headers + body), or null when none completed.</summary>
    public string? ReceivedMessage { get; private set; }

    /// <summary>Starts listening on a free loopback port.</summary>
    public FakeSmtpServer()
    {
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Accepts connections until stopped, handling each in its own session task.</summary>
    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                lock (_sessions) _sessions.Add(Task.Run(() => HandleSessionAsync(client)));
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (ObjectDisposedException) { /* shutting down */ }
    }

    /// <summary>Speaks a minimal SMTP dialogue on one connection (greeting, EHLO, STARTTLS when a certificate is set, AUTH, MAIL/RCPT/DATA, QUIT), recording what it receives.</summary>
    private async Task HandleSessionAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                await using NetworkStream network = client.GetStream();
                Stream stream = network;
                var reader = new StreamReader(stream, Encoding.ASCII);
                var writer = new StreamWriter(stream, new ASCIIEncoding()) { NewLine = "\r\n", AutoFlush = true };

                await writer.WriteLineAsync("220 fake.smtp ESMTP ready");
                while (await reader.ReadLineAsync(_stop.Token) is { } line)
                {
                    lock (Commands) Commands.Add(line);
                    string verb = line.Split(' ', 2)[0].ToUpperInvariant();
                    switch (verb)
                    {
                        case "EHLO":
                        case "HELO":
                            await writer.WriteLineAsync("250-fake.smtp greets you");
                            if (Certificate != null && !TlsEstablished) await writer.WriteLineAsync("250-STARTTLS");
                            await writer.WriteLineAsync("250-AUTH PLAIN");
                            await writer.WriteLineAsync("250 8BITMIME");
                            break;
                        case "STARTTLS" when Certificate != null:
                            await writer.WriteLineAsync("220 2.0.0 Ready to start TLS");
                            var ssl = new SslStream(stream, leaveInnerStreamOpen: true);
                            await ssl.AuthenticateAsServerAsync(Certificate, clientCertificateRequired: false, checkCertificateRevocation: false);
                            stream = ssl;
                            reader = new StreamReader(stream, Encoding.ASCII);
                            writer = new StreamWriter(stream, new ASCIIEncoding()) { NewLine = "\r\n", AutoFlush = true };
                            TlsEstablished = true;
                            break;
                        case "AUTH":
                            await HandleAuthAsync(line, reader, writer);
                            break;
                        case "MAIL":
                        case "RCPT":
                        case "RSET":
                        case "NOOP":
                            await writer.WriteLineAsync("250 OK");
                            break;
                        case "DATA":
                            await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                            StringBuilder message = new();
                            while (await reader.ReadLineAsync(_stop.Token) is { } dataLine && dataLine != ".")
                                message.AppendLine(dataLine.StartsWith("..") ? dataLine[1..] : dataLine);
                            ReceivedMessage = message.ToString();
                            await writer.WriteLineAsync("250 OK queued");
                            break;
                        case "QUIT":
                            if (!DropConnectionOnQuit) await writer.WriteLineAsync("221 bye");
                            return;
                        default:
                            await writer.WriteLineAsync("502 command not implemented");
                            break;
                    }
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // the client went away / the server is shutting down
            }
        }
    }

    /// <summary>Handles an <c>AUTH</c> command (with or without an initial payload), accepting or rejecting per <see cref="RejectAuthentication"/>.</summary>
    private async Task HandleAuthAsync(string line, StreamReader reader, StreamWriter writer)
    {
        string[] parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        string? payload = parts.Length == 3 ? parts[2] : null;
        if (payload == null)
        {
            await writer.WriteLineAsync("334 ");
            payload = await reader.ReadLineAsync(_stop.Token);
        }

        string[] decoded = Encoding.UTF8.GetString(Convert.FromBase64String(payload ?? "")).Split('\0');
        if (decoded.Length == 3) Credentials = (decoded[1], decoded[2]);

        await writer.WriteLineAsync(RejectAuthentication
            ? "535 5.7.8 Authentication credentials invalid"
            : "235 2.7.0 Authentication successful");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try { await _acceptLoop; } catch (OperationCanceledException) { /* expected */ }
        Task[] sessions;
        lock (_sessions) sessions = [.. _sessions];
        await Task.WhenAll(sessions);
        _stop.Dispose();
    }
}
