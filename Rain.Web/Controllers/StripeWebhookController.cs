using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using Microsoft.EntityFrameworkCore;
using Rain.Infrastructure.Persistence;
using Rain.Domain.Enums;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Identity;
using Rain.Infrastructure.Identity;
using Rain.Web.Services;

namespace Rain.Web.Controllers
{
    [ApiController]
    [Route("webhooks/stripe")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<StripeWebhookController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;
        public StripeWebhookController(IConfiguration config, ILogger<StripeWebhookController> logger, ApplicationDbContext db, IEmailSender emailSender, UserManager<ApplicationUser> userManager, INotificationService notifications)
        {
            _config = config; _logger = logger; _db = db; _emailSender = emailSender; _userManager = userManager; _notifications = notifications;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();
            var secret = _config["Stripe:WebhookSecret"];
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, signature, secret);
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "Invalid Stripe webhook signature");
                return BadRequest();
            }

            if (stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                if (session != null)
                {
                    var providerRef = session.Id;
                    var orderIdMeta = session.Metadata != null && session.Metadata.ContainsKey("orderId") ? session.Metadata["orderId"] : null;

                    var payment = await _db.Payments.FirstOrDefaultAsync(p => p.ProviderReference == providerRef);
                    if (payment == null && int.TryParse(orderIdMeta, out var oid))
                    {
                        payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == oid);
                    }
                    if (payment != null)
                    {
                        payment.Status = PaymentStatus.Captured;
                        payment.UpdatedAt = System.DateTime.UtcNow;
                        await _db.SaveChangesAsync();

                        // Notify buyer via NotificationService (localized)
                        await _notifications.PaymentSucceededAsync(payment.OrderId, payment.Amount, payment.Currency);
                    }
                }
            }
            else if (stripeEvent.Type == Events.PaymentIntentPaymentFailed)
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent != null)
                {
                    var orderIdMeta = intent.Metadata != null && intent.Metadata.ContainsKey("orderId") ? intent.Metadata["orderId"] : null;
                    if (int.TryParse(orderIdMeta, out var oid))
                    {
                        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == oid);
                        if (payment != null)
                        {
                            payment.Status = PaymentStatus.Failed;
                            payment.UpdatedAt = System.DateTime.UtcNow;
                            await _db.SaveChangesAsync();
                        }
                        await _notifications.PaymentFailedAsync(oid);
                    }
                }
            }
            else if (stripeEvent.Type == Events.ChargeRefunded)
            {
                var charge = stripeEvent.Data.Object as Charge;
                if (charge != null)
                {
                    var orderIdMeta = charge.Metadata != null && charge.Metadata.ContainsKey("orderId") ? charge.Metadata["orderId"] : null;
                    if (string.IsNullOrEmpty(orderIdMeta) && charge.PaymentIntentId != null)
                    {
                        // Try to fetch PaymentIntent to read metadata
                        try
                        {
                            var intentService = new PaymentIntentService();
                            var intent = await intentService.GetAsync(charge.PaymentIntentId);
                            if (intent?.Metadata != null && intent.Metadata.ContainsKey("orderId"))
                            {
                                orderIdMeta = intent.Metadata["orderId"];
                            }
                        }
                        catch { }
                    }
                    if (int.TryParse(orderIdMeta, out var oid))
                    {
                        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == oid);
                        if (payment != null)
                        {
                            payment.Status = PaymentStatus.Refunded;
                            payment.UpdatedAt = System.DateTime.UtcNow;
                            await _db.SaveChangesAsync();
                        }
                        var amount = (decimal)(charge.AmountRefunded / 100m);
                        var currency = charge.Currency?.ToUpper() ?? "USD";
                        await _notifications.PaymentRefundedAsync(oid, amount, currency);
                    }
                }
            }

            return Ok();
        }
    }
}
