using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Users;
using Kanban.API.Errors;

namespace Kanban.API.Services;

public class UserService(ApplicationDbContext context) : IUserService
{
    public async Task<Result<CurrentUserProfileResponse>> GetCurrentUserProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FindAsync([userId], cancellationToken);

        if (user is null) return Result.Failure<CurrentUserProfileResponse>(UserErrors.NotFound(userId));

        return new CurrentUserProfileResponse(user.Id, user.UserName, user.Email);
    }
}
