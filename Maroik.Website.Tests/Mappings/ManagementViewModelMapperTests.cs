using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Localization;
using Maroik.Website.Mappings;

namespace Maroik.Website.Tests.Mappings;

/// <summary>
/// Unit tests for <see cref="ManagementViewModelMapper"/> — presentation mapping from
/// <see cref="AccountResponse"/> to <see cref="Maroik.Website.Models.ViewModels.Management.AccountOutputViewModel"/>,
/// with particular attention to the UTC→local timezone conversion applied to the audit timestamps.
/// </summary>
public class ManagementViewModelMapperTests
{
    /// <summary>An admin account view created/updated at the given UTC times.</summary>
    private static AdminAccountResponse SampleAccount(DateTime created, DateTime updated) => new()
    {
        Email = "user@example.com",
        HashedPassword = "hashed",
        Nickname = "Nick",
        AvatarImagePath = "/avatars/a.png",
        Role = "User",
        TimeZoneIanaId = "Asia/Seoul",
        Locked = true,
        LoginAttempt = 3,
        EmailConfirmed = true,
        AgreedServiceTerms = true,
        RegistrationToken = "reg-token",
        ResetPasswordToken = "reset-token",
        Created = created,
        Updated = updated,
        Message = "note",
        Deleted = false
    };

    // -- ToDisplayViewModels: field mapping ------------------------------------

    /// <summary>To display view models copies all non timestamp fields.</summary>
    [Fact]
    public void ToDisplayViewModels_CopiesAllNonTimestampFields()
    {
        var created = new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var account = SampleAccount(created, created);

        var vms = new[] { account }.ToDisplayViewModels("UTC");

        var item = Assert.Single(vms);
        Assert.Equal("user@example.com", item.Email);
        Assert.Equal("hashed", item.HashedPassword);
        Assert.Equal("Nick", item.Nickname);
        Assert.Equal("/avatars/a.png", item.AvatarImagePath);
        Assert.Equal("User", item.Role);
        Assert.Equal("Asia/Seoul", item.TimeZoneIanaId);
        Assert.True(item.Locked);
        Assert.Equal(3, item.LoginAttempt);
        Assert.True(item.EmailConfirmed);
        Assert.True(item.AgreedServiceTerms);
        Assert.Equal("reg-token", item.RegistrationToken);
        Assert.Equal("reset-token", item.ResetPasswordToken);
        Assert.Equal("note", item.Message);
        Assert.False(item.Deleted);
    }

    // -- ToDisplayViewModels: timezone conversion -------------------------------

    /// <summary>To display view models utc leaves timestamps unchanged.</summary>
    [Fact]
    public void ToDisplayViewModels_Utc_LeavesTimestampsUnchanged()
    {
        var created = new DateTime(2025, 6, 10, 3, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2025, 6, 11, 4, 30, 0, DateTimeKind.Utc);
        var account = SampleAccount(created, updated);

        var vm = new[] { account }.ToDisplayViewModels("UTC")[0];

        Assert.Equal(created, vm.Created);
        Assert.Equal(updated, vm.Updated);
    }

    /// <summary>To display view models asia seoul converts created and updated nine hours ahead.</summary>
    [Fact]
    public void ToDisplayViewModels_AsiaSeoul_ConvertsCreatedAndUpdatedNineHoursAhead()
    {
        var created = new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2025, 6, 10, 15, 0, 0, DateTimeKind.Utc);
        var account = SampleAccount(created, updated);

        var vm = new[] { account }.ToDisplayViewModels("Asia/Seoul")[0];

        // Asia/Seoul is UTC+9 with no daylight saving.
        Assert.Equal(new DateTime(2025, 6, 10, 9, 0, 0), vm.Created);
        Assert.Equal(new DateTime(2025, 6, 11, 0, 0, 0), vm.Updated);
        Assert.Equal(created.ConvertTimeByTimeZoneIanaId("Asia/Seoul"), vm.Created);
        Assert.Equal(updated.ConvertTimeByTimeZoneIanaId("Asia/Seoul"), vm.Updated);
    }

    /// <summary>To display view models different time zones produce different converted timestamps.</summary>
    [Fact]
    public void ToDisplayViewModels_DifferentTimeZones_ProduceDifferentConvertedTimestamps()
    {
        var created = new DateTime(2025, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var account = SampleAccount(created, created);

        var utcVm = new[] { account }.ToDisplayViewModels("UTC")[0];
        var seoulVm = new[] { account }.ToDisplayViewModels("Asia/Seoul")[0];

        Assert.NotEqual(utcVm.Created, seoulVm.Created);
        Assert.Equal(created, utcVm.Created);
        Assert.Equal(new DateTime(2025, 6, 10, 21, 0, 0), seoulVm.Created);
    }

    // -- ToDisplayViewModels: multiple items ------------------------------------

    /// <summary>To display view models maps each item in the sequence.</summary>
    [Fact]
    public void ToDisplayViewModels_MapsEachItemInTheSequence()
    {
        var account1 = SampleAccount(DateTime.UtcNow, DateTime.UtcNow);
        account1.Email = "a@example.com";
        var account2 = SampleAccount(DateTime.UtcNow, DateTime.UtcNow);
        account2.Email = "b@example.com";

        var vms = new[] { account1, account2 }.ToDisplayViewModels("UTC");

        Assert.Equal(2, vms.Count);
        Assert.Equal("a@example.com", vms[0].Email);
        Assert.Equal("b@example.com", vms[1].Email);
    }

    /// <summary>To display view models empty sequence returns empty list.</summary>
    [Fact]
    public void ToDisplayViewModels_EmptySequence_ReturnsEmptyList()
    {
        var vms = Array.Empty<AdminAccountResponse>().ToDisplayViewModels("UTC");

        Assert.Empty(vms);
    }
}
