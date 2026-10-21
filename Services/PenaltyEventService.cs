using FiveDollarPlan.Data;
using FiveDollarPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace FiveDollarPlan.Services;

public class PenaltyEventService
{
    private readonly AppDbContext _db;
    private readonly IPlanService _plans;

    public PenaltyEventService(
        AppDbContext db,
        IPlanService plans)
    {
        _db = db;
        _plans = plans;
    }

    public async Task<Guid> TriggerAsync(Guid planId, Guid actorId)
    {
        // 开始事务：事件和付款单一起保存，一起撤销。
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var eventTimeUtc = DateTime.UtcNow;

        var plan = await _plans.GetPlanAsync(planId);

        if (plan == null)
        {
            throw new KeyNotFoundException("Plan not found.");
        }

        // 只有发起人可以触发违约。
        if (plan.CreatorId != actorId)
        {
            throw new UnauthorizedAccessException(
                "Only the creator can trigger a penalty.");
        }

        // 计划尚未开始或已经结束时，不能触发。
        if (eventTimeUtc < plan.StartAtUtc ||
            eventTimeUtc > plan.EndAtUtc)
        {
            throw new InvalidOperationException(
                "The plan is outside its active period.");
        }

        // 单次计划最多只能产生一次违约事件。
        if (plan.Mode == PlanMode.Single)
        {
            bool alreadyTriggered = await _db.Set<PenaltyEvent>()
                .AnyAsync(e => e.PlanId == planId);

            if (alreadyTriggered)
            {
                throw new InvalidOperationException(
                    "This single plan has already been triggered.");
            }
        }
        else if (plan.Mode == PlanMode.Multiple)
        {
            // 多次计划还需要检查额外的触发时间范围。
            if ((plan.TriggerStartAtUtc.HasValue &&
                 eventTimeUtc < plan.TriggerStartAtUtc.Value) ||
                (plan.TriggerEndAtUtc.HasValue &&
                 eventTimeUtc > plan.TriggerEndAtUtc.Value))
            {
                throw new InvalidOperationException(
                    "The plan is outside its trigger period.");
            }
        }
        else
        {
            throw new InvalidOperationException("Unknown plan mode.");
        }

        // 在同一个事务中，读取事件发生时的成员名单。
        var memberIds = await _plans.GetMemberIdsAtAsync(
            planId, eventTimeUtc);

        var penaltyEvent = new PenaltyEvent
        {
            PlanId = planId,
            TriggeredAtUtc = eventTimeUtc,
            CreatedByUserId = actorId
        };

        _db.Set<PenaltyEvent>().Add(penaltyEvent);

        // 给每位参与者生成付款单，排除发起人自己。
        foreach (var recipientId in memberIds
            .Where(id => id != plan.CreatorId)
            .Distinct())
        {
            _db.Set<PaymentRecord>().Add(new PaymentRecord
            {
                PenaltyEventId = penaltyEvent.Id,
                PayerId = plan.CreatorId,
                RecipientId = recipientId,
                Amount = plan.Amount
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return penaltyEvent.Id;
    }
}