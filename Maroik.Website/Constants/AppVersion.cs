using System.Reflection;

namespace Maroik.Website.Constants;

/// <summary>
/// The running website's version, read once from its assembly's <see cref="AssemblyInformationalVersionAttribute"/> and shown in
/// the main footer (<c>Views/Shared/_MainFooter.cshtml</c>). CI's publish job passes <c>APP_VERSION</c> / <c>SOURCE_REVISION</c> to
/// the Dockerfile, which hands them to <c>dotnet build/publish</c> as <c>-p:Version</c> / <c>-p:SourceRevisionId</c>, so a production
/// image reads "1.4.2+2ecebf469478"; a local or docker-compose.debug.yml build keeps the <c>Version</c> in Directory.Build.props
/// ("1.0.0-dev", plus the commit the SDK finds, if any). <c>AssemblyName.Version</c> is not used: it is the four-part
/// AssemblyVersion, which drops both the "-dev" suffix and the "+commit" part.
/// </summary>
public static class AppVersion
{
    /// <summary>The whole InformationalVersion, e.g. "1.4.2+2ecebf469478" ("unknown" if the assembly has none).</summary>
    public static string Informational { get; } = Read(typeof(AppVersion).Assembly);

    /// <summary><see cref="Informational"/> as the footer shows it, e.g. "v1.4.2".</summary>
    public static string Display { get; } = ToDisplay(Informational);

    /// <summary>The InformationalVersion of <paramref name="assembly"/>, unchanged, or "unknown" when it carries none.</summary>
    public static string Read(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>"v" and the part of <paramref name="informational"/> before the first '+' (the build metadata, i.e. the commit, is left out).</summary>
    public static string ToDisplay(string informational) => "v" + informational.Split('+')[0];
}
