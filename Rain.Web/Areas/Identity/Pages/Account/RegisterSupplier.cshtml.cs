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
using Microsoft.Extensions.Configuration;
using Rain.Domain.Entities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;

namespace Rain.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterSupplierModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _cfg;

        public RegisterSupplierModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IEmailSender emailSender, ApplicationDbContext db, IConfiguration cfg)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _db = db;
            _cfg = cfg;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "الاسم الظاهر مطلوب")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين {2} و {1} حرفاً")]
            [RegularExpression(@"^[A-Za-z0-9\u0621-\u064A\s&_-]+$", ErrorMessage = "الاسم الظاهر يحتوي على رموز غير مسموحة")]
            public string DisplayName { get; set; } = string.Empty; // الاسم الذي سيظهر في الموقع
            [Required(ErrorMessage = "الاسم الثلاثي مطلوب")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين {2} و {1} حرفاً")]
            [RegularExpression(@"^[A-Za-z\u0621-\u064A\s]+$", ErrorMessage = "الاسم يجب أن يحتوي على أحرف ومسافات فقط")]
            public string FullName { get; set; } = string.Empty;
            [Required(ErrorMessage = "اسم الشركة/المحل مطلوب")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "اسم الشركة يجب أن يكون بين {2} و {1} حرفاً")]
            [RegularExpression(@"^[A-Za-z0-9\u0621-\u064A\s&_-]+$", ErrorMessage = "اسم الشركة يحتوي على رموز غير مسموحة")]
            public string CompanyOrShopName { get; set; } = string.Empty;
            [Required(ErrorMessage = "رقم الهاتف مطلوب")]
            [RegularExpression(@"^\+?\d{7,15}$", ErrorMessage = "صيغة رقم الهاتف غير صحيحة")]
            public string PhoneWithCountry { get; set; } = string.Empty;
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
            [Required(ErrorMessage = "نوع الشركة مطلوب")]
            [StringLength(80, ErrorMessage = "النص طويل جداً")]
            public string CompanyType { get; set; } = string.Empty;
            [Required(ErrorMessage = "مجال المنتجات مطلوب")]
            [StringLength(120, ErrorMessage = "النص طويل جداً")]
            public string ProductScope { get; set; } = string.Empty;
            [Required(ErrorMessage = "مكان الإقامة مطلوب")]
            [StringLength(120, ErrorMessage = "النص طويل جداً")]
            public string ResidenceLocation { get; set; } = string.Empty;
            [Required(ErrorMessage = "الموقع بالتحديد مطلوب")]
            [StringLength(160, ErrorMessage = "النص طويل جداً")]
            public string ExactLocation { get; set; } = string.Empty;
            [Required(ErrorMessage = "الخطة مطلوبة")]
            public SupplierPlanType PlanType { get; set; } = SupplierPlanType.Commission;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            if (!ModelState.IsValid) return Page();

            var existing = await _userManager.FindByEmailAsync(Input.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Input.Email", "البريد الإلكتروني مسجّل مسبقاً");
                return Page();
            }
            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email, DisplayName = Input.DisplayName, UserType = UserType.Supplier };
            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                // لا يُمنح دور Supplier هنا؛ ينتظر الموافقة من الإدارة
                var app = new SupplierApplication
                {
                    UserId = user.Id,
                    DisplayName = Input.DisplayName,
                    FullName = Input.FullName,
                    CompanyOrShopName = Input.CompanyOrShopName,
                    PhoneWithCountry = Input.PhoneWithCountry,
                    Email = Input.Email,
                    CompanyType = Input.CompanyType,
                    ProductScope = Input.ProductScope,
                    ResidenceLocation = Input.ResidenceLocation,
                    ExactLocation = Input.ExactLocation,
                    PlanType = Input.PlanType,
                    Status = SupplierApplicationStatus.Pending
                };
                _db.SupplierApplications.Add(app);
                await _db.SaveChangesAsync();

                var adminEmail = _cfg["Admin:Email"] ?? "admin@rain.local";
                await _emailSender.SendEmailAsync(adminEmail, "طلب مورد جديد", $"تم استلام طلب مورد جديد من: {Input.DisplayName}.\nالبريد: {Input.Email}\nالخطة: {Input.PlanType}");

                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, code }, Request.Scheme) ?? string.Empty;
                await _emailSender.SendEmailAsync(Input.Email, "تأكيد البريد الإلكتروني", $"يرجى تأكيد حسابك عبر <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>هذا الرابط</a>.");
                TempData["Info"] = "تم استلام طلبك كمورّد. بانتظار موافقة الإدارة لمنح الصلاحيات وسيتم إشعارك بالبريد.";
                return RedirectToPage("/Account/Login");
            }
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }
    }
}
