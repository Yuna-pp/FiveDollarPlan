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

        Assert.True(model.DataAvailable);
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

        Assert.True(model.DataAvailable);
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
    public void Index_ShowsUnavailableDataWithoutFictionalValues(string environment, bool canPreview)
    {
        var controller = new DashboardController(new TestEnvironment(environment));

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Equal("Index", result.ViewName);
        Assert.False(model.DataAvailable);
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
        var controller = new DashboardController(new TestEnvironment(Environments.Development));

        var result = Assert.IsType<ViewResult>(controller.Preview());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Equal("Index", result.ViewName);
        Assert.True(model.IsPreview);
        Assert.True(model.DataAvailable);
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
        var controller = new DashboardController(new TestEnvironment(environment));

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
