using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Website.Contracts;

namespace Maroik.Website.Middlewares;

/// <summary>
/// Middleware that enforces role-based access to static files served under
/// <c>/admin/</c> and <c>/user/</c> path prefixes.
/// Requests to <c>/admin/…</c> are blocked (HTTP 403) for non-Admin sessions,
/// and requests to <c>/user/…</c> are blocked for non-User sessions.
/// The role is the database's current one, re-validated the way <c>AuthorizationFilter</c> does it — not the
/// snapshot the session took at login — so a demotion, deletion or password change takes effect on the next
/// asset request. Only those two folders cost the database read.
/// </summary>
public class RoleBasedStaticFileMiddleware(RequestDelegate next)
{
    /// <summary>Checks the request path against the account's current role and either blocks (403) or forwards the request.</summary>
    public async Task InvokeAsync(HttpContext context, ISessionService sessionService, IAccountService accountService)
    {
        var path = context.Request.Path.Value;

        if (!string.IsNullOrEmpty(path))
        {
            // Leading slashes are dropped before the prefix test: the static-file provider trims them
            // when it resolves the file, so "//admin/x" serves the same file as "/admin/x" and would
            // otherwise slip past a plain "/admin/" StartsWith check.
            //
            // OrdinalIgnoreCase, not Ordinal: the assets are served by the static-file middleware
            // (this project's MapStaticAssets extension is UseStaticFiles — not the ASP.NET
            // endpoint-routing API of the same name), which resolves the request path against the
            // physical wwwroot. On Linux (production) that filesystem is case-sensitive, so "/Admin/x"
            // just 404s; on a case-insensitive filesystem (Windows / macOS dev) it would resolve to the
            // same file as "/admin/x", and a case-sensitive test here would let it straight past the
            // gate. The case-insensitive test is therefore what keeps the gate host-independent.
            var relativePath = path.TrimStart('/');
            string? requiredRole =
                relativePath.StartsWith("admin/", StringComparison.OrdinalIgnoreCase) ? Role.Admin
                : relativePath.StartsWith("user/", StringComparison.OrdinalIgnoreCase) ? Role.User
                : null;

            if (requiredRole != null)
            {
                // Same validity rule as AuthorizationFilter: a session whose account is gone, deleted or
                // re-stamped (password changed, admin lock) is no signed-in account at all.
                var sessionAccount = sessionService.GetAccount();
                var account = sessionAccount == null
                    ? null
                    : await accountService.GetAccountByEmailAsync(sessionAccount.Email ?? "", context.RequestAborted);
                bool sessionValid = account is { Deleted: false } && account.SecurityStamp == sessionAccount!.SecurityStamp;

                if (!sessionValid || account!.Role != requiredRole)
                {
                    context.Response.StatusCode = 403;
                    return;
                }
            }
        }

        await next(context);
    }
}
