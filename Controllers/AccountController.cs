using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using FiveDollarPlan.Data;
using FiveDollarPlan.Models;
using System.Security.Claims;

namespace FiveDollarPlan.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AccountController(
        AppDbContext context,
        IWebHostEnvironment environment,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _environment = environment;
        _passwordHasher = passwordHasher;
    }

    // ==========================
    // REGISTER - GET
    // ==========================

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // ==========================
    // REGISTER - POST
    // ==========================

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
            ModelState.AddModelError(
                nameof(model.Name),
                "Please enter your name."
            );

            return View(model);
        }

        // Check whether the email is already registered
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

        // Validate and save the profile picture
        string? avatarUrl = null;

        try
        {
            avatarUrl = await SaveAvatarAsync(model.Avatar);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(
                nameof(model.Avatar),
                ex.Message
            );

            return View(model);
        }

        // Create user
        var user = new User
        {
            Name = name,
            Email = email,
            AvatarUrl = avatarUrl
        };

        // Hash password before saving
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
            // Remove uploaded image if registration fails
            DeleteAvatar(avatarUrl);

            ModelState.AddModelError(
                nameof(model.Email),
                "This email is already registered."
            );

            return View(model);
        }
        catch
        {
            // Remove uploaded image if database saving fails
            DeleteAvatar(avatarUrl);
            throw;
        }

        TempData["SuccessMessage"] = "Registration successful!";

        return RedirectToAction("Index", "Home");
    }

    // ==========================
    // LOGIN - GET
    // ==========================

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // ==========================
    // LOGIN - POST
    // ==========================

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

    // ==========================
    // LOGOUT
    // ==========================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction("Login", "Account");
    }

    // ==========================
    // SAVE PROFILE PICTURE
    // ==========================

    private async Task<string?> SaveAvatarAsync(IFormFile? avatar)
    {
        // Avatar is optional
        if (avatar == null || avatar.Length == 0)
        {
            return null;
        }

        // Maximum size: 2 MB
        if (avatar.Length > 2 * 1024 * 1024)
        {
            throw new ArgumentException(
                "Profile picture must be smaller than 2 MB."
            );
        }

        var allowedExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        string extension = Path.GetExtension(
            avatar.FileName
        ).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            throw new ArgumentException(
                "Only JPG, PNG and WebP images are allowed."
            );
        }

        // Check the actual image file signature
        byte[] header = new byte[12];

        using (var input = avatar.OpenReadStream())
        {
            int bytesRead = await input.ReadAsync(
                header.AsMemory(0, header.Length)
            );

            bool isJpeg =
                bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;

            bool isPng =
                bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(
                    new byte[]
                    {
                        137, 80, 78, 71, 13, 10, 26, 10
                    }
                );

            bool isWebp =
                bytesRead >= 12 &&
                System.Text.Encoding.ASCII.GetString(
                    header, 0, 4
                ) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(
                    header, 8, 4
                ) == "WEBP";

            bool validFormat = extension switch
            {
                ".jpg" or ".jpeg" => isJpeg,
                ".png" => isPng,
                ".webp" => isWebp,
                _ => false
            };

            if (!validFormat)
            {
                throw new ArgumentException(
                    "The selected file is not a valid image."
                );
            }
        }

        // Create the upload folder if it doesn't exist
        string webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        string folder = Path.Combine(
            webRoot,
            "uploads",
            "avatars"
        );

        Directory.CreateDirectory(folder);

        // Use a random filename
        string fileName = $"{Guid.NewGuid()}{extension}";

        string filePath = Path.Combine(
            folder,
            fileName
        );

        // Save image
        using (var stream = new FileStream(
            filePath,
            FileMode.CreateNew
        ))
        {
            await avatar.CopyToAsync(stream);
        }

        // Save this relative URL to SQLite
        return $"/uploads/avatars/{fileName}";
    }

    // ==========================
    // DELETE PROFILE PICTURE
    // Used when registration fails
    // ==========================

    private void DeleteAvatar(string? avatarUrl)
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            return;
        }

        string webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        string fileName = Path.GetFileName(avatarUrl);

        string filePath = Path.Combine(
            webRoot,
            "uploads",
            "avatars",
            fileName
        );

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}
