using FiveDollarPlan.Models;
using FiveDollarPlan.Services;
using System.Reflection;
using FiveDollarPlan.Controllers;
using FiveDollarPlan.Models.Dashboard;
using FiveDollarPlan.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;
using static FiveDollarPlan.Services.Dashboard.DashboardFinancialCalculator;
using static FiveDollarPlan.Services.Dashboard.DashboardPageBuilder;

namespace FiveDollarPlan.Dashboard.Tests;

public class DashboardPageTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Now = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_UsesFinancialCalculatorTotals()
    {
        PaymentInput[] payments =
        [
            new(OtherId, UserId, 1.25m, Now, Now),
            new(ThirdId, UserId, 2.75m, Now, Now),
            new(UserId, OtherId, 3m, Now, Now),
            new(OtherId, UserId, 2m, null, null),
            new(OtherId, UserId, 0.5m, Now, null),
            new(UserId, OtherId, 1m, Now, null)
        ];

        var model = Build(UserId, [], payments, Now);

        Assert.True(model.PlanDataAvailable);
        Assert.True(model.PaymentDataAvailable);
        Assert.False(model.IsPreview);
        Assert.Equal(4m, model.ConfirmedIncome);
        Assert.Equal(3m, model.ConfirmedExpense);
        Assert.Equal(2.5m, model.PendingIncome);
        Assert.Equal(1m, model.PendingExpense);
    }

    [Theory]
    [InlineData(false, false, DashboardPaymentState.Pending)]
    [InlineData(true, false, DashboardPaymentState.MarkedPaid)]
    [InlineData(true, true, DashboardPaymentState.Confirmed)]
    [InlineData(false, true, DashboardPaymentState.Confirmed)]
    public void Build_DerivesPaymentStatusFromTimestamps(bool paid, bool received, DashboardPaymentState expected)
    {
        var model = Build(UserId, [], [new(OtherId, UserId, 2m, paid ? Now : null, received ? Now : null)], Now);

        Assert.Equal(expected, Assert.Single(model.Payments).State);
    }

    [Fact]
    public void Build_SeparatesActiveAndEndedPlansUsingUtc()
    {
        PlanInput[] plans =
        [
            new(OtherId, "Future", Now.AddDays(1)),
            new(ThirdId, "Past", Now.AddDays(-1))
        ];

        var model = Build(UserId, plans, [], Now);

        Assert.Equal(OtherId, Assert.Single(model.ActivePlans).Id);
        Assert.Equal(ThirdId, Assert.Single(model.EndedPlans).Id);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Build_ClassifiesExactEndTimeAndAdjacentTicks(long ticks, bool ended)
    {
        var model = Build(UserId, [new(OtherId, "Boundary", Now.AddTicks(ticks))], [], Now);

        Assert.Equal(ended ? 1 : 0, model.EndedPlans.Count);
        Assert.Equal(ended ? 0 : 1, model.ActivePlans.Count);
    }

    [Fact]
    public void Build_EmptyDataHasZeroTotalsAndEmptyLists()
    {
        var model = Build(UserId, [], [], Now);

        Assert.True(model.PlanDataAvailable);
        Assert.True(model.PaymentDataAvailable);
        Assert.Equal(0m, model.ConfirmedIncome);
        Assert.Equal(0m, model.ConfirmedExpense);
        Assert.Equal(0m, model.PendingIncome);
        Assert.Equal(0m, model.PendingExpense);
        Assert.Empty(model.ActivePlans);
        Assert.Empty(model.EndedPlans);
        Assert.Empty(model.Payments);
    }

    [Fact]
    public void Build_PaymentRowsHaveCorrectDirectionAndExcludeUnrelatedRecords()
    {
        var model = Build(UserId, [],
        [
            new(OtherId, UserId, 2m, null, null),
            new(UserId, OtherId, 3m, Now, Now),
            new(OtherId, ThirdId, 5m, Now, Now)
        ], Now);

        Assert.Collection(model.Payments,
            row => { Assert.Equal("Income", row.Direction); Assert.Equal(2m, row.Amount); },
            row => { Assert.Equal("Expense", row.Direction); Assert.Equal(3m, row.Amount); });
        Assert.Equal(0m, model.ConfirmedIncome);
        Assert.Equal(3m, model.ConfirmedExpense);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task Index_ShowsRealEmptyPlansWithoutFictionalPayments(string environment, bool canPreview)
    {
        var controller = Controller(environment);

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Equal("Index", result.ViewName);
        Assert.True(model.PlanDataAvailable);
        Assert.False(model.PaymentDataAvailable);
        Assert.False(model.IsPreview);
        Assert.Equal(canPreview, model.CanPreview);
        Assert.Null(model.ConfirmedIncome);
        Assert.Null(model.ConfirmedExpense);
        Assert.Null(model.PendingIncome);
        Assert.Null(model.PendingExpense);
        Assert.Empty(model.ActivePlans);
        Assert.Empty(model.EndedPlans);
        Assert.Empty(model.Payments);
    }

    [Fact]
    public void Preview_InDevelopmentReturnsClearlyMarkedFictionalData()
    {
        var controller = Controller(Environments.Development);

        var result = Assert.IsType<ViewResult>(controller.Preview());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Equal("Index", result.ViewName);
        Assert.True(model.IsPreview);
        Assert.True(model.PlanDataAvailable);
        Assert.True(model.PaymentDataAvailable);
        Assert.Equal(5m, model.ConfirmedIncome);
        Assert.Equal(3m, model.ConfirmedExpense);
        Assert.Equal(3.5m, model.PendingIncome);
        Assert.Equal(4.5m, model.PendingExpense);
        Assert.NotEmpty(model.ActivePlans);
        Assert.NotEmpty(model.EndedPlans);
        Assert.Contains(model.Payments, row => row.State == DashboardPaymentState.Pending);
        Assert.Contains(model.Payments, row => row.State == DashboardPaymentState.MarkedPaid);
        Assert.Contains(model.Payments, row => row.State == DashboardPaymentState.Confirmed);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Custom")]
    public void Preview_OutsideDevelopmentReturnsNotFound(string environment)
    {
        var controller = Controller(environment);

        Assert.IsType<NotFoundResult>(controller.Preview());
    }

    [Fact]
    public void BothActions_RequireTheExistingAuthenticationPolicy()
    {
        // Checks our declared endpoint policy; HTTP middleware must also be checked separately.
        Assert.NotNull(typeof(DashboardController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(DashboardController).GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var action in new[] { nameof(DashboardController.Index), nameof(DashboardController.Preview) })
        {
            var method = typeof(DashboardController).GetMethod(action)!;
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>());
        }
    }

    [Theory]
    [InlineData(10, "20 Oct 2026 13:00 UTC+13:00 (Pacific/Auckland)")]
    [InlineData(6, "20 Jun 2026 12:00 UTC+12:00 (Pacific/Auckland)")]
    public void PlanDateDisplay_UsesAucklandTimezoneAndDaylightSaving(int month, string expected)
    {
        var row = new DashboardPlanRow(OtherId, "Example", new DateTime(2026, month, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(expected, row.EndAtNewZealand);
    }

    [Fact]
    public void WeeklySummary_UsesLastChartGroupForCardsAndPreservesAllTimeTotals()
    {
        var model = Build(UserId, [],
        [
            new(OtherId, UserId, 2m, null, Now),
            new(UserId, OtherId, 3m, null, Now),
            new(OtherId, UserId, 4m, null, Now.AddDays(-7)),
            new(OtherId, UserId, 5m, null, Now.AddDays(-40)),
            new(OtherId, UserId, 1m, null, Now.AddDays(1))
        ], Now);

        var weekly = Assert.IsType<DashboardWeeklyViewModel>(model.WeeklySummary);
        Assert.Equal(4, weekly.Weeks.Count);
        Assert.Same(weekly.Weeks[^1], weekly.CurrentWeek);
        Assert.Equal(2m, weekly.CurrentWeek.Income);
        Assert.Equal(3m, weekly.CurrentWeek.Expenses);
        Assert.Equal(-1m, weekly.CurrentWeek.NetBalance);
        Assert.Equal(12m, model.ConfirmedIncome); // Existing all-time semantics remain unchanged.
        Assert.Equal(6m, weekly.Weeks.Sum(w => w.Income));
    }

    [Fact]
    public async Task Index_DoesNotInventWeeklyData()
    {
        var result = Assert.IsType<ViewResult>(await Controller("Development").Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Null(model.WeeklySummary);
        Assert.True(model.PlanDataAvailable);
        Assert.False(model.PaymentDataAvailable);
    }

    [Fact]
    public void EmptyConnectedData_HasFourZeroWeeksAndSafeChartScale()
    {
        var weekly = Assert.IsType<DashboardWeeklyViewModel>(Build(UserId, [], [], Now).WeeklySummary);

        Assert.Equal(4, weekly.Weeks.Count);
        Assert.Equal(0m, weekly.ChartMaximum);
        Assert.Equal(0m, weekly.HeightPercent(0m));
        Assert.Equal(0m, weekly.CurrentWeek.NetBalance);
    }

    [Fact]
    public void Chart_UsesSameScaleForIncomeAndExpenses()
    {
        var weekly = Assert.IsType<DashboardWeeklyViewModel>(Build(UserId, [],
        [
            new(OtherId, UserId, 4m, null, Now),
            new(UserId, OtherId, 2m, null, Now),
            new(OtherId, UserId, 1m, null, Now.AddDays(-7))
        ], Now).WeeklySummary);

        Assert.Equal(4m, weekly.ChartMaximum);
        Assert.Equal(100m, weekly.HeightPercent(weekly.CurrentWeek.Income));
        Assert.Equal(50m, weekly.HeightPercent(weekly.CurrentWeek.Expenses));
        Assert.Equal(25m, weekly.HeightPercent(weekly.Weeks[2].Income));
        Assert.Equal(0m, weekly.HeightPercent(0m));
    }

    [Fact]
    public void ChartScale_DividesBeforeMultiplyingToAvoidOverflow()
    {
        var weekly = new DashboardWeeklyViewModel([new(new DateOnly(2026, 10, 19), decimal.MaxValue, 0m)]);

        Assert.Equal(100m, weekly.HeightPercent(decimal.MaxValue));
    }

    [Theory]
    [InlineData(2026, 10, 18, 11)] // Exactly Monday midnight in Auckland.
    [InlineData(2026, 9, 27, 11)] // Monday after spring DST change.
    [InlineData(2026, 4, 5, 12)] // Monday after autumn DST change.
    public void Preview_ProvidesFourCalendarWeeksWithoutFuturePayments(int year, int month, int day, int hour)
    {
        var now = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
        var model = DashboardPreviewData.Create(now);
        var weekly = Assert.IsType<DashboardWeeklyViewModel>(model.WeeklySummary);

        Assert.True(model.IsPreview);
        Assert.Equal(4, weekly.Weeks.Count);
        Assert.All(weekly.Weeks, w => { Assert.True(w.Income > 0m); Assert.True(w.Expenses > 0m); });
        Assert.Equal(4m, weekly.Weeks.Sum(w => w.Income));
        Assert.Equal(2.5m, weekly.Weeks.Sum(w => w.Expenses));
        Assert.Equal(5m, model.ConfirmedIncome);
        Assert.Equal(3m, model.ConfirmedExpense);
        Assert.Equal(1.5m, weekly.CurrentWeek.Income);
        Assert.Equal(1m, weekly.CurrentWeek.Expenses);
    }

    [Fact]
    public async Task Index_UsesAuthenticatedIdentityAndMapsRealPlans()
    {
        var plans = new FakePlans { Results = [
            new() { Id = OtherId, Name = "Real active", EndAtUtc = DateTime.UtcNow.AddDays(2) },
            new() { Id = ThirdId, Name = "Real ended", EndAtUtc = DateTime.UtcNow.AddDays(-2) }] };
        var result = Assert.IsType<ViewResult>(await Controller("Development", plans).Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);
        Assert.Equal(UserId, plans.RequestedUser);
        Assert.Equal("Real active", Assert.Single(model.ActivePlans).Name);
        Assert.Equal(OtherId, model.ActivePlans[0].Id);
        Assert.Equal(plans.Results[0].EndAtUtc, model.ActivePlans[0].EndAtUtc);
        Assert.Equal(ThirdId, Assert.Single(model.EndedPlans).Id);
        Assert.True(model.PlanDataAvailable);
        Assert.Null(model.PlanLoadError);
        Assert.False(model.PaymentDataAvailable);
        Assert.Null(model.ConfirmedIncome);
        Assert.Null(model.ConfirmedExpense);
        Assert.Null(model.PendingIncome);
        Assert.Null(model.PendingExpense);
        Assert.Null(model.WeeklySummary);
        Assert.Empty(model.Payments);
    }

    [Fact]
    public async Task Index_QueryFailureShowsSafeErrorInsteadOfEmptySuccess()
    {
        var model = Assert.IsType<DashboardPageViewModel>(Assert.IsType<ViewResult>(
            await Controller("Development", new FakePlans { Fail = true }).Index()).Model);
        Assert.False(model.PlanDataAvailable);
        Assert.False(model.PaymentDataAvailable);
        Assert.False(string.IsNullOrWhiteSpace(model.PlanLoadError));
        Assert.DoesNotContain("private connection details", model.PlanLoadError);
        Assert.Empty(model.ActivePlans);
        Assert.Empty(model.EndedPlans);
        Assert.Null(model.WeeklySummary);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void PlansOnly_NormalizesUtcAndPreservesExactBoundary(DateTimeKind kind)
    {
        var end = DateTime.SpecifyKind(Now, kind);
        var model = BuildPlans([new(OtherId, "Boundary", end), new(ThirdId, "Next", end.AddTicks(1))], end);
        Assert.Equal(DateTimeKind.Utc, Assert.Single(model.EndedPlans).EndAtUtc.Kind);
        Assert.Equal(ThirdId, Assert.Single(model.ActivePlans).Id);
        Assert.Equal("20 Oct 2026 13:00 UTC+13:00 (Pacific/Auckland)", model.EndedPlans[0].EndAtNewZealand);
        Assert.True(model.PlanDataAvailable);
        Assert.False(model.PaymentDataAvailable);
        Assert.Null(model.ConfirmedIncome);
    }

    [Fact]
    public void PlansOnly_RejectsLocalDates()
    {
        Assert.Throws<ArgumentException>(() => BuildPlans([], DateTime.SpecifyKind(Now, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => BuildPlans([new(OtherId, "Invalid", DateTime.SpecifyKind(Now, DateTimeKind.Local))], Now));
    }

    [Fact]
    public void Preview_DoesNotQueryRealServices()
    {
        var plans = new FakePlans { Fail = true };
        Assert.IsType<ViewResult>(Controller("Development", plans).Preview());
        Assert.Null(plans.RequestedUser);
    }

    [Fact]
    public void Index_DoesNotAcceptBrowserUserId()
    {
        Assert.Empty(typeof(DashboardController).GetMethod(nameof(DashboardController.Index))!.GetParameters());
    }

    private static DashboardController Controller(string environment, FakePlans? plans = null) =>
        new(new TestEnvironment(environment), new FakeCurrentUser(), plans ?? new FakePlans());

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public Guid GetRequiredUserId() => UserId;
    }

    private sealed class FakePlans : IPlanService
    {
        public Guid? RequestedUser { get; private set; }
        public bool Fail { get; init; }
        public IReadOnlyList<PlanSummaryDto> Results { get; init; } = [];
        public Task<IReadOnlyList<PlanSummaryDto>> GetPlansForUserAsync(Guid userId)
        {
            RequestedUser = userId;
            return Fail ? Task.FromException<IReadOnlyList<PlanSummaryDto>>(new InvalidOperationException("private connection details"))
                : Task.FromResult(Results);
        }
        public Task<PlanDto?> GetPlanAsync(Guid planId) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetMemberIdsAtAsync(Guid planId, DateTime eventTimeUtc) => throw new NotSupportedException();
    }

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "FiveDollarPlan.Dashboard.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
