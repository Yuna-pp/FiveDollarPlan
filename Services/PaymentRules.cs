using FiveDollarPlan.Models;

namespace FiveDollarPlan.Services;

public static class PaymentRules
{
public static void MarkPaid(PaymentRecord payment, Guid actorId)
    {
        if (payment.PayerId != actorId)
        {
            throw new UnauthorizedAccessException("Only the payer can mark this payment as paid.");
        }

        if (payment.PaidAtUtc == null)
        {
            payment.PaidAtUtc = DateTime.UtcNow;
        }
    }
        public static void ConfirmReceived(PaymentRecord payment, Guid actorId)
    {
        if (payment.RecipientId != actorId)
        {
            throw new UnauthorizedAccessException("Only the recipient can confirm receipt.");
        }

        if (payment.PaidAtUtc == null)
        {
            throw new InvalidOperationException("Payment must be marked as paid first.");
        }

        if (payment.ReceivedAtUtc == null)
        {
            payment.ReceivedAtUtc = DateTime.UtcNow;
        }
    }
}