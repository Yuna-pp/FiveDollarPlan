namespace FiveDollarPlan.Services.Dashboard;

// Dashboard-local calculation types; these are not shared service contracts.
internal static class DashboardFinancialCalculator
{
    // One input represents one existing payment record's fixed amount and parties.
    internal sealed record PaymentInput(
        Guid PayerId,
        Guid RecipientId,
        decimal Amount,
        DateTime? PaidAtUtc,
        DateTime? ReceivedAtUtc);

    internal sealed record FinancialSummary(
        decimal ConfirmedIncome,
        decimal ConfirmedExpense,
        decimal PendingIncome,
        decimal PendingExpense);

    internal static FinancialSummary Calculate(Guid userId, IEnumerable<PaymentInput> payments)
    {
        decimal confirmedIncome = 0m;
        decimal confirmedExpense = 0m;
        decimal pendingIncome = 0m;
        decimal pendingExpense = 0m;

        foreach (var payment in payments)
        {
            // Paid alone is still pending. Only Received confirms a payment.
            bool isConfirmed = payment.ReceivedAtUtc != null;

            if (payment.RecipientId == userId)
            {
                if (isConfirmed)
                    confirmedIncome += payment.Amount;
                else
                    pendingIncome += payment.Amount;
            }

            if (payment.PayerId == userId)
            {
                if (isConfirmed)
                    confirmedExpense += payment.Amount;
                else
                    pendingExpense += payment.Amount;
            }
        }

        return new FinancialSummary(
            confirmedIncome, confirmedExpense, pendingIncome, pendingExpense);
    }
}
