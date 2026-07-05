using Kanban.API.Common;
using Kanban.API.DTOs.Users;

namespace Kanban.API.Services;

public interface IUserService
{
    Task<Result<CurrentUserProfileResponse>> GetCurrentUserProfileAsync(string userId, CancellationToken cancellationToken = default);
}
