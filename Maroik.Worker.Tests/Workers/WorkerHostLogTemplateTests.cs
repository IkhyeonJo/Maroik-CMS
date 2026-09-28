using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;

namespace Maroik.Worker.Tests.Workers;

/// <summary>
/// The console line format the worker logs with (<see cref="WorkerHost.ConsoleOutputTemplate"/>). Logged values come from
/// queued messages (a recipient address …), so a value must not be able to end the line and start a forged entry of its own.
/// </summary>
public class WorkerHostLogTemplateTests
{
    /// <summary>Line breaks and terminal escape characters inside a value are escaped, so one event stays one console line.</summary>
    [Fact]
    public void ConsoleOutputTemplate_KeepsAValueWithLineBreaksOnOneLine()
    {
        const string forged = "a@b.com\n[00:00:00 INF] [x] Maroik: Email sent to admin@maroik.com\r\u001b[31m";

        string rendered = Render(WorkerHost.ConsoleOutputTemplate, "Email send failed for {To}", forged);

        Assert.EndsWith(Environment.NewLine, rendered);
        string line = rendered[..^Environment.NewLine.Length];
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.DoesNotContain('\u001b', line);
        Assert.Contains(@"a@b.com\n[00:00:00 INF] [x] Maroik: Email sent to admin@maroik.com\r", line);
    }

    private static string Render(string outputTemplate, string messageTemplate, string value)
    {
        var logEvent = new LogEvent(DateTimeOffset.UnixEpoch, LogEventLevel.Warning, null,
            new MessageTemplateParser().Parse(messageTemplate),
            [
                new LogEventProperty("To", new ScalarValue(value)),
                new LogEventProperty("SourceContext", new ScalarValue("Test")),
                new LogEventProperty("CorrelationId", new ScalarValue("c-1"))
            ]);
        var writer = new StringWriter();
        new MessageTemplateTextFormatter(outputTemplate).Format(logEvent, writer);
        return writer.ToString();
    }
}
