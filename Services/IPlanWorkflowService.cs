using FiveDollarPlan.Models;

namespace FiveDollarPlan.Services;

public interface IPlanWorkflowService
{
    Task<Plan> CreatePlanAsync(Guid creatorId, Plan plan);

    Task<JoinPlanResult> JoinPlanAsync(Guid userId, string inviteCode);

    Task<Plan?> GetPlanByInviteCodeAsync(string inviteCode);

    Task<bool> IsMemberAsync(Guid planId, Guid userId);

    Task<IReadOnlyList<Plan>> GetPlansForUserPageAsync(Guid userId);
}

public enum JoinPlanResult
{
    Joined,
    AlreadyMember,
    InviteNotFound
}
