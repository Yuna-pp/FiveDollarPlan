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
    private readonly PaymentService _payments;
    private readonly PenaltyEventService _events;

    public PaymentController(
        AppDbContext db,
        IPlanService plans,
        IUserService users,
        ICurrentUserService currentUser,
        PaymentService payments,
        PenaltyEventService events)
    {
        _db = db;
        _plans = plans;
        _users = users;
        _currentUser = currentUser;
        _payments = payments;
        _events = events;
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
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

        // 暂时显示最新一次事件，旧事件仍保留在数据库。
        var currentEvent = await _db.Set<PenaltyEvent>()
            .AsNoTracking()
            .Where(e => e.PlanId == id)
            .OrderByDescending(e => e.TriggeredAtUtc)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync();

        var payments = new List<PaymentRecord>();

        if (currentEvent != null)
        {
            payments = await _db.Set<PaymentRecord>()
                .AsNoTracking()
                .Where(p => p.PenaltyEventId == currentEvent.Id)
                .OrderBy(p => p.RecipientId)
                .ToListAsync();
        }

        var model = new PlanDetailsViewModel
        {
            Plan = plan,
            CurrentUserId = actorId,
            Creator = await _users.GetUserByIdAsync(plan.CreatorId),
            Members = await _users.GetUsersByIdsAsync(memberIds),
            CurrentEvent = currentEvent,
            Payments = payments
        };

        return View(model);
    }

    [HttpPost("Trigger")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trigger(Guid planId)
    {
        try
        {
            await _events.TriggerAsync(
                planId, _currentUser.GetRequiredUserId());

            TempData["SuccessMessage"] = "Penalty event created.";
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

        return RedirectToAction(nameof(Details), new { id = planId });
    }

    [HttpPost("MarkPaid/{paymentId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkPaid(Guid paymentId)
    {
        return UpdatePaymentAsync(paymentId, confirmReceived: false);
    }

    [HttpPost("ConfirmReceived/{paymentId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ConfirmReceived(Guid paymentId)
    {
        return UpdatePaymentAsync(paymentId, confirmReceived: true);
    }

    private async Task<IActionResult> UpdatePaymentAsync(
        Guid paymentId, bool confirmReceived)
    {
        // 从数据库确定所属计划，不相信网页传来的计划编号。
        var planId = await (
            from payment in _db.Set<PaymentRecord>()
            join penalty in _db.Set<PenaltyEvent>()
                on payment.PenaltyEventId equals penalty.Id
            where payment.Id == paymentId
            select (Guid?)penalty.PlanId
        ).SingleOrDefaultAsync();

        if (planId == null)
        {
            return NotFound();
        }

        var actorId = _currentUser.GetRequiredUserId();

        try
        {
            if (confirmReceived)
            {
                await _payments.ConfirmReceivedAsync(paymentId, actorId);
            }
            else
            {
                await _payments.MarkPaidAsync(paymentId, actorId);
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

        return RedirectToAction(
            nameof(Details), new { id = planId.Value });
    }
}