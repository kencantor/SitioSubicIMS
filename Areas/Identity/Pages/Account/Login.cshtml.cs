using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Services.Logging;

namespace SitioSubicIMS.Web.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IAuditLogger _auditLogger;
        public LoginModel(
            SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<LoginModel> logger, IAuditLogger auditLogger)
        {
            _signInManager = signInManager;
            _userManager = userManager; // Assign here
            _logger = logger;
            _auditLogger = auditLogger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            ReturnUrl = returnUrl ?? Url.Content("~/");

            // Clear any existing external cookie just in case
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            if (ModelState.IsValid)
            {
                // Find the user by email
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user != null && !user.IsActive)
                {
                    // If the user is deactivated, prevent login and display an error
                    ModelState.AddModelError(string.Empty, "Your account is deactivated. Please contact support.");
                    await _auditLogger.LogAsync("Login Blocked", "Account is deactivated.", user.Email);
                    return RedirectToPage("./Deactivated");
                }
                if (user != null && user.IsLocked)
                {
                    // If the user is locked, prevent login and display an error
                    ModelState.AddModelError(string.Empty, "Your account is locked. Please contact support.");
                    await _auditLogger.LogAsync("Login Blocked", "Account is locked.", user.Email);
                    return RedirectToPage("./Lockout");
                }
                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    await _auditLogger.LogAsync("Login", $"{user.UserName} successfully logged in.", user.Email);
                    return LocalRedirect(ReturnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl, Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return Page();
        }
    }
}
