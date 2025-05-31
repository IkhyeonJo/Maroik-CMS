using Maroik.Common.DataAccess.Models;

namespace Maroik.WebSite.Models.ViewModels.Management
{
    public class PrivateNoteOutputViewModel
    {
        public string Method { get; set; }

        public Pager Pager { get; set; }

        public IEnumerable<Board> NoticeBoards { get; set; } = [];

        public IEnumerable<Board> Boards { get; set; } = [];

        public BoardOutputViewModel BoardOutputViewModel { get; set; }

        public int DetailBoardId { get; set; }

        public int DetailCurrentPage { get; set; }

        public int EditBoardId { get; set; }

        public int EditCurrentPage { get; set; }
    }
}