// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Maroik.Website.Models.ViewModels;

/// <summary>
/// View model for the default ASP.NET Core error page.
/// Carries the request ID so developers can correlate the error
/// with server-side logs or Application Insights traces.
/// </summary>
public class ErrorViewModel
{
    /// <summary>The HTTP request ID (Activity ID or trace identifier) that caused the error.</summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> when <see cref="RequestId"/> is populated
    /// and should be rendered in the error view; <see langword="false"/> otherwise.
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
