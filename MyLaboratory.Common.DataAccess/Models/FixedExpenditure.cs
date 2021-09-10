using System;
using System.Collections.Generic;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    public partial class FixedExpenditure
    {
        public int Id { get; set; }
        public string AccountEmail { get; set; }
        public string MainClass { get; set; }
        public string SubClass { get; set; }
        public string Contents { get; set; }
        public long Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string MyDepositAsset { get; set; }
        public byte DepositMonth { get; set; }
        public byte DepositDay { get; set; }
        public DateTime MaturityDate { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string Note { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
