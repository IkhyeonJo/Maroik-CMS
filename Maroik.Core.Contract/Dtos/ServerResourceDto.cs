namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Parsed server and Docker resource metrics read from the log files
/// written by the host monitoring script.
/// Consumed by the admin dashboard view.
/// </summary>
public class ServerResourceDto
{
    /// <summary>CPU usage as a numeric percentage (e.g. 12.5).</summary>
    public double HostCpuNumeric { get; init; }

    /// <summary>Raw CPU information string extracted from the log file.</summary>
    public string HostCpuInfo { get; init; } = "";

    /// <summary>Formatted memory usage/limit string (e.g. "8 GB / 16 GB").</summary>
    public string MemUsageLimit { get; init; } = "";

    /// <summary>Raw memory information string extracted from the log file.</summary>
    public string HostMemoryInfo { get; init; } = "";

    /// <summary>Formatted disk usage/limit string (e.g. "120 GB / 500 GB").</summary>
    public string DiskUsageLimit { get; init; } = "";

    /// <summary>Raw disk information string extracted from the log file.</summary>
    public string HostDiskInfo { get; init; } = "";

    /// <summary>Per-container resource usage rows, one entry per running Docker container.</summary>
    public List<DockerContainerResourceDto> DockerContainerResult { get; init; } = [];
}
