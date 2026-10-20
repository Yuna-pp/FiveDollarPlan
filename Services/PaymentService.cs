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
}