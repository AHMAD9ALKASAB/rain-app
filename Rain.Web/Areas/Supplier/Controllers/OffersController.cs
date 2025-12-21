using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using System.Linq;
using System.Threading.Tasks;

namespace Rain.Web.Areas.Supplier.Controllers
{
    [Area("Supplier")]
    [Authorize(Roles = "Supplier")]
    public class OffersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public OffersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var offers = await _db.SupplierOffers
                .AsNoTracking()
                .Include(o => o.Product)
                .Where(o => o.SupplierId == user.Id)
                .OrderByDescending(o => o.Id)
                .ToListAsync();
            return View(offers);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Products = new SelectList(await _db.Products.AsNoTracking().OrderBy(p=>p.Name).ToListAsync(), "Id", "Name");
            return View(new SupplierOffer { IsActive = true, MinOrderQty = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupplierOffer input)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = new SelectList(await _db.Products.AsNoTracking().OrderBy(p=>p.Name).ToListAsync(), "Id", "Name", input.ProductId);
                return View(input);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            input.SupplierId = user.Id;
            input.IsActive = input.IsActive;
            _db.SupplierOffers.Add(input);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == user.Id);
            if (offer == null) return NotFound();
            ViewBag.Products = new SelectList(await _db.Products.AsNoTracking().OrderBy(p=>p.Name).ToListAsync(), "Id", "Name", offer.ProductId);
            return View(offer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SupplierOffer input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == user.Id);
            if (offer == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Products = new SelectList(await _db.Products.AsNoTracking().OrderBy(p=>p.Name).ToListAsync(), "Id", "Name", input.ProductId);
                return View(input);
            }

            offer.ProductId = input.ProductId;
            offer.Price = input.Price;
            offer.StockQty = input.StockQty;
            offer.MinOrderQty = input.MinOrderQty;
            offer.IsActive = input.IsActive;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.AsNoTracking().Include(o=>o.Product).FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == user.Id);
            if (offer == null) return NotFound();
            return View(offer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var offer = await _db.SupplierOffers.FirstOrDefaultAsync(o => o.Id == id && o.SupplierId == user.Id);
            if (offer == null) return NotFound();
            _db.SupplierOffers.Remove(offer);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
