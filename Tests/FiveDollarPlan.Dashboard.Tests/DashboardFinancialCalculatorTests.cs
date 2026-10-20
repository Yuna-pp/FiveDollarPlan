using FiveDollarPlan.Services.Dashboard;
using Xunit;
using static FiveDollarPlan.Services.Dashboard.DashboardFinancialCalculator;

namespace FiveDollarPlan.Dashboard.Tests;

public class DashboardFinancialCalculatorTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime PaidAt = new(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ReceivedAt = PaidAt.AddHours(1);

    [Fact]
    public void ConfirmedIncomingPayment_OnlyIncreasesConfirmedIncome()
    {
        var result = Calculate(UserId, [new(OtherId, UserId, 4.25m, PaidAt, ReceivedAt)]);

        Assert.Equal(new FinancialSummary(4.25m, 0m, 0m, 0m), result);
    }

    [Fact]
    public void ConfirmedOutgoingPayment_OnlyIncreasesConfirmedExpense()
    {
        var result = Calculate(UserId, [new(UserId, OtherId, 3.75m, PaidAt, ReceivedAt)]);

        Assert.Equal(new FinancialSummary(0m, 3.75m, 0m, 0m), result);
    }

    [Fact]
    public void PendingIncomingPayment_OnlyIncreasesPendingIncome()
    {
        var result = Calculate(UserId, [new(OtherId, UserId, 2.50m, null, null)]);

        Assert.Equal(new FinancialSummary(0m, 0m, 2.50m, 0m), result);
    }

    [Fact]
    public void PendingOutgoingPayment_OnlyIncreasesPendingExpense()
    {
        var result = Calculate(UserId, [new(UserId, OtherId, 1.25m, null, null)]);

        Assert.Equal(new FinancialSummary(0m, 0m, 0m, 1.25m), result);
    }

    [Fact]
    public void MarkedPaidIncomingPayment_RemainsPendingUntilReceived()
    {
        var result = Calculate(UserId, [new(OtherId, UserId, 5m, PaidAt, null)]);

        Assert.Equal(new FinancialSummary(0m, 0m, 5m, 0m), result);
    }

    [Fact]
    public void MarkedPaidOutgoingPayment_RemainsPendingUntilReceived()
    {
        var result = Calculate(UserId, [new(UserId, OtherId, 5m, PaidAt, null)]);

        Assert.Equal(new FinancialSummary(0m, 0m, 0m, 5m), result);
    }

    [Fact]
    public void UnrelatedPayments_DoNotAffectAnyTotal()
    {
        PaymentInput[] payments =
        [
            new(OtherId, ThirdId, 5m, PaidAt, ReceivedAt),
            new(ThirdId, OtherId, 4m, null, null),
            new(OtherId, ThirdId, 3m, PaidAt, null)
        ];

        Assert.Equal(new FinancialSummary(0m, 0m, 0m, 0m), Calculate(UserId, payments));
    }

    [Fact]
    public void EmptyCollection_ReturnsZeroForEveryTotal()
    {
        Assert.Equal(new FinancialSummary(0m, 0m, 0m, 0m), Calculate(UserId, []));
    }

    [Fact]
    public void MixedPayments_SumEachCategoryWithoutDoubleCounting()
    {
        PaymentInput[] payments =
        [
            new(OtherId, UserId, 2m, PaidAt, ReceivedAt),
            new(ThirdId, UserId, 3m, PaidAt, ReceivedAt),
            new(UserId, OtherId, 1.25m, PaidAt, ReceivedAt),
            new(UserId, ThirdId, 2.75m, PaidAt, ReceivedAt),
            new(OtherId, UserId, 1m, null, null),
            new(ThirdId, UserId, 2m, PaidAt, null),
            new(UserId, OtherId, 0.50m, null, null),
            new(UserId, ThirdId, 1.50m, PaidAt, null),
            new(OtherId, ThirdId, 5m, PaidAt, ReceivedAt)
        ];

        Assert.Equal(new FinancialSummary(5m, 4m, 3m, 2m), Calculate(UserId, payments));
    }

    [Fact]
    public void FractionalAmounts_AreAddedExactlyUsingDecimal()
    {
        PaymentInput[] payments =
        [
            new(OtherId, UserId, 0.1m, PaidAt, ReceivedAt),
            new(OtherId, UserId, 0.2m, PaidAt, ReceivedAt),
            new(UserId, OtherId, 0.1m, PaidAt, ReceivedAt),
            new(UserId, OtherId, 0.2m, PaidAt, ReceivedAt),
            new(OtherId, UserId, 0.1m, null, null),
            new(OtherId, UserId, 0.2m, PaidAt, null),
            new(UserId, OtherId, 0.1m, null, null),
            new(UserId, OtherId, 0.2m, PaidAt, null)
        ];

        Assert.Equal(new FinancialSummary(0.3m, 0.3m, 0.3m, 0.3m), Calculate(UserId, payments));
    }

    [Fact]
    public void ReceivedTimestampAlone_DeterminesConfirmation()
    {
        // Payment creation/validation belongs to the payment module. The calculator
        // follows the agreed ReceivedAtUtc criterion even for inconsistent input.
        PaymentInput[] payments =
        [
            new(OtherId, UserId, 2m, null, ReceivedAt),
            new(UserId, OtherId, 3m, null, ReceivedAt)
        ];

        Assert.Equal(new FinancialSummary(2m, 3m, 0m, 0m), Calculate(UserId, payments));
    }
}
