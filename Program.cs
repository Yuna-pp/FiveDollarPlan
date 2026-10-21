using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using FiveDollarPlan.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using FiveDollarPlan.Models;
using FiveDollarPlan.Services;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    ICurrentUserService,
    CurrentUserService>();

builder.Services.AddScoped<
    IUserService,
    UserService>();
builder.Services.AddScoped<PlanService>();

builder.Services.AddScoped<IPlanService>(sp =>
    sp.GetRequiredService<PlanService>());

builder.Services.AddScoped<IPlanWorkflowService>(sp =>
    sp.GetRequiredService<PlanService>());

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=fivedollar.db";

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var usePostgres = databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
if (!usePostgres && !databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("DatabaseProvider must be Sqlite or Postgres.");

if (usePostgres)
{
    var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
        ?? builder.Configuration["DATABASE_URL"]
        ?? throw new InvalidOperationException("Postgres requires ConnectionStrings__Postgres or DATABASE_URL.");
    var normalizedConnection = DatabaseConfiguration.NormalizePostgresConnection(postgresConnection);
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(normalizedConnection));
    builder.Services.AddDataProtection()
        .SetApplicationName("FiveDollarPlan")
        .PersistKeysToDbContext<AppDbContext>();
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Cookie.Name = "FiveDollarPlan.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddScoped<PenaltyEventService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<IPaymentService>(sp =>
    sp.GetRequiredService<PaymentService>());

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
// Apply database migrations automatically when the app starts
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (usePostgres)
    {
        // PostgreSQL has its own schema. Never apply the SQLite-specific migrations here.
        // Creates the initial schema only; it never drops or resets an existing database.
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }
}

app.Run();
