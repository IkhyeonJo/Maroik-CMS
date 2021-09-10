using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MyLaboratory.Common.DataAccess.Data;
using MyLaboratory.Common.DataAccess.Models;
using MyLaboratory.Common.DataAccess.Contracts;
using System.Threading.Tasks;

namespace MyLaboratory.Common.DataAccess.Services
{
    public class SubCategoryRepository : ISubCategoryRepository
    {
        private readonly ApplicationDbContext context;

        public SubCategoryRepository(ApplicationDbContext context)
        {
            this.context = context;
        }
        /// <summary>
        /// 권한에 따른 서브카테고리들을 구합니다.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<IEnumerable<SubCategory>> GetSubCategoryByRoleAsync(string role)
        {
            return await context.SubCategories
                    .Where(a => a.Role == role)
                    .OrderBy(a => a.Order)
                    .ToListAsync();
        }
        /// <summary>
        /// 서브카테고리를 생성합니다.
        /// </summary>
        /// <param name="subCategory"></param>
        /// <returns></returns>
        public async Task CreateSubCategoryAsync(SubCategory subCategory)
        {
            await context.SubCategories.AddAsync(subCategory);
            await context.SaveChangesAsync();
        }
        /// <summary>
        /// 서브카테고리를 업데이트합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task UpdateSubCategoryAsync(SubCategory subCategory)
        {
            context.SubCategories.Update(subCategory);
            await context.SaveChangesAsync();
        }
        /// <summary>
        /// 서브카테고리를 제거합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task DeleteSubCategoryAsync(SubCategory subCategory)
        {
            context.SubCategories.Remove(subCategory);
            await context.SaveChangesAsync();
        }
    }
}