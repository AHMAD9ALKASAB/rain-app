using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;

namespace Rain.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterIndividualModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly Rain.Web.Services.IRecaptchaVerifier _recaptcha;

        public RegisterIndividualModel(UserManager<ApplicationUser> userManager, 
                                     SignInManager<ApplicationUser> signInManager, 
                                     IEmailSender emailSender, 
                                     Rain.Web.Services.IRecaptchaVerifier recaptcha)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _recaptcha = recaptcha;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "الاسم مطلوب")]
            [StringLength(60, MinimumLength = 2, ErrorMessage = "الاسم يجب أن يكون بين {2} و {1} حرفاً")]
            [RegularExpression(@"^[A-Za-z\u0621-\u064A\s]{2,60}$", ErrorMessage = "الاسم يجب أن يحتوي على أحرف ومسافات فقط")]
            [Display(Name = "الاسم")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
            [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
            [Display(Name = "البريد الإلكتروني")]
            public string Email { get; set; } = string.Empty;

            [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
            [RegularExpression(@"^\+?\d{7,15}$", ErrorMessage = "صيغة رقم الهاتف غير صحيحة")]
            [StringLength(20, ErrorMessage = "رقم الهاتف طويل جداً")]
            [Display(Name = "رقم الهاتف")]
            public string? PhoneNumber { get; set; }

            [Required(ErrorMessage = "كلمة المرور مطلوبة")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون بين {2} و {1} محرفاً")]
            [DataType(DataType.Password)]
            [Display(Name = "كلمة المرور")]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين")]
            [Display(Name = "تأكيد كلمة المرور")]
            public string ConfirmPassword { get; set; } = string.Empty;
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

            // 🔧 **تحسين: تنظيف البيانات المدخلة**
            Input.Email = Input.Email?.Trim() ?? string.Empty;
            Input.FullName = Input.FullName?.Trim() ?? string.Empty;
            Input.PhoneNumber = Input.PhoneNumber?.Trim();

            try
            {
                // 🔧 **الإصلاح: تعطيل reCAPTCHA مؤقتًا للتجربة**
                // var token = Request.Form["g-recaptcha-response"].ToString();
                // var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                // if (!await _recaptcha.VerifyAsync(token, ip))
                // {
                //     ModelState.AddModelError(string.Empty, "يرجى التحقق من أنك لست برنامجاً آلياً (reCAPTCHA)");
                //     return Page();
                // }

                var reserved = new []{"admin","administrator","root","support","rain"};
                if (!string.IsNullOrWhiteSpace(Input.FullName) && 
                    reserved.Any(r => string.Equals(Input.FullName.Trim(), r, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError("Input.FullName", "الاسم غير مسموح به");
                    return Page();
                }

                // 🔧 **الإصلاح: تحقق من صحة البريد الإلكتروني مرة أخرى**
                if (!new EmailAddressAttribute().IsValid(Input.Email))
                {
                    ModelState.AddModelError("Input.Email", "البريد الإلكتروني غير صالح");
                    return Page();
                }

                var existing = await _userManager.FindByEmailAsync(Input.Email);
                if (existing != null)
                {
                    ModelState.AddModelError("Input.Email", "البريد الإلكتروني مسجّل مسبقاً");
                    return Page();
                }

                var user = new ApplicationUser 
                { 
                    UserName = Input.Email, 
                    Email = Input.Email, 
                    PhoneNumber = Input.PhoneNumber, 
                    DisplayName = Input.FullName, 
                    UserType = UserType.Individual,
                    EmailConfirmed = true // 🔧 **مهم: تأكيد البريد تلقائياً**
                };

                var result = await _userManager.CreateAsync(user, Input.Password);
                
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Individual");
                    
                    // 🔧 **الإصلاح: إرسال البريد اختياري فقط إذا كانت الإعدادات موجودة**
                    try
                    {
                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", userId = user.Id, code },
                            protocol: Request.Scheme);

                        if (!string.IsNullOrEmpty(callbackUrl))
                        {
                            await _emailSender.SendEmailAsync(
                                Input.Email,
                                "تأكيد البريد الإلكتروني",
                                $"<p>مرحباً {Input.FullName},</p>" +
                                $"<p>يرجى تأكيد حسابك عبر <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>هذا الرابط</a>.</p>");
                        }
                    }
                    catch (Exception ex)
                    {
                        // لا تجعل فشل إرسال البريد يوقف التسجيل
                        Console.WriteLine($"⚠️ Email sending failed (non-critical): {ex.Message}");
                    }

                    // 🔧 **الإصلاح: تسجيل الدخول تلقائيًا بعد التسجيل**
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    
                    TempData["Success"] = "تم إنشاء الحساب بنجاح! مرحباً بك.";
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"حدث خطأ: {ex.Message}");
                Console.WriteLine($"❌ Registration error: {ex}");
            }

            return Page();
        }
    }
}
