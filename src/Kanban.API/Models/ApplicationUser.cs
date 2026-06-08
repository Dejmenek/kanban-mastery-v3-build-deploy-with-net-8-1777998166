using Microsoft.AspNetCore.Identity;

namespace Kanban.API.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<BoardMember> BoardMemberships { get; set; } = [];
}
