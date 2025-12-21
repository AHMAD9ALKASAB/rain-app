using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using System.Linq;
using System.Threading.Tasks;

namespace Rain.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize(Roles = "Individual,Shop,Admin")]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        public ReviewsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db; _userManager = userManager;
        }

        // GET: /User/Reviews/Create?orderId=1&offerId=1
        public async Task<IActionResult> Create(int orderId, int offerId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var order = await _db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.SupplierOffer)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == user.Id);
            if (order == null || order.Status != OrderStatus.Delivered) return NotFound();
            if (!order.Items.Any(i => i.SupplierOfferId == offerId)) return NotFound();

            // prevent duplicate
            var exists = await _db.Reviews.AnyAsync(r => r.OrderId == orderId && r.SupplierOfferId == offerId && r.CreatedByUserId == user.Id);
            if (exists)
            {
                TempData["Msg"] = "تم إضافة تقييم مسبقاً";
                return RedirectToAction("Details", "Orders", new { area = "User", id = orderId });
            }

            ViewBag.OrderId = orderId;
            ViewBag.OfferId = offerId;
            return View(new Review { Rating = 5 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int orderId, int offerId, Review input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == user.Id);
            if (order == null || order.Status != OrderStatus.Delivered) return NotFound();
            if (!order.Items.Any(i => i.SupplierOfferId == offerId)) return NotFound();

            if (input.Rating < 1 || input.Rating > 5)
                ModelState.AddModelError("Rating", "التقييم بين 1 و5");

            if (!ModelState.IsValid)
            {
                ViewBag.OrderId = orderId;
                ViewBag.OfferId = offerId;
                return View(input);
            }

            var review = new Review
            {
                SupplierOfferId = offerId,
                OrderId = orderId,
                Rating = input.Rating,
                Comment = input.Comment,
                CreatedByUserId = user.Id
            };
            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            // Update supplier offer aggregates
            var offer = await _db.SupplierOffers.FirstOrDefaultAsync(o => o.Id == offerId);
            if (offer != null)
            {
                var agg = await _db.Reviews
                    .Where(r => r.SupplierOfferId == offerId)
                    .GroupBy(r => r.SupplierOfferId)
                    .Select(g => new { Count = g.Count(), Avg = g.Average(r => r.Rating) })
                    .FirstOrDefaultAsync();
                if (agg != null)
                {
                    offer.RatingsCount = agg.Count;
                    offer.AverageRating = agg.Avg;
                    await _db.SaveChangesAsync();
                }
            }
            return RedirectToAction("Details", "Orders", new { area = "User", id = orderId });
        }
    }
}
