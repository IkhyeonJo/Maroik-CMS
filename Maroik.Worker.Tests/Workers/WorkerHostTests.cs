using System.Text.Json;
using Maroik.Core.Client.Tests.Clients;
using Maroik.Core.Contract.Misc.Messaging;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Worker.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Maroik.Worker.Tests.Workers;

/// <summary>
/// The worker's composition root, built by the same <see cref="WorkerHost"/> the executable runs: how it binds the SMTP settings,
/// which broker it connects to and — against a real RabbitMQ and an in-process SMTP relay — that a queued e-mail really is
/// delivered by the fully wired host.
/// </summary>
public class WorkerHostTests(WorkerBrokerFixture broker) : IClassFixture<WorkerBrokerFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Command-line arguments pointing the worker host at <paramref name="rabbitMq"/> and a local SMTP relay (the database is an unused dummy).</summary>
    private static string[] Args(string rabbitMq, int smtpPort = 2525, string? smtpSsl = "false") =>
    [
        $"--ConnectionStrings:RabbitMQ={rabbitMq}",
        "--ConnectionStrings:DefaultConnection=Host=127.0.0.1;Port=1;Database=unused;Username=u;Password=p",
        "--ServerSetting:SmtpOptions:smtpUserName=mailer",
        "--ServerSetting:SmtpOptions:smtpPassword=s3cret",
        "--ServerSetting:SmtpOptions:smtpHost=127.0.0.1",
        $"--ServerSetting:SmtpOptions:smtpPort={smtpPort}",
        $"--ServerSetting:SmtpOptions:smtpSsl={smtpSsl}",
        "--ServerSetting:SmtpOptions:fromEmail=noreply@maroik.test",
        "--ServerSetting:SmtpOptions:fromFullName=Maroik Test",
    ];

    /// <summary>The SMTP settings are read from <c>ServerSetting:SmtpOptions</c> with their types converted (port → int, ssl → bool).</summary>
    [Fact]
    public void Build_BindsTheSmtpSettings()
    {
        using IHost host = WorkerHost.Build(Args("amqp://guest:guest@127.0.0.1:5672/", smtpPort: 587, smtpSsl: "true"));

        ServerSetting settings = host.Services.GetRequiredService<IOptions<ServerSetting>>().Value;

        Assert.Equal("mailer", settings.SmtpUserName);
        Assert.Equal("s3cret", settings.SmtpPassword);
        Assert.Equal("127.0.0.1", settings.SmtpHost);
        Assert.Equal(587, settings.SmtpPort);
        Assert.True(settings.SmtpSsl);
        Assert.Equal("noreply@maroik.test", settings.FromEmail);
        Assert.Equal("Maroik Test", settings.FromFullName);
    }

    /// <summary>The RabbitMQ connection is the configured one, with automatic and topology recovery on and a recognizable client name.</summary>
    [Fact]
    public void Build_ConfiguresTheBrokerConnectionFactory()
    {
        using IHost host = WorkerHost.Build(Args("amqp://worker:pw@broker.example:5673/vhost"));

        var factory = Assert.IsType<ConnectionFactory>(host.Services.GetRequiredService<IConnectionFactory>());

        Assert.Equal(new Uri("amqp://worker:pw@broker.example:5673/vhost"), factory.Uri);
        Assert.True(factory.AutomaticRecoveryEnabled);
        Assert.True(factory.TopologyRecoveryEnabled);
        Assert.Equal("maroik-worker-email-consumer", factory.ClientProvidedName);
    }

    /// <summary>With no broker setting at all (no environment-specific appsettings) the worker falls back to a local development broker.</summary>
    [Fact]
    public void Build_WithoutABrokerConnectionString_UsesTheLocalDefault()
    {
        string[] args = [.. Args("unused").Where(a => !a.StartsWith("--ConnectionStrings:RabbitMQ", StringComparison.Ordinal)), "--environment=NoSuchEnvironment"];
        using IHost host = WorkerHost.Build(args);

        var factory = Assert.IsType<ConnectionFactory>(host.Services.GetRequiredService<IConnectionFactory>());

        Assert.Equal("localhost", factory.HostName);
        Assert.Equal(5672, factory.Port);
        Assert.Equal("guest", factory.UserName);
        Assert.Equal("/", factory.VirtualHost);
    }

    /// <summary>
    /// The checked-in production placeholder (an empty connection string, to be supplied by the deployment) makes start-up fail
    /// immediately and loudly — it is not silently replaced by localhost, where nothing would ever be delivered.
    /// </summary>
    [Fact]
    public void Build_WithAnEmptyBrokerConnectionString_FailsAtStartup()
    {
        Assert.Throws<UriFormatException>(() => WorkerHost.Build(Args("")));
    }

    /// <summary>The message handler is wired per scope, so it can use the scoped mail client and DbContext.</summary>
    [Fact]
    public void Build_WiresTheHandlerPerScope()
    {
        using IHost host = WorkerHost.Build(Args("amqp://guest:guest@127.0.0.1:5672/"));

        using IServiceScope scope = host.Services.CreateScope();

        Assert.IsType<EmailMessageHandler>(scope.ServiceProvider.GetRequiredService<Contracts.IEmailMessageHandler>());
    }

    /// <summary>
    /// End to end: the host built by <see cref="WorkerHost"/> connects to the broker, consumes a queued message and hands it to the
    /// SMTP relay — with the configured credentials and sender — and acks it.
    /// </summary>
    [Fact]
    public async Task TheBuiltHost_DeliversAQueuedEmailThroughTheRelay()
    {
        await broker.ResetQueuesAsync(Ct);
        await using var relay = new FakeSmtpServer();
        using IHost host = WorkerHost.Build(Args(broker.ConnectionString, relay.Port));
        await host.StartAsync(Ct);
        try
        {
            await using IConnection connection = await broker.CreateFactory().CreateConnectionAsync(Ct);
            await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
            var message = new SendEmailMessage("to@example.com", "Host wiring subject", "<p>hello from the queue</p>", "corr-host");
            await DeclareQueuesAsync(channel);
            await channel.BasicPublishAsync("", QueueNames.Email, mandatory: false, new BasicProperties { Persistent = true },
                JsonSerializer.SerializeToUtf8Bytes(message), Ct);

            await WaitUntilAsync(() => relay.ReceivedMessage != null);

            Assert.Contains("Host wiring subject", relay.ReceivedMessage);
            Assert.Contains("<p>hello from the queue</p>", relay.ReceivedMessage);
            Assert.Equal(("mailer", "s3cret"), relay.Credentials);
            Assert.Contains(relay.Commands, c => c.Contains("noreply@maroik.test", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(relay.Commands, c => c.StartsWith("RCPT", StringComparison.OrdinalIgnoreCase) && c.Contains("to@example.com", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.StopAsync(Ct);
        }

        await using IConnection check = await broker.CreateFactory().CreateConnectionAsync(Ct);
        await using IChannel checkChannel = await check.CreateChannelAsync(cancellationToken: Ct);
        Assert.Equal(0u, (await checkChannel.QueueDeclarePassiveAsync(QueueNames.Email, Ct)).MessageCount); // acked
    }

    /// <summary>Declares both queues exactly as the worker does.</summary>
    private static async Task DeclareQueuesAsync(IChannel channel)
    {
        // the same declarations the worker makes (identical arguments, so this is accepted whichever side gets there first)
        await channel.QueueDeclareAsync(QueueNames.EmailDeadLetter, durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
        await channel.QueueDeclareAsync(QueueNames.Email, durable: true, exclusive: false, autoDelete: false, arguments: QueueNames.CreateEmailQueueArguments(), cancellationToken: Ct);
    }

    /// <summary>Polls <paramref name="condition"/> every 100 ms; throws <see cref="TimeoutException"/> after 20 seconds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var deadline = DateTime.UtcNow.AddSeconds(20); DateTime.UtcNow < deadline; await Task.Delay(100, Ct))
            if (condition()) return;
        throw new TimeoutException("the relay never received the mail");
    }

    /// <summary>
    /// The executable itself (<c>Program.cs</c>): started as a real process against the broker it connects, starts consuming,
    /// and — on a graceful stop request (SIGINT / Ctrl+C) — shuts down cleanly with exit code 0.
    /// </summary>
    [Fact]
    public async Task TheExecutable_StartsConsuming_AndStopsGracefully()
    {
        if (OperatingSystem.IsWindows()) return; // a graceful stop is delivered as SIGINT, which this test sends with `kill`
        await broker.ResetQueuesAsync(Ct);
        string dll = Path.Combine(AppContext.BaseDirectory, "Maroik.Worker.dll");
        var info = new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            WorkingDirectory = AppContext.BaseDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            Environment =
            {
                ["ConnectionStrings__RabbitMQ"] = broker.ConnectionString,
                ["ConnectionStrings__DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=unused;Username=u;Password=p",
                ["DOTNET_ENVIRONMENT"] = "Production"
            }
        };
        var output = new System.Text.StringBuilder();
        using var process = new System.Diagnostics.Process();
        process.StartInfo = info;
        process.OutputDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { lock (output) output.AppendLine(e.Data); };
        Assert.True(process.Start());
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await WaitUntilAsync(() => { lock (output) return output.ToString().Contains("Email consumer started", StringComparison.Ordinal) || process.HasExited; });
            Assert.False(process.HasExited, output.ToString());

            using (var kill = System.Diagnostics.Process.Start("kill", $"-INT {process.Id}")) await kill.WaitForExitAsync(Ct);
            await process.WaitForExitAsync(Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);

            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
