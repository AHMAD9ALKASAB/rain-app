using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using Rain.Infrastructure.Files;

namespace Rain.Web.Areas.Supplier.Controllers
{
    [Area("Supplier")]
    [Authorize(Roles = "Supplier")]
    public class MyProductsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorage _files;
        // Allowed currencies (ISO codes + common regionals)
        private static readonly string[] AllowedCurrencies = new[]
        {
            "SYP", // الليرة السورية
            "TRY", // الليرة التركية
            "USD", // الدولار الأمريكي
            "EUR", // اليورو
            "SAR", // الريال السعودي
            "AED", // الدرهم الإماراتي
            "JOD", // الدينار الأردني
            "QAR", // الريال القطري
            "KWD", // الدينار الكويتي
            "EGP", // الجنيه المصري
            "GBP", // الجنيه الإسترليني
            "LBP", // الليرة اللبنانية
            "OMR", // الريال العُماني
            "BHD"  // الدينار البحريني
        };

        public MyProductsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IFileStorage files)
        {
            _db = db; _userManager = userManager; _files = files;
        }

        private async Task<ApplicationUser?> GetCurrentSupplierAsync() => await _userManager.GetUserAsync(User);

        public async Task<IActionResult> Index()
        {
            var user = await GetCurrentSupplierAsync();
            if (user == null)
            {
                return Challenge();
            }
            var supplierId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(supplierId))
            {
                return Challenge();
            }
            var offers = await _db.SupplierOffers
                .Include(o=>o.Product!).ThenInclude(p=>p.Images)
                .Where(o=>o.SupplierId == supplierId)
                .OrderByDescending(o=>o.Id)
                .ToListAsync();
            return View(offers);
        }

        public IActionResult Create()
        {
            ViewBag.Currencies = AllowedCurrencies;
            return View(new CreateVm{ MinOrderQty = 1, StockQty = 1 });
        }

        public class CreateVm
        {
            public IFormFile? Image { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? SellerName { get; set; }
            public decimal Price { get; set; }
            public string Currency { get; set; } = "KWD";
            public string? Description { get; set; }
            public int StockQty { get; set; }
            public int MinOrderQty { get; set; } = 1;
            public string SupplierDisplayName { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateVm input)
        {
            if (string.IsNullOrWhiteSpace(input.Name)) ModelState.AddModelError("Name","الاسم مطلوب");
            if (input.Price <= 0) ModelState.AddModelError("Price","السعر غير صالح");
            if (!string.IsNullOrWhiteSpace(input.SellerName) && input.SellerName.Length > 100)
                ModelState.AddModelError("SellerName","اسم البائع طويل جداً");
            if (string.IsNullOrWhiteSpace(input.Currency) || !AllowedCurrencies.Contains(input.Currency.ToUpperInvariant()))
                ModelState.AddModelError("Currency","العملة غير مدعومة");
            if (input.Image != null)
            {
                var okTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp" };
                if (!okTypes.Contains(input.Image.ContentType?.ToLowerInvariant() ?? string.Empty))
                    ModelState.AddModelError("Image", "نوع الصورة غير مدعوم (يسمح PNG/JPG/WebP)");
                if (input.Image.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError("Image", "حجم الصورة كبير (الحد 5MB)");
            }
            if (!ModelState.IsValid) { ViewBag.Currencies = AllowedCurrencies; return View(input); }

            var user = await GetCurrentSupplierAsync();
            if (user == null)
            {
                return Challenge();
            }
            var supplierId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(supplierId))
            {
                return Challenge();
            }

            var product = new Product { Name = input.Name, Description = input.Description, SellerName = input.SellerName };
            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            if (input.Image != null && input.Image.Length > 0)
            {
                var path = await _files.SaveProductImageAsync(product.Id, input.Image);
                _db.ProductImages.Add(new ProductImage { ProductId = product.Id, Url = path });
            }

            var offer = new SupplierOffer
            {
                ProductId = product.Id,
                SupplierId = supplierId,
                Price = input.Price,
                Currency = input.Currency?.ToUpperInvariant() ?? "KWD",
                StockQty = input.StockQty,
                MinOrderQty = input.MinOrderQty,
                IsActive = false
            };
            _db.SupplierOffers.Add(offer);
            await _db.SaveChangesAsync();

            TempData["Success"] = "تم حفظ المنتج. يمكنك نشره ليظهر للجميع";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await GetCurrentSupplierAsync();
            if (user == null)
            {
                return Challenge();
            }
            var supplierId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(supplierId))
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.Include(o=>o.Product!).ThenInclude(p=>p.Images)
                .FirstOrDefaultAsync(o=>o.Id == id && o.SupplierId == supplierId);
            if (offer == null) return NotFound();
            if (offer.Product == null)
            {
                TempData["Error"] = "لا يمكن تعديل هذا المنتج لعدم توفر بيانات المنتج الأصلية";
                return RedirectToAction(nameof(Index));
            }
            var vm = new EditVm
            {
                Id = offer.Id,
                Name = offer.Product!.Name,
                Description = offer.Product.Description,
                SellerName = offer.Product.SellerName,
                Price = offer.Price,
                Currency = offer.Currency,
                StockQty = offer.StockQty,
                MinOrderQty = offer.MinOrderQty
            };
            ViewBag.Currencies = AllowedCurrencies;
            return View(vm);
        }

        public class EditVm
        {
            public int Id { get; set; }
            public IFormFile? Image { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? SellerName { get; set; }
            public decimal Price { get; set; }
            public string Currency { get; set; } = "KWD";
            public string? Description { get; set; }
            public int StockQty { get; set; }
            public int MinOrderQty { get; set; } = 1;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditVm input)
        {
            var user = await GetCurrentSupplierAsync();
            if (user == null)
            {
                return Challenge();
            }
            var supplierId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(supplierId))
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.Include(o=>o.Product!).ThenInclude(p=>p.Images)
                .FirstOrDefaultAsync(o=>o.Id == id && o.SupplierId == supplierId);
            if (offer == null) return NotFound();
            if (offer.Product == null)
            {
                TempData["Error"] = "لا يمكن تعديل هذا المنتج لعدم توفر بيانات المنتج الأصلية";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(input.Name)) ModelState.AddModelError("Name","الاسم مطلوب");
            if (input.Price <= 0) ModelState.AddModelError("Price","السعر غير صالح");
            if (!string.IsNullOrWhiteSpace(input.SellerName) && input.SellerName.Length > 100)
                ModelState.AddModelError("SellerName","اسم البائع طويل جداً");
            if (string.IsNullOrWhiteSpace(input.Currency) || !AllowedCurrencies.Contains(input.Currency.ToUpperInvariant()))
                ModelState.AddModelError("Currency","العملة غير مدعومة");
            if (!ModelState.IsValid) return View(input);

            offer.Product!.Name = input.Name;
            offer.Product.Description = input.Description;
            offer.Product.SellerName = input.SellerName;
            offer.Price = input.Price;
            offer.Currency = input.Currency?.ToUpperInvariant() ?? offer.Currency;
            offer.StockQty = input.StockQty;
            offer.MinOrderQty = input.MinOrderQty;

            if (input.Image != null && input.Image.Length > 0)
            {
                var path = await _files.SaveProductImageAsync(offer.ProductId, input.Image);
                _db.ProductImages.Add(new ProductImage { ProductId = offer.ProductId, Url = path });
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "تم حفظ التعديل";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            var user = await GetCurrentSupplierAsync();
            if (user == null)
            {
                return Challenge();
            }
            var supplierId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(supplierId))
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.FirstOrDefaultAsync(o=>o.Id==id && o.SupplierId==supplierId);
            if (offer == null) return NotFound();
            if (!offer.IsActive)
            {
                offer.IsActive = true;
                await _db.SaveChangesAsync();
                TempData["Success"] = "تم نشر منتجك الجديد بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var user = await GetCurrentSupplierAsync();
            if (user == null)
            {
                return Challenge();
            }
            var supplierId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(supplierId))
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.Include(o=>o.Product!).FirstOrDefaultAsync(o=>o.Id==id && o.SupplierId==supplierId);
            if (offer == null) return NotFound();
            return View(offer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await GetCurrentSupplierAsync();
            if (user == null)
            {
                return Challenge();
            }
            var supplierId = user.Id ?? string.Empty;
            if (string.IsNullOrEmpty(supplierId))
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.FirstOrDefaultAsync(o=>o.Id==id && o.SupplierId==supplierId);
            if (offer == null) return NotFound();

            var hasOrders = await _db.OrderItems.AnyAsync(oi=>oi.SupplierOfferId == offer.Id);
            if (hasOrders)
            {
                TempData["Error"] = "عذراً! لا يمكنك حذف هذا المنتج الآن بسبب وجود طلبات سابقة أو حجوزات";
                return RedirectToAction(nameof(Index));
            }

            _db.SupplierOffers.Remove(offer);
            await _db.SaveChangesAsync();
            TempData["Success"] = "تم حذف المنتج";
            return RedirectToAction(nameof(Index));
        }
    }
}
