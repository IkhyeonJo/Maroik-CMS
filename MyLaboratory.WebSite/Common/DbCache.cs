using Microsoft.EntityFrameworkCore;
using MyLaboratory.WebSite.Models;
using System.Collections.Generic;
using MyLaboratory.Common.DataAccess.Models;

namespace MyLaboratory.WebSite.Common
{
    public static class DbCache
    {
        public static IEnumerable<Category> AdminCategories = null;
        public static IEnumerable<SubCategory> AdminSubCategories = null;

        public static IEnumerable<Category> UserCategories = null;
        public static IEnumerable<SubCategory> UserSubCategories = null;
    }
}