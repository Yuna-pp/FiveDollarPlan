using FiveDollarPlan.Data;
using FiveDollarPlan.Models;
using FiveDollarPlan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiveDollarPlan.Controllers;

[Authorize]
[Route("Payment")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPlanService _plans;
    private readonly IUserService _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentService _payments;

    public PaymentController(
        AppDbContext db,
        IPlanService plans,
        IUserService users,
        ICurrentUserService currentUser,
        IPaymentService payments)
    {
        _db = db;
        _plans = plans;
        _users = users;
        _currentUser = currentUser;
        _payments = payments;
    }

    // 默认显示最新事件，也支持通过 eventId 查看指定事件。
    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(
        Guid id, Guid? eventId = null)
    {
        var actorId = _currentUser.GetRequiredUserId();
        var plan = await _plans.GetPlanAsync(id);

        if (plan == null)
        {
            return NotFound();
        }

        var memberIds = await _plans.GetMemberIdsAtAsync(
            id, DateTime.UtcNow);

        if (actorId != plan.CreatorId &&
            !memberIds.Contains(actorId))
        {
            return Forbid();
        }

        // 指定的事件必须属于当前计划。
        var eventQuery = _db.Set<PenaltyEvent>()
            .AsNoTracking()
            .Where(e => e.PlanId == id);

        if (eventId.HasValue)
        {
            eventQuery = eventQuery.Where(
                e => e.Id == eventId.Value);
        }

        var currentEvent = await eventQuery
            .OrderByDescending(e => e.TriggeredAtUtc)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync();

        if (eventId.HasValue && currentEvent == null)
        {
            return NotFound();
        }

        var payments = new List<PaymentRecord>();

        if (currentEvent != null)
        {
            payments = await _db.Set<PaymentRecord>()
                .AsNoTracking()
                .Where(p => p.PenaltyEventId == currentEvent.Id)
                .OrderBy(p => p.RecipientId)
                .ToListAsync();
        }

        // 同时查询当前成员与这次事件的收款人资料。
        var profileIds = memberIds
            .Concat(payments.Select(p => p.RecipientId))
            .Append(plan.CreatorId)
            .Distinct()
            .ToList();

        var profiles = await _users.GetUsersByIdsAsync(profileIds);

        var model = new PlanDetailsViewModel
        {
            Plan = plan,
            CurrentUserId = actorId,
            Creator = profiles.FirstOrDefault(
                u => u.Id == plan.CreatorId),
            Members = profiles
                .Where(u => memberIds.Contains(u.Id))
                .ToList(),
            CurrentEvent = currentEvent,
            Payments = payments
        };

        return View(model);
    }

    // 创建违约事件，并跳转到刚创建的事件。
    [HttpPost("Trigger")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trigger(Guid planId)
    {
        try
        {
            var eventId = await _payments.TriggerAsync(
                planId, _currentUser.GetRequiredUserId());

            TempData["SuccessMessage"] = "Penalty event created.";

            return RedirectToAction(
                nameof(Details), new { id = planId, eventId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            TempData["PaymentError"] = ex.Message;
        }

        return RedirectToAction(
            nameof(Details), new { id = planId });
    }

    // 付款人确认线下付款。
    [HttpPost("MarkPaid/{paymentId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkPaid(Guid paymentId)
    {
        return UpdatePaymentAsync(
            paymentId, confirmReceived: false);
    }

    // 收款人确认线下收款。
    [HttpPost("ConfirmReceived/{paymentId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ConfirmReceived(Guid paymentId)
    {
        return UpdatePaymentAsync(
            paymentId, confirmReceived: true);
    }

    private async Task<IActionResult> UpdatePaymentAsync(
        Guid paymentId, bool confirmReceived)
    {
        // 从数据库查询真实的计划编号与事件编号。
        var location = await (
            from payment in _db.Set<PaymentRecord>()
            join penalty in _db.Set<PenaltyEvent>()
                on payment.PenaltyEventId equals penalty.Id
            where payment.Id == paymentId
            select new
            {
                PlanId = penalty.PlanId,
                EventId = penalty.Id
            }
        ).SingleOrDefaultAsync();

        if (location == null)
        {
            return NotFound();
        }

        var actorId = _currentUser.GetRequiredUserId();

        try
        {
            if (confirmReceived)
            {
                await _payments.ConfirmReceivedAsync(
                    paymentId, actorId);
            }
            else
            {
                await _payments.MarkPaidAsync(
                    paymentId, actorId);
            }

            TempData["SuccessMessage"] = "Payment status updated.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            TempData["PaymentError"] = ex.Message;
        }

        // 确认旧事件的付款后，仍返回该事件。
        return RedirectToAction(
            nameof(Details),
            new
            {
                id = location.PlanId,
                eventId = location.EventId
            });
    }
}