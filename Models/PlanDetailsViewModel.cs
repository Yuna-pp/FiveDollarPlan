namespace FiveDollarPlan.Models;

public class PlanDetailsViewModel
{
    public IReadOnlyList<PenaltyEvent> Events { get; init; }
        = Array.Empty<PenaltyEvent>();

    public IReadOnlyList<UserProfileDto> RecipientProfiles { get; init; }
        = Array.Empty<UserProfileDto>();

    // 计划名称、发起人、金额等信息
    public PlanDto Plan { get; init; } = null!;

    // 当前登录的人，用来判断显示哪个按钮
    public Guid CurrentUserId { get; init; }

    // 发起人的姓名和头像
    public UserProfileDto? Creator { get; init; }

    // 当前参与者的姓名和头像
    public IReadOnlyList<UserProfileDto> Members { get; init; }
        = Array.Empty<UserProfileDto>();

    // 当前计划所有历史事件的付款记录，由视图按 PenaltyEventId 分组。
    public IReadOnlyList<PaymentRecord> Payments { get; init; }
        = Array.Empty<PaymentRecord>();
}
