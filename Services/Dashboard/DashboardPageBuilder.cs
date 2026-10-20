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
        var planModel = BuildPlans(plans, utcNow);
        return BuildPayments(userId, payments, utcNow) with
        {
            PlanDataAvailable = planModel.PlanDataAvailable,
            ActivePlans = planModel.ActivePlans,
            EndedPlans = planModel.EndedPlans
        };
    }

    internal static DashboardPageViewModel BuildPayments(Guid userId,
        IEnumerable<PaymentInput> payments, DateTime utcNow)
    {
        var userPayments = payments
            .Where(payment => payment.PayerId == userId || payment.RecipientId == userId)
            .ToArray();
        var summary = DashboardFinancialCalculator.Calculate(userId, userPayments);
        return new DashboardPageViewModel
        {
            PaymentDataAvailable = true,
            WeeklySummary = new DashboardWeeklyViewModel(
                DashboardWeeklyCalculator.Calculate(userId, userPayments, utcNow)
                    .Select(week => new DashboardWeekViewModel(week.WeekStart, week.Income, week.Expenses))
                    .ToArray()),
            ConfirmedIncome = summary.ConfirmedIncome,
            ConfirmedExpense = summary.ConfirmedExpense,
            PendingIncome = summary.PendingIncome,
            PendingExpense = summary.PendingExpense,
            Payments = userPayments.Select(payment => new DashboardPaymentRow(
                payment.RecipientId == userId ? "Income" : "Expense",
                payment.Amount,
                payment.ReceivedAtUtc != null ? DashboardPaymentState.Confirmed :
                payment.PaidAtUtc != null ? DashboardPaymentState.MarkedPaid :
                DashboardPaymentState.Pending)).ToArray()
        };
    }

    internal static DashboardPageViewModel BuildPlans(IEnumerable<PlanInput> plans, DateTime utcNow)
    {
        utcNow = DashboardPlanRow.AsUtc(utcNow);
        var activePlans = new List<DashboardPlanRow>();
        var endedPlans = new List<DashboardPlanRow>();
        foreach (var plan in plans)
        {
            var endAtUtc = DashboardPlanRow.AsUtc(plan.EndAtUtc);
            var row = new DashboardPlanRow(plan.Id, plan.Name, endAtUtc);
            (HasEnded(endAtUtc, utcNow) ? endedPlans : activePlans).Add(row);
        }

        // The caller composes independently loaded Plan and Payment sections.
        return new DashboardPageViewModel
        {
            PlanDataAvailable = true,
            ActivePlans = activePlans,
            EndedPlans = endedPlans
        };
    }

    // Provisional team rule: ending and payment settlement are independent.
    private static bool HasEnded(DateTime endAtUtc, DateTime utcNow) => endAtUtc <= utcNow;
}
