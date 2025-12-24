using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Domain.Enums;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;

namespace Rain.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<string> Roles { get; private set; } = new List<string>();
    public bool EmailConfirmed { get; private set; }
    public List<Address> Addresses { get; private set; } = new();
    public List<Order> RecentOrders { get; private set; } = new();
    public DashboardStats Stats { get; private set; } = new();

    public class InputModel
    {
        [Display(Name = "اسم العرض")]
        [StringLength(64, ErrorMessage = "الاسم يجب ألا يتجاوز 64 محرفًا.")]
        public string? DisplayName { get; set; }

        [Phone]
        [Display(Name = "رقم الهاتف")]
        public string? PhoneNumber { get; set; }
    }

    public class DashboardStats
    {
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime? LastOrderAt { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        var updated = false;
        if (!string.Equals(user.DisplayName, Input.DisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = string.IsNullOrWhiteSpace(Input.DisplayName)
                ? user.DisplayName
                : Input.DisplayName!.Trim();
            updated = true;
        }

        var phone = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber!.Trim();
        if (!string.Equals(user.PhoneNumber, phone, StringComparison.Ordinal))
        {
            user.PhoneNumber = phone;
            updated = true;
        }

        if (updated)
        {
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await LoadAsync(user);
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "تم تحديث بيانات الحساب بنجاح.";
        }
        else
        {
            StatusMessage = "لا توجد تغييرات لحفظها.";
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Input = new InputModel
        {
            DisplayName = user.DisplayName ?? user.UserName,
            PhoneNumber = user.PhoneNumber
        };

        Roles = await _userManager.GetRolesAsync(user);
        EmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

        Addresses = await _db.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.Id)
            .ToListAsync();

        var ordersQuery = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items!)
                .ThenInclude(i => i.SupplierOffer!)
                .ThenInclude(so => so.Product)
            .Where(o => o.BuyerUserId == user.Id);

        Stats = new DashboardStats
        {
            TotalOrders = await ordersQuery.CountAsync(),
            TotalSpent = await ordersQuery.SumAsync(o => (decimal?)o.Total) ?? 0,
            LastOrderAt = await ordersQuery.OrderByDescending(o => o.CreatedAt).Select(o => (DateTime?)o.CreatedAt).FirstOrDefaultAsync()
        };

        RecentOrders = await ordersQuery
            .OrderByDescending(o => o.CreatedAt)
            .Take(4)
            .ToListAsync();
    }
}
