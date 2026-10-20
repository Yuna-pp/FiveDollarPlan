using FiveDollarPlan.Models;

namespace FiveDollarPlan.Services;

public interface IPlanService
{
    Task<PlanDto?> GetPlanAsync(Guid planId);

    Task<IReadOnlyList<PlanSummaryDto>> GetPlansForUserAsync(Guid userId);

    Task<IReadOnlyList<Guid>> GetMemberIdsAtAsync(Guid planId, DateTime eventTimeUtc);
}
