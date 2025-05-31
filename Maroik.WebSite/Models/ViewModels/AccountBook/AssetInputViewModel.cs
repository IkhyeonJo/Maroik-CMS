﻿using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.AccountBook
{
    public class AssetInputViewModel
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
        [Display(Name = "Note")]
        public string Note { get; set; }
        [Display(Name = "Deleted")]
        public bool Deleted { get; set; }
        public string OriginalProductName { get; set; }
    }
}