using static FiveDollarPlan.Services.Dashboard.DashboardFinancialCalculator;

namespace FiveDollarPlan.Services.Dashboard;

internal static class DashboardWeeklyCalculator
{
    private static readonly TimeZoneInfo Auckland = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");

    internal sealed record WeekAmounts(DateOnly WeekStart, decimal Income, decimal Expenses);

    internal static IReadOnlyList<WeekAmounts> Calculate(Guid userId, IEnumerable<PaymentInput> payments, DateTime utcNow)
    {
        var now = AsUtc(utcNow, nameof(utcNow));
        var firstMonday = GetCurrentWeekStart(now).AddDays(-21);
        var income = new decimal[4];
        var expenses = new decimal[4];

        foreach (var payment in payments)
        {
            if (payment.ReceivedAtUtc is not DateTime receivedAt)
                continue;

            var receivedUtc = AsUtc(receivedAt, nameof(payment.ReceivedAtUtc));
            if (receivedUtc > now)
                continue;

            if (payment.PayerId != userId && payment.RecipientId != userId)
                continue;

            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(receivedUtc, Auckland));
            int daysFromStart = localDate.DayNumber - firstMonday.DayNumber;
            if (daysFromStart < 0 || daysFromStart >= 28)
                continue;

            // Divide local calendar days, not UTC hours: DST weeks can have 167 or 169 hours.
            int weekIndex = daysFromStart / 7;
            if (payment.RecipientId == userId)
                income[weekIndex] += payment.Amount;
            if (payment.PayerId == userId)
                expenses[weekIndex] += payment.Amount;
        }

        return Array.AsReadOnly(Enumerable.Range(0, 4)
            .Select(i => new WeekAmounts(firstMonday.AddDays(i * 7), income[i], expenses[i]))
            .ToArray());
    }

    internal static DateOnly GetCurrentWeekStart(DateTime utcNow)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utcNow, nameof(utcNow)), Auckland));
        int daysSinceMonday = ((int)localDate.DayOfWeek + 6) % 7;
        return localDate.AddDays(-daysSinceMonday);
    }

    private static DateTime AsUtc(DateTime value, string parameterName) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        // SQLite values follow the team's UTC storage contract even when Kind is not retained.
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => throw new ArgumentException("Use UTC timestamps; Local timestamps are not supported.", parameterName)
    };
}
