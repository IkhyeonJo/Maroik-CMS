using Maroik.Core.Client.Clients;
using Maroik.Core.Contract.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Maroik.Core.Client.Extensions;

/// <summary>
/// Extension methods for registering Maroik.Core.Client services into the DI container.
/// Call <see cref="AddClientServices"/> from Program.cs during application startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the mail, file-storage, ClamAV, and RabbitMQ email-publisher client implementations.
        /// </summary>
        /// <param name="rabbitMqConnectionString">
        /// AMQP connection string (e.g. "amqp://user:pass@host:5672/") used by <see cref="IEmailPublisher"/>.
        /// </param>
        public void AddClientServices(string rabbitMqConnectionString)
        {
            // Register the SMTP email client
            services.AddScoped<IMailClient, MailClient>();

            // Register the HTTP file-storage proxy client
            services.AddScoped<IFileClient, FileClient>();

            // Register the ClamAV antivirus scanning client
            services.AddScoped<IClamavClient, ClamavClient>();

            // Register the RabbitMQ email publisher. Singleton: owns one long-lived AMQP connection,
            // opened lazily on first publish so a RabbitMQ outage doesn't block app startup.
            // Automatic/topology recovery (the client defaults, pinned here) reopen the connection
            // and channel after a broker restart or network blip without a manual reconnect loop.
            services.AddSingleton<IEmailPublisher>(sp => new RabbitMqEmailPublisher(
                new ConnectionFactory
                {
                    Uri = new Uri(rabbitMqConnectionString),
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true,
                    ClientProvidedName = "maroik-website-email-publisher"
                },
                sp.GetRequiredService<ILogger<RabbitMqEmailPublisher>>()));
        }
    }
}
