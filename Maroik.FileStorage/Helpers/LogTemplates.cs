namespace Maroik.FileStorage.Helpers;

/// <summary>Serilog output templates shared by the bootstrap logger and the host logger in <c>Program.cs</c>.</summary>
public static class LogTemplates
{
    /// <summary>
    /// The console sink's line format. <c>{Message:j}</c>, not the usual <c>:lj</c>: property values are rendered JSON-escaped (in
    /// quotes), so a request-supplied value holding a line break cannot end the line and forge an entry of its own.
    /// </summary>
    public const string Console = "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:j}{NewLine}{Exception}";
}
