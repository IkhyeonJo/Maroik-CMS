using Maroik.Core.Contract.Messages;
using RabbitMQ.Client;

namespace Maroik.Worker.Workers;

/// <summary>
/// Moves every dead-lettered e-mail (<see cref="QueueNames.EmailDeadLetter"/>) back into <see cref="QueueNames.Email"/> with a
/// fresh retry budget — what an operator runs once the cause (an SMTP outage longer than the retry delays, a wrong mail
/// setting) is fixed: <c>docker exec Maroik.Worker dotnet Maroik.Worker.dll redrive-email-dead-letters</c>.
/// </summary>
/// <param name="connectionFactory">The broker the worker consumes from.</param>
/// <param name="logger">Logs how many messages were moved.</param>
internal sealed class EmailDeadLetterRedriver(IConnectionFactory connectionFactory, ILogger<EmailDeadLetterRedriver> logger)
{
    /// <summary>The command-line verb that runs <see cref="RedriveAsync"/> instead of the consumer.</summary>
    internal const string Command = "redrive-email-dead-letters";

    /// <summary>
    /// Headers dropped from a re-driven message: this worker's retry counter (so the message gets every retry again) and the
    /// broker's own death records, which would otherwise keep growing with each round trip.
    /// </summary>
    private static readonly string[] _droppedHeaders =
    [
        EmailConsumerWorker.RetryCountHeader, "x-death", "x-first-death-exchange", "x-first-death-queue", "x-first-death-reason",
        "x-last-death-exchange", "x-last-death-queue", "x-last-death-reason",
    ];

    /// <summary>
    /// Re-publishes each message that is in the dead-letter queue when the call starts and returns how many were moved. Only
    /// that many are taken, so a message that fails again and is dead-lettered while this runs is not moved a second time.
    /// Each message is acked off the dead-letter queue only after the broker confirms its copy, so none is lost on a failure.
    /// </summary>
    public async Task<int> RedriveAsync(CancellationToken ct)
    {
        await using IConnection connection = await connectionFactory.CreateConnectionAsync(ct);
        await using IChannel channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        uint waiting = (await channel.QueueDeclarePassiveAsync(QueueNames.EmailDeadLetter, ct)).MessageCount;
        int moved = 0;
        while (moved < waiting && await channel.BasicGetAsync(QueueNames.EmailDeadLetter, autoAck: false, ct) is { } dead)
        {
            Dictionary<string, object?> headers = dead.BasicProperties.Headers != null ? new(dead.BasicProperties.Headers) : [];
            foreach (string header in _droppedHeaders) headers.Remove(header);
            var properties = new BasicProperties(dead.BasicProperties) { Headers = headers };

            await channel.BasicPublishAsync("", QueueNames.Email, mandatory: false, properties, dead.Body, ct);
            await channel.BasicAckAsync(dead.DeliveryTag, multiple: false, ct);
            moved++;
        }

        logger.LogInformation("Re-drove {Count} message(s) from '{DeadLetterQueue}' to '{Queue}'", moved, QueueNames.EmailDeadLetter, QueueNames.Email);
        return moved;
    }
}
