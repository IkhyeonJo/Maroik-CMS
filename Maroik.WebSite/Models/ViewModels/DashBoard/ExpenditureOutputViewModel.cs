using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.DashBoard
{
    public class ExpenditureOutputViewModel
    {
        [Required(ErrorMessage = "Please enter Id")]
        [Display(Name = "Id")]
        public long Id { get; set; }
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
        [Display(Name = "MonetaryUnit")]
        public string MonetaryUnit { get; set; }
        [Required(ErrorMessage = "Please enter PaymentMethod")]
        [Display(Name = "PaymentMethod")]
        public string PaymentMethod { get; set; }
        [Display(Name = "Note")]
        public string Note { get; set; }
        [Required(ErrorMessage = "Please enter MyDepositAsset")]
        [Display(Name = "MyDepositAsset")]
        public string MyDepositAsset { get; set; }
        [Required(ErrorMessage = "Please enter Created")]
        [Display(Name = "Created")]
        public DateTime Created { get; set; }
        [Required(ErrorMessage = "Please enter Updated")]
        [Display(Name = "Updated")]
        public DateTime Updated { get; set; }
    }
}