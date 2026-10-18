using System.ComponentModel;
using System.Reflection;

namespace Maroik.Core.Contract.Extensions;

/// <summary>
/// Extension methods for retrieving the <see cref="System.ComponentModel.DescriptionAttribute"/>
/// value from an enum member at runtime.
/// </summary>
public static class EnumExtensions
{
    extension(Enum en)
    {
        /// <summary>
        /// Returns the <see cref="System.ComponentModel.DescriptionAttribute"/> string of this enum value.
        /// Falls back to <c>ToString()</c> when no attribute is present.
        /// </summary>
        /// <returns>The description string, or the enum member name if no attribute exists.</returns>
        public string GetDescription()
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
}
