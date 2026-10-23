using System.Globalization;
using Maroik.Core.Contract.Dtos;
using Maroik.Website.Models.ViewModels.Kanbans;

namespace Maroik.Website.Mappers;

/// <summary>
/// Maps the service's board (<see cref="KanbanLaneResponse"/> list) to the view model <c>Kanban/UserIndex</c> renders. The lane
/// order, the card order and the overdue flag are already decided by the domain; this only shapes them for display.
/// </summary>
public static class KanbanViewModelMapper
{
    /// <summary>The one format a card's due date is shown in, for every culture (K10).</summary>
    private const string DueDateFormat = "yyyy-MM-dd";

    extension(List<KanbanLaneResponse> board)
    {
        /// <summary>The board as the page renders it: the lanes in order, each card's due date as a Gregorian yyyy-MM-dd string.</summary>
        public KanbanBoardViewModel ToViewModel() => new()
        {
            Lanes =
            [
                .. board.Select(lane => new KanbanLaneViewModel
                {
                    Lane = lane.Lane ?? "",
                    Cards =
                    [
                        .. lane.Cards.Select(card => new KanbanCardOutputViewModel
                        {
                            Id = card.Id,
                            Title = card.Title,
                            Description = card.Description,
                            DueDate = card.DueDate?.ToString(DueDateFormat, CultureInfo.InvariantCulture),
                            Overdue = card.Overdue
                        })
                    ]
                })
            ]
        };
    }
}
