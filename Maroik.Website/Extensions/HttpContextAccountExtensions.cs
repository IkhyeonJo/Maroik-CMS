using Maroik.Core.Contract.Dtos;
using Maroik.Website.Constants;
using Maroik.Website.Filters;

namespace Maroik.Website.Extensions;

/// <summary>
/// Typed access to the logged-in account for the current request, replacing the untyped
/// <c>ViewBag.LoggedInAccount</c> (dynamic) lookup.
/// </summary>
public static class HttpContextAccountExtensions
{
    extension(HttpContext context)
    {
        /// <summary>
        /// Returns the logged-in account that <see cref="ViewBagPopulatorFilter"/> resolved for this
        /// request — the database-fresh account when a session exists, or its anonymous placeholder
        /// (<c>Role.Anonymous</c>, no email) otherwise. Never null inside an MVC action or the views it
        /// renders, because that filter runs globally before every action.
        /// </summary>
        /// <exception cref="InvalidOperationException">The filter has not run for this request.</exception>
        public AccountResponse GetLoggedInAccount() =>
            context.Items[HttpContextItemKeys.LoggedInAccount] as AccountResponse
            ?? throw new InvalidOperationException($"{nameof(ViewBagPopulatorFilter)} has not resolved the logged-in account for this request.");
    }
}
