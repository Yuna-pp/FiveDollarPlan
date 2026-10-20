namespace FiveDollarPlan.Services;

public interface ICurrentUserService
{
    Guid GetRequiredUserId();

    bool IsAuthenticated { get; }
}