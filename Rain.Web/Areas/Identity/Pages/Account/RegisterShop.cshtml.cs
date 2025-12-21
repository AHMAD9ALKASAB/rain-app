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
    public class RegisterShopModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly Rain.Web.Services.IRecaptchaVerifier _recaptcha;

        public RegisterShopModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IEmailSender emailSender, Rain.Web.Services.IRecaptchaVerifier recaptcha)
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
            [StringLength(80, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين {2} و {1} حرفاً")]
            [RegularExpression(@"^[A-Za-z\u0621-\u064A\s]+$", ErrorMessage = "الاسم يجب أن يحتوي على أحرف ومسافات فقط")]
            public string OwnerName { get; set; } = string.Empty;
            [Required(ErrorMessage = "اسم المحل مطلوب")]
            [StringLength(80, MinimumLength = 2, ErrorMessage = "اسم المحل يجب أن يكون بين {2} و {1} حرفاً")]
            [RegularExpression(@"^[A-Za-z0-9\u0621-\u064A\s&_-]+$", ErrorMessage = "اسم المحل يحتوي على رموز غير مسموحة")]
            public string ShopName { get; set; } = string.Empty;
            [Required(ErrorMessage = "نوع المحل مطلوب")]
            public string ShopType { get; set; } = string.Empty;
            [Required(ErrorMessage = "موقع المحل مطلوب")]
            [StringLength(120, ErrorMessage = "الموقع طويل جداً")]
            public string ShopLocation { get; set; } = string.Empty;
            [Required(ErrorMessage = "رقم الهاتف مطلوب")]
            [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
            [RegularExpression(@"^\+?\d{7,15}$", ErrorMessage = "صيغة رقم الهاتف غير صحيحة")]
            public string PhoneNumber { get; set; } = string.Empty;
            [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
            [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
            public string Email { get; set; } = string.Empty;
            [Required(ErrorMessage = "كلمة المرور مطلوبة")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون بين {2} و {1} محرفاً")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            if (!ModelState.IsValid) return Page();

            var token = Request.Form["g-recaptcha-response"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            if (!await _recaptcha.VerifyAsync(token, ip))
            {
                ModelState.AddModelError(string.Empty, "يرجى التحقق من أنك لست برنامجاً آلياً (reCAPTCHA)");
                return Page();
            }

            var reserved = new []{"admin","administrator","root","support","rain"};
            if (!string.IsNullOrWhiteSpace(Input.ShopName) && reserved.Any(r=> string.Equals(Input.ShopName.Trim(), r, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("Input.ShopName", "اسم المحل غير مسموح به");
                return Page();
            }

            var existing = await _userManager.FindByEmailAsync(Input.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Input.Email", "البريد الإلكتروني مسجّل مسبقاً");
                return Page();
            }
            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email, PhoneNumber = Input.PhoneNumber, DisplayName = Input.ShopName, UserType = UserType.Shop };
            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Shop");
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, code }, Request.Scheme) ?? string.Empty;
                await _emailSender.SendEmailAsync(Input.Email, "تأكيد البريد الإلكتروني", $"يرجى تأكيد حسابك عبر <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>هذا الرابط</a>.");
                TempData["Success"] = "تم إنشاء حساب المحل. يرجى تأكيد البريد الإلكتروني. سعر الجملة يُطبق عند شراء 50 قطعة فأكثر.";
                return RedirectToPage("/Account/Login");
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }
    }
}
