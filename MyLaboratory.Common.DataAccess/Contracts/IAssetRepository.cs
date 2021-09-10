using System.Collections.Generic;
using System.Threading.Tasks;
using MyLaboratory.Common.DataAccess.Models;

namespace MyLaboratory.Common.DataAccess.Contracts
{
    public interface IAssetRepository
    {
        /// <summary>
        /// 로그인 계정에 해당하는 모든 자산을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public Task<List<Asset>> GetAssetsAsync(string email);

        /// <summary>
        /// 로그인 계정에 해당하는 특정 자산을 구합니다.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="productName"></param>
        /// <returns></returns>
        public Task<Asset> GetAssetAsync(string email, string productName);

        /// <summary>
        /// 자산을 생성합니다.
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public Task CreateAssetAsync(Asset asset);

        /// <summary>
        /// 자산을 업데이트합니다.
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public Task UpdateAssetAsync(Asset asset);
    }
}