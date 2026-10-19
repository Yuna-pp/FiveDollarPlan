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
        var receivedAt = utcNow.AddDays(-1);
        PlanInput[] plans =
        [
            new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Read for 20 minutes (fictional)", utcNow.AddDays(7)),
            new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Take a daily walk (fictional)", utcNow.AddDays(-3))
        ];
        PaymentInput[] payments =
        [
            new(exampleFriend, exampleUser, 5m, paidAt, receivedAt),
            new(exampleUser, exampleFriend, 3m, paidAt, receivedAt),
            new(exampleFriend, exampleUser, 2m, null, null),
            new(exampleUser, exampleFriend, 4m, null, null),
            new(exampleFriend, exampleUser, 1.5m, paidAt, null),
            new(exampleUser, exampleFriend, 0.5m, paidAt, null)
        ];

        return Build(exampleUser, plans, payments, utcNow) with { IsPreview = true, CanPreview = true };
    }
}
