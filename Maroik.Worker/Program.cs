// Maroik.Worker - Program.cs
// Background service that consumes queued messages from RabbitMQ (currently: outgoing
// account emails) so the Website never blocks a request on an SMTP round-trip.
// `dotnet Maroik.Worker.dll redrive-email-dead-letters` instead moves the dead-lettered e-mails back
// into the e-mail queue and exits (run by an operator once the cause of the failures is fixed).

using System.Globalization;
using Maroik.Worker;
using Maroik.Worker.Workers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: WorkerHost.ConsoleOutputTemplate, formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    if (args is [EmailDeadLetterRedriver.Command, ..])
    {
        using var redriveHost = WorkerHost.Build(args[1..]);
        await redriveHost.Services.GetRequiredService<EmailDeadLetterRedriver>().RedriveAsync(CancellationToken.None);
        return;
    }

    var host = WorkerHost.Build(args);
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Maroik.Worker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
