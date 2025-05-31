using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maroik.Common.DataAccess.Services
{
    public class BoardCommentRepository : IBoardCommentRepository
    {
        private readonly ApplicationDbContext _context;

        public BoardCommentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 한 게시물의 모든 댓글을 구합니다.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<BoardComment>> GetOneBoardCommentsAsync(long boardId)
        {
            return await _context.BoardComments.Where(x => x.BoardId == boardId).OrderBy(m => m.Order).ToListAsync();
        }

        /// <summary>
        /// 한 댓글을 구합니다.
        /// </summary>
        /// <returns></returns>
        public async Task<BoardComment> GetBoardCommentAsync(int id)
        {
            return await _context.BoardComments.Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 댓글을 생성합니다.
        /// </summary>
        /// <param name="boardComment"></param>
        /// <returns></returns>
        public async Task WriteBoardCommentAsync(BoardComment boardComment)
        {
            _ = await _context.BoardComments.AddAsync(boardComment);
            _ = await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 댓글을 삭제합니다.
        /// </summary>
        /// <param name="boardComment"></param>
        /// <returns></returns>
        public async Task DeleteBoardCommentAsync(BoardComment boardComment)
        {
            _ = _context.BoardComments.Update(boardComment);
            _ = await _context.SaveChangesAsync();
        }

    }
}