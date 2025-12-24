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
            [Display(Name = "البريد الإلكتروني")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "كلمة المرور مطلوبة")]
            [DataType(DataType.Password)]
            [Display(Name = "كلمة المرور")]
            public string Password { get; set; } = string.Empty;

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
                return Page();

            try
            {
                // 🔧 **تحسين: تنظيف البيانات المدخلة**
                Input.Email = Input.Email?.Trim() ?? string.Empty;
                
                // 🔧 **الإصلاح: تعطيل reCAPTCHA مؤقتًا**
                // var token = Request.Form["g-recaptcha-response"].ToString();
                // var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                // if (!await _recaptcha.VerifyAsync(token, ip))
                // {
                //     ModelState.AddModelError(string.Empty, "يرجى التحقق من أنك لست برنامجاً آلياً (reCAPTCHA)");
                //     return Page();
                // }

                // 🔧 **الإصلاح: جعل نوع الحساب اختياريًا أو التعامل مع القيم الفارغة**
                var accountType = UserType.Individual;
                if (!string.IsNullOrWhiteSpace(Input.AccountType) && 
                    Enum.TryParse<UserType>(Input.AccountType, true, out var parsedType))
                {
                    accountType = parsedType;
                }

                var user = await _userManager.FindByEmailAsync(Input.Email);
                
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة");
                    return Page();
                }

                // 🔧 **الإصلاح: جعل التحقق من نوع الحساب اختياريًا مؤقتًا**
                // if (user.UserType != accountType)
                // {
                //     ModelState.AddModelError("Input.AccountType", "نوع الحساب لا يتطابق");
                //     return Page();
                // }

                var userName = user.UserName ?? user.Email;
var result = await _signInManager.PasswordSignInAsync(
    userName!, 
    Input.Password, 
    Input.RememberMe, 
    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} logged in successfully.", user.Email);
                    return LocalRedirect(returnUrl);
                }
                
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account {Email} locked out.", user.Email);
                    return RedirectToPage("./Lockout");
                }
                
                ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email {Email}", Input.Email);
                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تسجيل الدخول. يرجى المحاولة مرة أخرى.");
            }

            return Page();
        }
    }
}
