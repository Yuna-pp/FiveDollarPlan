# FiveDollarPlan

FiveDollarPlan is a group accountability web application built for the SWEN502 course project at Te Herenga Waka—Victoria University of Wellington. Users create goal-based plans, invite friends, and track small financial commitments when a plan's creator reports a breach.

**Live demo:** https://fivedollarplan.onrender.com/

## Features

- **Accounts:** Register and log in with an email address and password, with an optional profile picture. Includes basic email format validation, duplicate email checks, password hashing, and cookie authentication.
- **Plans:** Create plans with a name, an amount from NZ$0 to NZ$5, start and end times, and Single or Multiple mode. Invite others using a shareable invitation link or code.
- **Breach events:** A plan's creator can trigger a breach during the active period. Single plans allow one event; Multiple plans allow repeated events. Each event creates a payment record from the creator to each other eligible member.
- **Payment tracking:** Payers mark payments as paid, and recipients confirm receipt. View breach history with the latest events first and payment status for each event.
- **Dashboard:** View active and ended plans, payment summaries, and a weekly financial overview.

Payments take place outside the application. FiveDollarPlan records payment and receipt confirmations; it does not transfer money or process card payments. Email addresses are checked for basic format, but email ownership verification is not implemented.

## Technology

- C# and .NET 10
- ASP.NET Core MVC with Razor views
- Entity Framework Core 10 and SQLite
- Bootstrap, CSS, and JavaScript
- xUnit tests
- Docker deployment on Render

## Run locally

Install the .NET 10 SDK and Git, then clone the repository:

```bash
git clone https://github.com/Yuna-pp/FiveDollarPlan.git
cd FiveDollarPlan
dotnet restore
```

Initialize the database using the EF Core CLI. Install the tool if it is not already available:

```bash
dotnet tool install --global dotnet-ef --version "10.0.*"
dotnet ef database update
```

Start the application:

```bash
dotnet run --launch-profile http
```

Open **http://localhost:5252**. If the port is already in use:

```bash
dotnet run --launch-profile http --urls http://localhost:5253
```

The default SQLite database is `fivedollar.db` in the project directory. Configure a different location using `ConnectionStrings:DefaultConnection` in application configuration or the `ConnectionStrings__DefaultConnection` environment variable.

## Try the workflow

1. Register an account and log in.
2. Create a plan with a start time before the current time and an end time in the future to demonstrate an active plan.
3. Share its invitation link or code with another user. Use a separate browser or private window for the second account.
4. Have the second user log in and join the plan.
5. As the creator, trigger a breach to generate payment records.
6. After paying outside the application, mark the payment as paid. The recipient can then confirm receipt.
7. Review the payment history and Dashboard.

Plan times are entered in the Pacific/Auckland time zone and stored in UTC.

## Tests

Run the Dashboard test suite:

```bash
dotnet test Tests/FiveDollarPlan.Dashboard.Tests/FiveDollarPlan.Dashboard.Tests.csproj
```

Additional HTTP regression checks cover registration, plans, payment history, and related workflows. They require Python 3.9+, the .NET 10 SDK, and the EF Core CLI, and create an isolated temporary database and server:

```bash
python3 checks/PaymentHistoryChecks/verify.py
```

## Project structure

```text
Controllers/    MVC request handlers
Models/         Database entities, view models, and data contracts
Services/       Account context, plans, payments, and Dashboard logic
Data/           Entity Framework database context
Migrations/     Database schema migrations
Views/          Razor pages and shared layouts
wwwroot/        Stylesheets, JavaScript, and static assets
Tests/          Automated Dashboard tests
checks/         Additional regression checks
Program.cs      Application startup and dependency registration
```

## Deployment

GitHub `main` contains the synchronized course application code. The `render-setup` branch includes the Dockerfile and startup database migration logic used by the current Render service.

The Docker image builds with the .NET 10 SDK and runs with the ASP.NET Core 10 runtime on port `10000`. Render is configured to deploy the `render-setup` branch automatically when it changes.

The Render service uses a free instance, so its first request after inactivity can take longer. SQLite data and uploaded avatars need persistent storage to survive instance replacement or redeployment; do not rely on an ephemeral container filesystem for lasting data.

## Course repositories

- GitLab: https://gitlab.ecs.vuw.ac.nz/course-work/swen502/2026/projects/taoyuni/fivedollarsplan
- GitHub: https://github.com/Yuna-pp/FiveDollarPlan

The GitLab course repository may require university access.
