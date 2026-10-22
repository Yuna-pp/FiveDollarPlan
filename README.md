# FiveDollarPlan

**A simple way to keep a promise.**

FiveDollarPlan is a four-person SWEN502 course project at Te Herenga Waka—Victoria University of Wellington. It helps friends set goals, invite each other to plans, and track small financial commitments when a plan's creator reports a missed commitment.

[Live demo](https://fivedollarplan.onrender.com/) · [Team](#team--contributions) · [Local setup](#run-locally)

## What it does

- **Accounts:** Register and log in with an email address and password, with optional avatar upload. Includes email format validation, duplicate email checks, password hashing, and cookie authentication.
- **Plans and invitations:** Create a plan with dates, an amount from NZ$0 to NZ$5 per participant, and Single or Multiple mode. Share an invitation link or code so friends can join.
- **Breach records:** The creator can record a missed commitment during the active plan period. Each event generates payment records for the other eligible members; Single plans allow one event, while Multiple plans allow repeated events.
- **Payment confirmations:** The payer marks a payment as paid, then the recipient confirms receipt. Payment history keeps each event's records separate and shows the newest events first.
- **Dashboard:** View active and ended plans, confirmed and pending income/expenses, and four weeks of financial summaries.

**Payments happen outside the application.** The website records users' payment and receipt confirmations; it does not transfer money or verify bank transactions. Email format validation does not verify ownership of an email address.

## Team & contributions

| Team member | Primary responsibilities |
| --- | --- |
| [Yuna-pp](https://github.com/Yuna-pp) | Account registration and login; database design and integration. |
| [niniQAQ1](https://github.com/niniQAQ1) | Payment tracking and receipt confirmation. |
| [hanqingrui-creator](https://github.com/hanqingrui-creator) | Dashboard and financial summaries. |
| [Nora-shi6](https://github.com/Nora-shi6) | Plan creation and related plan workflows. |

This is a collaborative team project. The table describes each member's main area of responsibility; the final application brings these components together.

## Technology stack

| Area | Technologies |
| --- | --- |
| Backend | C#, .NET 10, ASP.NET Core MVC |
| Interface | Razor views, HTML, CSS, Bootstrap, JavaScript |
| Data access | Entity Framework Core |
| Local database | SQLite |
| Deployed database | PostgreSQL on Render; Data Protection keys also persist in the database |
| Testing | xUnit and Python HTTP regression checks |
| Deployment | Docker, GitHub, Render |

## Run locally

Install Git and the **.NET 10 SDK**, then:

```bash
git clone https://github.com/FiveDollarPlan-Team/FiveDollarPlan.git
cd FiveDollarPlan
dotnet restore
dotnet run --launch-profile http
```

Open **http://localhost:5252**. If that port is occupied, use:

```bash
dotnet run --launch-profile http --urls http://localhost:5253
```

Local development defaults to SQLite (`fivedollar.db`). Startup applies the existing SQLite migrations. The local database and the deployed PostgreSQL database are separate and do not automatically synchronize.

## Try the main workflow

1. Register an account and log in.
2. Create a plan with a start time before now and an end time in the future.
3. Share its invitation link or code with a friend, who signs in and joins.
4. As the creator, record a breach to generate payment records.
5. After payment outside the application, mark it as paid; the recipient then confirms receipt.
6. Review payment history and the Dashboard.

Use a separate browser or private window for a second account. Plan times use **Pacific/Auckland** and are stored in UTC. A plan ending and its payments being settled are separate states.

## Tests

```bash
dotnet test Tests/FiveDollarPlan.Dashboard.Tests/FiveDollarPlan.Dashboard.Tests.csproj
dotnet test Tests/FiveDollarPlan.Persistence.Tests/FiveDollarPlan.Persistence.Tests.csproj
```

Additional HTTP regression checks use a temporary database and server. They require Python 3.9+, .NET 10, and the EF Core CLI (`dotnet tool install --global dotnet-ef --version "10.0.*"`):

```bash
python3 checks/PaymentHistoryChecks/verify.py
```

## Deployment and data persistence

Render deploys the **`render-setup`** branch using Docker, which packages the application with its .NET runtime and listens on port `10000`. The web service connects to an independent PostgreSQL database, so accounts, plans, memberships, payment records, and authentication keys survive web-instance restarts and redeployments.

Connection details belong in Render's secret environment variables, never in Git. See [PERSISTENCE.md](PERSISTENCE.md) for configuration and schema-management details. The free database currently expires on **20 November 2026**; it must be exported or upgraded before expiry. The free web service may take longer to respond after inactivity. Uploaded avatar files still use temporary web-instance storage and may not survive restarts.

## Code layout

`Controllers/` handles requests; `Services/` contains business logic; `Models/` defines entities, DTOs, and view models; `Data/` and `Migrations/` manage persistence; `Views/` and `wwwroot/` provide the interface. `Tests/` and `checks/` contain automated checks.

## Repositories

- [GitHub team repository](https://github.com/FiveDollarPlan-Team/FiveDollarPlan)
- [Original GitLab course repository](https://gitlab.ecs.vuw.ac.nz/course-work/swen502/2026/projects/taoyuni/fivedollarsplan) — university access may be required.
