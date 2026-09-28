namespace Maroik.Core.Contract.Dtos;

/// <summary>Resource usage for one running Docker container, as reported by the host monitoring script.</summary>
public class DockerContainerResourceDto
{
    /// <summary>Container name.</summary>
    public string Name { get; init; } = "";

    /// <summary>CPU usage percentage, formatted (e.g. "12.3%").</summary>
    public string CpuPercent { get; init; } = "";

    /// <summary>Formatted memory usage/limit string (e.g. "100MiB / 512MiB").</summary>
    public string MemUsageDisplay { get; init; } = "";
}
