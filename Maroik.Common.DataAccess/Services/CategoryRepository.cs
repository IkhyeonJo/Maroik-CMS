using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public CategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 모든 카테고리들을 구합니다.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Category> GetCategory()
        {
            return _context.Categories.ToList();
        }
        /// <summary>
        /// 권한에 따른 카테고리들을 구합니다.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Category>> GetCategoryByRoleAsync(string role)
        {
            return await _context.Categories
                    .Where(a => a.Role == role)
                    .OrderBy(a => a.Order)
                    .ToListAsync();
        }
        /// <summary>
        /// 카테고리를 생성합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task CreateCategoryAsync(Category category)
        {
            _ = await _context.Categories.AddAsync(category);
            _ = await _context.SaveChangesAsync();
        }
        /// <summary>
        /// 카테고리를 업데이트합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task UpdateCategoryAsync(Category category)
        {
            _ = _context.Categories.Update(category);
            _ = await _context.SaveChangesAsync();
        }
        /// <summary>
        /// 카테고리를 제거합니다.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public async Task DeleteCategoryAsync(Category category)
        {
            _ = _context.Categories.Remove(category);
            _ = await _context.SaveChangesAsync();
        }
    }
}