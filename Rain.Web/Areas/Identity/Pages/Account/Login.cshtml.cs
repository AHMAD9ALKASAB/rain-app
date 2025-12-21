using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;

namespace Rain.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly Rain.Web.Services.IRecaptchaVerifier _recaptcha;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginModel(SignInManager<ApplicationUser> signInManager,
                           ILogger<LoginModel> logger,
                           Rain.Web.Services.IRecaptchaVerifier recaptcha,
                           UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _logger = logger;
            _recaptcha = recaptcha;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
            [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "كلمة المرور مطلوبة")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "نوع الحساب مطلوب")]
            [Display(Name = "نوع الحساب")]
            public string AccountType { get; set; } = UserType.Individual.ToString();

            [Display(Name = "تذكرني؟")]
            public bool RememberMe { get; set; }
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            if (!ModelState.IsValid)
            {
                return Page();
            }
            // reCAPTCHA validation
            var token = Request.Form["g-recaptcha-response"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            if (!await _recaptcha.VerifyAsync(token, ip))
            {
                ModelState.AddModelError(string.Empty, "يرجى التحقق من أنك لست برنامجاً آلياً (reCAPTCHA)");
                return Page();
            }

            if (!Enum.TryParse<UserType>(Input.AccountType ?? string.Empty, true, out var requestedType))
            {
                ModelState.AddModelError("Input.AccountType", "نوع الحساب غير صالح");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "لا يوجد حساب مرتبط بهذا البريد الإلكتروني");
                return Page();
            }

            if (user.UserType != requestedType)
            {
                ModelState.AddModelError("Input.AccountType", "نوع الحساب لا يتطابق مع بيانات الحساب");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                return LocalRedirect(returnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "بيانات تسجيل الدخول غير صحيحة");
                return Page();
            }
        }
    }
}
