using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    public partial class Category
    {
        public Category()
        {
            SubCategories = new HashSet<SubCategory>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconPath { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public string Role { get; set; }
        public int Order { get; set; }

        public virtual ICollection<SubCategory> SubCategories { get; set; }
    }
}
