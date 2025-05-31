using Maroik.Common.DataAccess.Models;

namespace Maroik.WebSite.Utilities
{
    public static class DbCache
    {
        public static IEnumerable<Category> AdminCategories { get; set; } = null;
        public static IEnumerable<SubCategory> AdminSubCategories { get; set; } = null;

        public static IEnumerable<Category> UserCategories { get; set; } = null;
        public static IEnumerable<SubCategory> UserSubCategories { get; set; } = null;

        public static IEnumerable<Category> AnonymousCategories { get; set; } = null;
        public static IEnumerable<SubCategory> AnonymousSubCategories { get; set; } = null;
    }
}