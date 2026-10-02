using Maroik.Core.PostgreSQL.Data;

namespace Maroik.Website.Tests.Infrastructure;

/// <summary>Account rows a test needs as foreign-key parents, inserted straight into the database.</summary>
internal static class TestAccounts
{
    /// <summary>
    /// Inserts an account (under a random e-mail) holding <paramref name="nickname"/> unless one already does, so a post or comment can
    /// be seeded with that <c>Writer</c>: <c>Board_fk_0</c> / <c>BoardComment_fk_1</c> point the writer at <c>Account.Nickname</c>.
    /// </summary>
    public static void EnsureNickname(ApplicationDbContext db, string nickname)
    {
        if (db.Accounts.Any(a => a.Nickname == nickname)) return;

        db.Accounts.Add(new Maroik.Core.PostgreSQL.Models.Account
        {
            Email = $"writer-{Guid.NewGuid():N}@test.com",
            HashedPassword = "$2a$13$placeholder",
            Nickname = nickname,
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Role = "User",
            TimeZoneIanaId = "UTC",
            Locked = false,
            LoginAttempt = 0,
            EmailConfirmed = true,
            AgreedServiceTerms = true,
            Deleted = false,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
