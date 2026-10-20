using Microsoft.EntityFrameworkCore;
using FiveDollarPlan.Data;
using FiveDollarPlan.Models;

namespace FiveDollarPlan.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserProfileDto?> GetUserByIdAsync(
        Guid userId)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfileDto(
                u.Id,
                u.Name,
                u.AvatarUrl
            ))
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<UserProfileDto>>
        GetUsersByIdsAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return new List<UserProfileDto>();
        }

        return await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserProfileDto(
                u.Id,
                u.Name,
                u.AvatarUrl
            ))
            .ToListAsync();
    }
}