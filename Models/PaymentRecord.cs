namespace FiveDollarPlan.Models;

public class PaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PenaltyEventId { get; set; }

    public Guid PayerId { get; set; }

    public Guid RecipientId { get; set; }

    public decimal Amount { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }
}