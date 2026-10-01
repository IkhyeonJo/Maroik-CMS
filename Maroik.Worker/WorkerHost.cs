using Maroik.Core.Client.Clients;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Repository.Extensions;
using Maroik.Core.Service.Services;
using Maroik.Worker.Contracts;
using Maroik.Worker.Handlers;
using Maroik.Worker.Workers;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;

namespace Maroik.Worker;

/// <summary>
/// Composes the worker's host: settings, data access, mail client, message handler and the RabbitMQ consumer.
/// Kept out of <c>Program.cs</c> so tests can build and run the very same host (against a real broker) instead of a copy.
/// </summary>
internal static class WorkerHost
{
    /// <summary>Broker address used when <c>ConnectionStrings:RabbitMQ</c> is not configured (a local development broker).</summary>
    private const string DefaultRabbitMqConnectionString = "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// The console sink's line format, shared with the bootstrap logger in <c>Program.cs</c>. <c>{Message:j}</c>, not the usual
    /// <c>:lj</c>: property values are rendered JSON-escaped (in quotes), so a value holding a line break cannot end the line and
    /// forge an entry of its own.
    /// </summary>
    internal const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext}: {Message:j}{NewLine}{Exception}";

    /// <summary>Builds (does not start) the worker host from the command line / environment / appsettings in <paramref name="args"/>.</summary>
    internal static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSerilog((_, configuration) => configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: ConsoleOutputTemplate));

        #region ServerSettings (SMTP only — reuses the shared ServerSetting shape)

        builder.Services.Configure<ServerSetting>(cfg =>
        {
            var section = builder.Configuration.GetSection("ServerSetting");
            var smtp = section.GetSection("SmtpOptions");
            cfg.SmtpUserName = smtp["smtpUserName"];
            cfg.SmtpPassword = smtp["smtpPassword"];
            cfg.SmtpHost = smtp["smtpHost"];
            cfg.SmtpPort = Convert.ToInt32(smtp["smtpPort"]);
            cfg.SmtpSsl = Convert.ToBoolean(smtp["smtpSsl"]);
            cfg.FromEmail = smtp["fromEmail"];
            cfg.FromFullName = smtp["fromFullName"];
        });

        #endregion

        #region Data access (needed by IAccountMailStatusService to update Account.Message on SMTP failure)

        builder.Services.AddRepositoryContext(builder.Configuration);
        builder.Services.AddRepositoryServices();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IAccountMailStatusService, AccountMailStatusService>();

        #endregion

        builder.Services.AddScoped<IMailClient, MailClient>();
        builder.Services.AddScoped<IEmailMessageHandler, EmailMessageHandler>();

        string rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
            ?? DefaultRabbitMqConnectionString;
        builder.Services.AddSingleton<IConnectionFactory>(
            new ConnectionFactory
            {
                Uri = new Uri(rabbitMqConnectionString),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                ClientProvidedName = "maroik-worker-email-consumer"
            });
        builder.Services.AddHostedService<EmailConsumerWorker>();

        return builder.Build();
    }
}
