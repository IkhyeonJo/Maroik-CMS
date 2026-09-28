namespace Maroik.Core.Service.Extensions;

/// <summary>Helpers for classifying database exceptions without a hard dependency on Npgsql.</summary>
public static class DbExceptionExtensions
{
    /// <summary>PostgreSQL SQLSTATE for a unique / primary-key constraint violation.</summary>
    private const string UniqueViolationSqlState = "23505";

    /// <summary>Exact-match unique constraint on <c>Account.Nickname</c>.</summary>
    private const string AccountNicknameUniqueConstraint = "Account_Nickname_unique";

    /// <summary>Case-insensitive unique index on <c>lower(Account.Nickname)</c>.</summary>
    private const string AccountNicknameLowerUniqueIndex = "Account_unique_index_0";

    /// <summary>
    /// Fallback for an exception that lost its typed <c>SqlState</c>: Npgsql renders a
    /// <c>PostgresException</c>'s message as <c>"{SqlState}: {MessageText}"</c>, so the SQLSTATE is
    /// recognized only in that leading position. A bare substring test would also match any message
    /// that merely contains the digits — e.g. a foreign-key violation whose DETAIL echoes a key value
    /// (<c>Key (CalendarId)=(23505) is not present…</c>) — and misreport it as a unique violation.
    /// </summary>
    private static bool MessageStartsWithUniqueViolationSqlState(Exception e) =>
        e.Message.AsSpan().TrimStart().StartsWith(UniqueViolationSqlState + ":", StringComparison.Ordinal);

    extension(Exception e)
    {
        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="e"/> — or anything in its
        /// <see cref="Exception.InnerException"/> chain — is a PostgreSQL unique-constraint violation
        /// (SQLSTATE <c>23505</c>).
        /// <para>
        /// Primary check: Npgsql's <c>PostgresException</c> exposes the SQLSTATE as a <c>SqlState</c>
        /// (and <c>Code</c>) string property, read here reflectively so <c>Maroik.Core.Service</c> needs
        /// no Npgsql package reference. This is the structured code, not the culture- and
        /// version-dependent message text. As a fallback (wrapped / rethrown / logged exceptions that no
        /// longer carry the typed property) the SQLSTATE is also matched when a message in the chain
        /// starts with it, in Npgsql's <c>"23505: …"</c> format — not as an arbitrary substring, which
        /// would misclassify e.g. a foreign-key violation that merely echoes a key value of 23505.
        /// </para>
        /// </summary>
        public bool IsPostgresUniqueViolation()
        {
            for (Exception? current = e; current is not null; current = current.InnerException)
            {
                var type = current.GetType();
                var sqlState =
                    type.GetProperty("SqlState")?.GetValue(current) as string
                    ?? type.GetProperty("Code")?.GetValue(current) as string;

                if (sqlState == UniqueViolationSqlState)
                    return true;

                if (MessageStartsWithUniqueViolationSqlState(current))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="e"/> — or anything in its
        /// <see cref="Exception.InnerException"/> chain — is a PostgreSQL unique-constraint violation
        /// (SQLSTATE <c>23505</c>) specifically on the constraint named <paramref name="constraintName"/>.
        /// <para>
        /// A table can carry more than one unique constraint that all raise the same SQLSTATE (e.g. a
        /// surrogate-key primary key alongside a separate business-key uniqueness constraint), so
        /// <see cref="IsPostgresUniqueViolation"/> alone cannot tell them apart. This checks Npgsql's
        /// <c>PostgresException.ConstraintName</c> (read reflectively, for the same no-hard-dependency
        /// reason as above), falling back to matching the constraint name as a substring of the
        /// exception message for exceptions that no longer carry the typed property.
        /// </para>
        /// </summary>
        public bool IsPostgresUniqueViolationOn(string constraintName)
        {
            for (Exception? current = e; current is not null; current = current.InnerException)
            {
                var type = current.GetType();
                var sqlState =
                    type.GetProperty("SqlState")?.GetValue(current) as string
                    ?? type.GetProperty("Code")?.GetValue(current) as string;

                bool isUniqueViolation = sqlState == UniqueViolationSqlState
                                         || MessageStartsWithUniqueViolationSqlState(current);
                if (!isUniqueViolation)
                    continue;

                var constraint = type.GetProperty("ConstraintName")?.GetValue(current) as string;
                if (constraint == constraintName)
                    return true;
                if (constraint is null && current.Message.Contains(constraintName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="e"/> is a unique violation on
        /// <c>Account.Nickname</c>: either the exact-match constraint <c>Account_Nickname_unique</c> or
        /// the case-insensitive <c>Account_unique_index_0</c> index (so "bob" racing "Bob" is
        /// reported as a nickname conflict, not blamed on the email).
        /// </summary>
        public bool IsAccountNicknameUniqueViolation() =>
            e.IsPostgresUniqueViolationOn(AccountNicknameUniqueConstraint)
            || e.IsPostgresUniqueViolationOn(AccountNicknameLowerUniqueIndex);
    }
}
