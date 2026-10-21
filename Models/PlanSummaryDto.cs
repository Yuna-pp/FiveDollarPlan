namespace FiveDollarPlan.Models;

public class PlanSummaryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime EndAtUtc { get; set; }
}
