namespace FiveDollarPlan.Models;

public class PlanMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlanId { get; set; }

    public Guid UserId { get; set; }

    public DateTime JoinedAtUtc { get; set; }
}
