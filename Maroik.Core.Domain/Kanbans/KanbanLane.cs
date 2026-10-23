namespace Maroik.Core.Domain.Kanbans;

/// <summary>
/// The four fixed lanes of a Kanban board, declared in their display order (K5). The member names are the values stored in
/// <c>KanbanCard.Lane</c> (mirrored by the DB's <c>KanbanCard_Lane_check</c>) and the resx keys their localized labels are
/// looked up under; only the label is translated. The lanes cannot be added, renamed, deleted or reordered.
/// </summary>
public enum KanbanLane
{
    /// <summary>Work kept for later (en-US "Backlog", ko-KR "나중에").</summary>
    Backlog,

    /// <summary>Work planned next (en-US "To do", ko-KR "예정").</summary>
    ToDo,

    /// <summary>Work under way (en-US "In progress", ko-KR "진행 중").</summary>
    InProgress,

    /// <summary>Finished work (en-US "Done", ko-KR "완료").</summary>
    Done
}
