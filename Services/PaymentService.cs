using FiveDollarPlan.Data;
using FiveDollarPlan.Models;
using Microsoft.EntityFrameworkCore;

namespace FiveDollarPlan.Services;

public class PaymentService
{
    private readonly AppDbContext _db;

    public PaymentService(AppDbContext db)
    {
        _db = db;
    }

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

    public async Task ConfirmReceivedAsync(Guid paymentId, Guid actorId)
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