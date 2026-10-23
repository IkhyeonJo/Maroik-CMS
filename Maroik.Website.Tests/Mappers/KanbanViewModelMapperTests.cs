using Maroik.Core.Contract.Dtos;
using Maroik.Website.Mappers;
using Maroik.Website.Models.ViewModels.Kanbans;

namespace Maroik.Website.Tests.Mappers;

/// <summary>Unit tests for <see cref="KanbanViewModelMapper"/>: the service's board becomes the page's view model unchanged in order.</summary>
public class KanbanViewModelMapperTests
{
    /// <summary>Every lane is kept in the service's order, and every card keeps its fields; the due date becomes yyyy-MM-dd.</summary>
    [Fact]
    public void ToViewModel_KeepsTheLanesAndCards_InOrder()
    {
        List<KanbanLaneResponse> board =
        [
            new() { Lane = "Backlog", Cards = [] },
            new()
            {
                Lane = "ToDo",
                Cards =
                [
                    new() { Id = 3, Lane = "ToDo", Title = "First", Description = "a\nb", DueDate = new DateOnly(2026, 5, 28), Overdue = true },
                    new() { Id = 1, Lane = "ToDo", Title = "Second" },
                ],
            },
        ];

        KanbanBoardViewModel vm = board.ToViewModel();

        Assert.Equal(["Backlog", "ToDo"], vm.Lanes.Select(l => l.Lane));
        Assert.Empty(vm.Lanes[0].Cards);
        Assert.Equal([3L, 1L], vm.Lanes[1].Cards.Select(c => c.Id));
        KanbanCardOutputViewModel first = vm.Lanes[1].Cards[0];
        Assert.Equal("First", first.Title);
        Assert.Equal("a\nb", first.Description);
        Assert.Equal("2026-05-28", first.DueDate);
        Assert.True(first.Overdue);
        KanbanCardOutputViewModel second = vm.Lanes[1].Cards[1];
        Assert.Null(second.Description);
        Assert.Null(second.DueDate);
        Assert.False(second.Overdue);
    }
}
