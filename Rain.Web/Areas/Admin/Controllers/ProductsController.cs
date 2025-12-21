using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Infrastructure.Files;
using Rain.Infrastructure.Persistence;
using System.Linq;
using System.Threading.Tasks;

namespace Rain.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorage _files;
        public ProductsController(ApplicationDbContext db, IFileStorage files)
        {
            _db = db; _files = files;
        }

        public async Task<IActionResult> Index(string? keyword)
        {
            var q = _db.Products
                .AsNoTracking()
                .Include(p=>p.Brand)
                .Include(p=>p.Category)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(p => p.Name.Contains(k));
            }
            var items = await q.OrderByDescending(p=>p.Id).ToListAsync();
            return View(items);
        }

        public async Task<IActionResult> Create()
        {
            await LoadLookups();
            return View(new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product input)
        {
            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(input);
            }
            _db.Products.Add(input);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var product = await _db.Products.Include(p=>p.Images).FirstOrDefaultAsync(p=>p.Id==id);
            if (product==null) return NotFound();
            await LoadLookups();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product input)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p=>p.Id==id);
            if (product==null) return NotFound();
            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return View(input);
            }
            product.Name = input.Name;
            product.CategoryId = input.CategoryId;
            product.BrandId = input.BrandId;
            product.Description = input.Description;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(int id, Microsoft.AspNetCore.Http.IFormFile file)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p=>p.Id==id);
            if (product==null) return NotFound();
            if (file != null && file.Length > 0)
            {
                // Validate size (<= 2 MB) and content type
                const long maxBytes = 2 * 1024 * 1024;
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
                if (file.Length > maxBytes)
                {
                    ModelState.AddModelError("file", "الملف كبير جداً (الحد الأقصى 2MB)");
                    return await Edit(id);
                }
                if (!allowedTypes.Contains(file.ContentType))
                {
                    ModelState.AddModelError("file", "نوع الصورة غير مدعوم. المسموح: JPG/PNG/WebP");
                    return await Edit(id);
                }
                var url = await _files.SaveProductImageAsync(id, file);
                _db.ProductImages.Add(new ProductImage{ ProductId = id, Url = url });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var img = await _db.ProductImages.FirstOrDefaultAsync(i=>i.Id==id);
            if (img==null) return NotFound();
            var productId = img.ProductId;
            await _files.DeleteAsync(img.Url);
            _db.ProductImages.Remove(img);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        private async Task LoadLookups()
        {
            ViewBag.Categories = new SelectList(await _db.Categories.AsNoTracking().OrderBy(c=>c.Name).ToListAsync(), "Id", "Name");
            ViewBag.Brands = new SelectList(await _db.Brands.AsNoTracking().OrderBy(b=>b.Name).ToListAsync(), "Id", "Name");
        }
    }
}
