using FiveDollarPlan.Models.Dashboard;
using static FiveDollarPlan.Services.Dashboard.DashboardFinancialCalculator;

namespace FiveDollarPlan.Services.Dashboard;

internal static class DashboardPageBuilder
{
    // Local presentation input, not a shared PlanSummaryDto. Caller supplies only this user's plans.
    internal sealed record PlanInput(Guid Id, string Name, DateTime EndAtUtc);

    internal static DashboardPageViewModel Build(Guid userId, IEnumerable<PlanInput> plans,
        IEnumerable<PaymentInput> payments, DateTime utcNow)
    {
        var userPayments = payments
            .Where(payment => payment.PayerId == userId || payment.RecipientId == userId)
            .ToArray();
        var summary = DashboardFinancialCalculator.Calculate(userId, userPayments);
        var activePlans = new List<DashboardPlanRow>();
        var endedPlans = new List<DashboardPlanRow>();

        foreach (var plan in plans)
        {
            var row = new DashboardPlanRow(plan.Id, plan.Name, plan.EndAtUtc);
            (HasEnded(plan.EndAtUtc, utcNow) ? endedPlans : activePlans).Add(row);
        }

        return new DashboardPageViewModel
        {
            DataAvailable = true,
            ConfirmedIncome = summary.ConfirmedIncome,
            ConfirmedExpense = summary.ConfirmedExpense,
            PendingIncome = summary.PendingIncome,
            PendingExpense = summary.PendingExpense,
            ActivePlans = activePlans,
            EndedPlans = endedPlans,
            Payments = userPayments.Select(payment => new DashboardPaymentRow(
                payment.RecipientId == userId ? "Income" : "Expense",
                payment.Amount,
                payment.ReceivedAtUtc != null ? DashboardPaymentState.Confirmed :
                payment.PaidAtUtc != null ? DashboardPaymentState.MarkedPaid :
                DashboardPaymentState.Pending)).ToArray()
        };
    }

    // Provisional team rule: ending and payment settlement are independent.
    private static bool HasEnded(DateTime endAtUtc, DateTime utcNow) => endAtUtc <= utcNow;
}
