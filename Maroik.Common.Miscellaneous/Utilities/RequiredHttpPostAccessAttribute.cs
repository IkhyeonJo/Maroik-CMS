namespace Maroik.Common.Miscellaneous.Utilities
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RequiredHttpPostAccessAttribute : Attribute
    {
        public string Role { get; set; } = string.Empty;
    }
}
