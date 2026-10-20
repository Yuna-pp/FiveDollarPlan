using Microsoft.EntityFrameworkCore;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

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


app.Run();
