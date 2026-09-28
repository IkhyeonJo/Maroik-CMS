using Maroik.FileStorage.Helpers;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;

namespace Maroik.FileStorage.Tests.Helpers;

/// <summary>
/// The console line format FileStorage logs with. Logged values can come from a request (an upload path …), so a value must
/// not be able to end the line and start a forged entry of its own.
/// </summary>
public class LogTemplatesTests
{
    /// <summary>Line breaks and terminal escape characters inside a value are escaped, so one event stays one console line.</summary>
    [Fact]
    public void Console_KeepsAValueWithLineBreaksOnOneLine()
    {
        const string forged = "upload/a.png\n[00:00:00 INF] Maroik: File uploaded: upload/b.png\r\u001b[31m";

        string rendered = Render(LogTemplates.Console, "File upload refused: {FilePath}", forged);

        Assert.EndsWith(Environment.NewLine, rendered);
        string line = rendered[..^Environment.NewLine.Length];
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.DoesNotContain('\u001b', line);
        Assert.Contains(@"upload/a.png\n[00:00:00 INF] Maroik: File uploaded: upload/b.png\r", line);
    }

    private static string Render(string outputTemplate, string messageTemplate, string value)
    {
        var logEvent = new LogEvent(DateTimeOffset.UnixEpoch, LogEventLevel.Warning, null,
            new MessageTemplateParser().Parse(messageTemplate),
            [new LogEventProperty("FilePath", new ScalarValue(value)), new LogEventProperty("SourceContext", new ScalarValue("Test"))]);
        var writer = new StringWriter();
        new MessageTemplateTextFormatter(outputTemplate).Format(logEvent, writer);
        return writer.ToString();
    }
}
