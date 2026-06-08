namespace Kanban.API.Models;

public class BoardMember
{
    public int BoardId { get; set; }
    public string MemberId { get; set; }
    public Role Role { get; set; }

    public ApplicationUser Member { get; set; } = null!;
    public Board Board { get; set; } = null!;
}

public enum Role
{
    Owner,
    Member
}
