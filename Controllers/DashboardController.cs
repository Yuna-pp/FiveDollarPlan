using FiveDollarPlan.Models.Dashboard;
using FiveDollarPlan.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace FiveDollarPlan.Controllers;

[Authorize]
public class DashboardController(IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        // Do not turn missing data services into a misleading zero balance.
        return View("Index", new DashboardPageViewModel { CanPreview = environment.IsDevelopment() });
    }

    [HttpGet]
    public IActionResult Preview()
    {
        if (!environment.IsDevelopment())
            return NotFound();

        return View("Index", DashboardPreviewData.Create(DateTime.UtcNow));
    }
}
