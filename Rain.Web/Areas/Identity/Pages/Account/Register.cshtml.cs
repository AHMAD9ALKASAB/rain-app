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
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
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
            if (!ModelState.IsValid) return Page();

            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email, EmailConfirmed = false };
            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password.");

                // Assign role or create supplier application
                switch ((Input.AccountType ?? "Individual").Trim())
                {
                    case "Shop":
                        user.UserType = UserType.Shop;
                        await _userManager.UpdateAsync(user);
                        await _userManager.AddToRoleAsync(user, "Shop");
                        break;
                    case "Supplier":
                        user.UserType = UserType.Supplier;
                        user.DisplayName = Input.DisplayName ?? user.DisplayName;
                        await _userManager.UpdateAsync(user);
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
                            Status = SupplierApplicationStatus.Pending
                        };
                        _db.SupplierApplications.Add(app);
                        await _db.SaveChangesAsync();
                        // notify admin about new supplier application
                        var adminEmail = _cfg["Admin:Email"] ?? "admin@rain.local";
                        await _emailSender.SendEmailAsync(adminEmail, "طلب مورد جديد", $"تم استلام طلب مورد جديد من: {Input.DisplayName ?? Input.FullName ?? Input.Email}.\nالبريد: {Input.Email}\nالخطة: {Input.PlanType}");
                        TempData["Info"] = "تم استلام طلبك كمورّد. بانتظار موافقة الإدارة لمنح الصلاحيات.";
                        break;
                    default:
                        user.UserType = UserType.Individual;
                        await _userManager.UpdateAsync(user);
                        await _userManager.AddToRoleAsync(user, "Individual");
                        break;
                }

                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme) ?? string.Empty;

                await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                TempData["Info"] = "تم إنشاء الحساب. يرجى تأكيد البريد الإلكتروني عبر الرابط المرسل.";
                return RedirectToPage("./Login");
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
