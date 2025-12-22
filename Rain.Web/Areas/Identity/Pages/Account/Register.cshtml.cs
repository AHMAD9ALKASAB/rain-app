using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using Rain.Domain.Enums;
using Rain.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Rain.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IConfiguration _cfg;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<RegisterModel> logger,
            ApplicationDbContext db,
            IConfiguration cfg)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            _db = db;
            _cfg = cfg;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Account Type")]
            public string AccountType { get; set; } = "Individual"; // Individual | Shop | Supplier

            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 4)] // تم التغيير من 6 إلى 4
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            // Supplier application fields (used when AccountType == Supplier)
            [Display(Name = "Display name (public)")]
            public string? DisplayName { get; set; }

            [Display(Name = "Full name")]
            public string? FullName { get; set; }

            [Display(Name = "Company/Shop name")]
            public string? CompanyOrShopName { get; set; }

            [Display(Name = "Phone with country code")]
            public string? PhoneWithCountry { get; set; }

            [Display(Name = "Company type")]
            public string? CompanyType { get; set; }

            [Display(Name = "Products scope")]
            public string? ProductScope { get; set; }

            [Display(Name = "Residence location")]
            public string? ResidenceLocation { get; set; }

            [Display(Name = "Exact location")]
            public string? ExactLocation { get; set; }

            [Display(Name = "Supplier plan")]
            public SupplierPlanType PlanType { get; set; } = SupplierPlanType.Commission;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            
            // 🔧 **إضافة هذا السطر لإصلاح مشكلة returnUrl**
            if (string.IsNullOrEmpty(returnUrl) || returnUrl == "/")
            {
                returnUrl = "/Home";
            }
            
            if (!ModelState.IsValid) return Page();

            // 🔧 **إنشاء مستخدم مع EmailConfirmed = true مباشرة**
            var user = new ApplicationUser 
            { 
                UserName = Input.Email, 
                Email = Input.Email, 
                EmailConfirmed = true, // تأكيد البريد تلقائياً
                UserType = Input.AccountType switch
                {
                    "Shop" => UserType.Shop,
                    "Supplier" => UserType.Supplier,
                    _ => UserType.Individual
                }
            };
            
            // 🔧 **إضافة DisplayName للموردين**
            if (Input.AccountType == "Supplier" && !string.IsNullOrEmpty(Input.DisplayName))
            {
                user.DisplayName = Input.DisplayName;
            }

            var result = await _userManager.CreateAsync(user, Input.Password);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password.");

                // 🔧 **تسجيل الدخول مباشرة بدون انتظار تأكيد البريد**
                await _signInManager.SignInAsync(user, isPersistent: false);

                // 🔧 **إضافة المستخدم إلى الدور المناسب**
                string roleName = Input.AccountType switch
                {
                    "Shop" => "Shop",
                    "Supplier" => "Supplier",
                    _ => "Individual"
                };

                // 🔧 **التأكد من وجود الدور أولاً**
                var roleExists = await _userManager.IsInRoleAsync(user, roleName);
                if (!roleExists)
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
                    if (!addRoleResult.Succeeded)
                    {
                        _logger.LogWarning($"Failed to add user to role {roleName}");
                    }
                }

                // 🔧 **معالجة طلب الموردين (بدون إرسال بريد)**
                if (Input.AccountType == "Supplier")
                {
                    try
                    {
                        var app = new SupplierApplication
                        {
                            UserId = user.Id,
                            DisplayName = Input.DisplayName ?? string.Empty,
                            FullName = Input.FullName ?? string.Empty,
                            CompanyOrShopName = Input.CompanyOrShopName ?? string.Empty,
                            PhoneWithCountry = Input.PhoneWithCountry ?? string.Empty,
                            Email = Input.Email,
                            CompanyType = Input.CompanyType ?? string.Empty,
                            ProductScope = Input.ProductScope ?? string.Empty,
                            ResidenceLocation = Input.ResidenceLocation ?? string.Empty,
                            ExactLocation = Input.ExactLocation ?? string.Empty,
                            PlanType = Input.PlanType,
                            Status = SupplierApplicationStatus.Pending,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        _db.SupplierApplications.Add(app);
                        await _db.SaveChangesAsync();
                        
                        TempData["Info"] = "تم استلام طلبك كمورّد. يمكنك استخدام الموقع كزائر حتى موافقة الإدارة.";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating supplier application");
                        TempData["Info"] = "تم إنشاء الحساب بنجاح! سيتم مراجعة طلب المورد قريباً.";
                    }
                }
                else
                {
                    TempData["Success"] = "تم إنشاء الحساب وتسجيل الدخول بنجاح!";
                }

                // 🔧 **تجاهل إرسال بريد التأكيد (لأننا قمنا بتأكيده تلقائياً)**
                // لا نرسل أي بريد تأكيد

                // 🔧 **إعادة التوجيه إلى الصفحة الرئيسية**
                return LocalRedirect(returnUrl);
            }
            
            // 🔧 **معالجة الأخطاء بشكل أفضل**
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                _logger.LogError($"Registration error: {error.Description}");
            }
            
            // 🔧 **إضافة رسالة خطأ عامة**
            if (result.Errors.Any())
            {
                TempData["Error"] = "حدث خطأ أثناء إنشاء الحساب. الرجاء التحقق من البيانات والمحاولة مرة أخرى.";
            }
            
            return Page();
        }
    }
}
