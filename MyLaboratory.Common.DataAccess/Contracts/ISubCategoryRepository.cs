using System.Collections.Generic;
using System.Threading.Tasks;
using MyLaboratory.Common.DataAccess.Models;

namespace MyLaboratory.Common.DataAccess.Contracts
{
    public interface ISubCategoryRepository
    {
        /// <summary>
        /// 권한에 따른 서브카테고리들을 구합니다.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public Task<IEnumerable<SubCategory>> GetSubCategoryByRoleAsync(string role);
        /// <summary>
        /// 서브카테고리를 생성합니다.
        /// </summary>
        /// <param name="subCategory"></param>
        /// <returns></returns>
        public Task CreateSubCategoryAsync(SubCategory subCategory);
        /// <summary>
        /// 서브카테고리를 업데이트합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public Task UpdateSubCategoryAsync(SubCategory subCategory);
        /// <summary>
        /// 서브카테고리를 제거합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public Task DeleteSubCategoryAsync(SubCategory subCategory);
    }
}