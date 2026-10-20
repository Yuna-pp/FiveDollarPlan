namespace FiveDollarPlan.Models;

public class PlanPaymentStatusDto
{
    public Guid PlanId { get; init; }

    public IReadOnlyList<PenaltyEventStatusDto> Events { get; init; }
        = Array.Empty<PenaltyEventStatusDto>();
}

public class PenaltyEventStatusDto
{
    public Guid PenaltyEventId { get; init; }
    public DateTime TriggeredAtUtc { get; init; }

    public int TotalPayments { get; init; }
    public int PaidCount { get; init; }
    public int ReceivedCount { get; init; }

    // 没有付款记录时，暂不判定为“全部确认”
    public bool? AllConfirmed =>
        TotalPayments == 0 ? null : ReceivedCount == TotalPayments;
}