using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Maroik.FileStorage.Tests;

/// <summary>
/// The executable itself (<c>Program.cs</c>), run as a real process: it starts, serves and stops cleanly on SIGINT, and when it cannot
/// start (its port is taken) it logs a fatal message and exits with a failure code instead of hanging or swallowing the error.
/// </summary>
public class ProgramProcessTests
{
    /// <summary>Client for probing the started process, with a short timeout.</summary>
    private static readonly HttpClient _http = new()
        { Timeout = TimeSpan.FromSeconds(5) };
    /// <summary>Starts the built FileStorage host as a separate Production process listening on <paramref name="port"/>.</summary>
    private static Process Start(int port)
    {
        string dll = Path.Combine(AppContext.BaseDirectory, "Maroik.FileStorage.dll");
        var info = new ProcessStartInfo("dotnet", $"\"{dll}\" --urls http://127.0.0.1:{port}")
        {
            WorkingDirectory = AppContext.BaseDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            Environment =
            {
                ["Clamav__Host"] = "127.0.0.1",
                ["Clamav__Port"] = "3310",
                ["DOTNET_ENVIRONMENT"] = "Production"
            }
        };
        var process = new Process { StartInfo = info };
        Assert.True(process.Start());
        return process;
    }

    /// <summary>A loopback port that was free a moment ago.</summary>
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Started on a free port it answers HTTP (an unknown file is a 404) and stops with exit code 0 on SIGINT.</summary>
    [Fact]
    public async Task TheExecutable_Serves_AndStopsGracefully()
    {
        if (OperatingSystem.IsWindows()) return; // a graceful stop is delivered as SIGINT, sent here with `kill`
        int port = FreePort();
        using Process process = Start(port);
        try
        {
            HttpResponseMessage? response = null;
            for (var deadline = DateTime.UtcNow.AddSeconds(30); DateTime.UtcNow < deadline && response == null; await Task.Delay(200, TestContext.Current.CancellationToken))
            {
                if (process.HasExited) break;
                try
                {
 #pragma warning disable IDE0028
                    using var form = new MultipartFormDataContent();
 #pragma warning restore IDE0028
                    form.Add(new StringContent("upload/none.png"), "filePath");
                    response = await _http.PostAsync($"http://127.0.0.1:{port}/api/File/download", form, TestContext.Current.CancellationToken);
                }
                catch (HttpRequestException) { /* not listening yet */ }
            }

            Assert.False(process.HasExited, "the process exited before serving a request");
            Assert.Equal(HttpStatusCode.NotFound, response!.StatusCode);

            using (var kill = Process.Start("kill", $"-INT {process.Id}")) await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    /// <summary>If its port is already taken it cannot start: it logs a fatal message and exits with a failure code.</summary>
    [Fact]
    public async Task TheExecutable_ExitsWithAFailure_WhenItsPortIsTaken()
    {
        var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        try
        {
            int port = ((IPEndPoint)blocker.LocalEndpoint).Port;
            using Process process = Start(port);
            string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Maroik.FileStorage terminated unexpectedly", output);
        }
        finally
        {
            blocker.Stop();
        }
    }
}
