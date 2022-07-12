using System;
using System.Collections.Generic;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    /// <summary>
    /// MyLaboratory.WebSite 자산
    /// </summary>
    public partial class Asset
    {
        public Asset()
        {
            Expenditures = new HashSet<Expenditure>();
            FixedExpenditures = new HashSet<FixedExpenditure>();
            FixedIncomes = new HashSet<FixedIncome>();
            Incomes = new HashSet<Income>();
        }

        /// <summary>
        /// 상품명 (은행 계좌명, 증권 계좌명, 현금 등)
        /// </summary>
        public string ProductName { get; set; }
        /// <summary>
        /// 계정 이메일 (ID)
        /// </summary>
        public string AccountEmail { get; set; }
        /// <summary>
        /// 항목 (자유입출금 자산, 신탁 자산, 현금 자산, 저축성 자산, 투자성 자산, 부동산, 동산, 기타 실물 자산, 보험 자산)
        /// </summary>
        public string Item { get; set; }
        /// <summary>
        /// 금액
        /// </summary>
        public long Amount { get; set; }
        /// <summary>
        /// 화폐 단위 (KRW, USD, ETC)
        /// </summary>
        public string MonetaryUnit { get; set; }
        /// <summary>
        /// 생성일
        /// </summary>
        public DateTime Created { get; set; }
        /// <summary>
        /// 업데이트일
        /// </summary>
        public DateTime Updated { get; set; }
        /// <summary>
        /// 비고
        /// </summary>
        public string Note { get; set; }
        /// <summary>
        /// 삭제여부
        /// </summary>
        public bool Deleted { get; set; }

        public virtual Account AccountEmailNavigation { get; set; }
        public virtual ICollection<Expenditure> Expenditures { get; set; }
        public virtual ICollection<FixedExpenditure> FixedExpenditures { get; set; }
        public virtual ICollection<FixedIncome> FixedIncomes { get; set; }
        public virtual ICollection<Income> Incomes { get; set; }
    }
}
