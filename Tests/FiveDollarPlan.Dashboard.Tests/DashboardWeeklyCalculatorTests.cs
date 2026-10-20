using System.Globalization;
using FiveDollarPlan.Services.Dashboard;
using Xunit;
using static FiveDollarPlan.Services.Dashboard.DashboardFinancialCalculator;

namespace FiveDollarPlan.Dashboard.Tests;

public class DashboardWeeklyCalculatorTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FriendId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Now = Utc("2026-10-20T00:00:00Z");
    private static DateTime Utc(string text) => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    [Fact]
    public void EmptyPayments_PreserveFourMondayToSundayWeeksInOrder()
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId, [], Now);

        Assert.Equal(new[] { new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 5),
            new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 19) }, weeks.Select(w => w.WeekStart));
        Assert.All(weeks, w => { Assert.Equal(0m, w.Income); Assert.Equal(0m, w.Expenses); });
    }

    [Fact]
    public void ConfirmedPayments_GroupByReceivedTimeAndByUserRole()
    {
        PaymentInput[] payments =
        [
            new(FriendId, UserId, 1.25m, Now.AddDays(-20), Now),
            new(OtherId, UserId, 2.75m, null, Now),
            new(UserId, FriendId, 3m, Now.AddDays(-20), Now.AddDays(-7)),
            new(FriendId, UserId, 5m, Now, null),
            new(UserId, FriendId, 4m, null, null),
            new(FriendId, OtherId, 5m, Now, Now)
        ];

        var weeks = DashboardWeeklyCalculator.Calculate(UserId, payments, Now);

        Assert.Equal(4m, weeks[3].Income);
        Assert.Equal(0m, weeks[3].Expenses);
        Assert.Equal(3m, weeks[2].Expenses);
        Assert.Equal(0m, weeks[2].Income);
        Assert.Equal(4m, weeks.Sum(w => w.Income));
        Assert.Equal(3m, weeks.Sum(w => w.Expenses));
    }

    [Theory]
    [InlineData("2026-09-27T10:59:59.9999999Z", -1)]
    [InlineData("2026-09-27T11:00:00Z", 0)]
    [InlineData("2026-10-04T10:59:59.9999999Z", 0)]
    [InlineData("2026-10-04T11:00:00Z", 1)]
    [InlineData("2026-10-18T10:59:59.9999999Z", 2)]
    [InlineData("2026-10-18T11:00:00Z", 3)]
    public void AucklandMondayBoundaries_UseInclusiveStartExclusiveNextStart(string received, int expectedIndex)
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId,
            [new(FriendId, UserId, 2m, null, Utc(received))], Now);

        for (int i = 0; i < 4; i++)
            Assert.Equal(i == expectedIndex ? 2m : 0m, weeks[i].Income);
    }

    [Theory]
    [InlineData("2026-10-18T10:59:59Z", "2026-10-12")]
    [InlineData("2026-10-18T11:00:00Z", "2026-10-19")]
    [InlineData("2027-01-01T00:00:00Z", "2026-12-28")]
    [InlineData("2024-02-29T00:00:00Z", "2024-02-26")]
    public void CurrentWeek_UsesAucklandDateAcrossSundayYearAndLeapDay(string now, string expectedMonday)
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId, [], Utc(now));

        Assert.Equal(DateOnly.ParseExact(expectedMonday, "yyyy-MM-dd", CultureInfo.InvariantCulture), weeks[^1].WeekStart);
        Assert.All(weeks, w => Assert.Equal(DayOfWeek.Monday, w.WeekStart.DayOfWeek));
        Assert.Equal(21, weeks[^1].WeekStart.DayNumber - weeks[0].WeekStart.DayNumber);
    }

    [Theory]
    [InlineData("2026-10-20T00:00:00.0000001Z")]
    [InlineData("2026-10-21T00:00:00Z")]
    [InlineData("2026-10-26T00:00:00Z")]
    [InlineData("2027-01-01T00:00:00Z")]
    public void FutureConfirmedPayments_AreAlwaysExcluded(string received)
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId,
        [
            new(FriendId, UserId, 5m, Now, Utc(received)),
            new(UserId, FriendId, 4m, Now, Utc(received))
        ], Now);

        Assert.All(weeks, w => { Assert.Equal(0m, w.Income); Assert.Equal(0m, w.Expenses); });
    }

    [Fact]
    public void ConfirmationExactlyAtNow_IsIncluded()
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId, [new(FriendId, UserId, 2m, null, Now)], Now);

        Assert.Equal(2m, weeks[^1].Income);
    }

    [Theory]
    [InlineData("2026-09-28T00:00:00Z", "2026-09-26T13:59:59Z", "2026-09-26T14:00:00Z", "2026-09-21")]
    [InlineData("2026-04-06T00:00:00Z", "2026-04-04T13:30:00Z", "2026-04-04T14:30:00Z", "2026-03-30")]
    public void DaylightSavingSkippedAndRepeatedHours_KeepBothUtcInstantsInCorrectWeek(
        string now, string first, string second, string expectedMonday)
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId,
        [
            new(FriendId, UserId, 1m, null, Utc(first)),
            new(FriendId, UserId, 2m, null, Utc(second))
        ], Utc(now));

        var week = Assert.Single(weeks, w => w.Income != 0m);
        Assert.Equal(DateOnly.ParseExact(expectedMonday, "yyyy-MM-dd", CultureInfo.InvariantCulture), week.WeekStart);
        Assert.Equal(3m, week.Income);
    }

    [Theory]
    [InlineData("2026-09-28T00:00:00Z", "2026-09-27T10:59:59.9999999Z", "2026-09-27T11:00:00Z")]
    [InlineData("2026-04-06T00:00:00Z", "2026-04-05T11:59:59.9999999Z", "2026-04-05T12:00:00Z")]
    public void DaylightSavingWeeks_DoNotAssume168Hours(string now, string beforeMonday, string monday)
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId,
        [
            new(FriendId, UserId, 1m, null, Utc(beforeMonday)),
            new(FriendId, UserId, 2m, null, Utc(monday))
        ], Utc(now));

        Assert.Equal(1m, weeks[2].Income);
        Assert.Equal(2m, weeks[3].Income);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified, DateTimeKind.Unspecified)]
    public void UnspecifiedTimestamps_AreInterpretedAsUtc(DateTimeKind nowKind, DateTimeKind receivedKind)
    {
        var now = DateTime.SpecifyKind(Utc("2026-10-18T11:00:00Z"), nowKind);
        var received = DateTime.SpecifyKind(Utc("2026-10-18T11:00:00Z"), receivedKind);
        var weeks = DashboardWeeklyCalculator.Calculate(UserId, [new(FriendId, UserId, 2m, null, received)], now);

        Assert.Equal(new DateOnly(2026, 10, 19), weeks[^1].WeekStart);
        Assert.Equal(2m, weeks[^1].Income);
    }

    [Fact]
    public void LocalClock_IsExplicitlyRejected()
    {
        Assert.Throws<ArgumentException>(() => DashboardWeeklyCalculator.Calculate(UserId, [], DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void LocalReceivedTimestamp_IsExplicitlyRejected()
    {
        Assert.Throws<ArgumentException>(() => DashboardWeeklyCalculator.Calculate(UserId,
            [new(FriendId, UserId, 2m, null, DateTime.SpecifyKind(Now, DateTimeKind.Local))], Now));
    }

    [Fact]
    public void DecimalAmounts_AreExactAndMissingMiddleWeeksStayZero()
    {
        var weeks = DashboardWeeklyCalculator.Calculate(UserId,
        [
            new(FriendId, UserId, 0.1m, null, Now),
            new(FriendId, UserId, 0.2m, null, Now),
            new(UserId, FriendId, 0.3m, null, Now),
            new(UserId, FriendId, 0.4m, null, Now),
            new(FriendId, UserId, 2m, null, Utc("2026-09-28T00:00:00Z"))
        ], Now);

        Assert.Equal(0.3m, weeks[3].Income);
        Assert.Equal(0.7m, weeks[3].Expenses);
        Assert.Equal(2m, weeks[0].Income);
        Assert.Equal(0m, weeks[1].Income);
        Assert.Equal(0m, weeks[2].Income);
        Assert.Equal(0m, weeks[1].Expenses);
        Assert.Equal(0m, weeks[2].Expenses);
    }
}
