using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ModelFarm.Application.Services;
using ModelFarm.Contracts.Auth;

namespace ModelFarm.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IAuthService _authService;

    public LoginModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _authService.LoginAsync(new LoginRequest
        {
            Username = Input.Username,
            Password = Input.Password
        });

        if (result == null)
        {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        // Create claims for cookie authentication
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
            IsPersistent = Input.RememberMe,
            ExpiresUtc = result.RefreshTokenExpires
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return LocalRedirect(returnUrl);
    }

    public class LoginInputModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
