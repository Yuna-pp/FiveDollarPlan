# Payment module handoff

## Branches
- feature/payments: original payment MR !3.
- feature/payments-integration: latest implementation and checks.
- Integration branch includes feature/database-integration dependencies.
- Do not merge both branches without checking overlapping changes.

## Implemented
- PenaltyEvent and PaymentRecord models.
- Creator-only penalty triggering.
- Event-time membership snapshot read inside the event transaction.
- Creator excluded from payment recipients.
- Single-plan duplicate trigger rejection.
- Separate payment records for multiple events.
- Payer-only payment confirmation.
- Recipient-only receipt confirmation, after payment confirmation.
- Repeated confirmation preserves the original timestamp.
- Per-event confirmation summaries.
- Authorized plan details page and POST actions.

## Service contract
IPaymentService:
- TriggerAsync(Guid planId, Guid actorId)
- MarkPaidAsync(Guid paymentId, Guid actorId)
- ConfirmReceivedAsync(Guid paymentId, Guid actorId)
- GetPlanPaymentStatusAsync(Guid planId)
- GetUserPaymentsAsync(Guid userId)

The actor ID must come from ICurrentUserService, not submitted form data.
Query methods rely on their callers to enforce access permissions.

## Routes
- GET /Payment/Details/{planId}
- GET /Payment/Details/{planId}?eventId={eventId}
- POST /Payment/Trigger (form field: planId)
- POST /Payment/MarkPaid/{paymentId}
- POST /Payment/ConfirmReceived/{paymentId}

POST actions require authentication and an anti-forgery token.
Details defaults to the latest event.
An explicit eventId must belong to the requested plan.

## Dependencies and registration
- AppDbContext and PlanService must share the same scoped context.
- IPlanService provides plan details and event-time member IDs.
- IUserService provides user profiles.
- ICurrentUserService provides the logged-in user ID.
- Register PenaltyEventService, PaymentService and IPaymentService.
- Database mappings and migrations are maintained by the database owner.

## Verification
Run from the repository root:

dotnet run --project checks/PaymentChecks/PaymentChecks.csproj

12 checks passed locally:
- Payment and receipt permissions and order.
- Repeated confirmation preserves timestamps.
- Event and payment persistence in SQLite.
- Creator excluded from recipients.
- Single-plan duplicate rejection.
- Later joins do not change existing event records.
- Saved confirmation status and summaries.
- Multiple events retain independent payment status.
- Non-creator trigger rejection.
- Two concurrent single-plan trigger attempts:
  one created, one duplicate, one event and one payment saved.

These checks do not replace browser or broader concurrency testing.

## Outstanding decisions and acceptance
- Confirm trigger-window semantics with the plan owner.
  Current implementation compares UTC timestamps as a date range.
- Confirm latest-event default and navigation to older unpaid events.
  Explicit event URLs work; no event navigation UI is implemented.
- Confirm zero-recipient event handling.
  Current implementation permits an event with no payments;
  AllConfirmed is null when there are no payment records.
- Confirm post-end payment confirmation policy.
  Current payment confirmation methods do not check the plan end time.
- Browser acceptance with two accounts is pending.
- Join-versus-trigger and concurrent confirmation tests are pending.
- Existing dependency vulnerability warnings remain for team follow-up.