using System.Data;
using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Settings;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.Domain.FileManagers;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Fixtures;

/// <summary>
/// Builds a <see cref="FileManagerService"/> for its tests: an in-memory node repository, a mocked account repository answering one
/// account with <see cref="QuotaBytes"/>, a mocked unit of work, a mocked file client that accepts everything unless told otherwise, a
/// captured log and a stopped clock. Seeding helpers add folders and files of <see cref="Email"/>.
/// </summary>
internal sealed class FileManagerServiceHarness
{
    /// <summary>The account the tests act as.</summary>
    public const string Email = "user@example.com";

    /// <summary>Another account, whose items must stay invisible.</summary>
    public const string OtherEmail = "other@example.com";

    /// <summary>The base URL the service passes to the file client.</summary>
    public const string BaseUrl = "http://files.local";

    /// <summary>One GB in bytes.</summary>
    public const long Gb = 1024L * 1024 * 1024;

    /// <summary>The fixed "current time".</summary>
    public static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The nodes.</summary>
    public InMemoryFileManagerNodeRepository Nodes { get; } = new();

    /// <summary>The account repository.</summary>
    public Mock<IAccountRepository> Accounts { get; } = new();

    /// <summary>The unit of work.</summary>
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    /// <summary>The file client.</summary>
    public Mock<IFileClient> Files { get; } = new();

    /// <summary>The captured log.</summary>
    public FakeLogger<FileManagerService> Logger { get; } = new();

    /// <summary>The stopped clock.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(Now));

    /// <summary>The account's File Manager quota.</summary>
    public long QuotaBytes { get; set; } = 10 * Gb;

    /// <summary>Wires the mocks.</summary>
    public FileManagerServiceHarness()
    {
        Accounts.Setup(a => a.FindByEmailForUpdateAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(() => Account());
        Accounts.Setup(a => a.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(() => Account());
        UnitOfWork.Setup(u => u.BeginAsync(It.IsAny<IsolationLevel?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Files.Setup(f => f.AppendFileManagerChunkAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<Stream>(), BaseUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, long offset, Stream chunk, string _, CancellationToken _) => offset + chunk.Length);
        Files.Setup(f => f.CompleteFileManagerUploadAsync(It.IsAny<Guid>(), It.IsAny<long>(), BaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync(FileUploadResult.Stored);
        Files.Setup(f => f.AbortFileManagerUploadAsync(It.IsAny<Guid>(), BaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Files.Setup(f => f.DeleteFileManagerFileAsync(It.IsAny<string>(), BaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    /// <summary>The account, as the repository loads it.</summary>
    private Account Account() => Domain.Accounts.Account.Reconstitute(Email, "hash", "User", null, "User", "UTC", null, false, 0, true, true,
        null, null, Now, Now, null, false, "stamp", false, QuotaBytes);

    /// <summary>The service under test.</summary>
    public FileManagerService Sut => new(Nodes, Accounts.Object, UnitOfWork.Object, Files.Object,
        Options.Create(new ServerSetting { FileStorageBaseUrl = BaseUrl }), Logger, Time);

    /// <summary>Adds a folder and returns its id.</summary>
    public long Folder(string name, long? parentId = null, string email = Email)
    {
        _ = Nodes.CreateAsync(FileManagerNode.CreateFolder(email, parentId, name, Now).Value);
        return Nodes.All.Max(n => n.Id);
    }

    /// <summary>Adds a file (finished unless <paramref name="uploading"/>) and returns its id.</summary>
    public long File(string name, long? parentId = null, long size = 10, bool uploading = false, string email = Email, DateTime? created = null)
    {
        FileManagerNode file = FileManagerNode.StartUpload(email, parentId, name, size, Guid.NewGuid(), created ?? Now).Value;
        if (!uploading) file.CompleteUpload(created ?? Now);
        _ = Nodes.CreateAsync(file);
        return Nodes.All.Max(n => n.Id);
    }

    /// <summary>The stored node with <paramref name="id"/> (must exist).</summary>
    public FileManagerNode Node(long id) => Nodes.Get(id) ?? throw new InvalidOperationException($"No node {id}");

    /// <summary>Asserts the unit of work ran Begin and then Commit (no Rollback).</summary>
    public void AssertCommitted()
    {
        UnitOfWork.Verify(u => u.BeginAsync(It.IsAny<IsolationLevel?>(), It.IsAny<CancellationToken>()), Times.Once);
        UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Asserts the unit of work ran Begin and then Rollback (no Commit).</summary>
    public void AssertRolledBack()
    {
        UnitOfWork.Verify(u => u.BeginAsync(It.IsAny<IsolationLevel?>(), It.IsAny<CancellationToken>()), Times.Once);
        UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Asserts the account row was locked before the write.</summary>
    public void AssertAccountLocked() => Accounts.Verify(a => a.FindByEmailForUpdateAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
}
