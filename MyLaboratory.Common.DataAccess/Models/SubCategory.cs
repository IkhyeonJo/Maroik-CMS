using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MyLaboratory.Common.DataAccess.Models
{
    /// <summary>
    /// 서브 카테고리 /*MyLaboratory.WebSite의 로그인 후 접근 가능한 좌측 SideBar 설정 시 사용*/
    /// </summary>
    public partial class SubCategory
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 부모 카테고리 ID
        /// </summary>
        public int CategoryId { get; set; }
        /// <summary>
        /// 이름
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 표시이름
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// 표시 아이콘 경로 /*FontAwesome 사용*/
        /// </summary>
        public string IconPath { get; set; }
        /// <summary>
        /// 접근 MVC Action 명 /*이 값이 없으면 하위 카테고리 존재*/
        /// </summary>
        public string Action { get; set; }
        /// <summary>
        /// 접근 권한 설정
        /// </summary>
        public string Role { get; set; }
        /// <summary>
        /// 출력 순서
        /// </summary>
        public int Order { get; set; }

        public virtual Category Category { get; set; }
    }
}
