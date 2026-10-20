using FiveDollarPlan.Data;
using FiveDollarPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace FiveDollarPlan.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly PenaltyEventService _events;

    public PaymentService(
        AppDbContext db,
        PenaltyEventService events)
    {
        _db = db;
        _events = events;
    }

    // 触发违约：交给事件服务生成事件和付款记录。
    public Task<Guid> TriggerAsync(Guid planId, Guid actorId)
    {
        return _events.TriggerAsync(planId, actorId);
    }

    // 付款人确认已在线下付款。
    public async Task MarkPaidAsync(Guid paymentId, Guid actorId)
    {
        var payment = await _db.Set<PaymentRecord>()
            .SingleOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            throw new KeyNotFoundException("Payment not found.");
        }

        PaymentRules.MarkPaid(payment, actorId);

        await _db.SaveChangesAsync();
    }

    // 收款人确认已在线下收到付款。
    public async Task ConfirmReceivedAsync(
        Guid paymentId, Guid actorId)
    {
        var payment = await _db.Set<PaymentRecord>()
            .SingleOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            throw new KeyNotFoundException("Payment not found.");
        }

        PaymentRules.ConfirmReceived(payment, actorId);

        await _db.SaveChangesAsync();
    }

    // 查询某个用户作为付款人或收款人的付款记录。
    public async Task<IReadOnlyList<PaymentDto>> GetUserPaymentsAsync(
        Guid userId)
    {
        return await _db.Set<PaymentRecord>()
            .AsNoTracking()
            .Where(p => p.PayerId == userId || p.RecipientId == userId)
            .OrderBy(p => p.Id)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                PenaltyEventId = p.PenaltyEventId,
                PayerId = p.PayerId,
                RecipientId = p.RecipientId,
                Amount = p.Amount,
                PaidAtUtc = p.PaidAtUtc,
                ReceivedAtUtc = p.ReceivedAtUtc
            })
            .ToListAsync();
    }

    // 分别统计一个计划中，每次违约事件的确认情况。
    public async Task<PlanPaymentStatusDto> GetPlanPaymentStatusAsync(
        Guid planId)
    {
        var events = await _db.Set<PenaltyEvent>()
            .AsNoTracking()
            .Where(e => e.PlanId == planId)
            .OrderBy(e => e.TriggeredAtUtc)
            .ThenBy(e => e.Id)
            .Select(e => new PenaltyEventStatusDto
            {
                PenaltyEventId = e.Id,
                TriggeredAtUtc = e.TriggeredAtUtc,

                TotalPayments = _db.Set<PaymentRecord>()
                    .Count(p => p.PenaltyEventId == e.Id),

                PaidCount = _db.Set<PaymentRecord>()
                    .Count(p => p.PenaltyEventId == e.Id
                        && p.PaidAtUtc != null),

                ReceivedCount = _db.Set<PaymentRecord>()
                    .Count(p => p.PenaltyEventId == e.Id
                        && p.PaidAtUtc != null
                        && p.ReceivedAtUtc != null)
            })
            .ToListAsync();

        return new PlanPaymentStatusDto
        {
            PlanId = planId,
            Events = events
        };
    }
}