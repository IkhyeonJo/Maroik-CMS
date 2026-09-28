using Maroik.Website.Constants;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;

namespace Maroik.Website.Tests;

/// <summary>
/// The console line format the Website logs with. Logged values can come from a request (an e-mail address typed into the
/// login form, a file name …), so a value must not be able to end the line and start a forged entry of its own.
/// </summary>
public class LogTemplatesTests
{
    /// <summary>Line breaks and terminal escape characters inside a value are escaped, so one event stays one console line.</summary>
    [Fact]
    public void Console_KeepsAValueWithLineBreaksOnOneLine()
    {
        const string forged = "a@b.com\n[00:00:00 INF] Maroik: Login succeeded for admin@maroik.com\r\u001b[31m";

        string rendered = Render(LogTemplates.Console, "Login failed: no account for {Email}", forged);

        Assert.EndsWith(Environment.NewLine, rendered);
        string line = rendered[..^Environment.NewLine.Length];
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.DoesNotContain('\u001b', line);
        Assert.Contains(@"a@b.com\n[00:00:00 INF] Maroik: Login succeeded for admin@maroik.com\r", line);
    }

    private static string Render(string outputTemplate, string messageTemplate, string value)
    {
        var logEvent = new LogEvent(DateTimeOffset.UnixEpoch, LogEventLevel.Warning, null,
            new MessageTemplateParser().Parse(messageTemplate),
            [new LogEventProperty("Email", new ScalarValue(value)), new LogEventProperty("SourceContext", new ScalarValue("Test"))]);
        var writer = new StringWriter();
        new MessageTemplateTextFormatter(outputTemplate).Format(logEvent, writer);
        return writer.ToString();
    }
}
