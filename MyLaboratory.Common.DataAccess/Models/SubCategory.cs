using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    public partial class SubCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string IconPath { get; set; }
        public string Action { get; set; }
        public string Role { get; set; }
        public int Order { get; set; }

        public virtual Category Category { get; set; }
    }
}
