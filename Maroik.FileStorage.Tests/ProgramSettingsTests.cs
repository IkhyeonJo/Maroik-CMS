using Maroik.FileStorage.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Maroik.FileStorage.Tests;

/// <summary>
/// How <c>Program</c> binds <see cref="FileStorageSetting"/>: the ClamAV address from its own <c>Clamav</c> section, and the
/// storage root from <c>ServerSetting</c> or, when that is blank, <c>&lt;content root&gt;/upload</c> (where the container mounts
/// the upload volume).
/// </summary>
[Collection("FileStorage host (Program builds a process-wide Serilog logger)")]
public class ProgramSettingsTests(WebApplicationFactory<Program> baseFactory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>The settings the host resolves with the given configuration values.</summary>
    private FileStorageSetting SettingsWith(params (string Key, string Value)[] values)
    {
        using var factory = baseFactory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in values)
                b.UseSetting(key, value);
        });
        return factory.Services.GetRequiredService<IOptions<FileStorageSetting>>().Value;
    }

    /// <summary>The ClamAV host and port come from the <c>Clamav</c> section.</summary>
    [Fact]
    public void ClamavAddress_IsReadFromTheClamavSection()
    {
        FileStorageSetting settings = SettingsWith(("Clamav:Host", "clamav.internal"), ("Clamav:Port", "3311"));

        Assert.Equal("clamav.internal", settings.ClamavHost);
        Assert.Equal(3311, settings.ClamavPort);
    }

    /// <summary>A blank storage root falls back to the content root's <c>upload</c> folder; a configured one is kept.</summary>
    [Fact]
    public void StorageRoot_DefaultsToTheUploadFolder_UnlessConfigured()
    {
        string configured = Path.Combine(Path.GetTempPath(), "maroik-storage-root");

        FileStorageSetting byDefault = SettingsWith(("Clamav:Port", "3310"), ("ServerSetting:StorageRootPath", ""));
        FileStorageSetting explicitRoot = SettingsWith(("Clamav:Port", "3310"), ("ServerSetting:StorageRootPath", configured));

        Assert.Equal("upload", Path.GetFileName(byDefault.StorageRootPath));
        Assert.True(Path.IsPathRooted(byDefault.StorageRootPath));
        Assert.Equal(configured, explicitRoot.StorageRootPath);
    }
}
