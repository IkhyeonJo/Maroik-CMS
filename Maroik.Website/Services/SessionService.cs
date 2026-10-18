using System.Text;
using System.Text.Json;
using Maroik.Core.Contract.Dtos;
using Maroik.Website.Constants;
using Maroik.Website.Interfaces;

namespace Maroik.Website.Services;

/// <summary>
/// ASP.NET Core session-based implementation of <see cref="ISessionService"/>.
/// Serializes and deserializes <see cref="AccountResponse"/> to/from the server-side
/// session store using JSON (System.Text.Json) encoded as UTF-8 bytes.
/// The session key name comes from <see cref="SessionKeys.Account"/>
/// so it is consistent with all other session accesses in the application.
/// </summary>
public class SessionService(IHttpContextAccessor httpContextAccessor, ILogger<SessionService> logger) : ISessionService
{
    /// <inheritdoc />
    public async Task<AccountResponse?> GetAccountAsync(CancellationToken ct = default)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session == null) return null;

        // Load the session asynchronously before reading it. Read without this, ASP.NET Core loads it with a blocking
        // store (Valkey) call, and when that call fails or times out it only logs and carries on with an EMPTY session.
        // A page fires ~35 asset requests at once; under that load blocking reads timed out, so a signed-in user's
        // /user/** scripts were refused (RoleBasedStaticFileMiddleware) and pages came up blank. LoadAsync neither
        // blocks a thread nor hides the failure: an unreadable store throws to the global exception handler.
        await session.LoadAsync(ct);

        bool exists = session.TryGetValue(SessionKeys.Account, out byte[]? bytes);
        if (!exists || bytes == null || bytes.Length == 0) return null;

        try
        {
            return JsonSerializer.Deserialize<AccountResponse>(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception e)
        {
            // A corrupt / tampered session entry is treated as "no session" (the visitor logs in again),
            // but an operator should see it. The payload is not logged.
            logger.LogWarning(e, "Stored session account could not be read; treating the session as empty");
            return null;
        }
    }

    /// <inheritdoc />
    public void SetAccount(AccountResponse account)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        session?.Set(
            SessionKeys.Account,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(account)));
    }

    /// <inheritdoc />
    public void RemoveAccount()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        session?.Remove(SessionKeys.Account);
    }
}
