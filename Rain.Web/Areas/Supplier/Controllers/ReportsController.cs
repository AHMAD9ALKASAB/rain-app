using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;

namespace Rain.Web.Areas.Supplier.Controllers
{
    [Area("Supplier")]
    [Authorize(Roles = "Supplier")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        public ReportsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db; _userManager = userManager;
        }

        // GET: /Supplier/Reports/Earnings
        public async Task<IActionResult> Earnings(DateTime? from = null, DateTime? to = null, int? productId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }
            var q = _db.OrderItems
                .AsNoTracking()
                .Include(i => i.Order)
                .Include(i => i.SupplierOffer!).ThenInclude(so => so.Product)
                .Where(i => i.SupplierOffer != null && i.SupplierOffer.SupplierId == userId)
                .AsQueryable();

            if (from.HasValue)
            {
                q = q.Where(i => i.Order != null && i.Order.CreatedAt >= from.Value);
            }
            if (to.HasValue)
            {
                q = q.Where(i => i.Order != null && i.Order.CreatedAt <= to.Value);
            }
            if (productId.HasValue)
            {
                q = q.Where(i => i.SupplierOffer != null && i.SupplierOffer.ProductId == productId.Value);
            }

            var items = await q
                .OrderByDescending(i => i.Id)
                .Take(1000)
                .ToListAsync();

            var totalGross = items.Sum(i => i.LineTotal);
            var totalCommission = items.Sum(i => i.CommissionAmount);
            var totalNet = items.Sum(i => i.NetToSupplier);

            ViewBag.TotalGross = totalGross;
            ViewBag.TotalCommission = totalCommission;
            ViewBag.TotalNet = totalNet;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.ProductId = productId;
            // Products dropdown for this supplier
            var myProducts = await _db.SupplierOffers
                .AsNoTracking()
                .Include(o => o.Product)
                .Where(o => o.SupplierId == userId && o.Product != null)
                .Select(o => new { o.ProductId, Name = o.Product != null ? o.Product.Name : string.Empty })
                .Distinct()
                .OrderBy(p => p.Name)
                .ToListAsync();
            ViewBag.Products = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(myProducts, "ProductId", "Name", productId);
            return View(items);
        }

        // GET: /Supplier/Reports/ExportEarningsCsv
        public async Task<IActionResult> ExportEarningsCsv(DateTime? from = null, DateTime? to = null, int? productId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var userId = user.Id;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }
            var q = _db.OrderItems
                .AsNoTracking()
                .Include(i => i.Order)
                .Include(i => i.SupplierOffer!).ThenInclude(so => so.Product)
                .Where(i => i.SupplierOffer != null && i.SupplierOffer.SupplierId == userId)
                .AsQueryable();

            if (from.HasValue) q = q.Where(i => i.Order != null && i.Order.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(i => i.Order != null && i.Order.CreatedAt <= to.Value);
            if (productId.HasValue) q = q.Where(i => i.SupplierOffer != null && i.SupplierOffer.ProductId == productId.Value);

            var items = await q.OrderByDescending(i => i.Id).Take(5000).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("OrderId,Date,Product,Quantity,UnitPrice,LineTotal,CommissionRate,CommissionAmount,NetToSupplier");
            foreach (var i in items)
            {
                var date = i.Order?.CreatedAt.ToString("yyyy-MM-dd") ?? "";
                var productName = i.SupplierOffer?.Product?.Name?.Replace(',', ' ');
                sb.AppendLine($"{i.OrderId},{date},{productName},{i.Quantity},{i.UnitPrice},{i.LineTotal},{i.CommissionRate},{i.CommissionAmount},{i.NetToSupplier}");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"earnings_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}
