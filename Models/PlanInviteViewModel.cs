namespace FiveDollarPlan.Models;

public class PlanInviteViewModel
{
    public Guid PlanId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PlanMode Mode { get; set; }

    public DateTime StartAtLocal { get; set; }

    public DateTime EndAtLocal { get; set; }

    public string InviteCode { get; set; } = string.Empty;

    public bool IsMember { get; set; }

    public bool IsAuthenticated { get; set; }
}
