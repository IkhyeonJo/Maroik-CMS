using Maroik.Common.DataAccess.Models;

namespace Maroik.WebSite.Models.ViewModels.DashBoard
{
    public class UserIndexOutputViewModel
    {
        public List<IncomeOutputViewModel> IncomeYearOutputViewModels { get; set; } = [];
        public List<ExpenditureOutputViewModel> ExpenditureYearOutputViewModels { get; set; } = [];
        public List<IncomeOutputViewModel> IncomeYearMonthOutputViewModels { get; set; } = [];
        public List<ExpenditureOutputViewModel> ExpenditureYearMonthOutputViewModels { get; set; } = [];
        public IEnumerable<string> MonetaryUnits { get; set; } = [];
        public string DefaultMonetaryUnit { get; set; }
    }
}