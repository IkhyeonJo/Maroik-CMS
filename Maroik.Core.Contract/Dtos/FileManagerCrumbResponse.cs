// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>One step of a File Manager breadcrumb (U4): a folder above the open one, or the open one itself.</summary>
public class FileManagerCrumbResponse
{
    /// <summary>Folder ID.</summary>
    public long Id { get; set; }

    /// <summary>Folder name.</summary>
    public string Name { get; set; } = "";
}
