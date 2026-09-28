// Maroik.Worker - Program.cs
// Background service that consumes queued messages from RabbitMQ (currently: outgoing
// account emails) so the Website never blocks a request on an SMTP round-trip.

using Maroik.Worker;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: WorkerHost.ConsoleOutputTemplate)
    .CreateBootstrapLogger();

try
{
    var host = WorkerHost.Build(args);
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Maroik.Worker terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
