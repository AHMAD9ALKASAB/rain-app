using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using Rain.Domain.Entities;

namespace Rain.Infrastructure.Seed
{
    public static class SeedData
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;

            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var config = provider.GetRequiredService<IConfiguration>();
            var db = provider.GetRequiredService<ApplicationDbContext>();

            string[] roles = new[] { "Admin", "Supplier", "Shop", "Individual" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Seed a demo Buyer (Individual)
            var buyerEmail = "buyer1@rain.local";
            var buyer = await userManager.FindByEmailAsync(buyerEmail);
            if (buyer == null)
            {
                buyer = new ApplicationUser
                {
                    UserName = buyerEmail,
                    Email = buyerEmail,
                    EmailConfirmed = true,
                    DisplayName = "Demo Buyer"
                };
                var pwd = config["Admin:Password"] ?? "Admin@12345";
                if ((await userManager.CreateAsync(buyer, pwd)).Succeeded)
                {
                    await userManager.AddToRoleAsync(buyer, "Individual");
                }
            }

            if (!await db.Addresses.AnyAsync(a => a.UserId == buyer.Id))
            {
                db.Addresses.Add(new Address
                {
                    UserId = buyer.Id,
                    Line1 = "علاقة 1",
                    City = "المدينة",
                    Country = "الدولة",
                    IsDefault = true
                });
                await db.SaveChangesAsync();
            }

            var adminEmail = config["Admin:Email"] ?? "admin@rain.local";
            var adminPassword = config["Admin:Password"] ?? "Admin@12345";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    DisplayName = "Administrator"
                };
                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Seed a demo Supplier user
            var supplierEmail = "supplier1@rain.local";
            var supplier = await userManager.FindByEmailAsync(supplierEmail);
            if (supplier == null)
            {
                supplier = new ApplicationUser
                {
                    UserName = supplierEmail,
                    Email = supplierEmail,
                    EmailConfirmed = true,
                    DisplayName = "Demo Supplier"
                };
                var pwd = config["Admin:Password"] ?? "Admin@12345";
                if ((await userManager.CreateAsync(supplier, pwd)).Succeeded)
                {
                    await userManager.AddToRoleAsync(supplier, "Supplier");
                }
            }

            // Seed demo catalog (idempotent)
            var neededCats = new[] { "الإلكترونيات", "الأزياء", "المنزل", "العناية والجمال", "الرياضة" };
            var existingCatNames = await db.Categories.Select(c => c.Name).ToListAsync();
            var toAddCats = neededCats.Except(existingCatNames).Select(n => new Category { Name = n }).ToList();
            if (toAddCats.Count > 0)
            {
                db.Categories.AddRange(toAddCats);
            }

            var neededBrands = new[] { "RainTech", "NovaWear", "CasaPlus", "FitPro" };
            var existingBrandNames = await db.Brands.Select(b => b.Name).ToListAsync();
            var toAddBrands = neededBrands.Except(existingBrandNames).Select(n => new Brand { Name = n }).ToList();
            if (toAddBrands.Count > 0)
            {
                db.Brands.AddRange(toAddBrands);
            }
            if (toAddCats.Count > 0 || toAddBrands.Count > 0)
            {
                await db.SaveChangesAsync();
            }

            // Resolve references
            var cats = await db.Categories.Where(c => neededCats.Contains(c.Name)).OrderBy(c => c.Id).ToListAsync();
            var brands = await db.Brands.Where(b => neededBrands.Contains(b.Name)).OrderBy(b => b.Id).ToListAsync();

            // Add products if missing by name
            var demoProducts = new List<Product>
            {
                new Product { Name = "سماعات RAIN اللاسلكية", CategoryId = cats.First(c=>c.Name=="الإلكترونيات").Id, BrandId = brands.First(b=>b.Name=="RainTech").Id, Description = "صوت نقي وبطارية طويلة" },
                new Product { Name = "ساعة ذكية Nova", CategoryId = cats.First(c=>c.Name=="الإلكترونيات").Id, BrandId = brands.First(b=>b.Name=="RainTech").Id, Description = "تعقب صحة وإشعارات" },
                new Product { Name = "قميص رجالي كلاسيك", CategoryId = cats.First(c=>c.Name=="الأزياء").Id, BrandId = brands.First(b=>b.Name=="NovaWear").Id, Description = "خامة قطن مريحة" },
                new Product { Name = "فستان صيفي", CategoryId = cats.First(c=>c.Name=="الأزياء").Id, BrandId = brands.First(b=>b.Name=="NovaWear").Id, Description = "تصميم أنيق وخفيف" },
                new Product { Name = "طقم أواني Casa", CategoryId = cats.First(c=>c.Name=="المنزل").Id, BrandId = brands.First(b=>b.Name=="CasaPlus").Id, Description = "متين وسهل التنظيف" },
                new Product { Name = "مصباح مكتبي LED", CategoryId = cats.First(c=>c.Name=="المنزل").Id, BrandId = brands.First(b=>b.Name=="CasaPlus").Id, Description = "إضاءة مريحة للعين" },
                new Product { Name = "مجفف شعر", CategoryId = cats.First(c=>c.Name=="العناية والجمال").Id, BrandId = brands.First(b=>b.Name=="CasaPlus").Id, Description = "تقنية حماية الشعر" },
                new Product { Name = "معدات لياقة FitPro", CategoryId = cats.First(c=>c.Name=="الرياضة").Id, BrandId = brands.First(b=>b.Name=="FitPro").Id, Description = "تحمل عالي" }
            };
            var existingProductNames = await db.Products.Select(p => p.Name).ToListAsync();
            var toAddProducts = demoProducts.Where(p => !existingProductNames.Contains(p.Name)).ToList();
            if (toAddProducts.Count > 0)
            {
                db.Products.AddRange(toAddProducts);
                await db.SaveChangesAsync();
            }

            // Offers for missing products
            if (supplier != null)
            {
                var rnd = new Random(1234);
                var offerProductIds = await db.SupplierOffers.Select(o => o.ProductId).ToListAsync();
                var newOfferProducts = await db.Products.Where(p => !offerProductIds.Contains(p.Id)).ToListAsync();
                foreach (var p in newOfferProducts)
                {
                    db.SupplierOffers.Add(new SupplierOffer
                    {
                        ProductId = p.Id,
                        SupplierId = supplier.Id,
                        Price = Math.Round((decimal)(rnd.Next(20, 300) + rnd.NextDouble()), 2),
                        StockQty = rnd.Next(20, 200),
                        MinOrderQty = 1,
                        IsActive = true
                    });
                }
                if (newOfferProducts.Count > 0)
                    await db.SaveChangesAsync();
            }
        }
    }
}
