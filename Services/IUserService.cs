using FiveDollarPlan.Models;

namespace FiveDollarPlan.Services;

public interface IUserService
{
    Task<UserProfileDto?> GetUserByIdAsync(Guid userId);

    Task<IReadOnlyList<UserProfileDto>> GetUsersByIdsAsync(
        IEnumerable<Guid> userIds);
}