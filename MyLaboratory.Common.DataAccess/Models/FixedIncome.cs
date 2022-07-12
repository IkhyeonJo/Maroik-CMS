using System;
using System.Collections.Generic;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    /// <summary>
    /// MyLaboratory.WebSite 고정수입
    /// </summary>
    public partial class FixedIncome
    {
        /// <summary>
        /// PK
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 계정 이메일 (ID)
        /// </summary>
        public string AccountEmail { get; set; }
        /// <summary>
        /// 대분류 (정기수입/비정기수입)
        /// </summary>
        public string MainClass { get; set; }
        /// <summary>
        /// 소분류 (근로수입/사업수입/연금수입/금융소득/임대수입/기타수입)
        /// </summary>
        public string SubClass { get; set; }
        /// <summary>
        /// 내용 (회사명/사업명)
        /// </summary>
        public string Contents { get; set; }
        /// <summary>
        /// 금액
        /// </summary>
        public long Amount { get; set; }
        /// <summary>
        /// 입금 자산 (자산 상품명/현금)
        /// </summary>
        public string DepositMyAssetProductName { get; set; }
        /// <summary>
        /// 입금월
        /// </summary>
        public byte DepositMonth { get; set; }
        /// <summary>
        /// 입금일
        /// </summary>
        public byte DepositDay { get; set; }
        /// <summary>
        /// 만기일
        /// </summary>
        public DateTime MaturityDate { get; set; }
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
        /// 시간 미엄수 (고정 수입 시간 약속을 지키지 않았을 때 계속 알림에 표시하는 용도)
        /// </summary>
        public bool Unpunctuality { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
