namespace FiveDollarPlan.Models;

public class PenaltyEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlanId { get; set; }

    public DateTime TriggeredAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }
}