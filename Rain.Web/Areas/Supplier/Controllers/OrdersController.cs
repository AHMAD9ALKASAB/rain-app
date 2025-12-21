using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using Rain.Web.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Rain.Web.Areas.Supplier.Controllers
{
    [Area("Supplier")]
    [Authorize(Roles = "Supplier")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;
        public OrdersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifications)
        {
            _db = db; _userManager = userManager; _notifications = notifications;
        }

        // يعرض الطلبات التي تحتوي عناصر تخص عروض هذا المورّد
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .ThenInclude(i => i.SupplierOffer)
                .Where(o => o.Items.Any(i => i.SupplierOffer != null && i.SupplierOffer.SupplierId == user.Id))
                .OrderByDescending(o => o.Id)
                .ToListAsync();
            return View(orders);
        }

        // قبول الطلب (على مستوى الطلب بالكامل للتبسيط)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var order = await _db.Orders.Include(o=>o.Items).ThenInclude(i=>i.SupplierOffer)
                .FirstOrDefaultAsync(o => o.Id == id && o.Items.Any(i => i.SupplierOffer != null && i.SupplierOffer.SupplierId == user.Id));
            if (order == null) return NotFound();
            if (order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.Accepted;
                await _db.SaveChangesAsync();
                await _notifications.OrderAcceptedAsync(order.Id);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ship(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var order = await _db.Orders.Include(o=>o.Items).ThenInclude(i=>i.SupplierOffer)
                .FirstOrDefaultAsync(o => o.Id == id && o.Items.Any(i => i.SupplierOffer != null && i.SupplierOffer.SupplierId == user.Id));
            if (order == null) return NotFound();
            if (order.Status == OrderStatus.Accepted)
            {
                order.Status = OrderStatus.Shipped;
                await _db.SaveChangesAsync();
                await _notifications.OrderShippedAsync(order.Id);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
