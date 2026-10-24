namespace Maroik.Core.Domain.Calendars;

/// <summary>
/// The calendar owner a reminder would go to, as <see cref="ReminderDeliveryPolicy"/> judges it (the Calendars context does not
/// depend on the Accounts aggregate; the service fills this from the owner's account).
/// </summary>
/// <param name="Email">The owner's e-mail address.</param>
/// <param name="Deleted">Whether the account is deleted.</param>
/// <param name="Locked">Whether the account is locked.</param>
/// <param name="EmailConfirmed">Whether the owner has confirmed the address.</param>
public sealed record ReminderRecipient(string Email, bool Deleted, bool Locked, bool EmailConfirmed);
