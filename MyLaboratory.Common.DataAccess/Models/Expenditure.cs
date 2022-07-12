using System;
using System.Collections.Generic;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    /// <summary>
    /// MyLaboratory.WebSite 지출
    /// </summary>
    public partial class Expenditure
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
        /// 대분류 (정기저축/비소비지출/소비지출)
        /// </summary>
        public string MainClass { get; set; }
        /// <summary>
        /// 소분류 (예적금/내자산이체/투자 | 공적연금/부채상환/세금/사회보험/가구간 이전지출/비영리단체 이전 | (식비/외식비)/(주거/용품비)/교육비/의료비/교통비/통신비/(여가/문화)/(의류/신발)/용돈/보장성보험/기타지출/미파악지출)
        /// </summary>
        public string SubClass { get; set; }
        /// <summary>
        /// 내용 (A마트/B카드/C음식점/D도서관)
        /// </summary>
        public string Contents { get; set; }
        /// <summary>
        /// 금액
        /// </summary>
        public long Amount { get; set; }
        /// <summary>
        /// 결제 수단 (자산 상품명/현금)
        /// </summary>
        public string PaymentMethod { get; set; }
        /// <summary>
        /// 내 입금 자산 (자산 상품명/현금) (지출 중 [예적금, 내자산이체, 투자, 공적연금, 부채상환]일 때 사용)
        /// </summary>
        public string MyDepositAsset { get; set; }
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

        public virtual Asset Asset { get; set; }
    }
}
