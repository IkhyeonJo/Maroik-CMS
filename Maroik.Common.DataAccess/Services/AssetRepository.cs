using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class AssetRepository : IAssetRepository
    {
        private readonly ApplicationDbContext _context;

        public AssetRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 로그인 계정에 해당하는 모든 자산을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<List<Asset>> GetAssetsAsync(string email)
        {
            return await _context.Assets.Where(x => x.AccountEmail == email).ToListAsync();
        }

        /// <summary>
        /// 로그인 계정에 해당하는 특정 자산을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="productName"></param>
        /// <returns></returns>
        public async Task<Asset> GetAssetAsync(string email, string productName)
        {
            return await _context.Assets.FirstOrDefaultAsync(a => a.AccountEmail == email && a.ProductName == productName);
        }

        /// <summary>
        /// 자산을 생성합니다.
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public async Task CreateAssetAsync(Asset asset)
        {
            _ = await _context.Assets.AddAsync(asset);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 자산을 업데이트합니다.
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public async Task UpdateAssetAsync(Asset asset)
        {
            _ = _context.Assets.Update(asset);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 자산명을 포함하여 자산을 업데이트합니다.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="originalProductName"></param>
        /// <returns></returns>
        public async Task<int> UpdateAssetWithProductNameAsync(Asset asset, string originalProductName)
        {
            return await _context.Database.ExecuteSqlInterpolatedAsync
            (
                $"""
                 UPDATE "Asset"
                 SET 
                     "ProductName" = {asset.ProductName},
                     "Item" = {asset.Item},
                     "Amount" = {asset.Amount},
                     "MonetaryUnit" = {asset.MonetaryUnit},
                     "Note" = {asset.Note},
                     "Deleted" = {asset.Deleted},
                     "Updated" = {asset.Updated}
                 WHERE 
                     "ProductName" = {originalProductName}
                     AND "AccountEmail" = {asset.AccountEmail}
                 """
            );
        }
    }
}