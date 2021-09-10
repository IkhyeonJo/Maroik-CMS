using System.Collections.Generic;
using System.Threading.Tasks;
using MyLaboratory.Common.DataAccess.Models;

namespace MyLaboratory.Common.DataAccess.Contracts
{
    public interface ICategoryRepository
    {
        /// <summary>
        /// 권한에 따른 카테고리들을 구합니다.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public Task<IEnumerable<Category>> GetCategoryByRoleAsync(string role);
        /// <summary>
        /// 카테고리를 생성합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public Task CreateCategoryAsync(Category category);
        /// <summary>
        /// 카테고리를 업데이트합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public Task UpdateCategoryAsync(Category category);
        /// <summary>
        /// 카테고리를 제거합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public Task DeleteCategoryAsync(Category category);
    }
}