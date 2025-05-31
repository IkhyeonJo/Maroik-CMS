﻿using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.AccountBook
{
    public class AssetOutputViewModel
    {
        [Required(ErrorMessage = "Please enter ProductName")]
        [Display(Name = "ProductName")]
        public string ProductName { get; set; }
        [Required(ErrorMessage = "Please enter Item")]
        [Display(Name = "Item")]
        public string Item { get; set; }
        [Required(ErrorMessage = "Please enter Amount")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }
        [Required(ErrorMessage = "Please enter MonetaryUnit")]
        [Display(Name = "MonetaryUnit")]
        public string MonetaryUnit { get; set; }
        [Required(ErrorMessage = "Please enter Created")]
        [Display(Name = "Created")]
        public DateTime Created { get; set; }
        [Required(ErrorMessage = "Please enter Updated")]
        [Display(Name = "Updated")]
        public DateTime Updated { get; set; }
        [Required(ErrorMessage = "Please enter Note")]
        [Display(Name = "Note")]
        public string Note { get; set; }
        [Required(ErrorMessage = "Please enter Deleted")]
        [Display(Name = "Deleted")]
        public bool Deleted { get; set; }
    }
}