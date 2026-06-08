namespace Kanban.API.Models;

public class Card
{
    public int Id { get; set; }
    public int ColumnId { get; set; }

    public Column Column { get; set; } = null!;
}
