namespace FiveDollarPlan.Models;

public class PaymentDto
{
    public Guid Id { get; init; }
    public Guid PenaltyEventId { get; init; }
    public Guid PayerId { get; init; }
    public Guid RecipientId { get; init; }
    public decimal Amount { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
}