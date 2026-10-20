using FiveDollarPlan.Models;

namespace FiveDollarPlan.Services;

public interface IPaymentService
{
    Task<Guid> TriggerAsync(Guid planId, Guid actorId);

    Task MarkPaidAsync(Guid paymentId, Guid actorId);

    Task ConfirmReceivedAsync(Guid paymentId, Guid actorId);

    Task<PlanPaymentStatusDto> GetPlanPaymentStatusAsync(Guid planId);

    Task<IReadOnlyList<PaymentDto>> GetUserPaymentsAsync(Guid userId);
}