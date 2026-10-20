using System.Globalization;

namespace FiveDollarPlan.Models.Dashboard;

// Public for Razor's generated view types; these are Dashboard-only presentation models.
public sealed record DashboardPageViewModel
{
    public DashboardWeeklyViewModel? WeeklySummary { get; init; }
    public bool DataAvailable { get; init; }
    public bool IsPreview { get; init; }
    public bool CanPreview { get; init; }
    public decimal? ConfirmedIncome { get; init; }
    public decimal? ConfirmedExpense { get; init; }
    public decimal? PendingIncome { get; init; }
    public decimal? PendingExpense { get; init; }
    public IReadOnlyList<DashboardPlanRow> ActivePlans { get; init; } = [];
    public IReadOnlyList<DashboardPlanRow> EndedPlans { get; init; } = [];
    public IReadOnlyList<DashboardPaymentRow> Payments { get; init; } = [];
}

public sealed record DashboardPlanRow(Guid Id, string Name, DateTime EndAtUtc)
{
    public string EndAtNewZealand => TimeZoneInfo.ConvertTime(
        new DateTimeOffset(EndAtUtc), TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"))
        .ToString("dd MMM yyyy HH:mm 'UTC'zzz '(Pacific/Auckland)'", CultureInfo.GetCultureInfo("en-NZ"));
}

public enum DashboardPaymentState { Pending, MarkedPaid, Confirmed }

public sealed record DashboardPaymentRow(string Direction, decimal Amount, DashboardPaymentState State);

// Dashboard-specific presentation data; not a shared service DTO.
public sealed record DashboardWeeklyViewModel(IReadOnlyList<DashboardWeekViewModel> Weeks)
{
    public DashboardWeekViewModel CurrentWeek => Weeks[^1];
    public decimal ChartMaximum => Weeks.Max(week => Math.Max(week.Income, week.Expenses));
    public decimal HeightPercent(decimal amount) => ChartMaximum == 0m ? 0m : amount / ChartMaximum * 100m;
}

public sealed record DashboardWeekViewModel(DateOnly WeekStart, decimal Income, decimal Expenses)
{
    public DateOnly WeekEnd => WeekStart.AddDays(6);
    public decimal NetBalance => Income - Expenses;
}
