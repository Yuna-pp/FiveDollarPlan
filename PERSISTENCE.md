# Render database persistence

The deployment uses an independent free Render PostgreSQL database. Website restarts, idle spin-down and redeploys no longer reset accounts, plans, memberships or payment records. Data Protection keys are stored in the same database so authentication keys survive instance replacement.

## Configuration (Render secrets only)

- `DatabaseProvider=Postgres`
- `DATABASE_URL`: the Render **internal** PostgreSQL connection URL, in the same region as the web service. Never commit credentials.
- Alternatively, `ConnectionStrings__Postgres` accepts a native Npgsql connection string.

Local development defaults to SQLite and continues to use the existing SQLite migrations. A configured PostgreSQL deployment fails startup when its connection is missing or unavailable; it never silently falls back to an empty SQLite file.

For the initially empty PostgreSQL database, startup uses `EnsureCreatedAsync` to create the PostgreSQL schema from the current model, including `DataProtectionKeys`. It does not delete or recreate an existing schema. SQLite migrations contain SQLite-specific types and are intentionally not applied to PostgreSQL. Before future schema changes, introduce a separate PostgreSQL migration baseline; `EnsureCreated` does not upgrade existing tables.

The database was created on 2026-10-21; Render shows expiration on **2026-11-20**. This covers the presentation deadline of **2026-10-22 16:00 Pacific/Auckland**. Free PostgreSQL has limited capacity and no paid backup guarantee; export important data before expiry. Do not delete or suspend the database before the presentation. Web-service idle spin-down may still delay the first request, without erasing database records.

Existing ephemeral SQLite records that were already lost cannot be recovered by changing database providers. Recreate missing accounts and plans once after deployment, unless an independent backup is available. Old invitation codes will only work if their original plan records are recovered.

Uploaded avatars are still stored on the web container filesystem and are not covered by PostgreSQL persistence; use the default avatar for this presentation. Persistent user-upload storage is a separate follow-up.

## Verification

```sh
dotnet test Tests/FiveDollarPlan.Persistence.Tests/FiveDollarPlan.Persistence.Tests.csproj
dotnet test Tests/FiveDollarPlan.Dashboard.Tests/FiveDollarPlan.Dashboard.Tests.csproj
python3 checks/PaymentHistoryChecks/verify.py
```

After deployment, create a test account and plan, record the invitation code, restart the Render web service, and verify that the existing account can still log in and the same invitation still resolves.
