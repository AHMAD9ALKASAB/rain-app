using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using Microsoft.Extensions.Localization;

namespace Rain.Web.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public NotificationService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IStringLocalizer<SharedResource> localizer)
        {
            _db = db; _userManager = userManager; _emailSender = emailSender; _localizer = localizer;
        }

        public async Task OrderCreatedAsync(int orderId)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || string.IsNullOrWhiteSpace(order.BuyerUserId)) return;
            var user = await _userManager.FindByIdAsync(order.BuyerUserId);
            if (user?.Email == null) return;
            var subject = _localizer["OrderCreatedSubject"];
            var content = $"<p>{_localizer["EmailGreeting"]} {user.UserName},</p>" +
                          $"<p>#{order.Id}</p>" +
                          $"<p>{_localizer["EmailSignature"]}</p>";
            var body = HtmlEmailBuilder.Build(_localizer, subject, content);
            await _emailSender.SendEmailAsync(user.Email, subject, body);
        }

        public async Task PaymentFailedAsync(int orderId)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || string.IsNullOrWhiteSpace(order.BuyerUserId)) return;
            var user = await _userManager.FindByIdAsync(order.BuyerUserId);
            if (user?.Email == null) return;
            var subject = _localizer["PaymentFailedSubject"];
            var content = $"<p>{_localizer["EmailGreeting"]} {user.UserName},</p>" +
                          $"<p>#{orderId}</p>" +
                          $"<p>{_localizer["EmailSignature"]}</p>";
            var body = HtmlEmailBuilder.Build(_localizer, subject, content);
            await _emailSender.SendEmailAsync(user.Email, subject, body);
        }

        public async Task PaymentRefundedAsync(int orderId, decimal amount, string currency)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || string.IsNullOrWhiteSpace(order.BuyerUserId)) return;
            var user = await _userManager.FindByIdAsync(order.BuyerUserId);
            if (user?.Email == null) return;
            var subject = _localizer["PaymentRefundedSubject"];
            var content = $"<p>{_localizer["EmailGreeting"]} {user.UserName},</p>" +
                          $"<p>{amount:0.00} {currency} - #{orderId}</p>" +
                          $"<p>{_localizer["EmailSignature"]}</p>";
            var body = HtmlEmailBuilder.Build(_localizer, subject, content);
            await _emailSender.SendEmailAsync(user.Email, subject, body);
        }

        public async Task OrderAcceptedAsync(int orderId)
        {
            await NotifyBuyer(orderId, _localizer["OrderAcceptedSubject"], "");
        }

        public async Task OrderShippedAsync(int orderId)
        {
            await NotifyBuyer(orderId, _localizer["OrderShippedSubject"], "");
        }

        public async Task OrderDeliveredAsync(int orderId)
        {
            await NotifyBuyer(orderId, _localizer["OrderDeliveredSubject"], "");
        }

        public async Task PaymentSucceededAsync(int orderId, decimal amount, string currency)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || string.IsNullOrWhiteSpace(order.BuyerUserId)) return;
            var user = await _userManager.FindByIdAsync(order.BuyerUserId);
            if (user?.Email == null) return;
            var subject = _localizer["PaymentSucceededSubject"];
            var content = $"<p>{_localizer["EmailGreeting"]} {user.UserName},</p>" +
                          $"<p>{amount:0.00} {currency} - #{orderId}</p>" +
                          $"<p>{_localizer["EmailSignature"]}</p>";
            var body = HtmlEmailBuilder.Build(_localizer, subject, content);
            await _emailSender.SendEmailAsync(user.Email, subject, body);
        }

        private async Task NotifyBuyer(int orderId, string subject, string message)
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || string.IsNullOrWhiteSpace(order.BuyerUserId)) return;
            var user = await _userManager.FindByIdAsync(order.BuyerUserId);
            if (user?.Email == null) return;
            var content = $"<p>{_localizer["EmailGreeting"]} {user.UserName},</p>" +
                          (string.IsNullOrWhiteSpace(message) ? string.Empty : $"<p>{message}</p>") +
                          $"<p>#{order.Id}</p>" +
                          $"<p>{_localizer["EmailSignature"]}</p>";
            var body = HtmlEmailBuilder.Build(_localizer, subject, content);
            await _emailSender.SendEmailAsync(user.Email, subject, body);
        }
    }
}
