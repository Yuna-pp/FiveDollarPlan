namespace FiveDollarPlan.Models;

public class PlanDto
{
    public Guid Id { get; init; }

    public Guid CreatorId { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public PlanMode Mode { get; init; }

    public DateTime StartAtUtc { get; init; }

    public DateTime EndAtUtc { get; init; }

    public DateTime? TriggerStartAtUtc { get; init; }

    public DateTime? TriggerEndAtUtc { get; init; }
}
