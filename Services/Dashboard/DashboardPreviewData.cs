using FiveDollarPlan.Models.Dashboard;
using static FiveDollarPlan.Services.Dashboard.DashboardFinancialCalculator;
using static FiveDollarPlan.Services.Dashboard.DashboardPageBuilder;

namespace FiveDollarPlan.Services.Dashboard;

// Fictional examples only. Never use these identities to query an account or database.
internal static class DashboardPreviewData
{
    internal static DashboardPageViewModel Create(DateTime utcNow)
    {
        var exampleUser = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var exampleFriend = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var paidAt = utcNow.AddDays(-2);
        var currentMonday = DashboardWeeklyCalculator.GetCurrentWeekStart(utcNow);
        var auckland = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        DateTime LocalNoonUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified), auckland);
        PlanInput[] plans =
        [
            new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Read for 20 minutes (fictional)", utcNow.AddDays(7)),
            new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Take a daily walk (fictional)", utcNow.AddDays(-3))
        ];
        var payments = new List<PaymentInput>();
        decimal[] weeklyIncome = [0.5m, 0.75m, 1.25m, 1.5m];
        decimal[] weeklyExpenses = [0.25m, 0.5m, 0.75m, 1m];
        for (int i = 0; i < 4; i++)
        {
            // Current-week examples are valid even at Monday midnight; never future-date them.
            var receivedAt = i == 3 ? utcNow : LocalNoonUtc(currentMonday.AddDays((i - 3) * 7));
            payments.Add(new(exampleFriend, exampleUser, weeklyIncome[i], receivedAt.AddHours(-1), receivedAt));
            payments.Add(new(exampleUser, exampleFriend, weeklyExpenses[i], receivedAt.AddHours(-1), receivedAt));
        }

        // Older confirmations explain why all-time totals differ from the four-week overview.
        var olderReceivedAt = LocalNoonUtc(currentMonday.AddDays(-28));
        payments.Add(new(exampleFriend, exampleUser, 1m, olderReceivedAt.AddHours(-1), olderReceivedAt));
        payments.Add(new(exampleUser, exampleFriend, 0.5m, olderReceivedAt.AddHours(-1), olderReceivedAt));
        payments.Add(new(exampleFriend, exampleUser, 2m, null, null));
        payments.Add(new(exampleUser, exampleFriend, 4m, null, null));
        payments.Add(new(exampleFriend, exampleUser, 1.5m, paidAt, null));
        payments.Add(new(exampleUser, exampleFriend, 0.5m, paidAt, null));

        return Build(exampleUser, plans, payments, utcNow) with { IsPreview = true, CanPreview = true };
    }
}
