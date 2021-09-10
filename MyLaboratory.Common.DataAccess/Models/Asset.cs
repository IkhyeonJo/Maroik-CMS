using System;
using System.Collections.Generic;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    public partial class Asset
    {
        public Asset()
        {
            Expenditures = new HashSet<Expenditure>();
            FixedExpenditures = new HashSet<FixedExpenditure>();
            FixedIncomes = new HashSet<FixedIncome>();
            Incomes = new HashSet<Income>();
        }

        public string ProductName { get; set; }
        public string AccountEmail { get; set; }
        public string Item { get; set; }
        public long Amount { get; set; }
        public string MonetaryUnit { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string Note { get; set; }
        public bool Deleted { get; set; }

        public virtual Account AccountEmailNavigation { get; set; }
        public virtual ICollection<Expenditure> Expenditures { get; set; }
        public virtual ICollection<FixedExpenditure> FixedExpenditures { get; set; }
        public virtual ICollection<FixedIncome> FixedIncomes { get; set; }
        public virtual ICollection<Income> Incomes { get; set; }
    }
}
