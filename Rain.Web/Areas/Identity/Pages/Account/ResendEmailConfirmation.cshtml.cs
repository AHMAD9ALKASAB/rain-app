using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Rain.Infrastructure.Identity;

namespace Rain.Web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ResendEmailConfirmationModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
            [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (user == null)
            {
                StatusMessage = "لم يتم العثور على حساب مرتبط بهذا البريد الإلكتروني.";
                return Page();
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                StatusMessage = "تم تأكيد البريد الإلكتروني مسبقاً.";
                return Page();
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, code }, Request.Scheme) ?? string.Empty;

            await _emailSender.SendEmailAsync(Input.Email, "تأكيد البريد الإلكتروني", $"يرجى تأكيد حسابك عبر <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>هذا الرابط</a>.");

            StatusMessage = "تم إرسال رابط تأكيد جديد إلى بريدك الإلكتروني.";
            return Page();
        }
    }
}
