using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rain.Infrastructure.Persistence;
using System.Threading.Tasks;

namespace Rain.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            ViewBag.UsersCount = await _db.Users.CountAsync();
            ViewBag.ProductsCount = await _db.Products.CountAsync();
            ViewBag.OrdersCount = await _db.Orders.CountAsync();
            ViewBag.OffersCount = await _db.SupplierOffers.CountAsync();
            return View();
        }
    }
}
