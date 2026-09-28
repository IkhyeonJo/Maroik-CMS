namespace Maroik.Website.Attributes;

/// <summary>
/// Marks an HTTP POST action method with the role that is permitted to invoke it.
/// Can be applied multiple times (AllowMultiple = true) to grant access to several roles.
/// The <see cref="Maroik.Website.Filters.AuthorizationFilter"/> reads these attributes to enforce role-based access.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RequiredHttpPostAccessAttribute : Attribute
{
    /// <summary>The role string (e.g. "Admin", "User") that is allowed to call the decorated action.</summary>
    public string Role { get; set; } = "";
}
