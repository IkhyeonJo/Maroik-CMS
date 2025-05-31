using Maroik.Common.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Contracts
{
    public interface IBoardAttachedFileRepository
    {
        /// <summary>
        /// 등록된 모든 파일을 구합니다.
        /// </summary>
        /// <returns></returns>
        public Task<List<BoardAttachedFile>> GetAllAsync();

        /// <summary>
        /// 첨부파일을 저장합니다.
        /// </summary>
        /// <param name="boardAttachedFile"></param>
        /// <returns></returns>
        public Task SaveBoardAttachedFileAsync(BoardAttachedFile boardAttachedFile);

        /// <summary>
        /// 첨부파일을 업데이트합니다.
        /// </summary>
        /// <param name="boardAttachedFile"></param>
        /// <returns></returns>
        public Task UpdateBoardAttachedFileAsync(BoardAttachedFile boardAttachedFile);
    }
}