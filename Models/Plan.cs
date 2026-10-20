namespace FiveDollarPlan.Models;

public enum PlanMode
{
    Single = 0,
    Multiple = 1
}

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CreatorId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PlanMode Mode { get; set; }

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public DateTime? TriggerStartAtUtc { get; set; }

    public DateTime? TriggerEndAtUtc { get; set; }

    public string InviteCode { get; set; } = string.Empty;
}
