using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Rain.Infrastructure.Identity;

namespace Rain.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly UrlEncoder _urlEncoder;

        public AccountController(UserManager<ApplicationUser> userManager, IEmailSender emailSender, UrlEncoder urlEncoder)
        {
            _userManager = userManager; _emailSender = emailSender; _urlEncoder = urlEncoder;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailConfirmation()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Index", "Home");
            }
            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                TempData["Info"] = "Email already confirmed.";
                return RedirectToAction("Index", "Home");
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code },
                protocol: Request.Scheme);

            var subject = "Confirm your email";
            var message = $"Please confirm your account by <a href='{callbackUrl}'>clicking here</a>.";
            await _emailSender.SendEmailAsync(user.Email!, subject, message);

            TempData["Success"] = "Confirmation email sent.";
            return RedirectToAction("Index", "Home");
        }
    }
}
