using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Rain.Domain.Entities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;

namespace Rain.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SupplierApplicationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _email;
        private readonly IConfiguration _cfg;

        public SupplierApplicationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailSender email, IConfiguration cfg)
        {
            _db = db; _userManager = userManager; _email = email; _cfg = cfg;
        }

        public async Task<IActionResult> Index(string? status)
        {
            IQueryable<SupplierApplication> q = _db.SupplierApplications.AsNoTracking().OrderByDescending(a=>a.Id);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SupplierApplicationStatus>(status, out var s))
            {
                q = q.Where(a=>a.Status == s);
            }
            var list = await q.ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var app = await _db.SupplierApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (app == null) return NotFound();
            return View(app);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var app = await _db.SupplierApplications.FirstOrDefaultAsync(a => a.Id == id);
            if (app == null) return NotFound();
            if (app.Status != SupplierApplicationStatus.Approved)
            {
                app.Status = SupplierApplicationStatus.Approved;
                app.ReviewedAtUtc = DateTime.UtcNow;
                app.ReviewerUserId = _userManager.GetUserId(User);
                await _db.SaveChangesAsync();

                // Grant Supplier role
                var user = await _userManager.FindByIdAsync(app.UserId);
                if (user != null && !(await _userManager.IsInRoleAsync(user, "Supplier")))
                {
                    await _userManager.AddToRoleAsync(user, "Supplier");
                }

                // Notify applicant
                if (!string.IsNullOrWhiteSpace(app.Email))
                {
                    await _email.SendEmailAsync(app.Email, "تمت الموافقة على طلب المورد", $"مرحباً {app.DisplayName},\n\nتمت الموافقة على طلبك كمورّد. يمكنك الآن إدارة منتجاتك عبر صفحة المورد.");
                }
            }
            TempData["Success"] = "تمت الموافقة ومنح صلاحيات المورد";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? notes)
        {
            var app = await _db.SupplierApplications.FirstOrDefaultAsync(a => a.Id == id);
            if (app == null) return NotFound();
            app.Status = SupplierApplicationStatus.Rejected;
            app.ReviewNotes = notes;
            app.ReviewedAtUtc = DateTime.UtcNow;
            app.ReviewerUserId = _userManager.GetUserId(User);
            await _db.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(app.Email))
            {
                var msg = string.IsNullOrWhiteSpace(notes) ? "نعتذر، تم رفض طلب المورد." : $"تم رفض طلب المورد. السبب: {notes}";
                await _email.SendEmailAsync(app.Email, "رفض طلب المورد", msg);
            }
            TempData["Info"] = "تم رفض الطلب";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
