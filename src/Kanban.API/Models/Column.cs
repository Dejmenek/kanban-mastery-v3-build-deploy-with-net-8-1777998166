namespace Kanban.API.Models;

public class Column
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public int Position { get; set; }

    public ICollection<Card> Cards { get; set; } = [];
    public Board Board { get; set; } = null!;
}
