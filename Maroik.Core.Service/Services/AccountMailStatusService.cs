using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Helpers;
using Maroik.Core.Domain.Account;

namespace Maroik.Core.Service.Services;

/// <inheritdoc cref="IAccountMailStatusService"/>
public class AccountMailStatusService(IAccountRepository accountRepository, TimeProvider timeProvider) : IAccountMailStatusService
{
    /// <inheritdoc />
    public async Task MarkMailSendFailedAsync(string email, CancellationToken ct = default)
    {
        Account? account = await accountRepository.FindByEmailAsync(email, ct);
        if (account == null)
            return;

        // Column-scoped write (Message/Updated only): this runs from the worker and must not
        // full-row-overwrite an Account the web app is editing at the same moment.
        await accountRepository.UpdateMessageAsync(
            email, EnumHelper.GetDescription(AccountMessage.FailToMailSent), timeProvider.GetUtcNow().UtcDateTime, ct);
    }

    /// <inheritdoc />
    public async Task ClearMailSendFailedAsync(string email, CancellationToken ct = default)
    {
        Account? account = await accountRepository.FindByEmailAsync(email, ct);
        if (account == null)
            return;

        if (account.Message != EnumHelper.GetDescription(AccountMessage.FailToMailSent))
            return;

        await accountRepository.UpdateMessageAsync(email, null, timeProvider.GetUtcNow().UtcDateTime, ct);
    }
}
