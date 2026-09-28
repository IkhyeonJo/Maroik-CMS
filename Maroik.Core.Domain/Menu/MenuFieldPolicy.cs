using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Menu;

/// <summary>
/// The persisted shape shared by <see cref="Category"/> and <see cref="SubCategory"/>: every text
/// column is <c>character varying(255)</c>, <c>Role</c> is restricted by the DB CHECK
/// (<c>Category_Role_check</c> / <c>SubCategory_Role_check</c>) and <c>Order</c> by
/// <c>*_Order_check</c> (<c>&gt;= 0</c>). Mirrored here so an invalid value is a clean validation error
/// instead of a raw database exception (SQLSTATE 22001 / 23514).
/// <para>
/// Blank values are still accepted where they were before (the repository stores a missing text as
/// <c>""</c> and a missing <c>Role</c> as <c>Admin</c> — the most restrictive role, so a menu item
/// with no role is never exposed by accident); only values the database would refuse are rejected.
/// </para>
/// </summary>
public static class MenuFieldPolicy
{
    /// <summary>Role a menu item is restricted to (the values <c>Role</c> may hold in the database).</summary>
    public static class Roles
    {
        /// <summary>Visible to administrators.</summary>
        private const string Admin = "Admin";

        /// <summary>Visible to signed-in users.</summary>
        private const string User = "User";

        /// <summary>Visible to signed-out visitors.</summary>
        private const string Anonymous = "Anonymous";

        private static readonly StringTaxonomy _taxonomy = new(Admin, User, Anonymous);

        /// <summary>Every role a menu item may carry.</summary>
        public static IReadOnlySet<string> All => _taxonomy.All;

        /// <summary>Returns true when <paramref name="role"/> is one of the known menu roles.</summary>
        public static bool IsKnown(string? role) => _taxonomy.IsKnown(role);
    }

    /// <summary>
    /// Validates the length of each text field, the role (when given) and the order. The caller has
    /// already checked <c>Name</c> for emptiness with its own, type-specific error.
    /// </summary>
    public static ErrorOr<Success> Validate(
        string? name, string? displayName, string? iconPath, string? controller, string? action,
        string? role, long order)
    {
        (string Field, string? Value)[] texts =
        [
            ("Name", name), ("DisplayName", displayName), ("IconPath", iconPath),
            ("Controller", controller), ("Action", action)
        ];

        foreach ((string field, string? value) in texts)
        {
            if (value is { Length: > ShortTextPolicy.MaxLength })
                return LocalizableError.Validation("Menu.FieldTooLong", "'{0}' must be {1} characters or fewer.", field, ShortTextPolicy.MaxLength);
        }

        if (role != null && !Roles.IsKnown(role))
            return LocalizableError.Validation("Menu.RoleInvalid", "Role must be Admin, User or Anonymous.");

        if (order < 0)
            return LocalizableError.Validation("Menu.OrderNegative", "Order cannot be negative.");

        return Result.Success;
    }
}
