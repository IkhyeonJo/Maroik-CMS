using System;
using System.Collections.Generic;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    public partial class Income
    {
        public int Id { get; set; }
        public string AccountEmail { get; set; }
        public string MainClass { get; set; }
        public string SubClass { get; set; }
        public string Contents { get; set; }
        public long Amount { get; set; }
        public string DepositMyAssetProductName { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string Note { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
