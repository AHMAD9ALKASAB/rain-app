using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using Rain.Web.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Rain.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize(Roles = "Individual,Shop,Admin")] // يسمح للتجربة للمشرف أيضاً
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;

        public OrdersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _notifications = notifications;
        }

        // GET: /User/Orders
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }
            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items!).ThenInclude(i => i.SupplierOffer!).ThenInclude(so => so.Product)
                .Where(o => o.BuyerUserId == userId)
                .OrderByDescending(o => o.Id)
                .ToListAsync();
            return View(orders);
        }

        // GET: /User/Orders/Create?offerId=1
        public async Task<IActionResult> Create(int? offerId)
        {
            if (offerId.HasValue)
            {
                var offer = await _db.SupplierOffers.Include(o=>o.Product).FirstOrDefaultAsync(o => o.Id == offerId.Value && o.IsActive);
                if (offer == null) return NotFound();
                ViewBag.Offer = offer;
            }
            ViewBag.Addresses = new SelectList(await GetMyAddressesAsync(), "Id", "Line1");
            return View();
        }

        public class CreateOrderInput
        {
            public int SupplierOfferId { get; set; }
            public int Quantity { get; set; } = 1;
            public int? ShippingAddressId { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderInput input)
        {
            if (input.Quantity <= 0)
                ModelState.AddModelError("Quantity", "الكمية غير صحيحة");

            var offer = await _db.SupplierOffers.Include(o=>o.Product).FirstOrDefaultAsync(o => o.Id == input.SupplierOfferId && o.IsActive);
            if (offer == null)
            {
                ModelState.AddModelError("SupplierOfferId", "العرض غير موجود أو غير مفعل");
            }

            if (offer != null && input.Quantity < offer.MinOrderQty)
                ModelState.AddModelError("Quantity", $"الحد الأدنى للطلب هو {offer.MinOrderQty}");

            if (!ModelState.IsValid)
            {
                ViewBag.Offer = offer;
                ViewBag.Addresses = new SelectList(await GetMyAddressesAsync(), "Id", "Line1", input.ShippingAddressId);
                return View(input);
            }

            if (offer == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Pricing policy: Individual => +2%; Shop => base price when qty>=50 else +2%
            var isShop = await _userManager.IsInRoleAsync(user, "Shop");
            var isIndividual = await _userManager.IsInRoleAsync(user, "Individual");
            decimal unitPrice = offer.Price;
            bool qualifiesWholesale = isShop && input.Quantity >= 50;
            if (!qualifiesWholesale)
            {
                // apply 2% markup
                unitPrice = Math.Round(offer.Price * 1.02m, 2);
            }

            var order = new Order
            {
                BuyerUserId = userId,
                ShippingAddressId = input.ShippingAddressId,
                Total = unitPrice * input.Quantity,
                Items = new System.Collections.Generic.List<OrderItem>()
            };
            // Determine supplier plan: default Commission 2% if last approved application is Commission; 0 otherwise
            var lastApprovedApp = await _db.SupplierApplications
                .AsNoTracking()
                .Where(a => a.UserId == offer.SupplierId && a.Status == SupplierApplicationStatus.Approved)
                .OrderByDescending(a => a.ReviewedAtUtc)
                .FirstOrDefaultAsync();
            var plan = lastApprovedApp?.PlanType ?? SupplierPlanType.Commission;
            decimal commissionRate = plan == SupplierPlanType.Commission ? 0.02m : 0m;
            var lineTotal = unitPrice * input.Quantity;
            var commissionAmount = System.Math.Round(lineTotal * commissionRate, 2);
            var netToSupplier = lineTotal - commissionAmount;

            order.Items.Add(new OrderItem
            {
                SupplierOfferId = offer.Id,
                Quantity = input.Quantity,
                UnitPrice = unitPrice,
                CommissionRate = commissionRate,
                CommissionAmount = commissionAmount,
                NetToSupplier = netToSupplier
            });

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await _notifications.OrderCreatedAsync(order.Id);
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // GET: /User/Orders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }
            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items!).ThenInclude(i => i.SupplierOffer!).ThenInclude(so => so.Product)
                .Include(o => o.ShippingAddress)
                .FirstOrDefaultAsync(o => o.Id == id && o.BuyerUserId == userId);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivery(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.BuyerUserId == userId);
            if (order == null) return NotFound();
            if (order.Status == OrderStatus.Shipped)
            {
                order.Status = OrderStatus.Delivered;
                await _db.SaveChangesAsync();
                await _notifications.OrderDeliveredAsync(order.Id);
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<System.Collections.Generic.List<Address>> GetMyAddressesAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return new System.Collections.Generic.List<Address>();
            }
            var userId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                return new System.Collections.Generic.List<Address>();
            }
            return await _db.Addresses.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a=>a.IsDefault).ToListAsync();
        }
    }
}
