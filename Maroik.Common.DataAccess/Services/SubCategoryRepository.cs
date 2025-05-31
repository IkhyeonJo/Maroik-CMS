using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class SubCategoryRepository : ISubCategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public SubCategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 모든 서브카테고리들을 구합니다.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SubCategory> GetSubCategory()
        {
            return _context.SubCategories.ToList();
        }
        /// <summary>
        /// 권한에 따른 서브카테고리들을 구합니다.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<IEnumerable<SubCategory>> GetSubCategoryByRoleAsync(string role)
        {
            return await _context.SubCategories
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
            _ = await _context.SubCategories.AddAsync(subCategory);
            _ = await _context.SaveChangesAsync();
        }
        /// <summary>
        /// 서브카테고리를 업데이트합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task UpdateSubCategoryAsync(SubCategory subCategory)
        {
            _ = _context.SubCategories.Update(subCategory);
            _ = await _context.SaveChangesAsync();
        }
        /// <summary>
        /// 서브카테고리를 제거합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task DeleteSubCategoryAsync(SubCategory subCategory)
        {
            _ = _context.SubCategories.Remove(subCategory);
            _ = await _context.SaveChangesAsync();
        }
    }
}