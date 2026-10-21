using System.Security.Claims;
using FiveDollarPlan.Models;
using FiveDollarPlan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiveDollarPlan.Controllers;

[Authorize]
public class PlanController : Controller
{
    private readonly IPlanWorkflowService _plans;
    private static readonly TimeZoneInfo NewZealandTimeZone = FindNewZealandTimeZone();

    public PlanController(IPlanWorkflowService plans)
    {
        _plans = plans;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var plans = await _plans.GetPlansForUserPageAsync(GetRequiredUserId());
        return View(plans);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new PlanCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlanCreateViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Enter a plan name.");
        }

        DateTime? startAtUtc = null;
        DateTime? endAtUtc = null;
        if (model.StartAtLocal.HasValue
            && TryConvertAucklandTime(model.StartAtLocal.Value, nameof(model.StartAtLocal), out var start))
        {
            startAtUtc = start;
        }

        if (model.EndAtLocal.HasValue
            && TryConvertAucklandTime(model.EndAtLocal.Value, nameof(model.EndAtLocal), out var end))
        {
            endAtUtc = end;
        }

        if (startAtUtc.HasValue && endAtUtc.HasValue && startAtUtc >= endAtUtc)
        {
            ModelState.AddModelError(nameof(model.EndAtLocal), "The end must be after the start.");
        }

        if (!ModelState.IsValid || !model.Mode.HasValue
            || !startAtUtc.HasValue || !endAtUtc.HasValue)
        {
            return View(model);
        }

        var plan = new Plan
        {
            Name = model.Name.Trim(),
            Amount = model.Amount,
            Mode = model.Mode.Value,
            StartAtUtc = startAtUtc.Value,
            EndAtUtc = endAtUtc.Value
        };

        var createdPlan = await _plans.CreatePlanAsync(GetRequiredUserId(), plan);
        TempData["SuccessMessage"] = "Plan created. Share this invitation link with your friends.";
        TempData["CreatedInviteUrl"] = Url.ActionLink(
            nameof(Invite),
            "Plan",
            new { code = createdPlan.InviteCode },
            Request.Scheme);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(JoinPlanViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Enter a valid invitation code.";
            return RedirectToAction(nameof(Index));
        }

        var outcome = await _plans.JoinPlanAsync(GetRequiredUserId(), model.InviteCode);
        TempData[outcome switch
        {
            JoinPlanResult.Joined => "SuccessMessage",
            JoinPlanResult.AlreadyMember => "InfoMessage",
            _ => "ErrorMessage"
        }] = outcome switch
        {
            JoinPlanResult.Joined => "You joined the plan.",
            JoinPlanResult.AlreadyMember => "You are already a member of this plan.",
            _ => "That invitation code was not found."
        };

        if (outcome != JoinPlanResult.InviteNotFound)
        {
            return RedirectToAction(nameof(Invite), new { code = model.InviteCode.Trim() });
        }

        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    [HttpGet("Plan/Invite/{code}")]
    public async Task<IActionResult> Invite(string code)
    {
        var plan = await _plans.GetPlanByInviteCodeAsync(code);
        if (plan is null)
        {
            return NotFound();
        }

        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var isMember = isAuthenticated
            && await _plans.IsMemberAsync(plan.Id, GetRequiredUserId());

        var viewModel = new PlanInviteViewModel
        {
            PlanId = plan.Id,
            Name = plan.Name,
            Amount = plan.Amount,
            Mode = plan.Mode,
            StartAtLocal = TimeZoneInfo.ConvertTimeFromUtc(plan.StartAtUtc, NewZealandTimeZone),
            EndAtLocal = TimeZoneInfo.ConvertTimeFromUtc(plan.EndAtUtc, NewZealandTimeZone),
            InviteCode = plan.InviteCode,
            IsMember = isMember,
            IsAuthenticated = isAuthenticated
        };

        ViewData["LoginReturnUrl"] = Url.Action(
            nameof(Invite),
            "Plan",
            new { code = plan.InviteCode });

        return View(viewModel);
    }

    private Guid GetRequiredUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException("The signed-in user does not have a valid user ID claim.");
        }

        return userId;
    }

    private bool TryConvertAucklandTime(DateTime localTime, string modelField, out DateTime utcTime)
    {
        var unspecifiedTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        if (NewZealandTimeZone.IsInvalidTime(unspecifiedTime))
        {
            ModelState.AddModelError(modelField, "This local time does not exist because of the daylight-saving change.");
            utcTime = default;
            return false;
        }

        if (NewZealandTimeZone.IsAmbiguousTime(unspecifiedTime))
        {
            ModelState.AddModelError(modelField, "This local time occurs twice because of the daylight-saving change. Choose another time.");
            utcTime = default;
            return false;
        }

        utcTime = TimeZoneInfo.ConvertTimeToUtc(unspecifiedTime, NewZealandTimeZone);
        return true;
    }

    private static TimeZoneInfo FindNewZealandTimeZone()
    {
        foreach (var timeZoneId in new[] { "Pacific/Auckland", "New Zealand Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the platform's alternative time-zone identifier.
            }
        }

        throw new TimeZoneNotFoundException("The Pacific/Auckland time zone is not available.");
    }
}
