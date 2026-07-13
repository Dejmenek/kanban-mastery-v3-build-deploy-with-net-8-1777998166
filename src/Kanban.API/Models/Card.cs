namespace Kanban.API.Models;

public class Card
{
    public int Id { get; set; }
    public int ColumnId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Position { get; set; }

    public Column Column { get; set; } = null!;
}
