using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface IBoardRepository
    {
        /// <summary>
        /// 한 종류의 모든 게시물을 구합니다.
        /// </summary>
        /// <returns></returns>
        public Task<IEnumerable<Board>> GetOneTypeBoardsAsync(string type);

        /// <summary>
        /// 게시물을 생성합니다.
        /// </summary>
        /// <param name="board"></param>
        /// <returns></returns>
        public Task WriteBoardAsync(Board board);

        /// <summary>
        /// 계시물을 업데이트합니다.
        /// </summary>
        /// <param name="board"></param>
        /// <returns></returns>
        public Task UpdateBoardAsync(Board board);

        /// <summary>
        /// 계시물을 삭제합니다.
        /// </summary>
        /// <param name="board"></param>
        /// <returns></returns>
        public Task DeleteBoardAsync(Board board);

    }
}