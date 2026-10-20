using FiveDollarPlan.Data;
using FiveDollarPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace FiveDollarPlan.Services;

public class PlanService : IPlanService, IPlanWorkflowService
{
    private readonly AppDbContext _dbContext;

    public PlanService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PlanDto?> GetPlanAsync(Guid planId)
    {
        return await _dbContext.Set<Plan>()
            .AsNoTracking()
            .Where(plan => plan.Id == planId)
            .Select(plan => new PlanDto
            {
                Id = plan.Id,
                CreatorId = plan.CreatorId,
                Name = plan.Name,
                Amount = plan.Amount,
                Mode = plan.Mode,
                StartAtUtc = plan.StartAtUtc,
                EndAtUtc = plan.EndAtUtc,
                TriggerStartAtUtc = plan.TriggerStartAtUtc,
                TriggerEndAtUtc = plan.TriggerEndAtUtc
            })
            .SingleOrDefaultAsync();
    }
    public async Task<IReadOnlyList<Guid>> GetMemberIdsAtAsync(
        Guid planId,
        DateTime eventTimeUtc)
    {
        return await _dbContext.Set<PlanMember>()
            .AsNoTracking()
            .Where(member => member.PlanId == planId
                && member.JoinedAtUtc <= eventTimeUtc)
            .Select(member => member.UserId)
            .ToListAsync();
    }

    public async Task<Plan> CreatePlanAsync(Guid creatorId, Plan plan)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var joinedAtUtc = DateTime.UtcNow;

        plan.Id = plan.Id == Guid.Empty ? Guid.NewGuid() : plan.Id;
        plan.CreatorId = creatorId;
        plan.InviteCode = await CreateUniqueInviteCodeAsync();

        _dbContext.Set<Plan>().Add(plan);
        _dbContext.Set<PlanMember>().Add(new PlanMember
        {
            PlanId = plan.Id,
            UserId = creatorId,
            JoinedAtUtc = joinedAtUtc
        });

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return plan;
    }

    public async Task<JoinPlanResult> JoinPlanAsync(Guid userId, string inviteCode)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var joinedAtUtc = DateTime.UtcNow;
        var normalizedCode = inviteCode.Trim().ToUpperInvariant();

        var plan = await _dbContext.Set<Plan>()
            .SingleOrDefaultAsync(item => item.InviteCode == normalizedCode);

        if (plan is null)
        {
            return JoinPlanResult.InviteNotFound;
        }

        var alreadyMember = await _dbContext.Set<PlanMember>()
            .AnyAsync(member => member.PlanId == plan.Id && member.UserId == userId);

        if (alreadyMember)
        {
            return JoinPlanResult.AlreadyMember;
        }

        _dbContext.Set<PlanMember>().Add(new PlanMember
        {
            PlanId = plan.Id,
            UserId = userId,
            JoinedAtUtc = joinedAtUtc
        });

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return JoinPlanResult.Joined;
    }

    public async Task<Plan?> GetPlanByInviteCodeAsync(string inviteCode)
    {
        var normalizedCode = inviteCode.Trim().ToUpperInvariant();

        return await _dbContext.Set<Plan>()
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.InviteCode == normalizedCode);
    }

    public Task<bool> IsMemberAsync(Guid planId, Guid userId)
    {
        return _dbContext.Set<PlanMember>()
            .AnyAsync(member => member.PlanId == planId && member.UserId == userId);
    }

    public async Task<IReadOnlyList<Plan>> GetPlansForUserPageAsync(Guid userId)
    {
        return await _dbContext.Set<Plan>()
            .AsNoTracking()
            .Where(plan => plan.CreatorId == userId
                || _dbContext.Set<PlanMember>()
                    .Any(member => member.PlanId == plan.Id && member.UserId == userId))
            .OrderBy(plan => plan.EndAtUtc)
            .ToListAsync();
    }

    private async Task<string> CreateUniqueInviteCodeAsync()
    {
        string inviteCode;
        do
        {
            inviteCode = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8));
        }
        while (await _dbContext.Set<Plan>()
            .AnyAsync(plan => plan.InviteCode == inviteCode));

        return inviteCode;
    }
}
