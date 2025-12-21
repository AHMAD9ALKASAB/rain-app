using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Payments;
using Rain.Infrastructure.Persistence;
using System.Linq;
using System.Threading.Tasks;

namespace Rain.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize(Roles = "Individual,Shop,Admin")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPaymentProvider _paymentProvider;

        public CheckoutController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPaymentProvider paymentProvider)
        {
            _db = db; _userManager = userManager; _paymentProvider = paymentProvider;
        }

        // GET: /User/Checkout/Select/{orderId}
        public async Task<IActionResult> Select(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id;
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == userId);
            if (order == null) return NotFound();
            ViewBag.Order = order;
            return View(new SelectPaymentInput { OrderId = orderId, Method = PaymentMethod.Card });
        }

        public class SelectPaymentInput
        {
            public int OrderId { get; set; }
            public PaymentMethod Method { get; set; }
        }

        // POST: /User/Checkout/Pay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(SelectPaymentInput input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id;
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == input.OrderId && o.BuyerUserId == userId);
            if (order == null) return NotFound();

            var payment = new Payment
            {
                OrderId = order.Id,
                Amount = order.Total,
                Currency = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) is Microsoft.Extensions.Configuration.IConfiguration cfg
                    ? (cfg["Payment:Currency"] ?? "KWD") : "KWD",
                Method = input.Method,
                Status = PaymentStatus.Pending,
                Provider = _paymentProvider.Name
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            var successUrl = Url.Action("Success", "Checkout", new { area = "User", orderId = order.Id }, Request.Scheme) ?? "/";
            var cancelUrl = Url.Action("Cancel", "Checkout", new { area = "User", orderId = order.Id }, Request.Scheme) ?? "/";

            var result = await _paymentProvider.CreatePaymentAsync(new PaymentRequest
            {
                OrderId = order.Id,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Method = payment.Method,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            });

            if (!result.Success || string.IsNullOrWhiteSpace(result.RedirectUrl))
            {
                TempData["Error"] = result.Error ?? "تعذر إنشاء عملية الدفع";
                return RedirectToAction("Select", new { orderId = order.Id });
            }

            payment.ProviderReference = result.ProviderReference;
            await _db.SaveChangesAsync();
            return Redirect(result.RedirectUrl);
        }

        // GET: /User/Checkout/Success?orderId=1&ref=abc
        public async Task<IActionResult> Success(int orderId, string? refId, string? @ref)
        {
            var reference = refId ?? @ref;
            var payment = await _db.Payments.Include(p=>p.Order).FirstOrDefaultAsync(p => p.OrderId == orderId);
            if (payment == null) return NotFound();

            var status = await _paymentProvider.VerifyAsync(reference ?? payment.ProviderReference ?? string.Empty);
            payment.Status = status;
            payment.UpdatedAt = System.DateTime.UtcNow;
            if (status == PaymentStatus.Captured || status == PaymentStatus.Authorized)
            {
                // بإمكاننا تحديث حالة الطلب إن رغبت لاحقاً
            }
            await _db.SaveChangesAsync();
            TempData["Success"] = "تمت عملية الدفع بنجاح";
            return RedirectToAction("Details", "Orders", new { area = "User", id = orderId });
        }

        // GET: /User/Checkout/Cancel?orderId=1
        public IActionResult Cancel(int orderId)
        {
            TempData["Info"] = "تم إلغاء عملية الدفع";
            return RedirectToAction("Details", "Orders", new { area = "User", id = orderId });
        }
    }
}
