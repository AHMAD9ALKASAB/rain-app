using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Rain.Infrastructure.Persistence;
using System.Linq;
using System.Threading.Tasks;

namespace Rain.Web.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private const int DefaultPageSize = 12;
        private static readonly Dictionary<string, string> ProductImageMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["سماعات rain اللاسلكية"] = "/images/products/headphones.jpg",
            ["ساعة ذكية nova"] = "/images/products/smartwatch.jpg",
            ["قميص رجالي كلاسيك"] = "/images/products/mens-shirt.jpg",
            ["فستان صيفي"] = "/images/products/dress.jpg",
            ["طقم أواني casa"] = "/images/products/home-kit.jpg",
            ["مصباح مكتبي led"] = "/images/products/lamp.jpg",
            ["مجفف شعر"] = "/images/products/hairdryer.jpg",
            ["معدات لياقة fitpro"] = "/images/products/fitness.jpg",
            ["حاسوب محمول rainbook"] = "/images/products/laptop.jpg",
            ["كاميرا رقمية visionpro"] = "/images/products/camera.jpg"
        };

        private static readonly Dictionary<string, decimal> CurrencyToSypRates = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SYP"] = 1m,
            ["USD"] = 14000m,
            ["EUR"] = 15000m,
            ["TRY"] = 420m,
            ["SAR"] = 3730m,
            ["AED"] = 3810m,
            ["KWD"] = 45500m,
            ["QAR"] = 3850m,
            ["EGP"] = 450m,
            ["GBP"] = 17600m,
            ["JOD"] = 19800m,
            ["BHD"] = 37200m,
            ["OMR"] = 36400m,
            ["LBP"] = 0.09m
        };

        public ProductsController(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<IActionResult> Index(string? keyword, int? categoryId, int? brandId, decimal? minPrice, decimal? maxPrice, double? minRating, string? sort, int page = 1, int pageSize = DefaultPageSize)
        {
            var query = _db.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.SupplierOffers.Where(o => o.IsActive))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                query = query.Where(p => p.Name.Contains(k));
            }
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId);
            if (brandId.HasValue)
                query = query.Where(p => p.BrandId == brandId);

            if (minPrice.HasValue)
                query = query.Where(p => p.SupplierOffers.Where(o=>o.IsActive).Select(o => (decimal?)o.Price).Min() >= minPrice.Value);
            if (maxPrice.HasValue)
                query = query.Where(p => p.SupplierOffers.Where(o=>o.IsActive).Select(o => (decimal?)o.Price).Min() <= maxPrice.Value);

            if (minRating.HasValue)
                query = query.Where(p => p.SupplierOffers.Any(o => o.IsActive && o.RatingsCount > 0 && o.AverageRating >= minRating.Value));

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.SupplierOffers.Min(o => (decimal?)o.Price) ?? decimal.MaxValue),
                "price_desc" => query.OrderByDescending(p => p.SupplierOffers.Min(o => (decimal?)o.Price) ?? decimal.MinValue),
                _ => query.OrderBy(p => p.Name)
            };

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Keyword = keyword;
            ViewBag.CategoryId = categoryId;
            ViewBag.BrandId = brandId;
            ViewBag.Sort = sort;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.MinRating = minRating;

            var categories = await _cache.GetOrCreateAsync("categories:list", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            });
            var brands = await _cache.GetOrCreateAsync("brands:list", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return await _db.Brands.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
            });
            ViewBag.Categories = categories;
            ViewBag.Brands = brands;
            ViewBag.FallbackImages = ProductImageMap;
            ViewBag.CurrencyRates = CurrencyToSypRates;

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.SupplierOffers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();
            // Map supplierId -> display name for active offers
            var supplierIds = product.SupplierOffers?.Select(o => o.SupplierId).Distinct().ToList() ?? new List<string>();
            if (supplierIds.Count > 0)
            {
                var users = await _db.Users.Where(u => supplierIds.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName, u.Email }).ToListAsync();
                ViewBag.SupplierNames = users.ToDictionary(u => u.Id, u => (string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName));
            }
            ViewBag.FallbackImages = ProductImageMap;
            ViewBag.CurrencyRates = CurrencyToSypRates;
            return View(product);
        }
    }
}
