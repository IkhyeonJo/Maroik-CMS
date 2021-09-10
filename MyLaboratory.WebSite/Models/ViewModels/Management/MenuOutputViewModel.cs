using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace MyLaboratory.WebSite.Models.ViewModels.Management
{
    public class MenuOutputViewModel
    {
        [Required(ErrorMessage = "Please enter Id")]
        [Display(Name = "Id")]
        public int Id { get; set; }
        [Required(ErrorMessage = "Please enter CategoryId")]
        [Display(Name = "CategoryId")]
        public int? CategoryId { get; set; }
        [Required(ErrorMessage = "Please enter Name")]
        [Display(Name = "Name")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Please enter DisplayName")]
        [Display(Name = "DisplayName")]
        public string DisplayName { get; set; }
        [Required(ErrorMessage = "Please enter IconPath")]
        [Display(Name = "IconPath")]
        public string IconPath { get; set; }
        [Required(ErrorMessage = "Please enter Controller")]
        [Display(Name = "Controller")]
        public string Controller { get; set; }
        [Required(ErrorMessage = "Please enter Action")]
        [Display(Name = "Action")]
        public string Action { get; set; }
        [Required(ErrorMessage = "Please enter Role")]
        [Display(Name = "Role")]
        public string Role { get; set; }
        [Required(ErrorMessage = "Please enter Order")]
        [Display(Name = "Order")]
        public int Order { get; set; }
    }
}