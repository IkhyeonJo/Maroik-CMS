using ErrorOr;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.Menu;
using Maroik.Core.Domain.ValueObjects;
using DomainAccount = Maroik.Core.Domain.Account.Account;
using DomainTitledContentPolicy = Maroik.Core.Domain.Primitives.TitledContentPolicy;

namespace Maroik.Core.Domain.Tests.Boundaries;

/// <summary>
/// The message template of each domain error is the key the Website uses to find its ko-KR translation
/// (see <c>LocalizableError</c>), so it is part of the behavior: these assert the code, type and exact
/// template of every error that had no such assertion. Changing a template means changing the resx keys too.
/// </summary>
public class ErrorContractTests
{
    /// <summary>Owner e-mail used by every aggregate built in these tests.</summary>
    private const string Email = "user@example.com";

    // ---- Asset ----------------------------------------------------------------------------

    /// <summary>Verifies the empty / invalid / too-long errors of <c>Asset.Create</c>, and that product name and note exactly at the limit are accepted.</summary>
    [Fact]
    public void Asset_Create_Errors()
    {
        ErrorAssert.Validation(Asset.Create(" ", Email, "FreeDepositAndWithdrawal", 1m, "KRW"), "Asset.ProductNameEmpty", "Product name cannot be empty.");
        ErrorAssert.Validation(Asset.Create("a", Email, " ", 1m, "KRW"), "Asset.ItemEmpty", "Asset category (item) cannot be empty.");
        ErrorAssert.Validation(Asset.Create("a", Email, "Nonsense", 1m, "KRW"), "Asset.ItemInvalid", "Asset category (item) is not a recognised value.");
        ErrorAssert.Validation(Asset.Create(new string('x', FinanceTextPolicy.MaxTextLength + 1), Email, "FreeDepositAndWithdrawal", 1m, "KRW"),
            "Asset.ProductNameTooLong", "Product name must be {0} characters or fewer.", FinanceTextPolicy.MaxTextLength);
        ErrorAssert.Validation(Asset.Create("a", Email, "FreeDepositAndWithdrawal", 1m, "KRW", new string('x', FinanceTextPolicy.MaxTextLength + 1)),
            "Finance.NoteTooLong", "Note must be {0} characters or fewer.", FinanceTextPolicy.MaxTextLength);
        Assert.False(Asset.Create(new string('x', FinanceTextPolicy.MaxTextLength), Email, "FreeDepositAndWithdrawal", 1m, "KRW", new string('x', FinanceTextPolicy.MaxTextLength)).IsError);
    }

    /// <summary>Verifies that depositing, withdrawing or setting a balance in another currency returns <c>Asset.CurrencyMismatch</c> with an operation-specific message.</summary>
    [Fact]
    public void Asset_DepositWithdrawAndUpdate_CurrencyErrors()
    {
        Asset asset = Asset.Create("a", Email, "FreeDepositAndWithdrawal", 100m, "KRW").Value;
        Money usd = Money.Create(1m, "USD").Value;

        ErrorAssert.Validation(asset.Deposit(usd), "Asset.CurrencyMismatch", "Cannot deposit a different currency into this asset.");
        ErrorAssert.Validation(asset.Withdraw(usd), "Asset.CurrencyMismatch", "Cannot withdraw a different currency from this asset.");
        ErrorAssert.Validation(asset.SetBalance(usd), "Asset.CurrencyMismatch", "Cannot change the currency of an existing asset.");
    }

    /// <summary>Verifies the empty-name, empty-item and unknown-item errors of <c>Asset.Update</c>.</summary>
    [Fact]
    public void Asset_Update_Errors()
    {
        Asset asset = Asset.Create("a", Email, "FreeDepositAndWithdrawal", 100m, "KRW").Value;

        ErrorAssert.Validation(asset.Update(" ", "FreeDepositAndWithdrawal", 1m, "KRW", null, false), "Asset.ProductNameEmpty", "Product name cannot be empty.");
        ErrorAssert.Validation(asset.Update("a", " ", 1m, "KRW", null, false), "Asset.ItemEmpty", "Asset category cannot be empty.");
        ErrorAssert.Validation(asset.Update("a", "Nonsense", 1m, "KRW", null, false), "Asset.ItemInvalid", "Asset category (item) is not a recognised value.");
    }

    // ---- Account --------------------------------------------------------------------------

    /// <summary>The fixed "current time" the account checks run at.</summary>
    private static readonly DateTime AccountNow = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Result of <c>Account.Create</c> with the given password hash and role.</summary>
    private static ErrorOr<DomainAccount> NewAccount(string hash = "$2a$hash", string role = Role.User) =>
        DomainAccount.Create(Email, hash, "Nick", role, "UTC", null, GuidToken.Generate(AccountNow), true, AccountNow);

    /// <summary>A persisted account whose confirmation, token and lockout state are set by the arguments.</summary>
    private static DomainAccount Reconstituted(bool emailConfirmed = true, string? resetToken = null, string? registrationToken = null,
        bool locked = false, long loginAttempt = 0) =>
        DomainAccount.Reconstitute(Email, "$2a$hash", "Nick", null, Role.User, "UTC", null, locked, loginAttempt, emailConfirmed, true,
            registrationToken, resetToken, DateTime.UtcNow, DateTime.UtcNow, null, false, "stamp", false);

    /// <summary>Verifies that <c>Account.Create</c> rejects an empty password hash and an unknown role.</summary>
    [Fact]
    public void Account_Create_Errors()
    {
        ErrorAssert.Validation(NewAccount(hash: " "), "Account.PasswordEmpty", "Hashed password cannot be empty.");
        ErrorAssert.Validation(NewAccount(role: "Root"), "Account.RoleInvalid", "Role must be either Admin or User.");
    }

    /// <summary>Verifies that confirming an already-confirmed account is a conflict and a wrong token is a validation error.</summary>
    [Fact]
    public void Account_ConfirmEmail_Errors()
    {
        ErrorAssert.Is(Reconstituted(emailConfirmed: true).ConfirmEmail("t", AccountNow), "Account.AlreadyConfirmed", ErrorType.Conflict, "Email address is already confirmed.");
        ErrorAssert.Validation(Reconstituted(emailConfirmed: false, registrationToken: GuidToken.Generate(AccountNow)).ConfirmEmail("wrong", AccountNow),
            "Account.InvalidToken", "Invalid email confirmation token.");
    }

    /// <summary>Verifies that a reset needs a confirmed email, the right token and a non-empty new hash.</summary>
    [Fact]
    public void Account_PasswordReset_Errors()
    {
        ErrorAssert.Is(Reconstituted(emailConfirmed: false).RequestPasswordReset(GuidToken.Generate(AccountNow), AccountNow), "Account.NotConfirmed", ErrorType.Failure,
            "Email must be confirmed before resetting the password.");

        string token = GuidToken.Generate(AccountNow);
        DomainAccount account = Reconstituted(resetToken: token);
        ErrorAssert.Validation(account.ResetPassword("wrong", "$2a$new", AccountNow), "Account.InvalidToken", "Invalid or expired password-reset token.");
        ErrorAssert.Validation(account.ResetPassword(token, " ", AccountNow), "Account.PasswordEmpty", "New hashed password cannot be empty.");
    }

    /// <summary>Verifies that <c>ChangeRole</c> rejects a role other than Admin or User.</summary>
    [Fact]
    public void Account_ChangeRole_RejectsAnUnknownRole()
    {
        ErrorAssert.Validation(Reconstituted().ChangeRole("Root", AccountNow),
            "Account.RoleInvalid", "Role must be either Admin or User.");
    }

    // ---- Board ----------------------------------------------------------------------------

    /// <summary>Verifies the conflicts on a deleted or locked post and the mismatch error for a comment that belongs to another post.</summary>
    [Fact]
    public void Board_Conflicts()
    {
        Domain.Board.Board board = Domain.Board.Board.Reconstitute(1, BoardTypes.FreeForum, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0L, deleted: true, locked: false, noticed: false);
        BoardComment comment = BoardComment.Create(1, 0, null, "Bob", "hi").Value;

        ErrorAssert.Is(board.Update("t", "c"), "Board.Deleted", ErrorType.Conflict, "Cannot update a deleted post.");
        ErrorAssert.Is(board.AddComment(comment), "Board.Deleted", ErrorType.Conflict, "Cannot add a comment to a deleted post.");
        ErrorAssert.Is(board.SoftDelete(), "Board.AlreadyDeleted", ErrorType.Conflict, "Post is already deleted.");

        Domain.Board.Board locked = Domain.Board.Board.Reconstitute(1, BoardTypes.FreeForum, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0L, deleted: false, locked: true, noticed: false);
        ErrorAssert.Is(locked.AddComment(comment, "Bob"), "Board.Locked", ErrorType.Conflict, "Cannot add a comment to a locked post.");

        BoardComment foreign = BoardComment.Create(999, 0, null, "Bob", "hi").Value;
        ErrorAssert.Validation(NewBoardWithId(1).AddComment(foreign), "Board.CommentMismatch", "Comment does not belong to this post.");
    }

    /// <summary>A persisted free-forum post with id <paramref name="id"/>.</summary>
    private static Domain.Board.Board NewBoardWithId(long id) =>
        Domain.Board.Board.Reconstitute(id, BoardTypes.FreeForum, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0L, false, false, false);

    /// <summary>Verifies the empty-writer, empty/too-long content and already-deleted errors of a board comment.</summary>
    [Fact]
    public void BoardComment_Errors()
    {
        ErrorAssert.Validation(BoardComment.Create(1, 0, null, " ", "hi"), "BoardComment.WriterEmpty", "Comment author (writer) cannot be empty.");
        ErrorAssert.Validation(BoardComment.Create(1, 0, null, "Bob", " "), "BoardComment.ContentEmpty", "Comment content cannot be empty.");
        ErrorAssert.Validation(BoardComment.Create(1, 0, null, "Bob", new string('x', DomainTitledContentPolicy.MaxBodyLength + 1)),
            "BoardComment.ContentTooLong", "Comment must be {0} characters or fewer.", DomainTitledContentPolicy.MaxBodyLength);
        Assert.False(BoardComment.Create(1, 0, null, "Bob", new string('x', DomainTitledContentPolicy.MaxBodyLength)).IsError);

        BoardComment deleted = BoardComment.Reconstitute(1, 1, 0, null, "Bob", "hi", DateTime.UtcNow, deleted: true);
        ErrorAssert.Is(deleted.SoftDelete(), "BoardComment.AlreadyDeleted", ErrorType.Conflict, "Comment is already deleted.");
    }

    // ---- Calendar reminders / other calendar --------------------------------------------------

    /// <summary>Verifies that subscribing to calendar ID 0 is refused and ID 1 is accepted.</summary>
    [Fact]
    public void OtherCalendar_Create_RejectsANonPositiveCalendarId()
    {
        ErrorAssert.Validation(OtherCalendar.Create(Email, 0), "OtherCalendar.InvalidCalendarId", "Calendar ID must be a positive integer.");
        Assert.False(OtherCalendar.Create(Email, 1).IsError);
    }

    // ---- Menu -----------------------------------------------------------------------------

    /// <summary>Verifies the empty-name errors of categories and sub-categories and the role / order errors of <c>MenuFieldPolicy</c>.</summary>
    [Fact]
    public void Menu_Errors()
    {
        ErrorAssert.Validation(Category.Create(" ", "d", "/i.png", "C", "A", Role.User, 0), "Category.NameEmpty", "Category name cannot be empty.");
        ErrorAssert.Validation(Category.Create("n", "d", "/i.png", "C", "A", Role.User, 0).Value.Update(" ", "d", "/i.png", "C", "A", Role.User, 0),
            "Category.NameEmpty", "Category name cannot be empty.");
        ErrorAssert.Validation(SubCategory.Create(1, " ", "d", "/i.png", "A", Role.User, 0), "SubCategory.NameEmpty", "Sub-category name cannot be empty.");
        ErrorAssert.Validation(SubCategory.Create(1, "n", "d", "/i.png", "A", Role.User, 0).Value.Update(1, " ", "d", "/i.png", "A", Role.User, 0),
            "SubCategory.NameEmpty", "Sub-category name cannot be empty.");
        ErrorAssert.Validation(MenuFieldPolicy.Validate("n", "d", "/i.png", "C", "A", "Root", 0), "Menu.RoleInvalid", "Role must be Admin, User or Anonymous.");
        ErrorAssert.Validation(MenuFieldPolicy.Validate("n", "d", "/i.png", "C", "A", Role.User, -1), "Menu.OrderNegative", "Order cannot be negative.");
    }

    // ---- Value objects ------------------------------------------------------------------------

    /// <summary>Verifies the empty errors of <c>HtmlColorCode</c>, <c>TimeZoneId</c> and <c>Email</c>, and the too-long email error.</summary>
    [Fact]
    public void ValueObject_EmptyErrors()
    {
        ErrorAssert.Validation(HtmlColorCode.Create(" "), "HtmlColorCode.Empty", "HTML color code cannot be empty.");
        ErrorAssert.Validation(TimeZoneId.Create(" "), "TimeZoneId.Empty", "Time-zone ID cannot be empty.");
        ErrorAssert.Validation(Domain.ValueObjects.Email.Create(" "), "Email.Empty", "Email address cannot be empty.");
        ErrorAssert.Validation(Domain.ValueObjects.Email.Create(new string('a', 250) + "@x.com"), "Email.TooLong", "Email address must be {0} characters or fewer.", 255);
    }

    // ---- Finance records ------------------------------------------------------------------------

    /// <summary>Verifies the empty main class / sub class / deposit asset / content errors of <c>Income.Record</c>.</summary>
    [Fact]
    public void Income_Record_Errors()
    {
        ErrorAssert.Validation(Income.Record(Email, " ", "LaborIncome", "c", 1m, "KRW", "a"), "Income.MainClassEmpty", "Main income category cannot be empty.");
        ErrorAssert.Validation(Income.Record(Email, "RegularIncome", " ", "c", 1m, "KRW", "a"), "Income.SubClassEmpty", "Sub income category cannot be empty.");
        ErrorAssert.Validation(Income.Record(Email, "RegularIncome", "LaborIncome", "c", 1m, "KRW", " "), "Income.DepositAssetEmpty", "Deposit asset product name cannot be empty.");
        ErrorAssert.Validation(Income.Record(Email, "RegularIncome", "LaborIncome", " ", 1m, "KRW", "a"), "Income.ContentEmpty", "Content cannot be empty.");
    }

    /// <summary>Verifies the empty main class / sub class / payment method / content errors of <c>Expenditure.Record</c>.</summary>
    [Fact]
    public void Expenditure_Record_Errors()
    {
        ErrorAssert.Validation(Expenditure.Record(Email, " ", "Deposit", "c", 1m, "KRW", "a", "b"), "Expenditure.MainClassEmpty", "Main expense category cannot be empty.");
        ErrorAssert.Validation(Expenditure.Record(Email, "RegularSavings", " ", "c", 1m, "KRW", "a", "b"), "Expenditure.SubClassEmpty", "Sub expense category cannot be empty.");
        ErrorAssert.Validation(Expenditure.Record(Email, "RegularSavings", "Deposit", "c", 1m, "KRW", " ", "b"), "Expenditure.PaymentMethodEmpty", "Payment method (asset name) cannot be empty.");
        ErrorAssert.Validation(Expenditure.Record(Email, "RegularSavings", "Deposit", " ", 1m, "KRW", "a", "b"), "Expenditure.ContentEmpty", "Content cannot be empty.");
    }
}
