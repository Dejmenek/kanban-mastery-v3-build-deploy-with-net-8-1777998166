namespace Kanban.API.Models;

public class Board
{
    public int Id { get; set; }

    public ICollection<Column> Columns { get; set; } = [];
    public ICollection<BoardMember> Members { get; set; } = [];
}
