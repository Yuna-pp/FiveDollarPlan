using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FiveDollarPlan.Data;
using FiveDollarPlan.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.Data.Sqlite;

namespace FiveDollarPlan.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AccountController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string name = model.Name.Trim();
        string email = model.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(model.Name), "Please enter your name.");
            return View(model);
        }

        bool emailExists = await _context.Users
            .AnyAsync(u => u.Email == email);

        if (emailExists)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "This email is already registered."
            );

            return View(model);
        }

        var user = new User
        {
            Name = name,
            Email = email
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            model.Password
        );

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqliteException sqliteEx
                && sqliteEx.SqliteErrorCode == 19
                && sqliteEx.SqliteExtendedErrorCode == 2067)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "This email is already registered."
            );

            return View(model);
        }

        TempData["SuccessMessage"] = "Registration successful!";

        return RedirectToAction("Index", "Home");
    }
    [HttpGet]
public IActionResult Login()
{
    return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    string email = model.Email.Trim().ToLowerInvariant();

    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Email == email);

    if (user == null)
    {
        ModelState.AddModelError(
            "",
            "Invalid email or password."
        );

        return View(model);
    }

    var result = _passwordHasher.VerifyHashedPassword(
        user,
        user.PasswordHash,
        model.Password
    );

    if (result == PasswordVerificationResult.Failed)
    {
        ModelState.AddModelError(
            "",
            "Invalid email or password."
        );

        return View(model);
    }

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Email, user.Email)
    };

    var identity = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme
    );

    var principal = new ClaimsPrincipal(identity);

    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal
    );

    return RedirectToAction("Index", "Home");
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Logout()
{
    await HttpContext.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme
    );

    return RedirectToAction("Login", "Account");
}
}