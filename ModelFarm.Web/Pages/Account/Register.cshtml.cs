using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Auth;

namespace ModelFarm.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly IAuthService _authService;

    public RegisterModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public RegisterInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validate username
        if (Input.Username.Length < 3 || Input.Username.Length > 50)
        {
            ErrorMessage = "Username must be between 3 and 50 characters.";
            return Page();
        }

        // Validate password
        if (Input.Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            return Page();
        }

        if (Input.Password != Input.ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        try
        {
            var result = await _authService.RegisterAsync(new RegisterRequest
            {
                Username = Input.Username,
                Password = Input.Password
            });

            // Auto-login after registration
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                new(ClaimTypes.Name, result.Username),
                new("AccessToken", result.AccessToken),
                new("RefreshToken", result.RefreshToken)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = result.RefreshTokenExpires
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return RedirectToPage("/Index");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    public class RegisterInputModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
