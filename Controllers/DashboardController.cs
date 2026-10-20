using FiveDollarPlan.Models.Dashboard;
using FiveDollarPlan.Services.Dashboard;
using FiveDollarPlan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace FiveDollarPlan.Controllers;

[Authorize]
public class DashboardController(IWebHostEnvironment environment,
    ICurrentUserService currentUserService, IPlanService planService, IPaymentService paymentService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Identity comes only from the existing authenticated session.
        var userId = currentUserService.GetRequiredUserId();
        var utcNow = DateTime.UtcNow;
        DashboardPageViewModel model;
        try
        {
            var plans = await planService.GetPlansForUserAsync(userId);
            model = DashboardPageBuilder.BuildPlans(plans.Select(plan =>
                new DashboardPageBuilder.PlanInput(plan.Id, plan.Name, plan.EndAtUtc)), utcNow);
        }
        catch (Exception)
        {
            // A failed query is unavailable data, not a successful empty result.
            model = new DashboardPageViewModel
            {
                PlanLoadError = "Unable to load your plans. Please try again later."
            };
        }

        // Await separately: both services may use the same scoped DbContext.
        try
        {
            var payments = await paymentService.GetUserPaymentsAsync(userId);
            var paymentModel = DashboardPageBuilder.BuildPayments(userId, payments.Select(payment =>
                new DashboardFinancialCalculator.PaymentInput(payment.PayerId, payment.RecipientId,
                    payment.Amount, payment.PaidAtUtc, payment.ReceivedAtUtc)), utcNow);
            model = paymentModel with
            {
                PlanDataAvailable = model.PlanDataAvailable,
                PlanLoadError = model.PlanLoadError,
                ActivePlans = model.ActivePlans,
                EndedPlans = model.EndedPlans
            };
        }
        catch (Exception)
        {
            model = model with { PaymentLoadError = "Unable to load your payments. Please try again later." };
        }

        return View("Index", model with { CanPreview = environment.IsDevelopment() });
    }

    [HttpGet]
    public IActionResult Preview()
    {
        if (!environment.IsDevelopment())
            return NotFound();

        return View("Index", DashboardPreviewData.Create(DateTime.UtcNow));
    }
}
