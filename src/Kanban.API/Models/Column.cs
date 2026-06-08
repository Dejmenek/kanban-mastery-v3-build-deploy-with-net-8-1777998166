namespace Kanban.API.Models;

public class Column
{
    public int Id { get; set; }
    public int BoardId { get; set; }

    public ICollection<Card> Cards { get; set; } = [];
    public Board Board { get; set; } = null!;
}
