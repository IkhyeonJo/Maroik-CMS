using System.ComponentModel;
using System.Reflection;

namespace Maroik.Core.Contract.Misc.Helpers;

/// <summary>
/// Utility class for retrieving the <see cref="System.ComponentModel.DescriptionAttribute"/>
/// value from an enum member at runtime.
/// </summary>
public static class EnumHelper
{
    /// <summary>
    /// Returns the <see cref="System.ComponentModel.DescriptionAttribute"/> string of the given enum value.
    /// Falls back to <c>en.ToString()</c> when no attribute is present.
    /// </summary>
    /// <param name="en">The enum value to inspect.</param>
    /// <returns>The description string, or the enum member name if no attribute exists.</returns>
    public static string GetDescription(Enum en)
    {
        Type type = en.GetType();

        // Retrieve the reflected member info for the enum value name
        MemberInfo[] memInfo = type.GetMember(en.ToString());

        if (memInfo.Length <= 0)
        {
            return en.ToString();
        }

        // Look for a [Description("...")] attribute on the member
        object[] attrs = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

        return attrs.Length > 0 ? ((DescriptionAttribute)attrs[0]).Description :
            // No [Description] attribute found — return the member name as-is
            en.ToString();
    }
}
