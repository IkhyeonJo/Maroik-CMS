using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.AccountBook
{
    public class IncomeInputViewModel
    {
        [Required(ErrorMessage = "Please enter Id")]
        [Display(Name = "Id")]
        public int Id { get; set; }
        [Required(ErrorMessage = "Please enter MainClass")]
        [Display(Name = "MainClass")]
        public string MainClass { get; set; }
        [Required(ErrorMessage = "Please enter SubClass")]
        [Display(Name = "SubClass")]
        public string SubClass { get; set; }
        [Required(ErrorMessage = "Please enter Content")]
        [Display(Name = "Content")]
        public string Content { get; set; }
        [Required(ErrorMessage = "Please enter Amount")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }
        public DateTime Created { get; set; }
        [Required(ErrorMessage = "Please enter DepositMyAssetProductName")]
        [Display(Name = "DepositMyAssetProductName")]
        public string DepositMyAssetProductName { get; set; }
        [Display(Name = "Note")]
        public string Note { get; set; }
    }
}